using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SpaceTimeCubeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;
    [SerializeField] private NearFarInteractor nearFarInteractor;

    [Header("Cube Settings")]
    [SerializeField] private float cubeWidth = 0.1f;
    [SerializeField] private float cubeHeight = 0.5f;
    [SerializeField] private Material cubeMaterial;

    private InputAction buttonA;
    private GameObject activeColumn = null;
    private Vector2Int activeCell = new Vector2Int(-1, -1);

    private void Awake()
    {
        buttonA = new InputAction(
            binding: "<XRController>{RightHand}/primaryButton"
        );
        buttonA.Enable();
    }

    private void OnDestroy()
    {
        buttonA.Disable();
    }

    private void Update()
    {
        // Provjeri je li gumb A upravo pritisnut (samo u trenutku pritiska, ne dok se drži)
        if (buttonA.WasPressedThisFrame())
        {
            TrySelectCell();
        }
    }

    private void TrySelectCell()
    {
        if (nearFarInteractor == null || heatmap == null)
        {
            Debug.LogWarning("SpaceTimeCubeManager: nearFarInteractor ili heatmap nije postavljen!");
            return;
        }

        // Uzmi poziciju i smjer raycasta iz NearFarInteractora
        Vector3 rayOrigin = nearFarInteractor.transform.position;
        Vector3 rayDirection = nearFarInteractor.transform.forward;

        Debug.DrawRay(rayOrigin, rayDirection * 10f, Color.red, 1f);

        // Bacamo fizički raycast prema gridu
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, 20f))
        {
            Debug.Log($"Raycast pogodio: {hit.collider.gameObject.name} na poziciji {hit.point}");

            // Provjeri je li pogođena točka unutar heatmap grida
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

        // Ako kliknemo na istu ćeliju — makni stupac
        if (activeCell == newCell && activeColumn != null)
        {
            Destroy(activeColumn);
            activeColumn = null;
            activeCell = new Vector2Int(-1, -1);
            Debug.Log("Stupac uklonjen.");
            return;
        }

        // Ukloni stari stupac ako postoji
        if (activeColumn != null)
        {
            Destroy(activeColumn);
            activeColumn = null;
        }

        // Stvori novi stupac
        Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);

        // Stupac raste prema gore od površine grida
        Vector3 columnPosition = new Vector3(
            cellCenter.x,
            cellCenter.y + cubeHeight / 2f,
            cellCenter.z
        );

        activeColumn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        activeColumn.name = $"SpaceTimeColumn_{gridX}_{gridY}";
        activeColumn.transform.position = columnPosition;
        activeColumn.transform.localScale = new Vector3(cubeWidth, cubeHeight, cubeWidth);

        // Makni collider sa stupca da ne ometa buduće raycastove prema gridu
        Collider col = activeColumn.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Postavi materijal ako je dodijeljen
        if (cubeMaterial != null)
        {
            activeColumn.GetComponent<Renderer>().material = cubeMaterial;
        }
        else
        {
            // Defaultni žuti materijal za testiranje
            activeColumn.GetComponent<Renderer>().material.color = Color.yellow;
        }

        activeCell = newCell;
        Debug.Log($"Stupac stvoren na ćeliji ({gridX}, {gridY}) pozicija: {columnPosition}");
    }
}