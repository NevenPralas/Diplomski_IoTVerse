using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ShaderGridHeatmap : MonoBehaviour
{
    [Header("Target Renderer")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Shader Property Names")]
    [SerializeField] private string heatmapTextureProperty = "_HeatmapTex";
    [SerializeField] private string gridSizeProperty = "_Size";

    [Header("Grid Settings")]
    [SerializeField] private int gridSizeX = 10;
    [SerializeField] private int gridSizeY = 10;
    [SerializeField] private float worldWidth = 10f;
    [SerializeField] private float worldHeight = 10f;

    [Header("Shader Grid Baseline")]
    [SerializeField] private float baselineGridX = 10f;
    [SerializeField] private float baselineGridY = 10f;

    [Header("Temperature Mapping")]
    [SerializeField] private float minTemperature = 18f;
    [SerializeField] private float maxTemperature = 22f;

    private Texture2D heatmapTexture;
    private Material runtimeMaterial;

    private float cellWidth;
    private float cellHeight;
    private Vector3 origin;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        InitializeHeatmap();

       /* Debug.Log("ShaderGridHeatmap Awake radi");

        PaintAtWorldPosition(new Vector3(0f, 0f, 0f), 18f);
        PaintAtWorldPosition(new Vector3(1f, 0f, 1f), 20f);
        PaintAtWorldPosition(new Vector3(2f, 0f, 2f), 22f);

        Debug.Log("Test boje upisane u heatmap teksturu"); */
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        InitializeHeatmap();
    }
#endif

    private void InitializeHeatmap()
    {
        if (gridSizeX <= 0) gridSizeX = 1;
        if (gridSizeY <= 0) gridSizeY = 1;
        if (baselineGridX <= 0f) baselineGridX = 1f;
        if (baselineGridY <= 0f) baselineGridY = 1f;

        cellWidth = worldWidth / gridSizeX;
        cellHeight = worldHeight / gridSizeY;

        origin = transform.position - new Vector3(worldWidth / 2f, 0f, worldHeight / 2f);

        heatmapTexture = new Texture2D(gridSizeX, gridSizeY, TextureFormat.RGBA32, false);
        heatmapTexture.wrapMode = TextureWrapMode.Clamp;
        heatmapTexture.filterMode = FilterMode.Point;

        ClearTexture();

        runtimeMaterial = targetRenderer.material;

        runtimeMaterial.SetTexture(heatmapTextureProperty, heatmapTexture);

        // Dinamičko usklađivanje sa shader gridom:
        // ako je baseline 10x10 kad je Size = 1,1
        // onda za 20x20 šaljemo Size = 2,2
        float shaderSizeX = gridSizeX / baselineGridX;
        float shaderSizeY = gridSizeY / baselineGridY;

        runtimeMaterial.SetVector(gridSizeProperty, new Vector4(shaderSizeX, shaderSizeY, 0f, 0f));
    }

    public void PaintAtWorldPosition(Vector3 worldPosition, float temperature)
    {
        if (TryGetCellIndex(worldPosition, out int gridX, out int gridY))
        {
            PaintCell(gridX, gridY, temperature);
            ApplyTexture();
        }
    }

    public void PaintAlongPath(Vector3 startWorldPosition, Vector3 endWorldPosition, float temperature)
    {
        float distance = Vector3.Distance(startWorldPosition, endWorldPosition);

        float sampleStep = Mathf.Min(cellWidth, cellHeight) * 0.35f;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleStep));

        int lastGridX = -1;
        int lastGridY = -1;
        bool changed = false;

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = sampleCount == 0 ? 0f : (float)i / sampleCount;
            Vector3 samplePos = Vector3.Lerp(startWorldPosition, endWorldPosition, t);

            if (TryGetCellIndex(samplePos, out int gridX, out int gridY))
            {
                if (gridX != lastGridX || gridY != lastGridY)
                {
                    PaintCell(gridX, gridY, temperature);
                    lastGridX = gridX;
                    lastGridY = gridY;
                    changed = true;
                }
            }
        }

        if (changed)
            ApplyTexture();
    }

    private void PaintCell(int gridX, int gridY, float temperature)
    {
        if (gridX < 0 || gridX >= gridSizeX || gridY < 0 || gridY >= gridSizeY)
            return;

        Color color = GetTemperatureColor(temperature);
        heatmapTexture.SetPixel(gridX, gridY, color);
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

        Color coldColor = new Color(1f, 0.65f, 0.15f, 0.85f);
        Color hotColor = new Color(1f, 0.15f, 0.15f, 0.85f);

        return Color.Lerp(coldColor, hotColor, t);
    }

    private void ApplyTexture()
    {
        heatmapTexture.Apply(false);
    }

    private void ClearTexture()
    {
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                heatmapTexture.SetPixel(x, y, clear);
            }
        }

        heatmapTexture.Apply(false);
    }

    public void ClearHeatmap()
    {
        ClearTexture();
    }
}