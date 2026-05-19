using UnityEngine;

public class NoiseTrailHoverTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpatioTemporalNoiseTrail noiseTrail;

    [Tooltip("Transform iz kojeg ide bijeli laser. Koristi isti ray origin koji koristiš za SpaceTimeSliceTooltip.")]
    [SerializeField] private Transform rayOrigin;

    [Header("Raycast")]
    [SerializeField] private float rayDistance = 50f;

    [Tooltip("Uključi layer na kojem je NoiseTrailManager, npr. Visualization.")]
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
    [SerializeField] private float markerRadius = 0.08f;
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
        if (noiseTrail == null || rayOrigin == null)
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

        SpatioTemporalNoiseTrail hitTrail =
            hit.collider.GetComponentInParent<SpatioTemporalNoiseTrail>();

        if (hitTrail == null || hitTrail != noiseTrail)
        {
            HideHover();
            return;
        }

        if (!noiseTrail.TryGetClosestHoverInfo(hit.point, out SpatioTemporalNoiseTrail.NoiseHoverInfo info))
        {
            HideHover();
            return;
        }

        ShowHover(info);
    }

    private void ShowHover(SpatioTemporalNoiseTrail.NoiseHoverInfo info)
    {
        if (tooltipObject == null || tooltipText == null)
            return;

        tooltipText.text = $"Noise: {info.noiseDb:F1} dBA";

        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        Vector3 up = Vector3.up;

        tooltipObject.transform.position =
            info.worldPoint +
            right * tooltipOffsetRight +
            up * tooltipOffsetUp;

        if (!tooltipObject.activeSelf)
            tooltipObject.SetActive(true);

        if (showMarker && markerObject != null)
        {
            markerObject.transform.position = info.worldPoint;
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
        tooltipObject = new GameObject("NoiseTrailHoverTooltip");
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
        markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = "NoiseTrailHoverMarker";
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
                shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            mat.color = markerColor;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", markerColor);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", markerColor);

            markerRenderer.material = mat;
        }
    }
}