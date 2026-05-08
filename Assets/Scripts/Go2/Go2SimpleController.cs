using UnityEngine;

/// <summary>
/// Go2 Realistic Trot Controller
///
/// Dodano:
/// - Autonomous navigation prema world targetu
/// - TurtleThingsBoardFollower može pozvati SetNavigationTarget(...)
/// - Ručno upravljanje: numpad 8/5/4/6 + 7/9
/// - Vanjska kontrola posture: SetStandingState(...)
/// </summary>
public class Go2SimpleController : MonoBehaviour
{
    [Header("Leg Joints")]
    public ArticulationBody FL_hip, FL_thigh, FL_calf;
    public ArticulationBody FR_hip, FR_thigh, FR_calf;
    public ArticulationBody RL_hip, RL_thigh, RL_calf;
    public ArticulationBody RR_hip, RR_thigh, RR_calf;

    [Header("Drive Settings")]
    public float stiffness = 15000f;
    public float damping = 600f;
    public float forceLimit = 2500f;

    [Header("Stand Pose")]
    public float standHip = 0f;
    public float standThigh = 35f;
    public float standCalf = -70f;

    [Header("Lie / Sit Pose")]
    public float lieHip = 0f;
    public float lieThigh = 130f;
    public float lieCalf = -165f;

    [Header("Pose Transition")]
    public float poseLerpSpeed = 5f;
    public bool startStanding = true;

    [Header("Manual Movement")]
    public float moveSpeed = 1.8f;
    public float turnSpeed = 80f;
    public bool allowMovementOnlyWhenStanding = true;

    [Header("Autonomous Navigation")]
    public bool enableAutonomousNavigation = true;
    public float targetReachDistance = 0.12f;
    public float slowDownDistance = 0.8f;
    public float turnDeadZoneDegrees = 4f;
    public float maxMoveAngleDegrees = 65f;
    public bool rotateToTargetBeforeMoving = true;
    public bool manualInputOverridesAuto = true;

    [Header("External Posture Control")]
    [SerializeField] private bool allowExternalPostureControl = true;
    [SerializeField] private bool logPostureChanges = true;

    [Header("Trot Gait")]
    public float stepFrequency = 2.5f;
    public float thighSwingForward = 14f;
    public float calfSwingForward = 16f;
    public float hipSwingLateral = 8f;
    public float legLiftThigh = 10f;
    public float legLiftCalf = 12f;

    private ArticulationBody rootBody;
    private Transform rootTf;

    private bool isStanding;
    private float currentHip, currentThigh, currentCalf;
    private float forwardInput, strafeInput, turnInput;
    private float gaitPhase;

    private Vector3 robotCenter;
    private float yaw;
    private Vector3 localRootOffset;

    private bool hasNavigationTarget = false;
    private Vector3 navigationTarget;

    void Start()
    {
        rootBody = FindRootBody();

        if (rootBody == null)
        {
            Debug.LogError("[Go2] Nema root ArticulationBody!");
            return;
        }

        rootTf = rootBody.transform;

        yaw = rootTf.eulerAngles.y;
        robotCenter = ComputeHipCenter();

        Quaternion initRot = Quaternion.Euler(0f, yaw, 0f);
        localRootOffset = Quaternion.Inverse(initRot) * (rootTf.position - robotCenter);

        isStanding = startStanding;
        currentHip = isStanding ? standHip : lieHip;
        currentThigh = isStanding ? standThigh : lieThigh;
        currentCalf = isStanding ? standCalf : lieCalf;

        ApplyAllInstant(currentHip, currentThigh, currentCalf);
    }

    void Update()
    {
        ReadInput();
        HandlePoseToggle();
        SmoothPose();
        UpdateGait();
        MoveRobot();
    }

    public void SetNavigationTarget(Vector3 worldTarget)
    {
        navigationTarget = worldTarget;
        navigationTarget.y = robotCenter.y;
        hasNavigationTarget = true;
    }

    public void ClearNavigationTarget()
    {
        hasNavigationTarget = false;
        forwardInput = 0f;
        strafeInput = 0f;
        turnInput = 0f;
    }

    public Vector3 GetRobotCenter()
    {
        return robotCenter;
    }

    public bool IsStanding()
    {
        return isStanding;
    }

    public void SetStandingState(bool shouldStand)
    {
        if (!allowExternalPostureControl)
            return;

        if (isStanding == shouldStand)
            return;

        isStanding = shouldStand;

        if (!isStanding)
        {
            forwardInput = 0f;
            strafeInput = 0f;
            turnInput = 0f;
            hasNavigationTarget = false;
        }

        if (logPostureChanges)
        {
            Debug.Log(shouldStand
                ? "[Go2] External posture command: STAND"
                : "[Go2] External posture command: SIT / LIE");
        }
    }

    void ReadInput()
    {
        float manualForward = 0f;
        float manualStrafe = 0f;
        float manualTurn = 0f;

        if (Input.GetKey(KeyCode.Keypad8)) manualForward = 1f;
        if (Input.GetKey(KeyCode.Keypad5)) manualForward = -1f;
        if (Input.GetKey(KeyCode.Keypad4)) manualStrafe = -1f;
        if (Input.GetKey(KeyCode.Keypad6)) manualStrafe = 1f;
        if (Input.GetKey(KeyCode.Keypad7)) manualTurn = -1f;
        if (Input.GetKey(KeyCode.Keypad9)) manualTurn = 1f;

        bool hasManualInput =
            Mathf.Abs(manualForward) > 0.01f ||
            Mathf.Abs(manualStrafe) > 0.01f ||
            Mathf.Abs(manualTurn) > 0.01f;

        if (manualInputOverridesAuto && hasManualInput)
        {
            forwardInput = manualForward;
            strafeInput = manualStrafe;
            turnInput = manualTurn;
            return;
        }

        if (enableAutonomousNavigation && hasNavigationTarget)
        {
            CalculateAutonomousInput();
            return;
        }

        forwardInput = 0f;
        strafeInput = 0f;
        turnInput = 0f;
    }

    void CalculateAutonomousInput()
    {
        Vector3 toTarget = navigationTarget - robotCenter;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        if (distance <= targetReachDistance)
        {
            forwardInput = 0f;
            strafeInput = 0f;
            turnInput = 0f;
            hasNavigationTarget = false;
            return;
        }

        Vector3 dir = toTarget.normalized;

        float desiredYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float angleError = Mathf.DeltaAngle(yaw, desiredYaw);

        if (Mathf.Abs(angleError) > turnDeadZoneDegrees)
            turnInput = Mathf.Clamp(angleError / 45f, -1f, 1f);
        else
            turnInput = 0f;

        bool canMoveForward = true;

        if (rotateToTargetBeforeMoving)
            canMoveForward = Mathf.Abs(angleError) <= maxMoveAngleDegrees;

        forwardInput = canMoveForward ? Mathf.Clamp01(distance / slowDownDistance) : 0f;
        strafeInput = 0f;
    }

    void HandlePoseToggle()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SetStandingState(!isStanding);
    }

    void SmoothPose()
    {
        float k = Time.deltaTime * poseLerpSpeed;

        currentHip = Mathf.Lerp(currentHip, isStanding ? standHip : lieHip, k);
        currentThigh = Mathf.Lerp(currentThigh, isStanding ? standThigh : lieThigh, k);
        currentCalf = Mathf.Lerp(currentCalf, isStanding ? standCalf : lieCalf, k);
    }

    void UpdateGait()
    {
        bool moving =
            Mathf.Abs(forwardInput) > 0.01f ||
            Mathf.Abs(strafeInput) > 0.01f ||
            Mathf.Abs(turnInput) > 0.01f;

        if (!isStanding || !moving)
        {
            gaitPhase = Mathf.Lerp(gaitPhase, 0f, Time.deltaTime * 4f);
            ApplyAllInstant(currentHip, currentThigh, currentCalf);
            return;
        }

        gaitPhase += Time.deltaTime * stepFrequency * 2f * Mathf.PI;

        if (gaitPhase >= 2f * Mathf.PI)
            gaitPhase -= 2f * Mathf.PI;

        ApplyLeg(FL_hip, FL_thigh, FL_calf, gaitPhase, isLeft: true, isFront: true);
        ApplyLeg(RR_hip, RR_thigh, RR_calf, gaitPhase, isLeft: false, isFront: false);
        ApplyLeg(FR_hip, FR_thigh, FR_calf, gaitPhase + Mathf.PI, isLeft: false, isFront: true);
        ApplyLeg(RL_hip, RL_thigh, RL_calf, gaitPhase + Mathf.PI, isLeft: true, isFront: false);
    }

    void ApplyLeg(
        ArticulationBody hip,
        ArticulationBody thigh,
        ArticulationBody calf,
        float phase,
        bool isLeft,
        bool isFront)
    {
        float s = Mathf.Sin(phase);
        float lift = Mathf.Max(0f, s);

        float dH = 0f;
        float dT = 0f;
        float dC = 0f;

        if (Mathf.Abs(forwardInput) > 0.01f)
        {
            float forwardSign = Mathf.Sign(forwardInput);
            dT += s * thighSwingForward * -forwardSign;
            dC -= s * calfSwingForward * -forwardSign;
        }

        if (Mathf.Abs(strafeInput) > 0.01f)
        {
            float dir = Mathf.Sign(strafeInput);
            float side = isLeft ? -1f : 1f;

            dH += s * hipSwingLateral * dir * side;
            dT += lift * 3f;
            dC -= lift * 3f;
        }

        if (Mathf.Abs(turnInput) > 0.01f && Mathf.Abs(forwardInput) < 0.01f)
        {
            float side = isLeft ? -1f : 1f;
            float turnSign = Mathf.Sign(turnInput);

            dH += s * hipSwingLateral * 0.5f * turnSign * side;
            dT -= lift * legLiftThigh;
            dC -= lift * legLiftCalf;
        }

        dT -= lift * legLiftThigh;
        dC -= lift * legLiftCalf;

        SetJoint(hip, currentHip + dH);
        SetJoint(thigh, currentThigh + dT);
        SetJoint(calf, currentCalf + dC);
    }

    void MoveRobot()
    {
        if (allowMovementOnlyWhenStanding && !isStanding)
            return;

        if (rootBody == null)
            return;

        bool hasTurn = Mathf.Abs(turnInput) > 0.001f;
        bool hasMove = Mathf.Abs(forwardInput) > 0.001f || Mathf.Abs(strafeInput) > 0.001f;

        if (!hasTurn && !hasMove)
            return;

        if (hasTurn)
            yaw += turnInput * turnSpeed * Time.deltaTime;

        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        if (hasMove)
        {
            Vector3 dir = rot * new Vector3(strafeInput, 0f, forwardInput);

            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            robotCenter += dir * moveSpeed * Time.deltaTime;
        }

        Vector3 newRootPos = robotCenter + rot * localRootOffset;
        newRootPos.y = rootTf.position.y;

        rootBody.TeleportRoot(newRootPos, rot);
    }

    Vector3 ComputeHipCenter()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        void Add(ArticulationBody body)
        {
            if (body != null)
            {
                sum += body.transform.position;
                count++;
            }
        }

        Add(FL_hip);
        Add(FR_hip);
        Add(RL_hip);
        Add(RR_hip);

        if (count > 0)
            return sum / count;

        if (rootTf != null)
            return rootTf.position;

        return transform.position;
    }

    void ApplyAllInstant(float h, float t, float c)
    {
        SetJoint(FL_hip, h);
        SetJoint(FL_thigh, t);
        SetJoint(FL_calf, c);

        SetJoint(FR_hip, h);
        SetJoint(FR_thigh, t);
        SetJoint(FR_calf, c);

        SetJoint(RL_hip, h);
        SetJoint(RL_thigh, t);
        SetJoint(RL_calf, c);

        SetJoint(RR_hip, h);
        SetJoint(RR_thigh, t);
        SetJoint(RR_calf, c);
    }

    void SetJoint(ArticulationBody joint, float degrees)
    {
        if (joint == null)
            return;

        ArticulationDrive drive = joint.xDrive;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        drive.target = degrees;
        joint.xDrive = drive;
    }

    ArticulationBody FindRootBody()
    {
        foreach (ArticulationBody body in GetComponentsInChildren<ArticulationBody>())
        {
            if (body.isRoot)
                return body;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hasNavigationTarget)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(navigationTarget, 0.15f);
        Gizmos.DrawLine(transform.position, navigationTarget);
    }
}