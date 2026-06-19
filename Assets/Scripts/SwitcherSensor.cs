using UnityEngine;
using UnityEngine.UI;

public class SwitcherSensor : MonoBehaviour
{
    public enum SensorMode
    {
        None,
        Temperature,
        Noise,
        Humidity,
        AirQuality
    }

    public enum VisualizationMethod
    {
        None,
        SpaceTimeCubes,
        BubbleGrid,
        SpatioTemporalTrail,
        LineGraph
    }

    [Header("Preferred Setup / Two Watches")]
    [Tooltip("WatchSwitcher on the A watch. A cycles the sensor/data type: Temperature -> Noise -> Humidity -> CO2.")]
    [SerializeField] private WatchSwitcher sensorWatchSwitcher;

    [Tooltip("WatchSwitcher on the B watch. B cycles the visualization method: Cubes -> Bubbles -> Trail -> Line graph.")]
    [SerializeField] private WatchSwitcher visualizationMethodWatchSwitcher;

    [Header("Fallback A Watch / Sensor Icons")]
    [Tooltip("Used only if Sensor Watch Switcher is not assigned.")]
    public Image temperatureIcon;
    [Tooltip("Used only if Sensor Watch Switcher is not assigned.")]
    public Image megaphoneIcon;
    [Tooltip("Used only if Sensor Watch Switcher is not assigned.")]
    public Image humidityIcon;
    [Tooltip("Used only if Sensor Watch Switcher is not assigned.")]
    public Image co2Icon;

    [Header("Fallback B Watch / Visualization Method Icons")]
    [Tooltip("Used only if Visualization Method Watch Switcher is not assigned.")]
    public Image cubeMethodIcon;
    [Tooltip("Used only if Visualization Method Watch Switcher is not assigned.")]
    public Image bubbleMethodIcon;
    [Tooltip("Used only if Visualization Method Watch Switcher is not assigned.")]
    public Image trailMethodIcon;
    [Tooltip("Used only if Visualization Method Watch Switcher is not assigned.")]
    public Image lineGraphMethodIcon;

    [Header("Visualization Roots / Managers")]
    [Tooltip("Root or manager for Space-Time Cubes, usually Grid or object with SpaceTimeCubeManager.")]
    public GameObject TemperatureTracker;

    [Tooltip("Root or manager for Bubble Grid, usually NoiseBubbleGridManager.")]
    public GameObject NoiseTracker;

    [Tooltip("Root or manager for Spatio-Temporal Trail, usually HumidityTrailManager.")]
    public GameObject HumidityTracker;

    [Tooltip("Root or manager for Line Graph, usually AirQualityTracker / CO2Grid.")]
    public GameObject AirQualityTracker;

    [Header("Default")]
    [SerializeField] private SensorMode defaultSensorModeWhenNoIconIsGreen = SensorMode.Temperature;
    [SerializeField] private VisualizationMethod defaultVisualizationMethodWhenNoIconIsGreen = VisualizationMethod.SpaceTimeCubes;

    [Header("Behaviour")]
    [SerializeField] private bool keepObjectsActiveAndOnlyHideVisuals = true;
    [SerializeField] private bool enforceVisibilityEveryFrame = true;
    [SerializeField] private bool logModeChanges = true;

    private const string SpaceTimeColumnRootPrefix = "SpaceTimeColumnRoot";
    private const string CO2LineGraphRootPrefix = "CO2LineGraphRoot";

    private SensorMode currentSensorMode = SensorMode.None;
    private VisualizationMethod currentVisualizationMethod = VisualizationMethod.None;

    private SensorMode lastAppliedSensorMode = SensorMode.None;
    private VisualizationMethod lastAppliedVisualizationMethod = VisualizationMethod.None;

    public SensorMode CurrentMode => currentSensorMode;
    public SensorMode CurrentSensorMode => currentSensorMode;
    public VisualizationMethod CurrentVisualizationMethod => currentVisualizationMethod;

    public bool IsTemperatureModeActive() => currentSensorMode == SensorMode.Temperature;
    public bool IsNoiseModeActive() => currentSensorMode == SensorMode.Noise;
    public bool IsHumidityModeActive() => currentSensorMode == SensorMode.Humidity;
    public bool IsAirQualityModeActive() => currentSensorMode == SensorMode.AirQuality;

    public bool IsSpaceTimeCubeMethodActive() => currentVisualizationMethod == VisualizationMethod.SpaceTimeCubes;
    public bool IsBubbleGridMethodActive() => currentVisualizationMethod == VisualizationMethod.BubbleGrid;
    public bool IsTrailMethodActive() => currentVisualizationMethod == VisualizationMethod.SpatioTemporalTrail;
    public bool IsLineGraphMethodActive() => currentVisualizationMethod == VisualizationMethod.LineGraph;

    private void Start()
    {
        EnsureTrackerObjectsAreActive();

        currentSensorMode = GetCurrentSensorMode();
        currentVisualizationMethod = GetCurrentVisualizationMethod();

        ApplyModes(currentSensorMode, currentVisualizationMethod, true);
    }

    private void Update()
    {
        currentSensorMode = GetCurrentSensorMode();
        currentVisualizationMethod = GetCurrentVisualizationMethod();
    }

    private void LateUpdate()
    {
        if (enforceVisibilityEveryFrame)
        {
            ApplyModes(currentSensorMode, currentVisualizationMethod, true);
        }
        else if (currentSensorMode != lastAppliedSensorMode || currentVisualizationMethod != lastAppliedVisualizationMethod)
        {
            ApplyModes(currentSensorMode, currentVisualizationMethod, false);
        }
    }

    private SensorMode GetCurrentSensorMode()
    {
        if (sensorWatchSwitcher != null)
            return SensorModeFromIndex(sensorWatchSwitcher.CurrentIndex);

        if (IsIconGreen(temperatureIcon)) return SensorMode.Temperature;
        if (IsIconGreen(megaphoneIcon)) return SensorMode.Noise;
        if (IsIconGreen(humidityIcon)) return SensorMode.Humidity;
        if (IsIconGreen(co2Icon)) return SensorMode.AirQuality;

        return defaultSensorModeWhenNoIconIsGreen;
    }

    private VisualizationMethod GetCurrentVisualizationMethod()
    {
        if (visualizationMethodWatchSwitcher != null)
            return VisualizationMethodFromIndex(visualizationMethodWatchSwitcher.CurrentIndex);

        if (IsIconGreen(cubeMethodIcon)) return VisualizationMethod.SpaceTimeCubes;
        if (IsIconGreen(bubbleMethodIcon)) return VisualizationMethod.BubbleGrid;
        if (IsIconGreen(trailMethodIcon)) return VisualizationMethod.SpatioTemporalTrail;
        if (IsIconGreen(lineGraphMethodIcon)) return VisualizationMethod.LineGraph;

        return defaultVisualizationMethodWhenNoIconIsGreen;
    }

    private SensorMode SensorModeFromIndex(int index)
    {
        switch (Mathf.Clamp(index, 0, 3))
        {
            case 0: return SensorMode.Temperature;
            case 1: return SensorMode.Noise;
            case 2: return SensorMode.Humidity;
            case 3: return SensorMode.AirQuality;
            default: return defaultSensorModeWhenNoIconIsGreen;
        }
    }

    private VisualizationMethod VisualizationMethodFromIndex(int index)
    {
        switch (Mathf.Clamp(index, 0, 3))
        {
            case 0: return VisualizationMethod.SpaceTimeCubes;
            case 1: return VisualizationMethod.BubbleGrid;
            case 2: return VisualizationMethod.SpatioTemporalTrail;
            case 3: return VisualizationMethod.LineGraph;
            default: return defaultVisualizationMethodWhenNoIconIsGreen;
        }
    }

    private void ApplyModes(SensorMode sensorMode, VisualizationMethod method, bool force)
    {
        if (!force && sensorMode == lastAppliedSensorMode && method == lastAppliedVisualizationMethod)
            return;

        bool showCubes = method == VisualizationMethod.SpaceTimeCubes;
        bool showBubbles = method == VisualizationMethod.BubbleGrid;
        bool showTrail = method == VisualizationMethod.SpatioTemporalTrail;
        bool showLineGraph = method == VisualizationMethod.LineGraph;

        SetTrackerVisible(TemperatureTracker, showCubes);
        SetTrackerVisible(NoiseTracker, showBubbles);
        SetTrackerVisible(HumidityTracker, showTrail);
        SetTrackerVisible(AirQualityTracker, showLineGraph);

        SetRuntimeRootsVisible(SpaceTimeColumnRootPrefix, showCubes);
        SetRuntimeRootsVisible(CO2LineGraphRootPrefix, showLineGraph);

        SetTrackerInteraction(TemperatureTracker, showCubes);
        SetTrackerInteraction(NoiseTracker, showBubbles);
        SetTrackerInteraction(HumidityTracker, showTrail);
        SetTrackerInteraction(AirQualityTracker, showLineGraph);

        SetTrackerVisualizationState(TemperatureTracker, showCubes);
        SetTrackerVisualizationState(NoiseTracker, showBubbles);
        SetTrackerVisualizationState(HumidityTracker, showTrail);
        SetTrackerVisualizationState(AirQualityTracker, showLineGraph);

        if (logModeChanges && (sensorMode != lastAppliedSensorMode || method != lastAppliedVisualizationMethod))
            Debug.Log($"SwitcherSensor changed | Sensor={sensorMode} | Visualization={method}");

        lastAppliedSensorMode = sensorMode;
        lastAppliedVisualizationMethod = method;
    }

    private void EnsureTrackerObjectsAreActive()
    {
        if (!keepObjectsActiveAndOnlyHideVisuals)
            return;

        ForceActive(TemperatureTracker);
        ForceActive(NoiseTracker);
        ForceActive(HumidityTracker);
        ForceActive(AirQualityTracker);
    }

    private void ForceActive(GameObject tracker)
    {
        if (tracker != null && !tracker.activeSelf)
            tracker.SetActive(true);
    }

    private void SetTrackerVisible(GameObject tracker, bool visible)
    {
        if (tracker == null)
            return;

        if (!keepObjectsActiveAndOnlyHideVisuals)
        {
            if (tracker.activeSelf != visible)
                tracker.SetActive(visible);

            return;
        }

        if (!tracker.activeSelf)
            tracker.SetActive(true);

        SetRenderersAndCollidersVisible(tracker, visible);
    }

    private void SetTrackerInteraction(GameObject tracker, bool interactionEnabled)
    {
        if (tracker == null)
            return;

        tracker.BroadcastMessage("SetInteractionEnabled", interactionEnabled, SendMessageOptions.DontRequireReceiver);
    }

    private void SetTrackerVisualizationState(GameObject tracker, bool visible)
    {
        if (tracker == null)
            return;

        tracker.BroadcastMessage("SetVisualizationVisible", visible, SendMessageOptions.DontRequireReceiver);
    }

    private void SetRuntimeRootsVisible(string rootPrefix, bool visible)
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];

            if (t != null && t.name.StartsWith(rootPrefix))
                SetRenderersAndCollidersVisible(t.gameObject, visible);
        }
    }

    private void SetRenderersAndCollidersVisible(GameObject root, bool visible)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = visible;
        }
    }

    private bool IsIconGreen(Image icon)
    {
        if (icon == null)
            return false;

        Color c = icon.color;
        return c == Color.green || (c.g > 0.65f && c.r < 0.45f && c.b < 0.45f);
    }
}
