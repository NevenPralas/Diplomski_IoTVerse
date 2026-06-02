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

    [Header("Temperature Gradient Colors")]
    [Tooltip("Boja za najnizu temperaturu, npr. minTemperature.")]
    [SerializeField] private Color lowTemperatureColor = new Color(0.831f, 0.384f, 0.102f, 0.85f);

    [Tooltip("Srednja boja gradijenta. Koristi se samo ako je Gradient Color Count postavljen na 3.")]
    [SerializeField] private Color middleTemperatureColor = new Color(0.753f, 0.188f, 0.188f, 0.85f);

    [Tooltip("Boja za najvisu temperaturu, npr. maxTemperature.")]
    [SerializeField] private Color highTemperatureColor = new Color(0.478f, 0.063f, 0.063f, 0.85f);

    [Tooltip("Ako je 2, koristi se prijelaz low -> high. Ako je 3, koristi se low -> middle -> high.")]
    [SerializeField, Range(2, 3)] private int gradientColorCount = 3;

    [Header("Cell Particles")]
    [SerializeField] private HeatmapCellParticles cellParticles;

    [Header("Debug / Fake Preview")]
    [SerializeField] private bool generateRandomCellsOnStart = false;
    [SerializeField] private int randomCellsCount = 12;
    [SerializeField] private bool clearBeforeRandomFill = true;

    [Header("Cell History")]
    [SerializeField] private float historyRetentionSeconds = 180f;

    [Header("Heatmap Display Aggregation")]
    [Tooltip("Ako je ukljuceno, osnovna boja celije prikazuje srednju temperaturu umjesto zadnje izmjerene temperature.")]
    [SerializeField] private bool useAverageTemperatureForCellColor = true;

    [Tooltip("Vremenski prozor za srednju temperaturu u sekundama. 0 ili manje znaci prosjek svih sacuvanih mjerenja za celiju.")]
    [SerializeField] private float averageTemperatureWindowSeconds = 60f;

    [Header("Automatic Expiration")]
    [Tooltip("Ako je ukljuceno, celija se potpuno brise kad nema nijedan sample mladji od Cell Data Lifetime Seconds.")]
    [SerializeField] private bool removeCellsWithNoRecentSamples = true;

    [Tooltip("Nakon koliko sekundi bez novog mjerenja celija gubi boju i history. Za tvoj use-case ostavi 60.")]
    [SerializeField] private float cellDataLifetimeSeconds = 60f;

    [Tooltip("Koliko cesto se provjerava treba li obrisati stare celije.")]
    [SerializeField] private float expirationRefreshInterval = 1f;

    private float expirationTimer = 0f;

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

    private void Update()
    {
        if (!removeCellsWithNoRecentSamples)
            return;

        expirationTimer += Time.deltaTime;

        if (expirationTimer < expirationRefreshInterval)
            return;

        expirationTimer = 0f;
        PruneExpiredSamplesAndRefreshTexture();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        InitializeHeatmap();

        cellDataLifetimeSeconds = Mathf.Max(1f, cellDataLifetimeSeconds);
        expirationRefreshInterval = Mathf.Max(0.1f, expirationRefreshInterval);
        historyRetentionSeconds = Mathf.Max(cellDataLifetimeSeconds, historyRetentionSeconds);
        averageTemperatureWindowSeconds = Mathf.Max(1f, averageTemperatureWindowSeconds);

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

        if (worldWidth <= 0f) worldWidth = 1f;
        if (worldHeight <= 0f) worldHeight = 1f;

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

        RecordCellSample(gridX, gridY, temperature);

        float displayedTemperature = GetDisplayedTemperatureForCell(gridX, gridY, temperature);
        Color color = GetTemperatureColor(displayedTemperature);
        heatmapTexture.SetPixel(gridSizeX - 1 - gridX, gridSizeY - 1 - gridY, color);

        if (cellParticles != null)
            cellParticles.ShowOrUpdateCellParticle(gridX, gridY, displayedTemperature);
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

    public void PruneExpiredSamplesAndRefreshTexture()
    {
        if (heatmapTexture == null)
            return;

        float currentTime = GetRelativeSimulationTime();
        float minAllowedTime = currentTime - cellDataLifetimeSeconds;

        bool textureChanged = false;
        List<Vector2Int> keysToRemove = new List<Vector2Int>();

        foreach (KeyValuePair<Vector2Int, List<CellTemperatureSample>> pair in cellHistory)
        {
            List<CellTemperatureSample> samples = pair.Value;

            int removed = samples.RemoveAll(sample => sample.relativeTime < minAllowedTime);

            if (removed > 0)
                textureChanged = true;

            if (samples.Count == 0)
            {
                keysToRemove.Add(pair.Key);
                ClearCellPixel(pair.Key.x, pair.Key.y);
                textureChanged = true;
            }
            else if (removed > 0)
            {
                RepaintCellFromCurrentHistory(pair.Key.x, pair.Key.y, samples);
            }
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            cellHistory.Remove(keysToRemove[i]);

            if (cellParticles != null)
                cellParticles.ClearCellParticle(keysToRemove[i].x, keysToRemove[i].y);
        }

        if (textureChanged)
            ApplyTexture();
    }

    private void RepaintCellFromCurrentHistory(int gridX, int gridY, List<CellTemperatureSample> samples)
    {
        if (samples == null || samples.Count == 0)
        {
            ClearCellPixel(gridX, gridY);
            return;
        }

        float displayedTemperature = samples[samples.Count - 1].temperature;

        if (useAverageTemperatureForCellColor &&
            TryGetAverageTemperatureForCell(gridX, gridY, averageTemperatureWindowSeconds, out float averageTemperature))
        {
            displayedTemperature = averageTemperature;
        }

        Color color = GetTemperatureColor(displayedTemperature);
        heatmapTexture.SetPixel(gridSizeX - 1 - gridX, gridSizeY - 1 - gridY, color);
    }

    private void ClearCellPixel(int gridX, int gridY)
    {
        if (heatmapTexture == null)
            return;

        if (gridX < 0 || gridX >= gridSizeX || gridY < 0 || gridY >= gridSizeY)
            return;

        heatmapTexture.SetPixel(gridSizeX - 1 - gridX, gridSizeY - 1 - gridY, new Color(0f, 0f, 0f, 0f));
    }

    private float GetDisplayedTemperatureForCell(int gridX, int gridY, float fallbackTemperature)
    {
        if (!useAverageTemperatureForCellColor)
            return fallbackTemperature;

        if (TryGetAverageTemperatureForCell(gridX, gridY, averageTemperatureWindowSeconds, out float averageTemperature))
            return averageTemperature;

        return fallbackTemperature;
    }

    public bool TryGetAverageTemperatureForCell(int gridX, int gridY, float windowSeconds, out float averageTemperature)
    {
        averageTemperature = 0f;

        Vector2Int key = new Vector2Int(gridX, gridY);

        if (!cellHistory.TryGetValue(key, out List<CellTemperatureSample> samples) || samples.Count == 0)
            return false;

        float currentTime = GetRelativeSimulationTime();
        float minAllowedTime = windowSeconds > 0f ? currentTime - windowSeconds : float.NegativeInfinity;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            CellTemperatureSample sample = samples[i];

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
        if (Mathf.Approximately(minTemperature, maxTemperature))
            return highTemperatureColor;

        float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);
        t = Mathf.Clamp01(t);

        if (gradientColorCount == 2)
            return Color.Lerp(lowTemperatureColor, highTemperatureColor, t);

        if (t <= 0.5f)
            return Color.Lerp(lowTemperatureColor, middleTemperatureColor, t / 0.5f);

        return Color.Lerp(middleTemperatureColor, highTemperatureColor, (t - 0.5f) / 0.5f);
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

    public float GetMinTemperature()
    {
        return minTemperature;
    }

    public float GetMaxTemperature()
    {
        return maxTemperature;
    }

    public float GetMiddleTemperature()
    {
        return (minTemperature + maxTemperature) * 0.5f;
    }
}