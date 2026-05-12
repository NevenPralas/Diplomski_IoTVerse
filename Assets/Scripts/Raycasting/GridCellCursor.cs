using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GridCellCursor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;

    [Header("Interactor References")]
    [SerializeField] private NearFarInteractor rightControllerNearFarInteractor;
    [SerializeField] private NearFarInteractor rightHandNearFarInteractor;

    [Header("Aim References")]
    [SerializeField] private Transform rightControllerRayOrigin;
    [SerializeField] private Transform rightHandAimPose;

    [Header("Cursor Visual")]
    [SerializeField] private Transform cursorVisual;
    [SerializeField] private float cursorYOffset = 0.02f;

    [Header("Raycast")]
    [SerializeField] private float raycastDistance = 50f;
    [SerializeField] private LayerMask gridLayerMask;
    [SerializeField] private bool debugRay = true;

    [Header("Average Temperature Tooltip")]
    [SerializeField] private bool showAverageTemperatureTooltip = true;

    [Tooltip("Ako je ukljuceno, labela se ne prikazuje dok je otvoren Space-Time stupac.")]
    [SerializeField] private bool hideAverageTooltipWhileColumnIsOpen = true;

    [Tooltip("Vremenski prozor za racunanje prosjecne temperature u sekundama. 0 ili manje znaci prosjek svih sacuvanih mjerenja za celiju.")]
    [SerializeField] private float averageTemperatureWindowSeconds = 60f;

    [SerializeField] private float tooltipOffsetRight = 0.12f;
    [SerializeField] private float tooltipOffsetUp = 0.06f;
    [SerializeField] private int tooltipFontSize = 48;
    [SerializeField] private float tooltipCharacterSize = 0.018f;
    [SerializeField] private Color tooltipTextColor = Color.white;
    [SerializeField] private Font tooltipFont;

    private bool hasValidCell = false;
    private Vector2Int currentCell = new Vector2Int(-1, -1);
    private Vector3 currentCellCenterWorld;

    private GameObject tooltipObject;
    private TextMesh tooltipText;

    private float nextColumnOpenCheckTime = 0f;
    private bool cachedColumnOpen = false;

    public bool HasValidCell => hasValidCell;
    public Vector2Int CurrentCell => currentCell;
    public Vector3 CurrentCellCenterWorld => currentCellCenterWorld;

    private void Awake()
    {
        if (cursorVisual == null)
            cursorVisual = transform;

        CreateAverageTemperatureTooltip();
        HideCursor();
        HideAverageTemperatureTooltip();
    }

    private void Update()
    {
        UpdateCursor();
    }

    private bool IsHandModeActive()
    {
        return rightHandNearFarInteractor != null &&
               rightHandNearFarInteractor.gameObject.activeInHierarchy &&
               rightHandNearFarInteractor.isActiveAndEnabled;
    }

    private bool IsControllerModeActive()
    {
        return rightControllerNearFarInteractor != null &&
               rightControllerNearFarInteractor.gameObject.activeInHierarchy &&
               rightControllerNearFarInteractor.isActiveAndEnabled;
    }

    private bool TryGetActiveRay(out Vector3 rayOrigin, out Vector3 rayDirection)
    {
        if (IsHandModeActive() && rightHandAimPose != null)
        {
            rayOrigin = rightHandAimPose.position;
            rayDirection = rightHandAimPose.forward.normalized;
            return true;
        }

        if (IsControllerModeActive() && rightControllerRayOrigin != null)
        {
            rayOrigin = rightControllerRayOrigin.position;
            rayDirection = rightControllerRayOrigin.forward.normalized;
            return true;
        }

        if (IsHandModeActive())
        {
            rayOrigin = rightHandNearFarInteractor.transform.position;
            rayDirection = rightHandNearFarInteractor.transform.forward.normalized;
            return true;
        }

        if (IsControllerModeActive())
        {
            rayOrigin = rightControllerNearFarInteractor.transform.position;
            rayDirection = rightControllerNearFarInteractor.transform.forward.normalized;
            return true;
        }

        rayOrigin = Vector3.zero;
        rayDirection = Vector3.forward;
        return false;
    }

    private void UpdateCursor()
    {
        if (heatmap == null)
        {
            HideCursor();
            HideAverageTemperatureTooltip();
            return;
        }

        if (!TryGetActiveRay(out Vector3 rayOrigin, out Vector3 rayDirection))
        {
            HideCursor();
            HideAverageTemperatureTooltip();
            return;
        }

        if (debugRay)
            Debug.DrawRay(rayOrigin, rayDirection * raycastDistance, Color.cyan);

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, raycastDistance, gridLayerMask))
        {
            if (heatmap.TryGetCellIndex(hit.point, out int gridX, out int gridY))
            {
                currentCell = new Vector2Int(gridX, gridY);
                currentCellCenterWorld = heatmap.GetCellCenterWorld(gridX, gridY);
                hasValidCell = true;

                Vector3 cursorPosition = currentCellCenterWorld;
                cursorPosition.y += cursorYOffset;

                cursorVisual.position = cursorPosition;
                ShowCursor();

                UpdateAverageTemperatureTooltip(gridX, gridY, hit.point);
                return;
            }
        }

        HideCursor();
        HideAverageTemperatureTooltip();
    }

    private void ShowCursor()
    {
        hasValidCell = true;

        if (cursorVisual != null && !cursorVisual.gameObject.activeSelf)
            cursorVisual.gameObject.SetActive(true);
    }

    private void HideCursor()
    {
        hasValidCell = false;
        currentCell = new Vector2Int(-1, -1);

        if (cursorVisual != null && cursorVisual.gameObject.activeSelf)
            cursorVisual.gameObject.SetActive(false);
    }

    private void CreateAverageTemperatureTooltip()
    {
        tooltipObject = new GameObject("GridCellAverageTemperatureTooltip");
        tooltipObject.transform.SetParent(transform, false);

        tooltipText = tooltipObject.AddComponent<TextMesh>();
        tooltipText.fontSize = tooltipFontSize;
        tooltipText.characterSize = tooltipCharacterSize;
        tooltipText.anchor = TextAnchor.MiddleLeft;
        tooltipText.alignment = TextAlignment.Left;
        tooltipText.color = tooltipTextColor;

        if (tooltipFont != null)
        {
            tooltipText.font = tooltipFont;

            MeshRenderer renderer = tooltipObject.GetComponent<MeshRenderer>();
            if (renderer != null && tooltipFont.material != null)
                renderer.material = tooltipFont.material;
        }

        tooltipObject.AddComponent<WorldLabelBillboard>();
    }

    private void UpdateAverageTemperatureTooltip(int gridX, int gridY, Vector3 hitPoint)
    {
        if (!showAverageTemperatureTooltip)
        {
            HideAverageTemperatureTooltip();
            return;
        }

        if (hideAverageTooltipWhileColumnIsOpen && IsAnySpaceTimeColumnOpen())
        {
            HideAverageTemperatureTooltip();
            return;
        }

        // Celija se smatra obojanom ako za nju postoji barem jedno mjerenje u povijesti.
        if (!TryCalculateAverageTemperatureForCell(gridX, gridY, averageTemperatureWindowSeconds, out float averageTemperature))
        {
            HideAverageTemperatureTooltip();
            return;
        }

        ShowAverageTemperatureTooltip(averageTemperature, hitPoint);
    }

    private bool TryCalculateAverageTemperatureForCell(int gridX, int gridY, float windowSeconds, out float averageTemperature)
    {
        averageTemperature = 0f;

        if (heatmap == null)
            return false;

        List<ShaderGridHeatmap.CellTemperatureSample> history = heatmap.GetCellHistory(gridX, gridY);

        if (history == null || history.Count == 0)
            return false;

        float currentTime = heatmap.GetRelativeSimulationTime();
        float minAllowedTime = windowSeconds > 0f ? currentTime - windowSeconds : float.NegativeInfinity;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < history.Count; i++)
        {
            ShaderGridHeatmap.CellTemperatureSample sample = history[i];

            if (sample.relativeTime < minAllowedTime)
                continue;

            sum += sample.temperature;
            count++;
        }

        if (count == 0)
            return false;

        averageTemperature = sum / count;
        return true;
    }

    private void ShowAverageTemperatureTooltip(float averageTemperature, Vector3 hitPoint)
    {
        if (tooltipObject == null || tooltipText == null)
            return;

        tooltipText.text = $"Average Temperature: {averageTemperature:F1} \u00B0C";

        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        Vector3 up = Vector3.up;

        tooltipObject.transform.position =
            hitPoint +
            right * tooltipOffsetRight +
            up * tooltipOffsetUp;

        if (!tooltipObject.activeSelf)
            tooltipObject.SetActive(true);
    }

    private void HideAverageTemperatureTooltip()
    {
        if (tooltipObject != null && tooltipObject.activeSelf)
            tooltipObject.SetActive(false);
    }

    private bool IsAnySpaceTimeColumnOpen()
    {
        if (!hideAverageTooltipWhileColumnIsOpen)
            return false;

        // Ne provjeravamo svaki frame da nepotrebno ne obilazimo sve objekte.
        if (Time.time < nextColumnOpenCheckTime)
            return cachedColumnOpen;

        nextColumnOpenCheckTime = Time.time + 0.15f;
        cachedColumnOpen = false;

        Transform[] allTransforms = FindObjectsOfType<Transform>();

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];

            if (t != null && t.gameObject.activeInHierarchy && t.name.StartsWith("SpaceTimeColumnRoot_"))
            {
                cachedColumnOpen = true;
                break;
            }
        }

        return cachedColumnOpen;
    }
}
