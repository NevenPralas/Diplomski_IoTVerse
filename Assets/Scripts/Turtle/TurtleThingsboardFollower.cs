using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class TurtleThingsBoardFollower : MonoBehaviour
{
    [Header("ThingsBoard")]
    [SerializeField] private string baseUrl = "http://IP:port";
    [SerializeField] private string username = "tenant@thingsboard.org";
    [SerializeField] private string password = "tenant";
    [SerializeField] private string deviceId = "0f757400-1897-11f1-a1d2-e5a5a57c1784";

    [Header("Go2 Robot Controller")]
    [SerializeField] private Go2SimpleController go2Controller;

    [Header("ROS / Go2 Bounds")]
    [SerializeField] private float rosMinX = -0.5f;
    [SerializeField] private float rosMaxX = 0.5f;
    [SerializeField] private float rosMinY = -0.5f;
    [SerializeField] private float rosMaxY = 0.5f;

    [Header("ROS / Go2 Reference -> Unity Center")]
    [SerializeField] private float rosReferenceX = 0f;
    [SerializeField] private float rosReferenceY = 0f;
    [SerializeField] private float unityCenterX = 0f;
    [SerializeField] private float unityCenterZ = 0f;

    [Header("Unity Bounds")]
    [SerializeField] private float unityMinX = -4f;
    [SerializeField] private float unityMaxX = 4f;
    [SerializeField] private float unityMinZ = -2f;
    [SerializeField] private float unityMaxZ = 2f;
    [SerializeField] private float unityHeight = 0.5f;
    [SerializeField] private float wallPadding = 0.15f;

    [Header("Polling")]
    [SerializeField] private float pollIntervalSeconds = 1f;

    [Header("Target Filtering")]
    [SerializeField] private float minimumTargetChangeDistance = 0.05f;

    [Header("Heatmap")]
    [SerializeField] private ShaderGridHeatmap heatmap;

    [Header("Heatmap Recording")]
    [Tooltip("Ako je uključeno, svake sekunde zapisuje temperaturu u trenutnu ćeliju robota.")]
    [SerializeField] private bool paintCurrentCellEveryPoll = true;

    [Header("Noise Trail")]
    [Tooltip("3D prostorno-vremenska putanja buke.")]
    [SerializeField] private SpatioTemporalNoiseTrail noiseTrail;

    [Tooltip("Ako je uključeno, vrijednost noise iz ThingsBoarda dodaje se u 3D putanju buke.")]
    [SerializeField] private bool addNoiseTrailSampleEveryPoll = true;

    [Tooltip("Ako je uključeno, pri svakom dohvatu ispisuje noise vrijednost u Console.")]
    [SerializeField] private bool logNoiseTelemetry = true;

    [Header("Noise Bubble Grid")]
    [Tooltip("Nova vizualizacija buke balonima po grid ćelijama.")]
    [SerializeField] private NoiseBubbleGrid noiseBubbleGrid;

    [Tooltip("Ako je uključeno, vrijednost noise iz ThingsBoarda dodaje se u balon za trenutnu grid ćeliju.")]
    [SerializeField] private bool addNoiseBubbleSampleEveryPoll = true;

    [Header("Real Go2 Posture Sync")]
    [SerializeField] private bool enablePostureSync = true;

    [Tooltip("Koristi se samo ako posture/is_standing nisu dostupni. Iz tvojih mjerenja: standing ≈ 0.32, sitting ≈ 0.067.")]
    [SerializeField] private float standingBodyHeightThreshold = 0.18f;

    [Tooltip("Ispisuje promjenu posture u Unity Console.")]
    [SerializeField] private bool logPostureSync = true;

    private string jwtToken;
    private Vector3 targetPosition;
    private Vector3 lastTargetPosition;
    private bool hasFirstPosition = false;

    private bool hasAppliedPosture = false;
    private bool lastAppliedStanding = true;

    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class LoginResponse
    {
        public string token;
        public string refreshToken;
    }

    private void Start()
    {
        if (go2Controller == null)
            go2Controller = GetComponent<Go2SimpleController>();

        if (go2Controller == null)
        {
            Debug.LogError("TurtleThingsBoardFollower: Go2SimpleController nije pronađen. Dodaj ga na isti objekt ili ga ručno povuci u Inspector.");
            enabled = false;
            return;
        }

        Vector3 currentRobotCenter = go2Controller.GetRobotCenter();

        targetPosition = currentRobotCenter;
        lastTargetPosition = currentRobotCenter;

        StartCoroutine(MainLoop());
    }

    private IEnumerator MainLoop()
    {
        yield return StartCoroutine(Login());

        if (string.IsNullOrEmpty(jwtToken))
        {
            Debug.LogError("ThingsBoard login nije uspio.");
            yield break;
        }

        while (true)
        {
            yield return StartCoroutine(GetLatestTelemetry());
            yield return new WaitForSeconds(pollIntervalSeconds);
        }
    }

    private IEnumerator Login()
    {
        string url = $"{baseUrl}/api/auth/login";

        LoginRequest loginData = new LoginRequest
        {
            username = username,
            password = password
        };

        string json = JsonUtility.ToJson(loginData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Login error: {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
        jwtToken = response.token;

        Debug.Log("ThingsBoard login uspješan.");
    }

    private IEnumerator GetLatestTelemetry()
    {
        string keys = "x,y,temperature,noise,posture,body_height,is_standing";
        string url = $"{baseUrl}/api/plugins/telemetry/DEVICE/{deviceId}/values/timeseries?keys={keys}";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("X-Authorization", $"Bearer {jwtToken}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Telemetry error: {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        string rawJson = request.downloadHandler.text;

        string xString = ExtractLatestValue(rawJson, "x");
        string yString = ExtractLatestValue(rawJson, "y");
        string tempString = ExtractLatestValue(rawJson, "temperature");
        string noiseString = ExtractLatestValue(rawJson, "noise");

        string postureString = ExtractLatestValue(rawJson, "posture");
        string bodyHeightString = ExtractLatestValue(rawJson, "body_height");
        string isStandingString = ExtractLatestValue(rawJson, "is_standing");

        ApplyPostureFromTelemetry(postureString, isStandingString, bodyHeightString);

        if (float.TryParse(xString, NumberStyles.Float, CultureInfo.InvariantCulture, out float rosX) &&
            float.TryParse(yString, NumberStyles.Float, CultureInfo.InvariantCulture, out float rosY))
        {
            float mappedX = MapAroundReference(
                rosX,
                rosMinX,
                rosMaxX,
                rosReferenceX,
                unityMinX + wallPadding,
                unityMaxX - wallPadding,
                unityCenterX
            );

            float mappedZ = MapAroundReference(
                rosY,
                rosMinY,
                rosMaxY,
                rosReferenceY,
                unityMinZ + wallPadding,
                unityMaxZ - wallPadding,
                unityCenterZ
            );

            Vector3 newTarget = new Vector3(mappedX, unityHeight, mappedZ);
            newTarget = ClampToRoom(newTarget);

            bool parsedTemperature = float.TryParse(
                tempString,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float temperature
            );

            if (!parsedTemperature)
            {
                Debug.LogWarning($"Ne mogu parsirati temperaturu. raw temperature='{tempString}'");
                temperature = 0f;
            }

            bool parsedNoise = float.TryParse(
                noiseString,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float noiseDb
            );

            if (!parsedNoise)
            {
                Debug.LogWarning($"Ne mogu parsirati buku. raw noise='{noiseString}'");
                noiseDb = 0f;
            }

            if (!hasFirstPosition)
            {
                targetPosition = newTarget;
                lastTargetPosition = newTarget;
                hasFirstPosition = true;

                go2Controller.SetNavigationTarget(newTarget);
            }
            else
            {
                float targetChangeDistance = Vector3.Distance(targetPosition, newTarget);

                if (targetChangeDistance >= minimumTargetChangeDistance)
                {
                    lastTargetPosition = targetPosition;
                    targetPosition = newTarget;

                    go2Controller.SetNavigationTarget(newTarget);
                }
            }

            if (paintCurrentCellEveryPoll)
            {
                if (heatmap != null)
                    heatmap.PaintAtWorldPosition(newTarget, temperature);
                else
                    Debug.LogWarning("Heatmap referenca nije postavljena na TurtleThingsBoardFollower.");
            }

            if (addNoiseTrailSampleEveryPoll)
            {
                if (noiseTrail != null)
                    noiseTrail.AddSample(newTarget, noiseDb);
                else
                    Debug.LogWarning("NoiseTrail referenca nije postavljena na TurtleThingsBoardFollower.");
            }
            if (addNoiseBubbleSampleEveryPoll)
            {
                if (noiseBubbleGrid != null)
                    noiseBubbleGrid.AddNoiseSample(newTarget, noiseDb);
                else
                    Debug.LogWarning("NoiseBubbleGrid referenca nije postavljena na TurtleThingsBoardFollower.");
            }

            if (logNoiseTelemetry)
            {
                Debug.Log(
                    $"Go2 ThingsBoard -> Unity | x={rosX:F4}, y={rosY:F4}, " +
                    $"temp={temperature:F2}, noise={noiseDb:F2} dBA, " +
                    $"posture='{postureString}', body_height='{bodyHeightString}' | " +
                    $"targetX={newTarget.x:F3}, targetZ={newTarget.z:F3}"
                );
            }
            else
            {
                Debug.Log(
                    $"Go2 ThingsBoard -> Unity | x={rosX:F4}, y={rosY:F4}, temp={temperature:F2}, " +
                    $"posture='{postureString}', body_height='{bodyHeightString}' | " +
                    $"targetX={newTarget.x:F3}, targetZ={newTarget.z:F3}"
                );
            }
        }
        else
        {
            Debug.LogWarning($"Ne mogu parsirati x/y vrijednosti. raw x='{xString}', y='{yString}'");
        }
    }

    private void ApplyPostureFromTelemetry(string posture, string isStandingRaw, string bodyHeightRaw)
    {
        if (!enablePostureSync || go2Controller == null)
            return;

        bool? shouldStand = null;

        if (!string.IsNullOrEmpty(posture) && posture != "0")
        {
            string normalizedPosture = posture.Trim().ToLowerInvariant();

            if (normalizedPosture == "standing" || normalizedPosture == "stand")
                shouldStand = true;
            else if (
                normalizedPosture == "sitting" ||
                normalizedPosture == "sit" ||
                normalizedPosture == "lying" ||
                normalizedPosture == "lie" ||
                normalizedPosture == "down")
                shouldStand = false;
        }

        if (shouldStand == null && !string.IsNullOrEmpty(isStandingRaw))
        {
            string normalized = isStandingRaw.Trim().ToLowerInvariant();

            if (normalized == "true" || normalized == "1")
                shouldStand = true;
            else if (normalized == "false" || normalized == "0")
                shouldStand = false;
        }

        if (shouldStand == null &&
            float.TryParse(bodyHeightRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float bodyHeight))
        {
            shouldStand = bodyHeight >= standingBodyHeightThreshold;
        }

        if (shouldStand == null)
            return;

        if (hasAppliedPosture && lastAppliedStanding == shouldStand.Value)
            return;

        hasAppliedPosture = true;
        lastAppliedStanding = shouldStand.Value;

        go2Controller.SetStandingState(shouldStand.Value);

        if (logPostureSync)
        {
            Debug.Log(
                shouldStand.Value
                    ? $"[Posture Sync] Real Go2 is STANDING. posture='{posture}', body_height='{bodyHeightRaw}'"
                    : $"[Posture Sync] Real Go2 is SITTING / LYING. posture='{posture}', body_height='{bodyHeightRaw}'"
            );
        }
    }

    private float MapAroundReference(
        float value,
        float rosMin,
        float rosMax,
        float rosReference,
        float unityMin,
        float unityMax,
        float unityCenter)
    {
        value = Mathf.Clamp(value, rosMin, rosMax);

        if (value >= rosReference)
        {
            float rosRangeRight = rosMax - rosReference;

            if (Mathf.Approximately(rosRangeRight, 0f))
                return unityCenter;

            float t = (value - rosReference) / rosRangeRight;
            return Mathf.Lerp(unityCenter, unityMax, t);
        }
        else
        {
            float rosRangeLeft = rosReference - rosMin;

            if (Mathf.Approximately(rosRangeLeft, 0f))
                return unityCenter;

            float t = (rosReference - value) / rosRangeLeft;
            return Mathf.Lerp(unityCenter, unityMin, t);
        }
    }

    private Vector3 ClampToRoom(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, unityMinX + wallPadding, unityMaxX - wallPadding);
        pos.z = Mathf.Clamp(pos.z, unityMinZ + wallPadding, unityMaxZ - wallPadding);
        pos.y = unityHeight;

        return pos;
    }

    private string ExtractLatestValue(string json, string key)
    {
        string pattern = $"\"{key}\":\\[\\{{\"ts\":\\d+,\"value\":\"([^\"]*)\"\\}}\\]";
        Match match = Regex.Match(json, pattern);

        if (match.Success)
            return match.Groups[1].Value;

        return "0";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        Vector3 center = new Vector3(
            (unityMinX + unityMaxX) * 0.5f,
            unityHeight,
            (unityMinZ + unityMaxZ) * 0.5f
        );

        Vector3 size = new Vector3(
            Mathf.Abs(unityMaxX - unityMinX),
            0.05f,
            Mathf.Abs(unityMaxZ - unityMinZ)
        );

        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new Vector3(unityCenterX, unityHeight, unityCenterZ), 0.12f);

        if (Application.isPlaying && hasFirstPosition)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(targetPosition, 0.15f);
        }
    }
}