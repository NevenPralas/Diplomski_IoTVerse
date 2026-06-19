using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SpaceTimeSliceTooltip : MonoBehaviour
{
    [Header("Ray Source")]
    [SerializeField] private NearFarInteractor rightControllerNearFarInteractor;
    [SerializeField] private Transform rightControllerRayOrigin;

    [Header("Raycast")]
    [SerializeField] private float raycastDistance = 50f;
    [SerializeField] private LayerMask hoverLayerMask = ~0;
    [SerializeField] private bool debugRay = false;

    [Header("Tooltip Visual")]
    [SerializeField] private float tooltipOffsetRight = 0.12f;
    [SerializeField] private float tooltipOffsetUp = 0.06f;
    [SerializeField] private int fontSize = 48;
    [SerializeField] private float characterSize = 0.018f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Font labelFont;

    [Header("Hover Highlight")]
    [SerializeField] private bool showWhiteOutline = true;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineExpand = 0.015f;
    [SerializeField] private float outlineLineWidth = 0.01f;

    [Header("Always Visible Pointer")]
    [SerializeField] private bool showPointer = true;
    [SerializeField] private Color pointerColor = Color.white;
    [SerializeField] private float pointerWidth = 0.01f;
    [SerializeField] private float pointerDefaultLength = 3f;
    [SerializeField] private bool pointerStopsAtHit = true;

    private GameObject tooltipObject;
    private TextMesh tooltipText;

    private GameObject outlineRoot;
    private LineRenderer[] outlineLines;

    private GameObject pointerObject;
    private LineRenderer pointerLine;

    private SpaceTimeSliceData currentSlice;

    private void Awake()
    {
        CreateTooltip();
        CreateOutline();
        CreatePointer();

        HideTooltip();
        HideOutline();
    }

    private void Update()
    {
        if (!TryGetRay(out Vector3 rayOrigin, out Vector3 rayDirection))
        {
            HideTooltip();
            HideOutline();
            HidePointer();
            return;
        }

        if (debugRay)
            Debug.DrawRay(rayOrigin, rayDirection * raycastDistance, Color.magenta);

        bool hasHit = Physics.Raycast(
            rayOrigin,
            rayDirection,
            out RaycastHit hit,
            raycastDistance,
            hoverLayerMask
        );

        UpdatePointer(rayOrigin, rayDirection, hasHit, hit);

        if (hasHit)
        {
            SpaceTimeSliceData sliceData = hit.collider.GetComponentInParent<SpaceTimeSliceData>();

            if (sliceData != null)
            {
                ShowTooltip(sliceData, hit.point);
                ShowOutline(hit.collider.transform);
                return;
            }
        }

        HideTooltip();
        HideOutline();
    }

    private bool TryGetRay(out Vector3 rayOrigin, out Vector3 rayDirection)
    {
        if (rightControllerRayOrigin != null)
        {
            rayOrigin = rightControllerRayOrigin.position;
            rayDirection = rightControllerRayOrigin.forward.normalized;
            return true;
        }

        if (rightControllerNearFarInteractor != null &&
            rightControllerNearFarInteractor.gameObject.activeInHierarchy &&
            rightControllerNearFarInteractor.isActiveAndEnabled)
        {
            rayOrigin = rightControllerNearFarInteractor.transform.position;
            rayDirection = rightControllerNearFarInteractor.transform.forward.normalized;
            return true;
        }

        rayOrigin = Vector3.zero;
        rayDirection = Vector3.forward;
        return false;
    }

    private void CreateTooltip()
    {
        tooltipObject = new GameObject("SpaceTimeSliceTooltip");
        tooltipObject.transform.SetParent(transform, false);

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

    private void ShowTooltip(SpaceTimeSliceData sliceData, Vector3 hitPoint)
    {
        currentSlice = sliceData;

        tooltipText.text = sliceData.FormatValueWithTitle();

        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        Vector3 up = Vector3.up;

        tooltipObject.transform.position =
            hitPoint +
            right * tooltipOffsetRight +
            up * tooltipOffsetUp;

        if (!tooltipObject.activeSelf)
            tooltipObject.SetActive(true);
    }

    private void HideTooltip()
    {
        currentSlice = null;

        if (tooltipObject != null && tooltipObject.activeSelf)
            tooltipObject.SetActive(false);
    }

    private void CreatePointer()
    {
        pointerObject = new GameObject("RightHandAlwaysVisiblePointer");
        pointerObject.transform.SetParent(transform, false);

        pointerLine = pointerObject.AddComponent<LineRenderer>();
        pointerLine.positionCount = 2;
        pointerLine.useWorldSpace = true;
        pointerLine.startWidth = pointerWidth;
        pointerLine.endWidth = pointerWidth;
        pointerLine.material = CreateUnlitMaterial(pointerColor);
        pointerLine.startColor = pointerColor;
        pointerLine.endColor = pointerColor;
    }

    private void UpdatePointer(Vector3 rayOrigin, Vector3 rayDirection, bool hasHit, RaycastHit hit)
    {
        if (!showPointer || pointerLine == null)
        {
            HidePointer();
            return;
        }

        Vector3 endPoint;

        if (hasHit && pointerStopsAtHit)
            endPoint = hit.point;
        else
            endPoint = rayOrigin + rayDirection * pointerDefaultLength;

        pointerLine.SetPosition(0, rayOrigin);
        pointerLine.SetPosition(1, endPoint);

        if (!pointerObject.activeSelf)
            pointerObject.SetActive(true);
    }

    private void HidePointer()
    {
        if (pointerObject != null && pointerObject.activeSelf)
            pointerObject.SetActive(false);
    }

    private void CreateOutline()
    {
        outlineRoot = new GameObject("HoveredSpaceTimeSliceWhiteOutline");
        outlineRoot.transform.SetParent(transform, false);

        outlineLines = new LineRenderer[12];

        for (int i = 0; i < outlineLines.Length; i++)
        {
            GameObject lineObject = new GameObject($"OutlineLine_{i}");
            lineObject.transform.SetParent(outlineRoot.transform, false);

            LineRenderer lr = lineObject.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = outlineLineWidth;
            lr.endWidth = outlineLineWidth;
            lr.material = CreateUnlitMaterial(outlineColor);
            lr.startColor = outlineColor;
            lr.endColor = outlineColor;

            outlineLines[i] = lr;
        }
    }

    private void ShowOutline(Transform target)
    {
        if (!showWhiteOutline || outlineRoot == null || outlineLines == null)
        {
            HideOutline();
            return;
        }

        Vector3 center = target.position;

        Vector3 right = target.right;
        Vector3 up = target.up;
        Vector3 forward = target.forward;

        Vector3 halfScale = target.lossyScale * 0.5f;

        float hx = halfScale.x + outlineExpand;
        float hy = halfScale.y + outlineExpand;
        float hz = halfScale.z + outlineExpand;

        Vector3 p000 = center - right * hx - up * hy - forward * hz;
        Vector3 p001 = center - right * hx - up * hy + forward * hz;
        Vector3 p010 = center - right * hx + up * hy - forward * hz;
        Vector3 p011 = center - right * hx + up * hy + forward * hz;
        Vector3 p100 = center + right * hx - up * hy - forward * hz;
        Vector3 p101 = center + right * hx - up * hy + forward * hz;
        Vector3 p110 = center + right * hx + up * hy - forward * hz;
        Vector3 p111 = center + right * hx + up * hy + forward * hz;

        SetLine(0, p000, p100);
        SetLine(1, p001, p101);
        SetLine(2, p010, p110);
        SetLine(3, p011, p111);

        SetLine(4, p000, p010);
        SetLine(5, p001, p011);
        SetLine(6, p100, p110);
        SetLine(7, p101, p111);

        SetLine(8, p000, p001);
        SetLine(9, p010, p011);
        SetLine(10, p100, p101);
        SetLine(11, p110, p111);

        if (!outlineRoot.activeSelf)
            outlineRoot.SetActive(true);
    }

    private void SetLine(int index, Vector3 a, Vector3 b)
    {
        if (outlineLines == null || index < 0 || index >= outlineLines.Length)
            return;

        outlineLines[index].SetPosition(0, a);
        outlineLines[index].SetPosition(1, b);
    }

    private void HideOutline()
    {
        if (outlineRoot != null && outlineRoot.activeSelf)
            outlineRoot.SetActive(false);
    }

    private Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.color = color;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        return mat;
    }
}