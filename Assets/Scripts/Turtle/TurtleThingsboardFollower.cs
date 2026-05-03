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

    [Header("ROS Bounds (turtlesim)")]
    [SerializeField] private float rosMinX = 0f;
    [SerializeField] private float rosMaxX = 11f;
    [SerializeField] private float rosMinY = 0f;
    [SerializeField] private float rosMaxY = 11f;

    [Header("ROS Reference -> Unity Center")]
    [SerializeField] private float rosReferenceX = 5.544445f;
    [SerializeField] private float rosReferenceY = 5.544445f;
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

    private string jwtToken;
    private Vector3 targetPosition;
    private Vector3 lastTargetPosition;
    private bool hasFirstPosition = false;

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
        string keys = "x,y,temperature";
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

            float.TryParse(
                tempString,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float temperature
            );

            if (!hasFirstPosition)
            {
                targetPosition = newTarget;
                lastTargetPosition = newTarget;
                hasFirstPosition = true;

                go2Controller.SetNavigationTarget(newTarget);

                if (heatmap != null)
                    heatmap.PaintAtWorldPosition(newTarget, temperature);
                else
                    Debug.LogWarning("Heatmap referenca nije postavljena na TurtleThingsBoardFollower.");
            }
            else
            {
                float targetChangeDistance = Vector3.Distance(targetPosition, newTarget);

                if (targetChangeDistance >= minimumTargetChangeDistance)
                {
                    lastTargetPosition = targetPosition;
                    targetPosition = newTarget;

                    go2Controller.SetNavigationTarget(newTarget);

                    if (heatmap != null)
                        heatmap.PaintAlongPath(lastTargetPosition, newTarget, temperature);
                    else
                        Debug.LogWarning("Heatmap referenca nije postavljena na TurtleThingsBoardFollower.");
                }
            }

            Debug.Log(
                $"ROS -> Go2 Target | rosX={rosX:F3}, rosY={rosY:F3}, temp={tempString} | " +
                $"targetX={newTarget.x:F3}, targetZ={newTarget.z:F3}"
            );
        }
        else
        {
            Debug.LogWarning($"Ne mogu parsirati x/y vrijednosti. raw x='{xString}', y='{yString}'");
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
        string pattern = $"\"{key}\":\\[\\{{\"ts\":\\d+,\"value\":\"([^\"]+)\"\\}}\\]";
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