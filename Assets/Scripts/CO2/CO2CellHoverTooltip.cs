using UnityEngine;

public class CO2CellHoverTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CO2GridLineGraph co2Grid;
    [SerializeField] private SwitcherSensor switcherSensor;

    [Tooltip("Novi zasebni CO2 cursor. Ako je spojen, tooltip koristi njegovu ćeliju.")]
    [SerializeField] private CO2GridCellCursor co2CellCursor;

    [Tooltip("Fallback transform iz kojeg ide bijeli laser/ray.")]
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
    [SerializeField] private bool showMarker = false;
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

        if (switcherSensor != null && !switcherSensor.IsLineGraphMethodActive())
            return false;

        if (co2Grid == null)
            return false;

        if (co2CellCursor == null && rayOrigin == null)
            return false;

        return true;
    }

    private void UpdateHover()
    {
        if (TryGetCellFromCursor(out int cursorX, out int cursorY))
        {
            UpdateHoverForCell(cursorX, cursorY);
            return;
        }

        if (TryGetCellFromRay(out int rayX, out int rayY, out Vector3 hitPoint))
        {
            UpdateHoverForCell(rayX, rayY, hitPoint);
            return;
        }

        HideHover();
    }

    private bool TryGetCellFromCursor(out int gridX, out int gridY)
    {
        gridX = -1;
        gridY = -1;

        if (co2CellCursor == null || !co2CellCursor.HasValidCell)
            return false;

        Vector2Int cell = co2CellCursor.CurrentCell;
        gridX = cell.x;
        gridY = cell.y;
        return true;
    }

    private bool TryGetCellFromRay(out int gridX, out int gridY, out Vector3 hitPoint)
    {
        gridX = -1;
        gridY = -1;
        hitPoint = Vector3.zero;

        if (rayOrigin == null)
            return false;

        Vector3 origin = rayOrigin.position;
        Vector3 direction = rayOrigin.forward.normalized;

        if (debugRay)
            Debug.DrawRay(origin, direction * rayDistance, Color.green);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            rayDistance,
            hoverLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            CO2GridLineGraph hitGrid = hit.collider.GetComponentInParent<CO2GridLineGraph>();

            if (hitGrid == null || hitGrid != co2Grid)
                continue;

            if (!co2Grid.TryGetCellIndex(hit.point, out gridX, out gridY))
                continue;

            hitPoint = hit.point;
            return true;
        }

        return false;
    }

    private void UpdateHoverForCell(int gridX, int gridY)
    {
        Vector3 cellCenter = co2Grid.GetCellCenterWorld(gridX, gridY);
        UpdateHoverForCell(gridX, gridY, cellCenter);
    }

    private void UpdateHoverForCell(int gridX, int gridY, Vector3 fallbackPoint)
    {
        if (!co2Grid.TryGetDisplayedCO2ForCell(gridX, gridY, out float displayedCO2))
        {
            HideHover();
            return;
        }

        Vector3 cellCenter = co2Grid.GetCellCenterWorld(gridX, gridY);

        tooltipText.text = $"Average {GetCurrentSensorLabel()}: {FormatValue(displayedCO2)}";
        ShowHover(cellCenter);
    }


    private string GetCurrentSensorLabel()
    {
        // Tooltip line grapha mora čitati metapodatke direktno iz CO2GridLineGraph,
        // jer B metoda LineGraph sada može prikazivati Temperature/Noise/Humidity/CO2.
        // Tako se izbjegne situacija da hover kaže jedno, a sam graf još ima staru jedinicu.
        return co2Grid != null ? co2Grid.GetValueTitle() : "Value";
    }

    private string FormatValue(float value)
    {
        string unit = co2Grid != null ? co2Grid.GetValueUnit() : "";
        int decimals = co2Grid != null ? co2Grid.GetValueDecimals() : 1;
        string suffix = string.IsNullOrWhiteSpace(unit) ? "" : " " + unit;
        return value.ToString("F" + Mathf.Clamp(decimals, 0, 3)) + suffix;
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