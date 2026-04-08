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

    private bool hasValidCell = false;
    private Vector2Int currentCell = new Vector2Int(-1, -1);
    private Vector3 currentCellCenterWorld;

    public bool HasValidCell => hasValidCell;
    public Vector2Int CurrentCell => currentCell;
    public Vector3 CurrentCellCenterWorld => currentCellCenterWorld;

    private void Awake()
    {
        if (cursorVisual == null)
            cursorVisual = transform;

        HideCursor();
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
            return;
        }

        if (!TryGetActiveRay(out Vector3 rayOrigin, out Vector3 rayDirection))
        {
            HideCursor();
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
                return;
            }
        }

        HideCursor();
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
}