using UnityEngine;
using UnityEngine.InputSystem;

public class RobotHandTriggerDriver : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty triggerAction;

    [Header("Finger Bones")]
    public Transform thumb;
    public Transform index;
    public Transform middle;
    public Transform ring;
    public Transform pinky;

    [Header("Closed Rotation Offsets")]
    public Vector3 thumbClosedEuler = new Vector3(0f, 0f, 20f);
    public Vector3 indexClosedEuler = new Vector3(0f, 0f, 55f);
    public Vector3 middleClosedEuler = new Vector3(0f, 0f, 65f);
    public Vector3 ringClosedEuler = new Vector3(0f, 0f, 65f);
    public Vector3 pinkyClosedEuler = new Vector3(0f, 0f, 65f);

    private Quaternion thumbOpenRot;
    private Quaternion indexOpenRot;
    private Quaternion middleOpenRot;
    private Quaternion ringOpenRot;
    private Quaternion pinkyOpenRot;

    private Quaternion thumbClosedRot;
    private Quaternion indexClosedRot;
    private Quaternion middleClosedRot;
    private Quaternion ringClosedRot;
    private Quaternion pinkyClosedRot;

    private void Awake()
    {
        if (thumb != null) thumbOpenRot = thumb.localRotation;
        if (index != null) indexOpenRot = index.localRotation;
        if (middle != null) middleOpenRot = middle.localRotation;
        if (ring != null) ringOpenRot = ring.localRotation;
        if (pinky != null) pinkyOpenRot = pinky.localRotation;

        if (thumb != null) thumbClosedRot = thumbOpenRot * Quaternion.Euler(thumbClosedEuler);
        if (index != null) indexClosedRot = indexOpenRot * Quaternion.Euler(indexClosedEuler);
        if (middle != null) middleClosedRot = middleOpenRot * Quaternion.Euler(middleClosedEuler);
        if (ring != null) ringClosedRot = ringOpenRot * Quaternion.Euler(ringClosedEuler);
        if (pinky != null) pinkyClosedRot = pinkyOpenRot * Quaternion.Euler(pinkyClosedEuler);
    }

    private void OnEnable()
    {
        triggerAction.action?.Enable();
    }

    private void OnDisable()
    {
        triggerAction.action?.Disable();
    }

    private void Update()
    {
        float triggerValue = 0f;

        if (triggerAction.action != null)
            triggerValue = triggerAction.action.ReadValue<float>();

        if (thumb != null)
            thumb.localRotation = Quaternion.Slerp(thumbOpenRot, thumbClosedRot, triggerValue);

        if (index != null)
            index.localRotation = Quaternion.Slerp(indexOpenRot, indexClosedRot, triggerValue);

        if (middle != null)
            middle.localRotation = Quaternion.Slerp(middleOpenRot, middleClosedRot, triggerValue);

        if (ring != null)
            ring.localRotation = Quaternion.Slerp(ringOpenRot, ringClosedRot, triggerValue);

        if (pinky != null)
            pinky.localRotation = Quaternion.Slerp(pinkyOpenRot, pinkyClosedRot, triggerValue);
    }
}