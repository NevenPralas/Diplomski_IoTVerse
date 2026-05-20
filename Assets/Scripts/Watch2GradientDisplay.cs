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
        Noise
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
    [Tooltip("Grid objekt koji ima ShaderGridHeatmap.")]
    [SerializeField] private ShaderGridHeatmap temperatureHeatmap;

    [Tooltip("NoiseTrailManager objekt koji ima SpatioTemporalNoiseTrail.")]
    [SerializeField] private SpatioTemporalNoiseTrail noiseTrail;

    [Header("Texture Settings")]
    [SerializeField] private int textureWidth = 256;
    [SerializeField] private int textureHeight = 32;

    [Header("Label Formatting")]
    [SerializeField] private int temperatureDecimals = 1;
    [SerializeField] private int noiseDecimals = 1;

    [SerializeField] private string temperatureTitle = "Temperature/°C";
    [SerializeField] private string noiseTitle = "Noise/dB";

    [Header("Behaviour")]
    [SerializeField] private bool hideWhenNoSensorActive = true;
    [SerializeField] private bool updateEveryFrame = true;
    [SerializeField] private float updateInterval = 0.15f;

    [Header("Editor Preview")]
    [SerializeField] private bool showPreviewInEditor = true;
    [SerializeField] private bool previewTemperature = true;

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
                mode = previewTemperature ? GradientMode.Temperature : GradientMode.Noise;
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

        if (logChanges && mode != lastMode)
        {
            Debug.Log("Watch2 gradient mode changed to: " + mode);
        }

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
        if (noiseTrail == null)
            return;

        float min = noiseTrail.GetMinNoiseDb();
        float max = noiseTrail.GetMaxNoiseDb();

        Color lowColor = MakeUiColor(noiseTrail.GetColorForNoiseDb(min));
        Color highColor = MakeUiColor(noiseTrail.GetColorForNoiseDb(max));

        string signature =
            "NOISE|" +
            min.ToString("F3") + "|" +
            max.ToString("F3") + "|" +
            ColorUtility.ToHtmlStringRGBA(lowColor) + "|" +
            ColorUtility.ToHtmlStringRGBA(highColor);

        if (signature != lastSignature || lastMode != GradientMode.Noise)
        {
            BuildGradientTexture(t =>
            {
                float value = Mathf.Lerp(min, max, t);
                return MakeUiColor(noiseTrail.GetColorForNoiseDb(value));
            });

            UpdateTitle(noiseTitle);
            UpdateValueLabels(min, max, noiseDecimals);

            lastSignature = signature;
        }
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
            {
                gradientTexture.SetPixel(x, y, color);
            }
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