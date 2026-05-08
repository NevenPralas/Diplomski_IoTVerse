using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class Go2UdpPoseReceiver : MonoBehaviour
{
    [Header("UDP")]
    [SerializeField] private int listenPort = 5005;
    [SerializeField] private bool printReceivedJson = false;

    [Header("Target Robot")]
    [SerializeField] private Transform virtualRobotRoot;

    [Header("Coordinate Mapping")]
    [Tooltip("Go2 X os se mapira na Unity X os.")]
    [SerializeField] private bool invertUnityX = false;

    [Tooltip("Go2 Y os se mapira na Unity Z os.")]
    [SerializeField] private bool invertUnityZ = false;

    [Tooltip("Skaliranje stvarnog pomaka u Unity prostoru. 1 = 1 metar stvarno je 1 Unity unit.")]
    [SerializeField] private float positionScale = 1f;

    [Tooltip("Offset u Unity prostoru. Koristi se za postavljanje početne pozicije digitalnog blizanca.")]
    [SerializeField] private Vector3 unityPositionOffset = Vector3.zero;

    [Tooltip("Dodatna korekcija yaw rotacije u stupnjevima ako virtualni robot nije okrenut isto kao stvarni.")]
    [SerializeField] private float yawOffsetDegrees = 0f;

    [Header("Filtering")]
    [Tooltip("Ignorira male promjene pozicije ispod ovog praga, u metrima prije skaliranja.")]
    [SerializeField] private float positionDeadzoneMeters = 0.03f;

    [Tooltip("Ignorira male promjene yaw rotacije ispod ovog praga.")]
    [SerializeField] private float yawDeadzoneDegrees = 1.5f;

    [Header("Smoothing")]
    [SerializeField] private bool useSmoothing = true;
    [SerializeField] private float positionSmoothSpeed = 8f;
    [SerializeField] private float rotationSmoothSpeed = 8f;

    [Header("Height")]
    [Tooltip("Ako je uključeno, Unity Y visina ostaje početna i ne koristi Go2 z.")]
    [SerializeField] private bool keepInitialUnityHeight = true;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running;

    private readonly object dataLock = new object();

    private bool hasReceivedPose = false;
    private Go2PosePacket latestPacket;

    private bool hasReference = false;
    private Vector3 go2ReferencePosition;
    private Vector3 unityReferencePosition;
    private float initialUnityY;

    private Vector3 targetUnityPosition;
    private Quaternion targetUnityRotation;

    [Serializable]
    private class Go2PosePacket
    {
        public string source;
        public string topic;
        public long timestamp_ms;

        public float x;
        public float y;
        public float z;

        public float qx;
        public float qy;
        public float qz;
        public float qw;

        public float yaw_rad;
        public float yaw_deg;
    }

    private void Start()
    {
        if (virtualRobotRoot == null)
            virtualRobotRoot = transform;

        initialUnityY = virtualRobotRoot.position.y;
        targetUnityPosition = virtualRobotRoot.position;
        targetUnityRotation = virtualRobotRoot.rotation;

        StartUdpReceiver();
    }

    private void Update()
    {
        if (!TryGetLatestPacket(out Go2PosePacket packet))
            return;

        Vector3 go2Position = new Vector3(packet.x, packet.y, packet.z);

        if (!hasReference)
        {
            hasReference = true;

            go2ReferencePosition = go2Position;
            unityReferencePosition = virtualRobotRoot.position + unityPositionOffset;

            targetUnityPosition = unityReferencePosition;
            targetUnityRotation = Quaternion.Euler(0f, packet.yaw_deg + yawOffsetDegrees, 0f);

            virtualRobotRoot.position = targetUnityPosition;
            virtualRobotRoot.rotation = targetUnityRotation;

            Debug.Log(
                $"[Go2UdpPoseReceiver] Reference set. " +
                $"Go2 ref=({go2ReferencePosition.x:F3}, {go2ReferencePosition.y:F3}, {go2ReferencePosition.z:F3}), " +
                $"Unity ref={unityReferencePosition}"
            );

            return;
        }

        Vector3 go2Delta = go2Position - go2ReferencePosition;

        float horizontalDeltaMagnitude = new Vector2(go2Delta.x, go2Delta.y).magnitude;

        if (horizontalDeltaMagnitude >= positionDeadzoneMeters)
        {
            float mappedX = go2Delta.x * positionScale;
            float mappedZ = go2Delta.y * positionScale;

            if (invertUnityX)
                mappedX *= -1f;

            if (invertUnityZ)
                mappedZ *= -1f;

            float mappedY = keepInitialUnityHeight
                ? initialUnityY
                : unityReferencePosition.y + go2Delta.z * positionScale;

            targetUnityPosition = new Vector3(
                unityReferencePosition.x + mappedX,
                mappedY,
                unityReferencePosition.z + mappedZ
            );
        }

        float targetYaw = packet.yaw_deg + yawOffsetDegrees;
        float currentTargetYaw = targetUnityRotation.eulerAngles.y;
        float yawDiff = Mathf.Abs(Mathf.DeltaAngle(currentTargetYaw, targetYaw));

        if (yawDiff >= yawDeadzoneDegrees)
        {
            targetUnityRotation = Quaternion.Euler(0f, targetYaw, 0f);
        }

        if (useSmoothing)
        {
            virtualRobotRoot.position = Vector3.Lerp(
                virtualRobotRoot.position,
                targetUnityPosition,
                Time.deltaTime * positionSmoothSpeed
            );

            virtualRobotRoot.rotation = Quaternion.Slerp(
                virtualRobotRoot.rotation,
                targetUnityRotation,
                Time.deltaTime * rotationSmoothSpeed
            );
        }
        else
        {
            virtualRobotRoot.position = targetUnityPosition;
            virtualRobotRoot.rotation = targetUnityRotation;
        }
    }

    private void StartUdpReceiver()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log($"[Go2UdpPoseReceiver] Listening UDP on port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Go2UdpPoseReceiver] Failed to start UDP receiver: {e.Message}");
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, listenPort);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data);

                if (printReceivedJson)
                    Debug.Log($"[Go2UdpPoseReceiver] {remoteEndPoint}: {json}");

                Go2PosePacket packet = JsonUtility.FromJson<Go2PosePacket>(json);

                lock (dataLock)
                {
                    latestPacket = packet;
                    hasReceivedPose = true;
                }
            }
            catch (SocketException)
            {
                // Expected when closing the socket.
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Go2UdpPoseReceiver] Receive error: {e.Message}");
            }
        }
    }

    private bool TryGetLatestPacket(out Go2PosePacket packet)
    {
        lock (dataLock)
        {
            if (!hasReceivedPose)
            {
                packet = null;
                return false;
            }

            packet = latestPacket;
            hasReceivedPose = false;
            return packet != null;
        }
    }

    [ContextMenu("Reset Reference Pose")]
    public void ResetReferencePose()
    {
        hasReference = false;

        if (virtualRobotRoot != null)
        {
            initialUnityY = virtualRobotRoot.position.y;
            targetUnityPosition = virtualRobotRoot.position;
            targetUnityRotation = virtualRobotRoot.rotation;
        }

        Debug.Log("[Go2UdpPoseReceiver] Reference pose reset. Next packet will become the new origin.");
    }

    private void OnApplicationQuit()
    {
        StopUdpReceiver();
    }

    private void OnDestroy()
    {
        StopUdpReceiver();
    }

    private void StopUdpReceiver()
    {
        running = false;

        try
        {
            udpClient?.Close();
        }
        catch
        {
            // ignored
        }

        try
        {
            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(200);
        }
        catch
        {
            // ignored
        }

        udpClient = null;
        receiveThread = null;
    }
}