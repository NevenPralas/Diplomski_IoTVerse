using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class TurtleThingsBoardFollower : MonoBehaviour
{
    [Header("ThingsBoard")]
    [SerializeField] private string baseUrl = "http://192.168.19.32:8080";
    [SerializeField] private string username = "tenant@thingsboard.org";
    [SerializeField] private string password = "tenant";
    [SerializeField] private string deviceId = "0f757400-1897-11f1-a1d2-e5a5a57c1784";

    [Header("Mapping")]
    [SerializeField] private float unityHeight = 0.5f;
    [SerializeField] private float positionScale = 1.0f;
    [SerializeField] private bool smoothMovement = true;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool rotateTowardsMovement = true;
    [SerializeField] private float rotationSpeed = 6f;

    [Header("Polling")]
    [SerializeField] private float pollIntervalSeconds = 1f;

    private string jwtToken;

    private Vector3 targetPosition;
    private Vector3 lastPosition;
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
        targetPosition = transform.position;
        lastPosition = transform.position;
        StartCoroutine(MainLoop());
    }

    private void Update()
    {
        if (smoothMovement)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                smoothSpeed * Time.deltaTime
            );
        }
        else
        {
            transform.position = targetPosition;
        }

        if (rotateTowardsMovement)
        {
            Vector3 moveDir = targetPosition - transform.position;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
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
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

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

        if (float.TryParse(xString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rosX) &&
            float.TryParse(yString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rosY))
        {
            Vector3 newTarget = new Vector3(
                rosX * positionScale,
                unityHeight,
                rosY * positionScale
            );

            if (!hasFirstPosition)
            {
                transform.position = newTarget;
                targetPosition = newTarget;
                lastPosition = newTarget;
                hasFirstPosition = true;
            }
            else
            {
                lastPosition = targetPosition;
                targetPosition = newTarget;
            }

            Debug.Log($"ROS/ThingsBoard -> Unity | x={rosX} y={rosY} temp={tempString}");
        }
    }

    private string ExtractLatestValue(string json, string key)
    {
        string pattern = $"\"{key}\":\\[\\{{\"ts\":\\d+,\"value\":\"([^\"]+)\"\\}}\\]";
        Match match = Regex.Match(json, pattern);

        if (match.Success)
            return match.Groups[1].Value;

        return "0";
    }
}