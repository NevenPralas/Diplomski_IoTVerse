using UnityEngine;

public class NoiseBubbleHoverTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NoiseBubbleGrid noiseBubbleGrid;
    [SerializeField] private SwitcherSensor switcherSensor;

    [Tooltip("Transform iz kojeg ide bijeli laser/ray.")]
    [SerializeField] private Transform rayOrigin;

    [Header("Raycast")]
    [SerializeField] private float rayDistance = 50f;
    [SerializeField] private LayerMask hoverLayerMask = ~0;
    [SerializeField] private bool debugRay = false;

    [Header("Tooltip Visual")]
    [SerializeField] private float tooltipOffsetRight = 0.14f;
    [SerializeField] private float tooltipOffsetUp = 0.10f;
    [SerializeField] private int fontSize = 46;
    [SerializeField] private float characterSize = 0.018f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Font labelFont;

    [Header("Marker Visual")]
    [SerializeField] private bool showMarker = true;
    [SerializeField] private float markerRadius = 0.055f;
    [SerializeField] private Color markerColor = Color.white;

    private GameObject tooltipObject;
    private TextMesh tooltipText;
    private GameObject markerObject;
    private Renderer markerRenderer;
    private bool interactionEnabled = true;
    private bool visualizationVisible = true;

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (!interactionEnabled) HideHover();
    }

    public void SetVisualizationVisible(bool visible)
    {
        visualizationVisible = visible;
        if (!visualizationVisible) HideHover();
    }

    private void Awake()
    {
        if (switcherSensor == null)
            switcherSensor = FindObjectOfType<SwitcherSensor>();

        CreateTooltip();
        CreateMarker();
        HideHover();
    }

    private void Update()
    {
        if (!ShouldAllowHover())
        {
            HideHover();
            return;
        }

        UpdateHover();
    }

    private bool ShouldAllowHover()
    {
        if (!interactionEnabled || !visualizationVisible)
            return false;

        if (switcherSensor != null && !switcherSensor.IsBubbleGridMethodActive())
            return false;

        return noiseBubbleGrid != null && rayOrigin != null;
    }

    private void UpdateHover()
    {
        Vector3 origin = rayOrigin.position;
        Vector3 direction = rayOrigin.forward.normalized;

        if (debugRay)
            Debug.DrawRay(origin, direction * rayDistance, Color.white);

        bool hasHit = Physics.Raycast(origin, direction, out RaycastHit hit, rayDistance, hoverLayerMask, QueryTriggerInteraction.Collide);

        if (!hasHit || hit.collider == null)
        {
            HideHover();
            return;
        }

        NoiseBubbleGrid hitGrid = hit.collider.GetComponentInParent<NoiseBubbleGrid>();

        if (hitGrid == null || hitGrid != noiseBubbleGrid)
        {
            HideHover();
            return;
        }

        if (!noiseBubbleGrid.TryGetClosestBubbleHoverInfo(hit.point, out NoiseBubbleGrid.NoiseBubbleHoverInfo info))
        {
            HideHover();
            return;
        }

        ShowHover(info);
    }

    private void ShowHover(NoiseBubbleGrid.NoiseBubbleHoverInfo info)
    {
        if (tooltipObject == null || tooltipText == null)
            return;

        tooltipText.text = $"{GetCurrentSensorLabel()}: {FormatValue(info.noiseDb)}";

        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        tooltipObject.transform.position = info.bubbleCenter + right * tooltipOffsetRight + Vector3.up * tooltipOffsetUp;

        if (!tooltipObject.activeSelf) tooltipObject.SetActive(true);

        if (showMarker && markerObject != null)
        {
            markerObject.transform.position = info.bubbleCenter;
            markerObject.transform.localScale = Vector3.one * markerRadius;
            if (!markerObject.activeSelf) markerObject.SetActive(true);
        }
        else if (markerObject != null)
        {
            markerObject.SetActive(false);
        }
    }

    private string GetCurrentSensorLabel()
    {
        if (switcherSensor == null) return "Value";
        switch (switcherSensor.CurrentSensorMode)
        {
            case SwitcherSensor.SensorMode.Temperature: return "Temperature";
            case SwitcherSensor.SensorMode.Noise: return "Noise";
            case SwitcherSensor.SensorMode.Humidity: return "Humidity";
            case SwitcherSensor.SensorMode.AirQuality: return "CO2";
            default: return "Value";
        }
    }

    private string FormatValue(float value)
    {
        if (switcherSensor == null) return value.ToString("F1");
        switch (switcherSensor.CurrentSensorMode)
        {
            case SwitcherSensor.SensorMode.Temperature: return value.ToString("F1") + " °C";
            case SwitcherSensor.SensorMode.Noise: return value.ToString("F1") + " dBA";
            case SwitcherSensor.SensorMode.Humidity: return value.ToString("F1") + " %";
            case SwitcherSensor.SensorMode.AirQuality: return value.ToString("F0") + " ppm";
            default: return value.ToString("F1");
        }
    }

    private void HideHover()
    {
        if (tooltipObject != null && tooltipObject.activeSelf) tooltipObject.SetActive(false);
        if (markerObject != null && markerObject.activeSelf) markerObject.SetActive(false);
    }

    private void CreateTooltip()
    {
        tooltipObject = new GameObject("NoiseBubbleHoverTooltip");
        tooltipObject.transform.SetParent(transform, true);
        tooltipText = tooltipObject.AddComponent<TextMesh>();
        tooltipText.fontSize = fontSize;
        tooltipText.characterSize = characterSize;
        tooltipText.anchor = TextAnchor.MiddleLeft;
        tooltipText.alignment = TextAlignment.Left;
        tooltipText.color = textColor;

        if (labelFont != null)
        {
            tooltipText.font = labelFont;
            MeshRenderer renderer = tooltipObject.GetComponent<MeshRenderer>();
            if (renderer != null && labelFont.material != null) renderer.material = labelFont.material;
        }

        tooltipObject.AddComponent<WorldLabelBillboard>();
    }

    private void CreateMarker()
    {
        if (!showMarker) return;

        markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = "NoiseBubbleHoverMarker";
        markerObject.transform.SetParent(transform, true);

        Collider col = markerObject.GetComponent<Collider>();
        if (col != null) Destroy(col);

        markerRenderer = markerObject.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", markerColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", markerColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", markerColor * 1.5f);
            }
            markerRenderer.material = mat;
        }
    }
}
