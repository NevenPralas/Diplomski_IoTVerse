using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class CO2GridCellCursor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CO2GridLineGraph co2Grid;
    [SerializeField] private SwitcherSensor switcherSensor;

    [Header("Room / Interaction Bounds")]
    [Tooltip("Opcionalni filter koji sprječava ciljanje CO2 ćelija izvan sobe ili preblizu zidu.")]
    [SerializeField] private CellInteractionBounds interactionBounds;
    [SerializeField] private bool useInteractionBounds = true;

    [Header("Interactor References")]
    [SerializeField] private NearFarInteractor rightControllerNearFarInteractor;
    [SerializeField] private NearFarInteractor rightHandNearFarInteractor;

    [Header("Aim References")]
    [SerializeField] private Transform rightControllerRayOrigin;
    [SerializeField] private Transform rightHandAimPose;

    [Header("Ray Priority")]
    [Tooltip("Ako ciljaš rukom, ostavi true. Ako ciljaš controllerom, stavi false.")]
    [SerializeField] private bool preferHandAimPose = true;

    [Header("Cursor Visual")]
    [Tooltip("Ovdje spoji svoj kopirani CO2CellCursor mesh/quad objekt.")]
    [SerializeField] private Transform cursorVisual;

    [SerializeField] private float cursorYOffset = 0.02f;

    [Header("Raycast")]
    [SerializeField] private float raycastDistance = 50f;

    [Tooltip("Layer na kojem je CO2Grid. Nemoj uključiti layer na kojem je sam CO2CellCursor.")]
    [SerializeField] private LayerMask gridLayerMask = ~0;

    [SerializeField] private bool debugRay = false;

    [Header("Behaviour")]
    [Tooltip("Ako je uključeno, cursor se vidi samo u AirQuality/CO2 modu.")]
    [SerializeField] private bool onlyShowInAirQualityMode = true;

    [Tooltip("Ako je uključeno, cursor se vidi i na praznim CO2 ćelijama. To želiš jer klik na praznu ćeliju može otvoriti prazan graf.")]
    [SerializeField] private bool showOnEmptyCells = true;

    private bool hasValidCell = false;
    private Vector2Int currentCell = new Vector2Int(-1, -1);
    private Vector3 currentCellCenterWorld;

    private bool interactionEnabled = true;
    private bool visualizationVisible = true;

    public bool HasValidCell => hasValidCell;
    public Vector2Int CurrentCell => currentCell;
    public Vector3 CurrentCellCenterWorld => currentCellCenterWorld;

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (!interactionEnabled)
            HideCursor();
    }

    public void SetVisualizationVisible(bool visible)
    {
        visualizationVisible = visible;

        if (!visualizationVisible)
            HideCursor();
    }

    private void Awake()
    {
        if (switcherSensor == null)
            switcherSensor = FindObjectOfType<SwitcherSensor>();

        if (cursorVisual == null)
            cursorVisual = transform;

        HideCursor();
    }

    private void Update()
    {
        UpdateCursor();
    }

    private bool ShouldUpdateCursor()
    {
        if (!interactionEnabled || !visualizationVisible)
            return false;

        if (co2Grid == null)
            return false;

        if (onlyShowInAirQualityMode && switcherSensor != null && !switcherSensor.IsAirQualityModeActive())
            return false;

        return true;
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
        if (preferHandAimPose)
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

            if (rightHandAimPose != null)
            {
                rayOrigin = rightHandAimPose.position;
                rayDirection = rightHandAimPose.forward.normalized;
                return true;
            }

            if (rightControllerRayOrigin != null)
            {
                rayOrigin = rightControllerRayOrigin.position;
                rayDirection = rightControllerRayOrigin.forward.normalized;
                return true;
            }
        }
        else
        {
            if (IsControllerModeActive() && rightControllerRayOrigin != null)
            {
                rayOrigin = rightControllerRayOrigin.position;
                rayDirection = rightControllerRayOrigin.forward.normalized;
                return true;
            }

            if (IsHandModeActive() && rightHandAimPose != null)
            {
                rayOrigin = rightHandAimPose.position;
                rayDirection = rightHandAimPose.forward.normalized;
                return true;
            }

            if (rightControllerRayOrigin != null)
            {
                rayOrigin = rightControllerRayOrigin.position;
                rayDirection = rightControllerRayOrigin.forward.normalized;
                return true;
            }

            if (rightHandAimPose != null)
            {
                rayOrigin = rightHandAimPose.position;
                rayDirection = rightHandAimPose.forward.normalized;
                return true;
            }
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
        if (!ShouldUpdateCursor())
        {
            HideCursor();
            return;
        }

        if (!TryGetActiveRay(out Vector3 rayOrigin, out Vector3 rayDirection))
        {
            HideCursor();
            return;
        }

        if (debugRay)
            Debug.DrawRay(rayOrigin, rayDirection * raycastDistance, Color.green);

        if (!TryRaycastCO2Grid(rayOrigin, rayDirection, out RaycastHit hit))
        {
            HideCursor();
            return;
        }

        if (!co2Grid.TryGetCellIndex(hit.point, out int gridX, out int gridY))
        {
            HideCursor();
            return;
        }

        if (!showOnEmptyCells && !co2Grid.TryGetDisplayedCO2ForCell(gridX, gridY, out _))
        {
            HideCursor();
            return;
        }

        if (useInteractionBounds &&
            interactionBounds != null &&
            !interactionBounds.IsCO2CellAllowed(co2Grid, gridX, gridY))
        {
            HideCursor();
            return;
        }

        currentCell = new Vector2Int(gridX, gridY);
        currentCellCenterWorld = co2Grid.GetCellCenterWorld(gridX, gridY);
        hasValidCell = true;

        Vector3 cursorPosition = currentCellCenterWorld;
        cursorPosition.y += cursorYOffset;

        if (cursorVisual != null)
            cursorVisual.position = cursorPosition;

        ShowCursor();
    }

    private bool TryRaycastCO2Grid(Vector3 origin, Vector3 direction, out RaycastHit bestHit)
    {
        bestHit = default;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            raycastDistance,
            gridLayerMask,
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

            if (cursorVisual != null && hit.collider.transform.IsChildOf(cursorVisual))
                continue;

            CO2GridLineGraph hitGrid = hit.collider.GetComponentInParent<CO2GridLineGraph>();

            if (hitGrid == co2Grid)
            {
                bestHit = hit;
                return true;
            }
        }

        return false;
    }

    private void ShowCursor()
    {
        hasValidCell = true;
        SetCursorVisualVisible(true);
    }

    private void HideCursor()
    {
        hasValidCell = false;
        currentCell = new Vector2Int(-1, -1);
        SetCursorVisualVisible(false);
    }

    private void SetCursorVisualVisible(bool visible)
    {
        if (cursorVisual == null)
            return;

        Renderer[] renderers = cursorVisual.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }

        Collider[] colliders = cursorVisual.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = visible;
        }
    }
}