using UnityEngine;

public class DiscreteHeatmap : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridSizeX = 20;
    [SerializeField] private int gridSizeY = 20;
    [SerializeField] private float worldWidth = 10f;
    [SerializeField] private float worldHeight = 10f;
    [SerializeField] private float yOffset = 0.02f;

    [Header("Temperature Mapping")]
    [SerializeField] private float minTemperature = 18f;
    [SerializeField] private float maxTemperature = 22f;

    [Header("References")]
    [SerializeField] private Material cellMaterialTemplate;

    private Renderer[,] cellRenderers;
    private float cellWidth;
    private float cellHeight;
    private Vector3 origin;

    private void Start()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        cellRenderers = new Renderer[gridSizeX, gridSizeY];

        cellWidth = worldWidth / gridSizeX;
        cellHeight = worldHeight / gridSizeY;

        // Donji lijevi kut heatmap područja
        origin = transform.position - new Vector3(worldWidth / 2f, 0f, worldHeight / 2f);

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cell.name = $"Cell_{x}_{y}";
                cell.transform.SetParent(transform);

                float worldX = origin.x + (x + 0.5f) * cellWidth;
                float worldZ = origin.z + (y + 0.5f) * cellHeight;

                cell.transform.position = new Vector3(worldX, transform.position.y + yOffset, worldZ);
                cell.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cell.transform.localScale = new Vector3(cellWidth, cellHeight, 1f);

                Renderer renderer = cell.GetComponent<Renderer>();
                renderer.material = new Material(cellMaterialTemplate);
                renderer.material.color = Color.white;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Collider col = cell.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);

                cellRenderers[x, y] = renderer;
            }
        }
    }

    public void PaintAtWorldPosition(Vector3 worldPosition, float temperature)
    {
        if (TryGetCellIndex(worldPosition, out int gridX, out int gridY))
        {
            PaintCell(gridX, gridY, temperature);
        }
    }

    public void PaintAlongPath(Vector3 startWorldPosition, Vector3 endWorldPosition, float temperature)
    {
        float distance = Vector3.Distance(startWorldPosition, endWorldPosition);

        // Uzorak dovoljno gust da ne preskoči ćelije.
        float sampleStep = Mathf.Min(cellWidth, cellHeight) * 0.35f;

        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleStep));

        int lastGridX = -1;
        int lastGridY = -1;

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = sampleCount == 0 ? 0f : (float)i / sampleCount;
            Vector3 samplePos = Vector3.Lerp(startWorldPosition, endWorldPosition, t);

            if (TryGetCellIndex(samplePos, out int gridX, out int gridY))
            {
                // Da ne bojamo istu ćeliju više puta zaredom bez potrebe
                if (gridX != lastGridX || gridY != lastGridY)
                {
                    PaintCell(gridX, gridY, temperature);
                    lastGridX = gridX;
                    lastGridY = gridY;
                }
            }
        }
    }

    private void PaintCell(int gridX, int gridY, float temperature)
    {
        if (gridX < 0 || gridX >= gridSizeX || gridY < 0 || gridY >= gridSizeY)
            return;

        Color color = GetTemperatureColor(temperature);
        cellRenderers[gridX, gridY].material.color = color;
    }

    public bool TryGetCellIndex(Vector3 worldPosition, out int gridX, out int gridY)
    {
        float localX = worldPosition.x - origin.x;
        float localZ = worldPosition.z - origin.z;

        gridX = Mathf.FloorToInt(localX / cellWidth);
        gridY = Mathf.FloorToInt(localZ / cellHeight);

        return gridX >= 0 && gridX < gridSizeX &&
               gridY >= 0 && gridY < gridSizeY;
    }

    private Color GetTemperatureColor(float temperature)
    {
        float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);

        // Možeš kasnije pojačati kontrast ako želiš
        Color coldColor = new Color(1f, 0.65f, 0.15f); // narančasta
        Color hotColor = new Color(1f, 0.15f, 0.15f);  // crvenija

        return Color.Lerp(coldColor, hotColor, t);
    }

    public void ClearHeatmap()
    {
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                cellRenderers[x, y].material.color = Color.white;
            }
        }
    }
}