using UnityEngine;

public class CO2CellHoverTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CO2GridLineGraph co2Grid;
    [SerializeField] private SwitcherSensor switcherSensor;

    [Tooltip("Transform iz kojeg ide bijeli laser/ray.")]
    [SerializeField] private Transform rayOrigin;

    [Header("Raycast")]
    [SerializeField] private float rayDistance = 50f;

    [Tooltip("Layer na kojem je CO2Grid, npr. CO2Grid ili HeatmapGrid.")]
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

        if (!interactionEnabled)
            HideHover();
    }

    public void SetVisualizationVisible(bool visible)
    {
        visualizationVisible = visible;

        if (!visualizationVisible)
            HideHover();
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

        if (switcherSensor != null && !switcherSensor.IsAirQualityModeActive())
            return false;

        if (co2Grid == null || rayOrigin == null)
            return false;

        return true;
    }

    private void UpdateHover()
    {
        Vector3 origin = rayOrigin.position;
        Vector3 direction = rayOrigin.forward.normalized;

        if (debugRay)
            Debug.DrawRay(origin, direction * rayDistance, Color.green);

        bool hasHit = Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            rayDistance,
            hoverLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (!hasHit || hit.collider == null)
        {
            HideHover();
            return;
        }

        CO2GridLineGraph hitGrid = hit.collider.GetComponentInParent<CO2GridLineGraph>();

        if (hitGrid == null || hitGrid != co2Grid)
        {
            HideHover();
            return;
        }

        if (!co2Grid.TryGetCellIndex(hit.point, out int gridX, out int gridY))
        {
            HideHover();
            return;
        }

        if (!co2Grid.TryGetDisplayedCO2ForCell(gridX, gridY, out float displayedCO2))
        {
            ShowNoData(hit.point, gridX, gridY);
            return;
        }

        Vector3 cellCenter = co2Grid.GetCellCenterWorld(gridX, gridY);

        tooltipText.text = $"Average CO2: {displayedCO2:F0} ppm";
        ShowHover(cellCenter);
    }

    private void ShowNoData(Vector3 hitPoint, int gridX, int gridY)
    {
        tooltipText.text = $"CO2 ({gridX},{gridY}): no data";
        ShowHover(hitPoint);
    }

    private void ShowHover(Vector3 worldPoint)
    {
        if (tooltipObject == null || tooltipText == null)
            return;

        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        Vector3 up = Vector3.up;

        tooltipObject.transform.position =
            worldPoint +
            right * tooltipOffsetRight +
            up * tooltipOffsetUp;

        if (!tooltipObject.activeSelf)
            tooltipObject.SetActive(true);

        if (showMarker && markerObject != null)
        {
            markerObject.transform.position = worldPoint + Vector3.up * 0.025f;
            markerObject.transform.localScale = Vector3.one * markerRadius;

            if (!markerObject.activeSelf)
                markerObject.SetActive(true);
        }
        else if (markerObject != null)
        {
            markerObject.SetActive(false);
        }
    }

    private void HideHover()
    {
        if (tooltipObject != null && tooltipObject.activeSelf)
            tooltipObject.SetActive(false);

        if (markerObject != null && markerObject.activeSelf)
            markerObject.SetActive(false);
    }

    private void CreateTooltip()
    {
        tooltipObject = new GameObject("CO2CellHoverTooltip");
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

            if (renderer != null && labelFont.material != null)
                renderer.material = labelFont.material;
        }

        tooltipObject.AddComponent<WorldLabelBillboard>();
    }

    private void CreateMarker()
    {
        if (!showMarker)
            return;

        markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = "CO2CellHoverMarker";
        markerObject.transform.SetParent(transform, true);

        Collider col = markerObject.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        markerRenderer = markerObject.GetComponent<Renderer>();

        if (markerRenderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", markerColor);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", markerColor);

            markerRenderer.material = mat;
        }
    }
}