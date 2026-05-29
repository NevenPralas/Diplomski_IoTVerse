using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class ThingsBoardSensorVisualizationRouter : MonoBehaviour
{
    [Header("ThingsBoard")]
    [SerializeField] private string baseUrl = "http://IP:port";
    [SerializeField] private string username = "tenant@thingsboard.org";
    [SerializeField] private string password = "tenant";
    [SerializeField] private string deviceId = "DEVICE_ID";

    [Header("Polling")]
    [SerializeField] private float pollIntervalSeconds = 1f;

    [Header("Robot Driving")]
    [SerializeField] private bool driveRobotFromThingsBoardXY = true;
    [SerializeField] private Go2SimpleController go2Controller;
    [SerializeField] private Transform fallbackRobotTransform;
    [SerializeField] private float minimumTargetChangeDistance = 0.05f;

    [Header("Position Mapping")]
    [SerializeField] private float rosMinX = -0.5f;
    [SerializeField] private float rosMaxX = 0.5f;
    [SerializeField] private float rosMinY = -0.5f;
    [SerializeField] private float rosMaxY = 0.5f;

    [SerializeField] private float rosReferenceX = 0f;
    [SerializeField] private float rosReferenceY = 0f;
    [SerializeField] private float unityCenterX = 0f;
    [SerializeField] private float unityCenterZ = 0f;

    [SerializeField] private float unityMinX = -4f;
    [SerializeField] private float unityMaxX = 4f;
    [SerializeField] private float unityMinZ = -2f;
    [SerializeField] private float unityMaxZ = 2f;
    [SerializeField] private float unityHeight = 0.5f;
    [SerializeField] private float wallPadding = 0.15f;

    [Header("Temperature Visualization")]
    [SerializeField] private ShaderGridHeatmap temperatureHeatmap;
    [SerializeField] private bool routeTemperatureToHeatmap = true;

    [Header("Noise Visualization")]
    [SerializeField] private NoiseBubbleGrid noiseBubbleGrid;
    [SerializeField] private bool routeNoiseToBubbles = true;

    [Header("Humidity Visualization")]
    [SerializeField] private SpatioTemporalNoiseTrail humidityTrail;
    [SerializeField] private bool routeHumidityToTrail = true;

    [Header("CO2 / Air Quality Visualization")]
    [SerializeField] private CO2GridLineGraph co2GridLineGraph;
    [SerializeField] private bool routeCO2ToGridLineGraph = true;

    [Header("Duplicate Protection")]
    [SerializeField] private bool skipDuplicateTelemetryTimestamp = true;

    [Header("Debug")]
    [SerializeField] private bool logTelemetry = true;
    [SerializeField] private bool logMissingValues = true;
    [SerializeField] private bool logRobotMovement = false;

    private string jwtToken;
    private long lastProcessedTimestamp = -1;
    private Vector3 lastRobotTarget;
    private bool hasLastRobotTarget = false;

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

    private void Awake()
    {
        if (go2Controller == null)
            go2Controller = FindObjectOfType<Go2SimpleController>();

        if (fallbackRobotTransform == null && go2Controller != null)
            fallbackRobotTransform = go2Controller.transform;
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
            Debug.LogError("ThingsBoardSensorVisualizationRouter: login nije uspio. JWT token je prazan.");
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
            Debug.LogError($"ThingsBoardSensorVisualizationRouter login error: {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
        jwtToken = response.token;

        Debug.Log("ThingsBoardSensorVisualizationRouter: ThingsBoard login uspješan.");
    }

    private IEnumerator GetLatestTelemetry()
    {
        string keys = "x,y,temperature,noise,humidity,co2";
        string url = $"{baseUrl}/api/plugins/telemetry/DEVICE/{deviceId}/values/timeseries?keys={keys}";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("X-Authorization", $"Bearer {jwtToken}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"ThingsBoardSensorVisualizationRouter telemetry error: {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        string rawJson = request.downloadHandler.text;

        bool hasX = TryExtractLatestFloat(rawJson, "x", out float rosX, out long xTs);
        bool hasY = TryExtractLatestFloat(rawJson, "y", out float rosY, out long yTs);
        bool hasTemperature = TryExtractLatestFloat(rawJson, "temperature", out float temperature, out long temperatureTs);
        bool hasNoise = TryExtractLatestFloat(rawJson, "noise", out float noiseDb, out long noiseTs);
        bool hasHumidity = TryExtractLatestFloat(rawJson, "humidity", out float humidityPercent, out long humidityTs);
        bool hasCO2 = TryExtractLatestFloat(rawJson, "co2", out float co2Ppm, out long co2Ts);

        long newestTs = Math.Max(
            Math.Max(xTs, yTs),
            Math.Max(
                Math.Max(temperatureTs, noiseTs),
                Math.Max(humidityTs, co2Ts)
            )
        );

        if (skipDuplicateTelemetryTimestamp && newestTs > 0 && newestTs == lastProcessedTimestamp)
            yield break;

        if (newestTs > 0)
            lastProcessedTimestamp = newestTs;

        if (logMissingValues && (!hasX || !hasY || !hasTemperature || !hasNoise || !hasHumidity || !hasCO2))
        {
            Debug.LogWarning(
                "ThingsBoardSensorVisualizationRouter: missing value. " +
                $"hasX={hasX}, hasY={hasY}, hasTemperature={hasTemperature}, hasNoise={hasNoise}, hasHumidity={hasHumidity}, hasCO2={hasCO2}\n" +
                $"Raw JSON: {rawJson}"
            );
        }

        if (!hasX || !hasY)
        {
            Debug.LogWarning("ThingsBoardSensorVisualizationRouter: nema x/y pa ne mogu mapirati poziciju.");
            yield break;
        }

        Vector3 worldPosition = MapRosToUnity(rosX, rosY);

        if (driveRobotFromThingsBoardXY)
            DriveRobot(worldPosition);

        if (routeTemperatureToHeatmap && hasTemperature)
        {
            if (temperatureHeatmap != null)
                temperatureHeatmap.PaintAtWorldPosition(worldPosition, temperature);
            else
                Debug.LogWarning("ThingsBoardSensorVisualizationRouter: temperatureHeatmap nije postavljen.");
        }

        if (routeNoiseToBubbles && hasNoise)
        {
            if (noiseBubbleGrid != null)
                noiseBubbleGrid.AddNoiseSample(worldPosition, noiseDb);
            else
                Debug.LogWarning("ThingsBoardSensorVisualizationRouter: noiseBubbleGrid nije postavljen.");
        }

        if (routeHumidityToTrail && hasHumidity)
        {
            if (humidityTrail != null)
                humidityTrail.AddSample(worldPosition, humidityPercent);
            else
                Debug.LogWarning("ThingsBoardSensorVisualizationRouter: humidityTrail nije postavljen.");
        }

        if (routeCO2ToGridLineGraph && hasCO2)
        {
            if (co2GridLineGraph != null)
                co2GridLineGraph.AddCO2Sample(worldPosition, co2Ppm);
            else
                Debug.LogWarning("ThingsBoardSensorVisualizationRouter: co2GridLineGraph nije postavljen.");
        }

        if (logTelemetry)
        {
            Debug.Log(
                $"[Sensor Router] ros=({rosX:F2},{rosY:F2}) | unity={worldPosition} | " +
                $"temperature={(hasTemperature ? temperature.ToString("F2", CultureInfo.InvariantCulture) : "N/A")} °C | " +
                $"noise={(hasNoise ? noiseDb.ToString("F2", CultureInfo.InvariantCulture) : "N/A")} dBA | " +
                $"humidity={(hasHumidity ? humidityPercent.ToString("F2", CultureInfo.InvariantCulture) : "N/A")} % | " +
                $"co2={(hasCO2 ? co2Ppm.ToString("F0", CultureInfo.InvariantCulture) : "N/A")} ppm"
            );
        }
    }

    private Vector3 MapRosToUnity(float rosX, float rosY)
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

        Vector3 mappedPosition = new Vector3(mappedX, unityHeight, mappedZ);
        return ClampToRoom(mappedPosition);
    }

    private void DriveRobot(Vector3 targetPosition)
    {
        if (hasLastRobotTarget)
        {
            float distance = Vector3.Distance(lastRobotTarget, targetPosition);

            if (distance < minimumTargetChangeDistance)
                return;
        }

        lastRobotTarget = targetPosition;
        hasLastRobotTarget = true;

        if (go2Controller != null)
        {
            go2Controller.SetNavigationTarget(targetPosition);

            if (logRobotMovement)
                Debug.Log($"Router driving Go2SimpleController to {targetPosition}");

            return;
        }

        if (fallbackRobotTransform != null)
        {
            fallbackRobotTransform.position = targetPosition;

            if (logRobotMovement)
                Debug.Log($"Router directly moving fallbackRobotTransform to {targetPosition}");

            return;
        }

        Debug.LogWarning("ThingsBoardSensorVisualizationRouter: driveRobotFromThingsBoardXY je uključen, ali nema Go2SimpleController ni fallbackRobotTransform.");
    }

    private float MapAroundReference(
        float value,
        float sourceMin,
        float sourceMax,
        float sourceReference,
        float targetMin,
        float targetMax,
        float targetCenter)
    {
        float negativeSourceRange = Mathf.Max(0.0001f, sourceReference - sourceMin);
        float positiveSourceRange = Mathf.Max(0.0001f, sourceMax - sourceReference);

        float negativeTargetRange = Mathf.Max(0.0001f, targetCenter - targetMin);
        float positiveTargetRange = Mathf.Max(0.0001f, targetMax - targetCenter);

        if (value < sourceReference)
        {
            float t = Mathf.InverseLerp(sourceReference, sourceReference - negativeSourceRange, value);
            return Mathf.Lerp(targetCenter, targetCenter - negativeTargetRange, t);
        }
        else
        {
            float t = Mathf.InverseLerp(sourceReference, sourceReference + positiveSourceRange, value);
            return Mathf.Lerp(targetCenter, targetCenter + positiveTargetRange, t);
        }
    }

    private Vector3 ClampToRoom(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, unityMinX + wallPadding, unityMaxX - wallPadding);
        position.z = Mathf.Clamp(position.z, unityMinZ + wallPadding, unityMaxZ - wallPadding);
        position.y = unityHeight;

        return position;
    }

    private bool TryExtractLatestFloat(string json, string key, out float value, out long timestamp)
    {
        value = 0f;
        timestamp = -1;

        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        string escapedKey = Regex.Escape(key);

        string quotedPattern =
            $"\"{escapedKey}\"\\s*:\\s*\\[\\s*\\{{\\s*\"ts\"\\s*:\\s*(\\d+)\\s*,\\s*\"value\"\\s*:\\s*\"([^\"]*)\"\\s*\\}}\\s*\\]";

        Match quotedMatch = Regex.Match(json, quotedPattern);

        if (quotedMatch.Success)
        {
            long.TryParse(quotedMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp);

            return float.TryParse(
                quotedMatch.Groups[2].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value
            );
        }

        string unquotedPattern =
            $"\"{escapedKey}\"\\s*:\\s*\\[\\s*\\{{\\s*\"ts\"\\s*:\\s*(\\d+)\\s*,\\s*\"value\"\\s*:\\s*([^,\\}}\\s]+)\\s*\\}}\\s*\\]";

        Match unquotedMatch = Regex.Match(json, unquotedPattern);

        if (unquotedMatch.Success)
        {
            long.TryParse(unquotedMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp);

            string rawValue = unquotedMatch.Groups[2].Value.Trim().Trim('"');

            return float.TryParse(
                rawValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value
            );
        }

        return false;
    }
}