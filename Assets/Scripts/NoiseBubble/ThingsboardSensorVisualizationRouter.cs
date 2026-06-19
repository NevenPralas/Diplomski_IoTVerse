using System;
using System.Collections;
using System.Collections.Generic;
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

    [Tooltip("Za ovu novu memorijsku logiku ovo treba ostati uključeno. Kad promijeniš A atribut, trenutni vizualni objekti se očiste pa se odmah rekreiraju iz memorije odabrane zadnje minute.")]
    [SerializeField] private bool clearVisualizationsWhenSensorChanges = true;

    [Header("Per Sensor Background Memory")]
    [Tooltip("Ako je uključeno, router pamti zadnju minutu za svaki atribut zasebno, neovisno o tome koji je trenutno aktivan na A satu.")]
    [SerializeField] private bool keepPerSensorHistory = true;

    [Tooltip("Koliko sekundi podataka se pamti za svaki atribut. Stavi isto kao history prozore vizualizacija, npr. 60.")]
    [SerializeField] private float sensorMemorySeconds = 60f;

    [Tooltip("Kad promijeniš A atribut, odabrani atribut se odmah ponovno iscrta iz memorije, umjesto da vizualizacija krene od nule.")]
    [SerializeField] private bool replayHistoryWhenSensorChanges = true;

    [Tooltip("Ako je uključeno, na promjenu A atributa trenutni frame se ne dodaje drugi put jer je već uključen u replay iz memorije.")]
    [SerializeField] private bool skipLiveRouteOnReplayFrame = true;

    [Tooltip("Ako je uključeno, i promjena B metode odmah rekreira trenutno odabrani atribut iz memorije zadnje minute. Ovo je bitno za Space-Time Cubes i Line Graph jer se oni često otvaraju tek nakon promjene metode.")]
    [SerializeField] private bool replayHistoryWhenVisualizationMethodChanges = true;

    [Tooltip("Kad se replay radi zbog promjene A ili B, čiste se runtime historyji metoda, ali se NE resetira interni sat za Space-Time Cubes i Line Graph. Ovo mora ostati uključeno.")]
    [SerializeField] private bool preserveVisualizationClocksDuringReplayClear = true;

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
    [Tooltip("Preporuka: true. Tada odabrani podatak ide u sve 4 metode, pa kad promijeniš B metodu imaš stanje zadnje minute.")]
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
    [SerializeField] private bool logMemoryReplay = true;

    private string jwtToken;
    private long lastProcessedTimestamp = -1;
    private Vector3 lastRobotTarget;
    private bool hasLastRobotTarget = false;
    private SwitcherSensor.SensorMode lastSensorMode = SwitcherSensor.SensorMode.None;
    private SwitcherSensor.VisualizationMethod lastVisualizationMethod = SwitcherSensor.VisualizationMethod.None;

    [Serializable] private class LoginRequest { public string username; public string password; }
    [Serializable] private class LoginResponse { public string token; public string refreshToken; }

    private struct SensorMemorySample
    {
        public Vector3 worldPosition;
        public float value;
        public float unityTime;
        public long telemetryTimestamp;
    }

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

    private readonly Dictionary<SwitcherSensor.SensorMode, List<SensorMemorySample>> sensorMemory =
        new Dictionary<SwitcherSensor.SensorMode, List<SensorMemorySample>>();

    private void Awake()
    {
        if (switcherSensor == null)
            switcherSensor = FindObjectOfType<SwitcherSensor>();

        if (go2Controller == null)
            go2Controller = FindObjectOfType<Go2SimpleController>();

        if (fallbackRobotTransform == null && go2Controller != null)
            fallbackRobotTransform = go2Controller.transform;

        EnsureMemoryBuckets();
    }

    private void OnValidate()
    {
        pollIntervalSeconds = Mathf.Max(0.05f, pollIntervalSeconds);
        sensorMemorySeconds = Mathf.Max(1f, sensorMemorySeconds);
        minimumTargetChangeDistance = Mathf.Max(0f, minimumTargetChangeDistance);
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

        if (keepPerSensorHistory)
        {
            RememberAllAvailableSensorValues(
                worldPosition,
                hasTemperature, temperature, temperatureTs,
                hasNoise, noiseDb, noiseTs,
                hasHumidity, humidityPercent, humidityTs,
                hasCO2, co2Ppm, co2Ts
            );
        }

        SwitcherSensor.SensorMode sensorMode = switcherSensor != null
            ? switcherSensor.CurrentSensorMode
            : SwitcherSensor.SensorMode.Temperature;

        SwitcherSensor.VisualizationMethod visualizationMethod = switcherSensor != null
            ? switcherSensor.CurrentVisualizationMethod
            : SwitcherSensor.VisualizationMethod.SpaceTimeCubes;

        SelectedValueConfig selected = GetSelectedValue(
            sensorMode,
            hasTemperature, temperature,
            hasNoise, noiseDb,
            hasHumidity, humidityPercent,
            hasCO2, co2Ppm
        );

        bool sensorChanged = sensorMode != lastSensorMode;
        bool methodChanged = visualizationMethod != lastVisualizationMethod;
        bool shouldReplayBecauseSensorChanged = sensorChanged && replayHistoryWhenSensorChanges;
        bool shouldReplayBecauseMethodChanged = methodChanged && replayHistoryWhenVisualizationMethodChanges;
        bool replayedHistoryThisFrame = false;

        if (sensorChanged || methodChanged)
        {
            SelectedValueConfig styleConfig = GetStyleConfig(sensorMode);
            ConfigureAllMethodsForSelectedSensor(styleConfig);

            bool shouldClearBeforeReplay = clearVisualizationsWhenSensorChanges && (shouldReplayBecauseSensorChanged || shouldReplayBecauseMethodChanged);

            if (shouldClearBeforeReplay)
                ClearAllMethodHistories(preserveVisualizationClocksDuringReplayClear);
            else if (sensorChanged && clearVisualizationsWhenSensorChanges)
                ClearAllMethodHistories(preserveVisualizationClocksDuringReplayClear);

            if (keepPerSensorHistory && (shouldReplayBecauseSensorChanged || shouldReplayBecauseMethodChanged))
            {
                replayedHistoryThisFrame = ReplaySensorHistory(sensorMode);

                if (logModeChanges)
                {
                    Debug.Log(
                        $"Mode changed | Sensor: {lastSensorMode} -> {sensorMode} | Method: {lastVisualizationMethod} -> {visualizationMethod}. " +
                        $"Replay={(replayedHistoryThisFrame ? "done" : "empty/disabled")}."
                    );
                }
            }
            else if (logModeChanges)
            {
                Debug.Log(
                    $"Mode changed | Sensor: {lastSensorMode} -> {sensorMode} | Method: {lastVisualizationMethod} -> {visualizationMethod}. " +
                    "Replay disabled."
                );
            }
        }

        lastSensorMode = sensorMode;
        lastVisualizationMethod = visualizationMethod;

        if (selected.hasValue)
        {
            ConfigureAllMethodsForSelectedSensor(selected);

            if (!(replayedHistoryThisFrame && skipLiveRouteOnReplayFrame))
                RouteSelectedValue(worldPosition, selected.value);
        }

        if (logTelemetry)
        {
            Debug.Log(
                $"[Sensor Router] sensor={sensorMode} | method={(switcherSensor != null ? switcherSensor.CurrentVisualizationMethod.ToString() : "N/A")} | " +
                $"ros=({rosX:F2},{rosY:F2}) | unity={worldPosition} | selected={(selected.hasValue ? selected.value.ToString("F2", CultureInfo.InvariantCulture) : "NO_VALUE")} {selected.unit}"
            );
        }
    }

    private void EnsureMemoryBuckets()
    {
        EnsureMemoryBucket(SwitcherSensor.SensorMode.Temperature);
        EnsureMemoryBucket(SwitcherSensor.SensorMode.Noise);
        EnsureMemoryBucket(SwitcherSensor.SensorMode.Humidity);
        EnsureMemoryBucket(SwitcherSensor.SensorMode.AirQuality);
    }

    private void EnsureMemoryBucket(SwitcherSensor.SensorMode mode)
    {
        if (!sensorMemory.ContainsKey(mode))
            sensorMemory[mode] = new List<SensorMemorySample>();
    }

    private void RememberAllAvailableSensorValues(
        Vector3 worldPosition,
        bool hasTemperature, float temperature, long temperatureTs,
        bool hasNoise, float noiseDb, long noiseTs,
        bool hasHumidity, float humidityPercent, long humidityTs,
        bool hasCO2, float co2Ppm, long co2Ts)
    {
        if (hasTemperature) RememberSensorValue(SwitcherSensor.SensorMode.Temperature, worldPosition, temperature, temperatureTs);
        if (hasNoise) RememberSensorValue(SwitcherSensor.SensorMode.Noise, worldPosition, noiseDb, noiseTs);
        if (hasHumidity) RememberSensorValue(SwitcherSensor.SensorMode.Humidity, worldPosition, humidityPercent, humidityTs);
        if (hasCO2) RememberSensorValue(SwitcherSensor.SensorMode.AirQuality, worldPosition, co2Ppm, co2Ts);

        PruneAllSensorMemory();
    }

    private void RememberSensorValue(SwitcherSensor.SensorMode mode, Vector3 worldPosition, float value, long telemetryTimestamp)
    {
        EnsureMemoryBucket(mode);
        List<SensorMemorySample> samples = sensorMemory[mode];

        if (samples.Count > 0)
        {
            SensorMemorySample last = samples[samples.Count - 1];
            if (telemetryTimestamp > 0 && last.telemetryTimestamp == telemetryTimestamp)
                return;
        }

        samples.Add(new SensorMemorySample
        {
            worldPosition = worldPosition,
            value = value,
            unityTime = Time.time,
            telemetryTimestamp = telemetryTimestamp
        });
    }

    private void PruneAllSensorMemory()
    {
        float minTime = Time.time - sensorMemorySeconds;

        foreach (KeyValuePair<SwitcherSensor.SensorMode, List<SensorMemorySample>> pair in sensorMemory)
        {
            List<SensorMemorySample> samples = pair.Value;
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i].unityTime < minTime)
                    samples.RemoveAt(i);
            }
        }
    }

    private bool ReplaySensorHistory(SwitcherSensor.SensorMode sensorMode)
    {
        if (!sensorMemory.TryGetValue(sensorMode, out List<SensorMemorySample> samples))
            return false;

        PruneAllSensorMemory();

        SelectedValueConfig config = GetStyleConfig(sensorMode);
        ConfigureAllMethodsForSelectedSensor(config);

        int routed = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            SensorMemorySample sample = samples[i];
            float ageSeconds = Mathf.Clamp(Time.time - sample.unityTime, 0f, sensorMemorySeconds);

            RouteValueWithAge(sample.worldPosition, sample.value, ageSeconds);
            routed++;
        }

        if (logMemoryReplay)
            Debug.Log($"[Sensor Router] Replayed {routed} samples for {sensorMode} from background memory.");

        return routed > 0;
    }

    private SelectedValueConfig GetSelectedValue(
        SwitcherSensor.SensorMode mode,
        bool hasTemperature, float temperature,
        bool hasNoise, float noiseDb,
        bool hasHumidity, float humidityPercent,
        bool hasCO2, float co2Ppm)
    {
        SelectedValueConfig config = GetStyleConfig(mode);

        switch (mode)
        {
            case SwitcherSensor.SensorMode.Temperature:
                config.hasValue = hasTemperature;
                config.value = temperature;
                return config;
            case SwitcherSensor.SensorMode.Noise:
                config.hasValue = hasNoise;
                config.value = noiseDb;
                return config;
            case SwitcherSensor.SensorMode.Humidity:
                config.hasValue = hasHumidity;
                config.value = humidityPercent;
                return config;
            case SwitcherSensor.SensorMode.AirQuality:
                config.hasValue = hasCO2;
                config.value = co2Ppm;
                return config;
            default:
                return new SelectedValueConfig { hasValue = false };
        }
    }

    private SelectedValueConfig GetStyleConfig(SwitcherSensor.SensorMode mode)
    {
        switch (mode)
        {
            case SwitcherSensor.SensorMode.Temperature:
                return new SelectedValueConfig { hasValue = true, title = "Temperature", unit = "°C", decimals = 1, min = minTemperature, max = maxTemperature, low = temperatureLowColor, middle = temperatureMiddleColor, high = temperatureHighColor };
            case SwitcherSensor.SensorMode.Noise:
                return new SelectedValueConfig { hasValue = true, title = "Noise", unit = "dBA", decimals = 1, min = minNoiseDb, max = maxNoiseDb, low = noiseLowColor, middle = noiseMiddleColor, high = noiseHighColor };
            case SwitcherSensor.SensorMode.Humidity:
                return new SelectedValueConfig { hasValue = true, title = "Humidity", unit = "%", decimals = 1, min = minHumidityPercent, max = maxHumidityPercent, low = humidityLowColor, middle = humidityMiddleColor, high = humidityHighColor };
            case SwitcherSensor.SensorMode.AirQuality:
                return new SelectedValueConfig { hasValue = true, title = "CO2", unit = "ppm", decimals = 0, min = minCO2Ppm, max = maxCO2Ppm, low = co2LowColor, middle = co2MiddleColor, high = co2HighColor };
            default:
                return new SelectedValueConfig { hasValue = false, title = "Value", unit = "", decimals = 1, min = 0f, max = 1f, low = Color.blue, middle = Color.yellow, high = Color.red };
        }
    }

    private void ConfigureAllMethodsForSelectedSensor(SelectedValueConfig config)
    {
        if (spaceTimeCubeHeatmap != null)
            spaceTimeCubeHeatmap.ApplyExternalValueGradient(config.min, config.max, config.low, config.middle, config.high, config.title, config.unit, config.decimals, true);

        if (bubbleGrid != null)
            bubbleGrid.ApplyExternalNoiseGradient(config.min, config.max, config.low, config.middle, config.high, true);

        if (spatioTemporalTrail != null)
            spatioTemporalTrail.ApplyExternalValueGradient(config.min, config.max, config.low, config.middle, config.high, true);

        if (lineGraph != null)
            lineGraph.ApplyExternalValueGradient(config.min, config.max, config.low, config.middle, config.high, config.title, config.unit, config.decimals, true);
    }

    private void RouteSelectedValue(Vector3 worldPosition, float value)
    {
        RouteValueInternal(worldPosition, value, 0f, false);
    }

    private void RouteValueWithAge(Vector3 worldPosition, float value, float ageSeconds)
    {
        RouteValueInternal(worldPosition, value, ageSeconds, true);
    }

    private void RouteValueInternal(Vector3 worldPosition, float value, float ageSeconds, bool useAge)
    {
        if (feedSelectedSensorToAllMethods)
        {
            if (spaceTimeCubeHeatmap != null)
            {
                if (useAge) spaceTimeCubeHeatmap.PaintAtWorldPositionWithAge(worldPosition, value, ageSeconds);
                else spaceTimeCubeHeatmap.PaintAtWorldPosition(worldPosition, value);
            }

            if (bubbleGrid != null)
            {
                if (useAge) bubbleGrid.AddNoiseSampleWithAge(worldPosition, value, ageSeconds);
                else bubbleGrid.AddNoiseSample(worldPosition, value);
            }

            if (spatioTemporalTrail != null)
            {
                if (useAge) spatioTemporalTrail.AddSampleWithAge(worldPosition, value, ageSeconds);
                else spatioTemporalTrail.AddSample(worldPosition, value);
            }

            if (lineGraph != null)
            {
                if (useAge) lineGraph.AddCO2SampleWithAge(worldPosition, value, ageSeconds);
                else lineGraph.AddCO2Sample(worldPosition, value);
            }

            return;
        }

        SwitcherSensor.VisualizationMethod method = switcherSensor != null
            ? switcherSensor.CurrentVisualizationMethod
            : SwitcherSensor.VisualizationMethod.SpaceTimeCubes;

        if (method == SwitcherSensor.VisualizationMethod.SpaceTimeCubes && spaceTimeCubeHeatmap != null)
        {
            if (useAge) spaceTimeCubeHeatmap.PaintAtWorldPositionWithAge(worldPosition, value, ageSeconds);
            else spaceTimeCubeHeatmap.PaintAtWorldPosition(worldPosition, value);
        }
        else if (method == SwitcherSensor.VisualizationMethod.BubbleGrid && bubbleGrid != null)
        {
            if (useAge) bubbleGrid.AddNoiseSampleWithAge(worldPosition, value, ageSeconds);
            else bubbleGrid.AddNoiseSample(worldPosition, value);
        }
        else if (method == SwitcherSensor.VisualizationMethod.SpatioTemporalTrail && spatioTemporalTrail != null)
        {
            if (useAge) spatioTemporalTrail.AddSampleWithAge(worldPosition, value, ageSeconds);
            else spatioTemporalTrail.AddSample(worldPosition, value);
        }
        else if (method == SwitcherSensor.VisualizationMethod.LineGraph && lineGraph != null)
        {
            if (useAge) lineGraph.AddCO2SampleWithAge(worldPosition, value, ageSeconds);
            else lineGraph.AddCO2Sample(worldPosition, value);
        }
    }

    private void ClearAllMethodHistories(bool preserveVisualizationClocks)
    {
        // Za replay zadnje minute NE smijemo resetirati interne satove heatmapa i line grapha.
        // Inače PaintAtWorldPositionWithAge/AddCO2SampleWithAge sve stare uzorke zalijepe na vrijeme 0
        // i Space-Time Cubes / Line Graph izgledaju kao da nemaju prethodnu minutu.
        bool resetClock = !preserveVisualizationClocks;

        if (spaceTimeCubeHeatmap != null) spaceTimeCubeHeatmap.ClearHeatmap(resetClock);
        if (bubbleGrid != null) bubbleGrid.ClearBubbles();
        if (spatioTemporalTrail != null) spatioTemporalTrail.ClearTrail();
        if (lineGraph != null) lineGraph.ClearCO2(resetClock);
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
