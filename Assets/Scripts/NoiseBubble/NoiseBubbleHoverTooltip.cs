using UnityEngine;

public class NoiseBubbleHoverTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NoiseBubbleGrid noiseBubbleGrid;

    [Tooltip("Transform iz kojeg ide bijeli laser/ray. Koristi isti ray origin koji koristiš za ostale hover tooltipe.")]
    [SerializeField] private Transform rayOrigin;

    [Header("Raycast")]
    [SerializeField] private float rayDistance = 50f;

    [Tooltip("Uključi layer na kojem su baloni, npr. Visualization.")]
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

    private void Awake()
    {
        CreateTooltip();
        CreateMarker();
        HideHover();
    }

    private void Update()
    {
        UpdateHover();
    }

    private void UpdateHover()
    {
        if (noiseBubbleGrid == null || rayOrigin == null)
        {
            HideHover();
            return;
        }

        Vector3 origin = rayOrigin.position;
        Vector3 direction = rayOrigin.forward.normalized;

        if (debugRay)
            Debug.DrawRay(origin, direction * rayDistance, Color.white);

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

        NoiseBubbleGrid hitGrid =
            hit.collider.GetComponentInParent<NoiseBubbleGrid>();

        if (hitGrid == null)
            hitGrid = noiseBubbleGrid;

        if (hitGrid != noiseBubbleGrid)
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

        tooltipText.text = $"Noise: {info.noiseDb:F1} dBA";

        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        Vector3 up = Vector3.up;

        tooltipObject.transform.position =
            info.bubbleCenter +
            right * tooltipOffsetRight +
            up * tooltipOffsetUp;

        if (!tooltipObject.activeSelf)
            tooltipObject.SetActive(true);

        if (showMarker && markerObject != null)
        {
            markerObject.transform.position = info.bubbleCenter;
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
        markerObject.name = "NoiseBubbleHoverMarker";
        markerObject.transform.SetParent(transform, true);

        Collider col = markerObject.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        markerRenderer = markerObject.GetComponent<Renderer>();

        if (markerRenderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", markerColor);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", markerColor);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", markerColor * 1.5f);
            }

            markerRenderer.material = mat;
        }
    }
}