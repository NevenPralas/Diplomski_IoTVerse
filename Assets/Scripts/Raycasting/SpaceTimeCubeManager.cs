using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SpaceTimeCubeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;

    [Header("Interactor References")]
    [SerializeField] private NearFarInteractor rightControllerNearFarInteractor;
    [SerializeField] private NearFarInteractor rightHandNearFarInteractor;

    [Header("Aim References")]
    [SerializeField] private Transform rightControllerRayOrigin;
    [SerializeField] private Transform rightHandAimPose;

    [Header("Input")]
    [SerializeField] private InputActionReference placeColumnAction;

    [Header("Cube Settings")]
    [SerializeField] private float cubeHeight = 0.5f;
    [SerializeField] private Material cubeMaterial;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Raycast")]
    [SerializeField] private float raycastDistance = 200f;
    [SerializeField] private bool debugRay = true;

    private GameObject activeColumn = null;
    private Vector2Int activeCell = new Vector2Int(-1, -1);

    private void OnEnable()
    {
        if (placeColumnAction != null)
            placeColumnAction.action.Enable();
    }

    private void OnDisable()
    {
        if (placeColumnAction != null)
            placeColumnAction.action.Disable();
    }

    private void Update()
    {
        if (placeColumnAction == null)
            return;

        if (placeColumnAction.action.WasPressedThisFrame())
        {
            TrySelectCell();
        }
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
        // 1) Ako je hand aktivan, koristi Aim Pose
        if (IsHandModeActive() && rightHandAimPose != null)
        {
            rayOrigin = rightHandAimPose.position;
            rayDirection = rightHandAimPose.forward.normalized;
            return true;
        }

        // 2) Inače koristi controller ray origin
        if (IsControllerModeActive() && rightControllerRayOrigin != null)
        {
            rayOrigin = rightControllerRayOrigin.position;
            rayDirection = rightControllerRayOrigin.forward.normalized;
            return true;
        }

        // 3) Fallback na same interaktore ako baš treba
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

    private void TrySelectCell()
    {
        if (heatmap == null)
        {
            Debug.LogWarning("SpaceTimeCubeManager: heatmap nije postavljen!");
            return;
        }

        if (!TryGetActiveRay(out Vector3 rayOrigin, out Vector3 rayDirection))
        {
            Debug.LogWarning("SpaceTimeCubeManager: nema aktivnog ray sourcea!");
            return;
        }

        if (debugRay)
            Debug.DrawRay(rayOrigin, rayDirection * raycastDistance, Color.red, 1.5f);

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, raycastDistance))
        {
            Debug.Log($"Raycast pogodio: {hit.collider.gameObject.name} na poziciji {hit.point}");

            if (heatmap.TryGetCellIndex(hit.point, out int gridX, out int gridY))
            {
                Debug.Log($"Pogođena ćelija: ({gridX}, {gridY})");
                PlaceColumn(gridX, gridY);
            }
            else
            {
                Debug.Log("Raycast je pogodio objekt, ali nije unutar heatmap grida.");
            }
        }
        else
        {
            Debug.Log("Raycast nije pogodio ništa.");
        }
    }

    private void PlaceColumn(int gridX, int gridY)
    {
        Vector2Int newCell = new Vector2Int(gridX, gridY);

        if (activeCell == newCell && activeColumn != null)
        {
            Destroy(activeColumn);
            activeColumn = null;
            activeCell = new Vector2Int(-1, -1);
            Debug.Log("Stupac uklonjen.");
            return;
        }

        if (activeColumn != null)
        {
            Destroy(activeColumn);
            activeColumn = null;
        }

        float cellW = heatmap.GetCellWidth();
        float cellD = heatmap.GetCellHeight();
        Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);

        Vector3 columnPosition = new Vector3(
            cellCenter.x,
            cellCenter.y + cubeHeight / 2f,
            cellCenter.z
        );

        activeColumn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        activeColumn.name = $"SpaceTimeColumn_{gridX}_{gridY}";
        activeColumn.transform.position = columnPosition;
        activeColumn.transform.localScale = new Vector3(cellW, cubeHeight, cellD);

        Collider col = activeColumn.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        if (cubeMaterial != null)
            activeColumn.GetComponent<Renderer>().material = cubeMaterial;
        else
            activeColumn.GetComponent<Renderer>().material.color = Color.yellow;

        ColumnAnimator animator = activeColumn.AddComponent<ColumnAnimator>();
        animator.Init(cellW, cubeHeight, cellD);

        PlaySpawnSound(columnPosition);

        activeCell = newCell;

        Debug.Log(
            $"Stupac stvoren na ćeliji ({gridX}, {gridY}) | veličina: ({cellW:F3}, {cubeHeight}, {cellD:F3})"
        );
    }

    private void PlaySpawnSound(Vector3 position)
    {
        if (spawnSound == null)
        {
            Debug.LogWarning("SpaceTimeCubeManager: spawnSound nije postavljen!");
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }
        else
        {
            AudioSource.PlayClipAtPoint(spawnSound, position);
        }
    }
}