using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class Watch2GradientDisplay : MonoBehaviour
{
    private enum GradientMode
    {
        None,
        Temperature,
        Noise,
        Humidity,
        CO2
    }

    [Header("Output")]
    [SerializeField] private Image gradientImage;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text minLabel;
    [SerializeField] private TMP_Text maxLabel;

    [Header("Sensor State Source")]
    [Tooltip("Objekt koji ima tvoju SwitcherSensor skriptu.")]
    [SerializeField] private SwitcherSensor switcherSensor;

    [Header("Data Sources")]
    [Tooltip("Grid objekt koji ima ShaderGridHeatmap za temperaturu.")]
    [SerializeField] private ShaderGridHeatmap temperatureHeatmap;

    [Tooltip("NoiseBubbleGridManager objekt koji ima NoiseBubbleGrid skriptu.")]
    [SerializeField] private NoiseBubbleGrid noiseBubbleGrid;

    [Tooltip("Objekt koji sada koristiš za vlagu/humidity trail.")]
    [SerializeField] private SpatioTemporalNoiseTrail humidityTrail;

    [Tooltip("CO2Grid objekt koji ima CO2GridLineGraph.")]
    [SerializeField] private CO2GridLineGraph co2GridLineGraph;

    [Header("Fallback Noise Range")]
    [Tooltip("Koristi se ako NoiseBubbleGrid nije spojen.")]
    [SerializeField] private float fallbackMinNoiseDb = 35f;

    [Tooltip("Koristi se ako NoiseBubbleGrid nije spojen.")]
    [SerializeField] private float fallbackMaxNoiseDb = 82f;

    [Header("Noise Colors")]
    [SerializeField] private Color noiseLowColor = new Color(0.10f, 0.35f, 1f, 1f);
    [SerializeField] private Color noiseMiddleColor = new Color(0.80f, 0.00f, 1f, 1f);
    [SerializeField] private Color noiseHighColor = new Color(1.00f, 0.05f, 0f, 1f);

    [Header("Humidity Range")]
    [SerializeField] private float minHumidityPercent = 35f;
    [SerializeField] private float maxHumidityPercent = 85f;

    [Header("Humidity Colors")]
    [SerializeField] private Color humidityLowColor = new Color(0.95f, 0.80f, 0.20f, 1f);
    [SerializeField] private Color humidityMiddleColor = new Color(0.15f, 0.70f, 1.00f, 1f);
    [SerializeField] private Color humidityHighColor = new Color(0.05f, 0.15f, 0.95f, 1f);

    [Header("CO2 Range")]
    [SerializeField] private float minCO2Ppm = 400f;
    [SerializeField] private float maxCO2Ppm = 2000f;

    [Header("CO2 Colors")]
    [SerializeField] private Color co2LowColor = new Color(0.10f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color co2MiddleColor = new Color(1.00f, 0.70f, 0.05f, 1f);
    [SerializeField] private Color co2HighColor = new Color(0.90f, 0.05f, 0.02f, 1f);

    [Header("Texture Settings")]
    [SerializeField] private int textureWidth = 256;
    [SerializeField] private int textureHeight = 32;

    [Header("Label Formatting")]
    [SerializeField] private int temperatureDecimals = 1;
    [SerializeField] private int noiseDecimals = 1;
    [SerializeField] private int humidityDecimals = 0;
    [SerializeField] private int co2Decimals = 0;

    [SerializeField] private string temperatureTitle = "Temperature/°C";
    [SerializeField] private string noiseTitle = "Noise/dB";
    [SerializeField] private string humidityTitle = "Humidity/%";
    [SerializeField] private string co2Title = "CO2/ppm";

    [Header("Behaviour")]
    [SerializeField] private bool hideWhenNoSensorActive = true;
    [SerializeField] private bool updateEveryFrame = true;
    [SerializeField] private float updateInterval = 0.15f;

    [Header("Editor Preview")]
    [SerializeField] private bool showPreviewInEditor = true;
    [SerializeField] private GradientMode editorPreviewMode = GradientMode.Temperature;

    [Header("Debug")]
    [SerializeField] private bool logChanges = false;

    private Texture2D gradientTexture;
    private Sprite gradientSprite;

    private GradientMode lastMode = GradientMode.None;
    private string lastSignature = "";
    private float nextUpdateTime = 0f;

    private void Awake()
    {
        FindReferencesIfMissing();
        ForceRefresh();
    }

    private void OnEnable()
    {
        FindReferencesIfMissing();
        ForceRefresh();
    }

    private void OnValidate()
    {
        textureWidth = Mathf.Max(8, textureWidth);
        textureHeight = Mathf.Max(2, textureHeight);

        minHumidityPercent = Mathf.Min(minHumidityPercent, maxHumidityPercent - 0.01f);
        maxHumidityPercent = Mathf.Max(maxHumidityPercent, minHumidityPercent + 0.01f);

        minCO2Ppm = Mathf.Min(minCO2Ppm, maxCO2Ppm - 1f);
        maxCO2Ppm = Mathf.Max(maxCO2Ppm, minCO2Ppm + 1f);

        fallbackMinNoiseDb = Mathf.Min(fallbackMinNoiseDb, fallbackMaxNoiseDb - 0.01f);
        fallbackMaxNoiseDb = Mathf.Max(fallbackMaxNoiseDb, fallbackMinNoiseDb + 0.01f);

        FindReferencesIfMissing();
        ForceRefresh();
    }

    private void Update()
    {
        FindReferencesIfMissing();

        if (gradientImage == null)
            return;

        if (Application.isPlaying && updateEveryFrame == false)
        {
            if (Time.time < nextUpdateTime)
                return;

            nextUpdateTime = Time.time + updateInterval;
        }

        GradientMode mode = GetCurrentMode();

        if (mode == GradientMode.None)
        {
            if (showPreviewInEditor && !Application.isPlaying)
            {
                mode = editorPreviewMode;
            }
            else if (hideWhenNoSensorActive)
            {
                ApplyVisibility(false);
                lastMode = GradientMode.None;
                return;
            }
        }

        ApplyVisibility(true);

        if (mode == GradientMode.Temperature)
        {
            UpdateTemperatureGradient();
        }
        else if (mode == GradientMode.Noise)
        {
            UpdateNoiseGradient();
        }
        else if (mode == GradientMode.Humidity)
        {
            UpdateHumidityGradient();
        }
        else if (mode == GradientMode.CO2)
        {
            UpdateCO2Gradient();
        }

        if (logChanges && mode != lastMode)
            Debug.Log("Watch2 gradient mode changed to: " + mode);

        lastMode = mode;
    }

    public void ForceRefresh()
    {
        lastSignature = "";
        lastMode = GradientMode.None;
    }

    private void ApplyVisibility(bool visible)
    {
        if (gradientImage != null)
            gradientImage.gameObject.SetActive(visible);

        if (titleLabel != null)
            titleLabel.gameObject.SetActive(visible);

        if (minLabel != null)
            minLabel.gameObject.SetActive(visible);

        if (maxLabel != null)
            maxLabel.gameObject.SetActive(visible);
    }

    private GradientMode GetCurrentMode()
    {
        if (switcherSensor == null)
            return GradientMode.None;

        if (IsIconGreen(switcherSensor.temperatureIcon))
            return GradientMode.Temperature;

        if (IsIconGreen(switcherSensor.megaphoneIcon))
            return GradientMode.Noise;

        if (IsIconGreen(switcherSensor.humidityIcon))
            return GradientMode.Humidity;

        if (IsIconGreen(switcherSensor.co2Icon))
            return GradientMode.CO2;

        return GradientMode.None;
    }

    private bool IsIconGreen(Image image)
    {
        if (image == null)
            return false;

        Color c = image.color;

        bool exactGreen = c == Color.green;
        bool visuallyGreen = c.g > 0.65f && c.r < 0.45f && c.b < 0.45f;

        return exactGreen || visuallyGreen;
    }

    private void UpdateTemperatureGradient()
    {
        if (temperatureHeatmap == null)
            return;

        float min = temperatureHeatmap.GetMinTemperature();
        float max = temperatureHeatmap.GetMaxTemperature();

        Color lowColor = MakeUiColor(temperatureHeatmap.GetColorForTemperature(min));
        Color highColor = MakeUiColor(temperatureHeatmap.GetColorForTemperature(max));

        string signature =
            "TEMP|" +
            min.ToString("F3") + "|" +
            max.ToString("F3") + "|" +
            ColorUtility.ToHtmlStringRGBA(lowColor) + "|" +
            ColorUtility.ToHtmlStringRGBA(highColor);

        if (signature != lastSignature || lastMode != GradientMode.Temperature)
        {
            BuildGradientTexture(t =>
            {
                float value = Mathf.Lerp(min, max, t);
                return MakeUiColor(temperatureHeatmap.GetColorForTemperature(value));
            });

            UpdateTitle(temperatureTitle);
            UpdateValueLabels(min, max, temperatureDecimals);

            lastSignature = signature;
        }
    }

    private void UpdateNoiseGradient()
    {
        float min = noiseBubbleGrid != null ? noiseBubbleGrid.MinNoiseDb : fallbackMinNoiseDb;
        float max = noiseBubbleGrid != null ? noiseBubbleGrid.MaxNoiseDb : fallbackMaxNoiseDb;

        Color lowColor = GetNoiseColor(min, min, max);
        Color highColor = GetNoiseColor(max, min, max);

        string signature =
            "NOISE_BUBBLE|" +
            min.ToString("F3") + "|" +
            max.ToString("F3") + "|" +
            ColorUtility.ToHtmlStringRGBA(lowColor) + "|" +
            ColorUtility.ToHtmlStringRGBA(highColor);

        if (signature != lastSignature || lastMode != GradientMode.Noise)
        {
            BuildGradientTexture(t =>
            {
                float value = Mathf.Lerp(min, max, t);
                return GetNoiseColor(value, min, max);
            });

            UpdateTitle(noiseTitle);
            UpdateValueLabels(min, max, noiseDecimals);

            lastSignature = signature;
        }
    }

    private void UpdateHumidityGradient()
    {
        float min = minHumidityPercent;
        float max = maxHumidityPercent;

        Color lowColor = GetHumidityColor(min);
        Color highColor = GetHumidityColor(max);

        string signature =
            "HUMIDITY|" +
            min.ToString("F3") + "|" +
            max.ToString("F3") + "|" +
            ColorUtility.ToHtmlStringRGBA(lowColor) + "|" +
            ColorUtility.ToHtmlStringRGBA(highColor);

        if (signature != lastSignature || lastMode != GradientMode.Humidity)
        {
            BuildGradientTexture(t =>
            {
                float value = Mathf.Lerp(min, max, t);
                return GetHumidityColor(value);
            });

            UpdateTitle(humidityTitle);
            UpdateValueLabels(min, max, humidityDecimals);

            lastSignature = signature;
        }
    }

    private void UpdateCO2Gradient()
    {
        float min = minCO2Ppm;
        float max = maxCO2Ppm;

        Color lowColor = GetCO2Color(min);
        Color highColor = GetCO2Color(max);

        string signature =
            "CO2|" +
            min.ToString("F3") + "|" +
            max.ToString("F3") + "|" +
            ColorUtility.ToHtmlStringRGBA(lowColor) + "|" +
            ColorUtility.ToHtmlStringRGBA(highColor);

        if (signature != lastSignature || lastMode != GradientMode.CO2)
        {
            BuildGradientTexture(t =>
            {
                float value = Mathf.Lerp(min, max, t);
                return GetCO2Color(value);
            });

            UpdateTitle(co2Title);
            UpdateValueLabels(min, max, co2Decimals);

            lastSignature = signature;
        }
    }

    private Color GetNoiseColor(float value, float min, float max)
    {
        float t = Mathf.InverseLerp(min, max, value);
        return MakeUiColor(ThreeColorGradient(noiseLowColor, noiseMiddleColor, noiseHighColor, t));
    }

    private Color GetHumidityColor(float humidityPercent)
    {
        float t = Mathf.InverseLerp(minHumidityPercent, maxHumidityPercent, humidityPercent);
        return MakeUiColor(ThreeColorGradient(humidityLowColor, humidityMiddleColor, humidityHighColor, t));
    }

    private Color GetCO2Color(float co2Ppm)
    {
        float t = Mathf.InverseLerp(minCO2Ppm, maxCO2Ppm, co2Ppm);
        return MakeUiColor(ThreeColorGradient(co2LowColor, co2MiddleColor, co2HighColor, t));
    }

    private Color ThreeColorGradient(Color low, Color middle, Color high, float t)
    {
        t = Mathf.Clamp01(t);

        if (t <= 0.5f)
            return Color.Lerp(low, middle, t / 0.5f);

        return Color.Lerp(middle, high, (t - 0.5f) / 0.5f);
    }

    private void UpdateTitle(string text)
    {
        if (titleLabel != null)
            titleLabel.text = text;
    }

    private void UpdateValueLabels(float min, float max, int decimals)
    {
        string format = "F" + Mathf.Max(0, decimals);

        if (minLabel != null)
            minLabel.text = min.ToString(format);

        if (maxLabel != null)
            maxLabel.text = max.ToString(format);
    }

    private void BuildGradientTexture(System.Func<float, Color> colorSampler)
    {
        if (gradientImage == null)
            return;

        if (gradientTexture == null ||
            gradientTexture.width != textureWidth ||
            gradientTexture.height != textureHeight)
        {
            DestroyGradientAssets();

            gradientTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            gradientTexture.name = "Watch2GradientTexture";
            gradientTexture.wrapMode = TextureWrapMode.Clamp;
            gradientTexture.filterMode = FilterMode.Bilinear;
        }

        for (int x = 0; x < textureWidth; x++)
        {
            float t = textureWidth <= 1 ? 0f : x / (float)(textureWidth - 1);
            Color color = colorSampler(t);
            color.a = 1f;

            for (int y = 0; y < textureHeight; y++)
                gradientTexture.SetPixel(x, y, color);
        }

        gradientTexture.Apply(false);

        if (gradientSprite != null)
        {
            if (Application.isPlaying)
                Destroy(gradientSprite);
            else
                DestroyImmediate(gradientSprite);
        }

        gradientSprite = Sprite.Create(
            gradientTexture,
            new Rect(0, 0, gradientTexture.width, gradientTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        gradientSprite.name = "Watch2GradientSprite";

        gradientImage.sprite = gradientSprite;
        gradientImage.type = Image.Type.Simple;
        gradientImage.preserveAspect = false;
        gradientImage.color = Color.white;
        gradientImage.material = null;
        gradientImage.raycastTarget = false;
    }

    private Color MakeUiColor(Color color)
    {
        color.a = 1f;
        return color;
    }

    private void FindReferencesIfMissing()
    {
        if (gradientImage == null)
        {
            Transform t = transform.Find("Gradient");

            if (t == null)
                t = FindChildDeep(transform, "Gradient");

            if (t != null)
                gradientImage = t.GetComponent<Image>();
        }

        if (titleLabel == null)
        {
            Transform t = FindChildDeep(transform, "TitleLabel");

            if (t != null)
                titleLabel = t.GetComponent<TMP_Text>();
        }

        if (minLabel == null)
        {
            Transform t = FindChildDeep(transform, "MinLabel");

            if (t != null)
                minLabel = t.GetComponent<TMP_Text>();
        }

        if (maxLabel == null)
        {
            Transform t = FindChildDeep(transform, "MaxLabel");

            if (t != null)
                maxLabel = t.GetComponent<TMP_Text>();
        }
    }

    private Transform FindChildDeep(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private void DestroyGradientAssets()
    {
        if (gradientSprite != null)
        {
            if (Application.isPlaying)
                Destroy(gradientSprite);
            else
                DestroyImmediate(gradientSprite);

            gradientSprite = null;
        }

        if (gradientTexture != null)
        {
            if (Application.isPlaying)
                Destroy(gradientTexture);
            else
                DestroyImmediate(gradientTexture);

            gradientTexture = null;
        }
    }

    private void OnDestroy()
    {
        DestroyGradientAssets();
    }
}