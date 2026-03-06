using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ThingsBoardPoller : MonoBehaviour
{
    [Header("ThingsBoard")]
    [SerializeField] private string baseUrl = "http://192.168.19.32:8080";
    [SerializeField] private string username = "sysadmin@thingsboard.org";
    [SerializeField] private string password = "sysadmin";
    [SerializeField] private string deviceId = "OVDJE_STAVI_DEVICE_ID";

    [Header("Polling")]
    [SerializeField] private float pollIntervalSeconds = 1f;

    private string jwtToken;

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

    [Serializable]
    private class TsValue
    {
        public long ts;
        public string value;
    }

    [Serializable]
    private class TelemetryResponse
    {
        public TsValue[] temperature;
        public TsValue[] x;
        public TsValue[] y;
    }

    private void Start()
    {
        StartCoroutine(MainLoop());
    }

    private IEnumerator MainLoop()
    {
        yield return StartCoroutine(Login());

        if (string.IsNullOrEmpty(jwtToken))
        {
            Debug.LogError("Login nije uspio. JWT token je prazan.");
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
        string keys = "temperature,x,y";
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

        TelemetryResponse telemetry = JsonUtility.FromJson<TelemetryResponse>(rawJson);

        string tempValue = GetLatestValue(telemetry.temperature);
        string xValue = GetLatestValue(telemetry.x);
        string yValue = GetLatestValue(telemetry.y);
        string timestamp = GetLatestTimestampString(telemetry.temperature, telemetry.x, telemetry.y);

        Debug.Log($"[ThingsBoard] t={timestamp} | temperature={tempValue} | x={xValue} | y={yValue}");
    }

    private string GetLatestValue(TsValue[] values)
    {
        if (values == null || values.Length == 0)
            return "N/A";

        return values[0].value;
    }

    private string GetLatestTimestampString(params TsValue[][] arrays)
    {
        foreach (var arr in arrays)
        {
            if (arr != null && arr.Length > 0)
            {
                DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(arr[0].ts).LocalDateTime;
                return dt.ToString("HH:mm:ss");
            }
        }

        return "N/A";
    }
}