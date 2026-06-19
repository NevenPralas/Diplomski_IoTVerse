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

    [Header("Mode Source")]
    [Tooltip("Objekt koji ima novu SwitcherSensor skriptu. A watch bira podatak, B watch bira metodu.")]
    [SerializeField] private SwitcherSensor switcherSensor;

    [Tooltip("Ako promijeniš podatak na A satu, brišu se postojeći historyji u svim metodama da se ne miješaju različiti atributi.")]
    [SerializeField] private bool clearVisualizationsWhenSensorChanges = true;

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

    [Header("Visualization Methods")]
    [SerializeField] private ShaderGridHeatmap spaceTimeCubeHeatmap;
    [SerializeField] private NoiseBubbleGrid bubbleGrid;
    [SerializeField] private SpatioTemporalNoiseTrail spatioTemporalTrail;
    [SerializeField] private CO2GridLineGraph lineGraph;

    [Header("Route Selected Sensor Into Methods")]
    [Tooltip("Preporuka: true. Tada se odabrani podatak zapisuje u sve 4 metode, pa kad promijeniš B metodu imaš stanje zadnje minute.")]
    [SerializeField] private bool feedSelectedSensorToAllMethods = true;

    [Header("Temperature Style")]
    [SerializeField] private float minTemperature = 15f;
    [SerializeField] private float maxTemperature = 30f;
    [SerializeField] private Color temperatureLowColor = new Color(0.1f, 0.35f, 1f, 1f);
    [SerializeField] private Color temperatureMiddleColor = new Color(1f, 0.45f, 0.05f, 1f);
    [SerializeField] private Color temperatureHighColor = new Color(1f, 0.05f, 0.02f, 1f);

    [Header("Noise Style")]
    [SerializeField] private float minNoiseDb = 0f;
    [SerializeField] private float maxNoiseDb = 80f;
    [SerializeField] private Color noiseLowColor = new Color(0.1f, 0.35f, 1f, 1f);
    [SerializeField] private Color noiseMiddleColor = new Color(1f, 0.45f, 0.05f, 1f);
    [SerializeField] private Color noiseHighColor = new Color(1f, 0.05f, 0.02f, 1f);

    [Header("Humidity Style")]
    [SerializeField] private float minHumidityPercent = 10f;
    [SerializeField] private float maxHumidityPercent = 90f;
    [SerializeField] private Color humidityLowColor = new Color(0.1f, 0.35f, 1f, 1f);
    [SerializeField] private Color humidityMiddleColor = new Color(1f, 0.45f, 0.05f, 1f);
    [SerializeField] private Color humidityHighColor = new Color(1f, 0.05f, 0.02f, 1f);

    [Header("CO2 Style")]
    [SerializeField] private float minCO2Ppm = 400f;
    [SerializeField] private float maxCO2Ppm = 2000f;
    [SerializeField] private Color co2LowColor = new Color(0.1f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color co2MiddleColor = new Color(1f, 0.70f, 0.05f, 1f);
    [SerializeField] private Color co2HighColor = new Color(0.9f, 0.05f, 0.02f, 1f);

    [Header("Duplicate Protection")]
    [SerializeField] private bool skipDuplicateTelemetryTimestamp = true;

    [Header("Debug")]
    [SerializeField] private bool logTelemetry = true;
    [SerializeField] private bool logMissingValues = true;
    [SerializeField] private bool logRobotMovement = false;
    [SerializeField] private bool logModeChanges = true;

    private string jwtToken;
    private long lastProcessedTimestamp = -1;
    private Vector3 lastRobotTarget;
    private bool hasLastRobotTarget = false;
    private SwitcherSensor.SensorMode lastSensorMode = SwitcherSensor.SensorMode.None;

    [Serializable] private class LoginRequest { public string username; public string password; }
    [Serializable] private class LoginResponse { public string token; public string refreshToken; }

    private struct SelectedValueConfig
    {
        public bool hasValue;
        public float value;
        public string title;
        public string unit;
        public int decimals;
        public float min;
        public float max;
        public Color low;
        public Color middle;
        public Color high;
    }

    private void Awake()
    {
        if (switcherSensor == null)
            switcherSensor = FindObjectOfType<SwitcherSensor>();

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
        LoginRequest loginData = new LoginRequest { username = username, password = password };
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

        long newestTs = Math.Max(Math.Max(xTs, yTs), Math.Max(Math.Max(temperatureTs, noiseTs), Math.Max(humidityTs, co2Ts)));

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

        SwitcherSensor.SensorMode sensorMode = switcherSensor != null
            ? switcherSensor.CurrentSensorMode
            : SwitcherSensor.SensorMode.Temperature;

        SelectedValueConfig selected = GetSelectedValue(
            sensorMode,
            hasTemperature, temperature,
            hasNoise, noiseDb,
            hasHumidity, humidityPercent,
            hasCO2, co2Ppm
        );

        if (clearVisualizationsWhenSensorChanges && sensorMode != lastSensorMode)
        {
            ClearAllMethodHistories();
            if (logModeChanges)
                Debug.Log($"Sensor changed: {lastSensorMode} -> {sensorMode}. Visualization histories cleared.");
        }

        lastSensorMode = sensorMode;

        if (selected.hasValue)
        {
            ConfigureAllMethodsForSelectedSensor(selected);
            RouteSelectedValue(worldPosition, selected.value);
        }

        if (logTelemetry)
        {
            Debug.Log(
                $"[Sensor Router] sensor={sensorMode} | method={(switcherSensor != null ? switcherSensor.CurrentVisualizationMethod.ToString() : "N/A")} | " +
                $"ros=({rosX:F2},{rosY:F2}) | unity={worldPosition} | selected={selected.value.ToString("F2", CultureInfo.InvariantCulture)} {selected.unit}"
            );
        }
    }

    private SelectedValueConfig GetSelectedValue(
        SwitcherSensor.SensorMode mode,
        bool hasTemperature, float temperature,
        bool hasNoise, float noiseDb,
        bool hasHumidity, float humidityPercent,
        bool hasCO2, float co2Ppm)
    {
        switch (mode)
        {
            case SwitcherSensor.SensorMode.Temperature:
                return new SelectedValueConfig { hasValue = hasTemperature, value = temperature, title = "Temperature", unit = "°C", decimals = 1, min = minTemperature, max = maxTemperature, low = temperatureLowColor, middle = temperatureMiddleColor, high = temperatureHighColor };
            case SwitcherSensor.SensorMode.Noise:
                return new SelectedValueConfig { hasValue = hasNoise, value = noiseDb, title = "Noise", unit = "dBA", decimals = 1, min = minNoiseDb, max = maxNoiseDb, low = noiseLowColor, middle = noiseMiddleColor, high = noiseHighColor };
            case SwitcherSensor.SensorMode.Humidity:
                return new SelectedValueConfig { hasValue = hasHumidity, value = humidityPercent, title = "Humidity", unit = "%", decimals = 1, min = minHumidityPercent, max = maxHumidityPercent, low = humidityLowColor, middle = humidityMiddleColor, high = humidityHighColor };
            case SwitcherSensor.SensorMode.AirQuality:
                return new SelectedValueConfig { hasValue = hasCO2, value = co2Ppm, title = "CO2", unit = "ppm", decimals = 0, min = minCO2Ppm, max = maxCO2Ppm, low = co2LowColor, middle = co2MiddleColor, high = co2HighColor };
            default:
                return new SelectedValueConfig { hasValue = false };
        }
    }

    private void ConfigureAllMethodsForSelectedSensor(SelectedValueConfig config)
    {
        if (spaceTimeCubeHeatmap != null)
            spaceTimeCubeHeatmap.ApplyExternalValueGradient(config.min, config.max, config.low, config.middle, config.high, true);

        if (bubbleGrid != null)
            bubbleGrid.ApplyExternalNoiseGradient(config.min, config.max, config.low, config.middle, config.high, true);

        if (spatioTemporalTrail != null)
            spatioTemporalTrail.ApplyExternalValueGradient(config.min, config.max, config.low, config.middle, config.high, true);

        if (lineGraph != null)
            lineGraph.ApplyExternalValueGradient(config.min, config.max, config.low, config.middle, config.high, config.title, config.unit, config.decimals, true);
    }

    private void RouteSelectedValue(Vector3 worldPosition, float value)
    {
        if (feedSelectedSensorToAllMethods)
        {
            if (spaceTimeCubeHeatmap != null) spaceTimeCubeHeatmap.PaintAtWorldPosition(worldPosition, value);
            if (bubbleGrid != null) bubbleGrid.AddNoiseSample(worldPosition, value);
            if (spatioTemporalTrail != null) spatioTemporalTrail.AddSample(worldPosition, value);
            if (lineGraph != null) lineGraph.AddCO2Sample(worldPosition, value);
            return;
        }

        SwitcherSensor.VisualizationMethod method = switcherSensor != null
            ? switcherSensor.CurrentVisualizationMethod
            : SwitcherSensor.VisualizationMethod.SpaceTimeCubes;

        if (method == SwitcherSensor.VisualizationMethod.SpaceTimeCubes && spaceTimeCubeHeatmap != null)
            spaceTimeCubeHeatmap.PaintAtWorldPosition(worldPosition, value);
        else if (method == SwitcherSensor.VisualizationMethod.BubbleGrid && bubbleGrid != null)
            bubbleGrid.AddNoiseSample(worldPosition, value);
        else if (method == SwitcherSensor.VisualizationMethod.SpatioTemporalTrail && spatioTemporalTrail != null)
            spatioTemporalTrail.AddSample(worldPosition, value);
        else if (method == SwitcherSensor.VisualizationMethod.LineGraph && lineGraph != null)
            lineGraph.AddCO2Sample(worldPosition, value);
    }

    private void ClearAllMethodHistories()
    {
        if (spaceTimeCubeHeatmap != null) spaceTimeCubeHeatmap.ClearHeatmap();
        if (bubbleGrid != null) bubbleGrid.ClearBubbles();
        if (spatioTemporalTrail != null) spatioTemporalTrail.ClearTrail();
        if (lineGraph != null) lineGraph.ClearCO2();
    }

    private Vector3 MapRosToUnity(float rosX, float rosY)
    {
        float mappedX = MapAroundReference(rosX, rosMinX, rosMaxX, rosReferenceX, unityMinX + wallPadding, unityMaxX - wallPadding, unityCenterX);
        float mappedZ = MapAroundReference(rosY, rosMinY, rosMaxY, rosReferenceY, unityMinZ + wallPadding, unityMaxZ - wallPadding, unityCenterZ);
        return ClampToRoom(new Vector3(mappedX, unityHeight, mappedZ));
    }

    private void DriveRobot(Vector3 targetPosition)
    {
        if (hasLastRobotTarget && Vector3.Distance(lastRobotTarget, targetPosition) < minimumTargetChangeDistance)
            return;

        lastRobotTarget = targetPosition;
        hasLastRobotTarget = true;

        if (go2Controller != null)
        {
            go2Controller.SetNavigationTarget(targetPosition);
            if (logRobotMovement) Debug.Log($"Router driving Go2SimpleController to {targetPosition}");
            return;
        }

        if (fallbackRobotTransform != null)
        {
            fallbackRobotTransform.position = targetPosition;
            if (logRobotMovement) Debug.Log($"Router directly moving fallbackRobotTransform to {targetPosition}");
            return;
        }

        Debug.LogWarning("ThingsBoardSensorVisualizationRouter: driveRobotFromThingsBoardXY je uključen, ali nema Go2SimpleController ni fallbackRobotTransform.");
    }

    private float MapAroundReference(float value, float sourceMin, float sourceMax, float sourceReference, float targetMin, float targetMax, float targetCenter)
    {
        float sourceHalfRange = Mathf.Max(Mathf.Abs(sourceMax - sourceReference), Mathf.Abs(sourceReference - sourceMin));
        float targetHalfRange = Mathf.Min(Mathf.Abs(targetMax - targetCenter), Mathf.Abs(targetCenter - targetMin));

        if (sourceHalfRange <= 0.0001f)
            return targetCenter;

        float normalizedOffset = (value - sourceReference) / sourceHalfRange;
        return targetCenter + normalizedOffset * targetHalfRange;
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

        string escapedKey = Regex.Escape(key);
        string pattern = $"\\\"{escapedKey}\\\"\\s*:\\s*\\[\\s*{{\\s*\\\"ts\\\"\\s*:\\s*(\\d+)\\s*,\\s*\\\"value\\\"\\s*:\\s*\\\"?([^\\\"}}]+)\\\"?";
        Match match = Regex.Match(json, pattern);

        if (!match.Success)
            return false;

        long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp);
        return float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
