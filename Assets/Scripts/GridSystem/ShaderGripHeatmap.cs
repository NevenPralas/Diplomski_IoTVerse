using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ShaderGridHeatmap : MonoBehaviour
{
    [Serializable]
    public class CellTemperatureSample
    {
        public float relativeTime;
        public float temperature;

        public CellTemperatureSample(float relativeTime, float temperature)
        {
            this.relativeTime = relativeTime;
            this.temperature = temperature;
        }
    }

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

    [Header("Cell Particles")]
    [SerializeField] private HeatmapCellParticles cellParticles;

    [Header("Debug / Fake Preview")]
    [SerializeField] private bool generateRandomCellsOnStart = false;
    [SerializeField] private int randomCellsCount = 12;
    [SerializeField] private bool clearBeforeRandomFill = true;

    [Header("Cell History")]
    [SerializeField] private float historyRetentionSeconds = 180f;

    private Texture2D heatmapTexture;
    private Material runtimeMaterial;
    private float cellWidth;
    private float cellHeight;
    private Vector3 origin;

    private float simulationStartTime;

    private readonly Dictionary<Vector2Int, List<CellTemperatureSample>> cellHistory =
        new Dictionary<Vector2Int, List<CellTemperatureSample>>();

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        simulationStartTime = Time.time;
        InitializeHeatmap();

        if (generateRandomCellsOnStart)
            PaintRandomCells(randomCellsCount, clearBeforeRandomFill);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        InitializeHeatmap();

        if (generateRandomCellsOnStart)
            PaintRandomCells(randomCellsCount, clearBeforeRandomFill);
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

    public void PaintRandomCells(int count, bool clearFirst = true)
    {
        if (clearFirst)
            ClearHeatmap();

        int safeCount = Mathf.Clamp(count, 1, gridSizeX * gridSizeY);

        for (int i = 0; i < safeCount; i++)
        {
            int gridX = UnityEngine.Random.Range(0, gridSizeX);
            int gridY = UnityEngine.Random.Range(0, gridSizeY);
            float temperature = UnityEngine.Random.Range(minTemperature, maxTemperature);

            PaintCell(gridX, gridY, temperature);
        }

        ApplyTexture();
    }

    private void PaintCell(int gridX, int gridY, float temperature)
    {
        if (gridX < 0 || gridX >= gridSizeX || gridY < 0 || gridY >= gridSizeY)
            return;

        Color color = GetTemperatureColor(temperature);
        heatmapTexture.SetPixel(gridSizeX - 1 - gridX, gridSizeY - 1 - gridY, color);

        RecordCellSample(gridX, gridY, temperature);

        if (cellParticles != null)
            cellParticles.ShowOrUpdateCellParticle(gridX, gridY, temperature);
    }

    private void RecordCellSample(int gridX, int gridY, float temperature)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);

        if (!cellHistory.TryGetValue(key, out List<CellTemperatureSample> samples))
        {
            samples = new List<CellTemperatureSample>();
            cellHistory[key] = samples;
        }

        float relativeTime = Time.time - simulationStartTime;

        // Nemoj spamati potpuno identične uzastopne zapise
        if (samples.Count > 0)
        {
            CellTemperatureSample last = samples[samples.Count - 1];

            if (Mathf.Abs(last.relativeTime - relativeTime) < 0.01f &&
                Mathf.Abs(last.temperature - temperature) < 0.001f)
            {
                return;
            }
        }

        samples.Add(new CellTemperatureSample(relativeTime, temperature));

        float minAllowedTime = relativeTime - historyRetentionSeconds;
        samples.RemoveAll(sample => sample.relativeTime < minAllowedTime);
    }

    public List<CellTemperatureSample> GetCellHistory(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);

        if (cellHistory.TryGetValue(key, out List<CellTemperatureSample> samples))
            return new List<CellTemperatureSample>(samples);

        return new List<CellTemperatureSample>();
    }

    public float GetRelativeSimulationTime()
    {
        return Time.time - simulationStartTime;
    }

    public bool TryGetCellIndex(Vector3 worldPosition, out int gridX, out int gridY)
    {
        float localX = worldPosition.x - origin.x;
        float localZ = worldPosition.z - origin.z;

        gridX = Mathf.FloorToInt(localX / cellWidth);
        gridY = Mathf.FloorToInt(localZ / cellHeight);

        return gridX >= 0 && gridX < gridSizeX && gridY >= 0 && gridY < gridSizeY;
    }

    public Vector3 GetCellCenterWorld(int gridX, int gridY)
    {
        float x = origin.x + (gridX + 0.5f) * cellWidth;
        float z = origin.z + (gridY + 0.5f) * cellHeight;
        float y = transform.position.y;

        return new Vector3(x, y, z);
    }

    private Color GetTemperatureColor(float temperature)
    {
        float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);

        Color orange = new Color(0.831f, 0.384f, 0.102f, 0.85f);
        Color midRed = new Color(0.753f, 0.188f, 0.188f, 0.85f);
        Color darkRed = new Color(0.478f, 0.063f, 0.063f, 0.85f);

        if (t <= 0.5f)
            return Color.Lerp(orange, midRed, t / 0.5f);
        else
            return Color.Lerp(midRed, darkRed, (t - 0.5f) / 0.5f);
    }

    private void ApplyTexture()
    {
        if (heatmapTexture != null)
            heatmapTexture.Apply(false);
    }

    private void ClearTexture()
    {
        if (heatmapTexture == null)
            return;

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
        cellHistory.Clear();
        simulationStartTime = Time.time;

        if (cellParticles != null)
            cellParticles.ClearAllParticles();
    }

    public Color GetColorForTemperature(float temperature)
    {
        return GetTemperatureColor(temperature);
    }

    public float GetCellWidth()
    {
        return cellWidth;
    }

    public float GetCellHeight()
    {
        return cellHeight;
    }
}