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

    [Header("Watch Icons")]
    public Image temperatureIcon;
    public Image megaphoneIcon;
    public Image humidityIcon;
    public Image co2Icon;

    [Header("Visualization Roots")]
    public GameObject TemperatureTracker;
    public GameObject NoiseTracker;
    public GameObject HumidityTracker;
    public GameObject AirQualityTracker;

    [Header("Default")]
    [SerializeField] private SensorMode defaultModeWhenNoIconIsGreen = SensorMode.Temperature;

    [Header("Behaviour")]
    [SerializeField] private bool keepObjectsActiveAndOnlyHideVisuals = true;
    [SerializeField] private bool enforceVisibilityEveryFrame = true;
    [SerializeField] private bool logModeChanges = true;

    private const string SpaceTimeColumnRootPrefix = "SpaceTimeColumnRoot";
    private const string CO2LineGraphRootPrefix = "CO2LineGraphRoot";

    private SensorMode currentMode = SensorMode.None;
    private SensorMode lastAppliedMode = SensorMode.None;

    public SensorMode CurrentMode => currentMode;

    public bool IsTemperatureModeActive()
    {
        return currentMode == SensorMode.Temperature;
    }

    public bool IsNoiseModeActive()
    {
        return currentMode == SensorMode.Noise;
    }

    public bool IsHumidityModeActive()
    {
        return currentMode == SensorMode.Humidity;
    }

    public bool IsAirQualityModeActive()
    {
        return currentMode == SensorMode.AirQuality;
    }

    private void Start()
    {
        EnsureTrackerObjectsAreActive();

        currentMode = GetCurrentMode();
        ApplyMode(currentMode, true);
    }

    private void Update()
    {
        currentMode = GetCurrentMode();
    }

    private void LateUpdate()
    {
        if (enforceVisibilityEveryFrame)
        {
            ApplyMode(currentMode, true);
        }
        else if (currentMode != lastAppliedMode)
        {
            ApplyMode(currentMode, false);
        }
    }

    private SensorMode GetCurrentMode()
    {
        if (IsIconGreen(temperatureIcon))
            return SensorMode.Temperature;

        if (IsIconGreen(megaphoneIcon))
            return SensorMode.Noise;

        if (IsIconGreen(humidityIcon))
            return SensorMode.Humidity;

        if (IsIconGreen(co2Icon))
            return SensorMode.AirQuality;

        return defaultModeWhenNoIconIsGreen;
    }

    private void ApplyMode(SensorMode mode, bool force)
    {
        if (!force && mode == lastAppliedMode)
            return;

        bool showTemperature = mode == SensorMode.Temperature;
        bool showNoise = mode == SensorMode.Noise;
        bool showHumidity = mode == SensorMode.Humidity;
        bool showAirQuality = mode == SensorMode.AirQuality;

        SetTrackerVisible(TemperatureTracker, showTemperature);
        SetTrackerVisible(NoiseTracker, showNoise);
        SetTrackerVisible(HumidityTracker, showHumidity);
        SetTrackerVisible(AirQualityTracker, showAirQuality);

        SetRuntimeRootsVisible(SpaceTimeColumnRootPrefix, showTemperature);
        SetRuntimeRootsVisible(CO2LineGraphRootPrefix, showAirQuality);

        SetTrackerInteraction(TemperatureTracker, showTemperature);
        SetTrackerInteraction(NoiseTracker, showNoise);
        SetTrackerInteraction(HumidityTracker, showHumidity);
        SetTrackerInteraction(AirQualityTracker, showAirQuality);

        SetTrackerVisualizationState(TemperatureTracker, showTemperature);
        SetTrackerVisualizationState(NoiseTracker, showNoise);
        SetTrackerVisualizationState(HumidityTracker, showHumidity);
        SetTrackerVisualizationState(AirQualityTracker, showAirQuality);

        if (logModeChanges && mode != lastAppliedMode)
            Debug.Log($"SwitcherSensor mode changed to: {mode}");

        lastAppliedMode = mode;
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

        tracker.BroadcastMessage(
            "SetInteractionEnabled",
            interactionEnabled,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private void SetTrackerVisualizationState(GameObject tracker, bool visible)
    {
        if (tracker == null)
            return;

        tracker.BroadcastMessage(
            "SetVisualizationVisible",
            visible,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private void SetRuntimeRootsVisible(string rootPrefix, bool visible)
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);

        foreach (Transform t in allTransforms)
        {
            if (t != null && t.name.StartsWith(rootPrefix))
            {
                SetRenderersAndCollidersVisible(t.gameObject, visible);
            }
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

        bool exactGreen = c == Color.green;
        bool visuallyGreen = c.g > 0.65f && c.r < 0.45f && c.b < 0.45f;

        return exactGreen || visuallyGreen;
    }
}