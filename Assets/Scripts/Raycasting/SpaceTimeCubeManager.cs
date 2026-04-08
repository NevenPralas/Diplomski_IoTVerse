using UnityEngine;
using UnityEngine.InputSystem;

public class SpaceTimeCubeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;
    [SerializeField] private GridCellCursor gridCellCursor;

    [Header("Input")]
    [SerializeField] private InputActionReference placeColumnAction;

    [Header("Cube Settings")]
    [SerializeField] private float cubeHeight = 0.5f;
    [SerializeField] private Material cubeMaterial;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioSource audioSource;

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
            TryPlaceAtCursor();
        }
    }

    private void TryPlaceAtCursor()
    {
        if (heatmap == null || gridCellCursor == null)
        {
            Debug.LogWarning("SpaceTimeCubeManager: heatmap ili gridCellCursor nije postavljen!");
            return;
        }

        if (!gridCellCursor.HasValidCell)
        {
            Debug.Log("Nema validne ciljane ćelije.");
            return;
        }

        Vector2Int cell = gridCellCursor.CurrentCell;
        PlaceColumn(cell.x, cell.y);
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

        Debug.Log($"Stupac stvoren na ćeliji ({gridX}, {gridY})");
    }

    private void PlaySpawnSound(Vector3 position)
    {
        if (spawnSound == null)
        {
            Debug.LogWarning("SpaceTimeCubeManager: spawnSound nije postavljen!");
            return;
        }

        if (audioSource != null)
            audioSource.PlayOneShot(spawnSound);
        else
            AudioSource.PlayClipAtPoint(spawnSound, position);
    }
}