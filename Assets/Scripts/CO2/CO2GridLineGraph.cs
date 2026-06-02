using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Renderer))]
public class CO2GridLineGraph : MonoBehaviour
{
    [Serializable]
    public class CO2Sample
    {
        public float relativeTime;
        public float co2Ppm;

        public CO2Sample(float relativeTime, float co2Ppm)
        {
            this.relativeTime = relativeTime;
            this.co2Ppm = co2Ppm;
        }
    }

    private class GraphInstance
    {
        public GameObject root;
        public Vector2Int cell;
    }

    [Header("Target Renderer")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Shader Property Names")]
    [SerializeField] private string heatmapTextureProperty = "_HeatmapTex";
    [SerializeField] private string gridSizeProperty = "_Size";

    [Header("Grid Settings")]
    [SerializeField] private int gridSizeX = 15;
    [SerializeField] private int gridSizeY = 15;
    [SerializeField] private float worldWidth = 10f;
    [SerializeField] private float worldHeight = 10f;

    [Header("Shader Grid Baseline")]
    [SerializeField] private float baselineGridX = 10f;
    [SerializeField] private float baselineGridY = 10f;

    [Header("CO2 Mapping / Gradient")]
    [SerializeField] private float minCO2Ppm = 400f;
    [SerializeField] private float maxCO2Ppm = 2000f;

    [SerializeField] private Color lowCO2Color = new Color(0.10f, 0.75f, 0.25f, 0.85f);
    [SerializeField] private Color middleCO2Color = new Color(1.00f, 0.70f, 0.05f, 0.85f);
    [SerializeField] private Color highCO2Color = new Color(0.90f, 0.05f, 0.02f, 0.85f);

    [Range(2, 3)]
    [SerializeField] private int gradientColorCount = 3;

    [Header("Empty / No Data Cell")]
    [SerializeField] private Color emptyCellColor = new Color(0f, 0f, 0f, 0f);

    [Header("History")]
    [SerializeField] private float historyRetentionSeconds = 180f;

    [Tooltip("Ako je uključeno, boja ćelije prikazuje Average CO2 u zadnjih averageWindowSeconds.")]
    [SerializeField] private bool useAverageCO2ForCellColor = true;

    [SerializeField] private float averageWindowSeconds = 60f;

    [Header("Expiration / Auto Clear")]
    [Tooltip("Ako je uključeno, ćelija se očisti ako nema nijedan noviji sample.")]
    [SerializeField] private bool removeCellsWithNoRecentSamples = true;

    [Tooltip("Koliko dugo ćelija ostaje obojana bez novih CO2 podataka.")]
    [SerializeField] private float cellDataLifetimeSeconds = 60f;

    [Tooltip("Koliko često se provjerava treba li očistiti stare ćelije.")]
    [SerializeField] private float expirationRefreshInterval = 1f;

    [Header("Interaction")]
    [Tooltip("Zasebni CO2 cursor. Ne koristi temperaturni GridCellCursor.")]
    [SerializeField] private CO2GridCellCursor co2CellCursor;

    [Tooltip("Fallback ako CO2 cursor nije spojen ili nema validnu ćeliju.")]
    [SerializeField] private Transform rayOrigin;

    [SerializeField] private InputActionReference openGraphAction;
    [SerializeField] private float raycastDistance = 50f;

    [Tooltip("Layer na kojem je CO2Grid. Nemoj uključiti layer na kojem je CO2CellCursor.")]
    [SerializeField] private LayerMask gridLayerMask = ~0;

    [SerializeField] private bool debugRay = false;

    [Header("Room / Interaction Bounds")]
    [SerializeField] private bool useInteractionBounds = true;
    [SerializeField] private CellInteractionBounds interactionBounds;

    [Header("Graph Opening Rules")]
    [Tooltip("Ako je false, ne možeš otvoriti graf lijevo/desno/gore/dolje od već otvorenog. Dijagonalno je dopušteno.")]
    [SerializeField] private bool allowOrthogonalAdjacentGraphs = false;

    [Tooltip("Ako je true, klik na zabranjenu susjednu ćeliju se ignorira. Ako je false, zatvara susjedni graf i otvara novi.")]
    [SerializeField] private bool ignoreClickWhenAdjacentGraphExists = true;

    [Header("Popup Graph")]
    [SerializeField] private Transform graphParent;
    [SerializeField] private float graphWidth = 0.9f;
    [SerializeField] private float graphHeight = 0.55f;
    [SerializeField] private float graphVerticalOffset = 0.22f;
    [SerializeField] private float graphLineWidth = 0.018f;
    [SerializeField] private float axisLineWidth = 0.008f;
    [SerializeField] private float graphWindowSeconds = 60f;

    [Tooltip("Ako klikneš istu ćeliju opet, graf se zatvara.")]
    [SerializeField] private bool toggleGraphOnSameCell = true;

    [Tooltip("0 znači neograničeno.")]
    [SerializeField] private int maxOpenGraphs = 0;

    [Header("Popup Close Animation")]
    [Tooltip("Ako je uključeno, CO2 line graph se pri zatvaranju elegantno smanji i lagano spusti umjesto da odmah nestane.")]
    [SerializeField] private bool animateGraphClose = true;

    [SerializeField] private float graphCloseDuration = 0.28f;
    [SerializeField] private float graphCloseDrop = 0.08f;

    [Tooltip("Ako je uključeno, panel, linija i labele lagano izblijede tijekom zatvaranja.")]
    [SerializeField] private bool fadeGraphOnClose = true;

    [Header("Popup Appearance")]
    [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color axisColor = new Color(0.06f, 0.06f, 0.06f, 1f);
    [SerializeField] private Color textColor = new Color(0.04f, 0.04f, 0.04f, 1f);

    [Tooltip("Koristi se samo ako Use Current CO2 Color For Graph Line nije uključen.")]
    [SerializeField] private Color fallbackLineColor = new Color(0.0f, 0.78f, 0.22f, 1f);

    [Tooltip("Ako je uključeno, linija i trenutna kuglica koriste boju trenutne CO2 vrijednosti iz CO2 gradijenta.")]
    [SerializeField] private bool useCurrentCO2ColorForGraphLine = true;

    [SerializeField] private int labelFontSize = 44;
    [SerializeField] private float labelCharacterSize = 0.018f;
    [SerializeField] private Font labelFont;

    [Header("Date / Time Labels")]
    [SerializeField] private bool showDateLabel = true;
    [SerializeField] private int dateLabelFontSize = 48;
    [SerializeField] private float dateLabelCharacterSize = 0.019f;
    [SerializeField] private float dateLabelVerticalOffset = 0.17f;

    [Header("Audio")]
    [Tooltip("Zvuk koji se pusti kad se CO2 line graph stvarno otvori.")]
    [SerializeField] private AudioClip spawnSound;

    [Tooltip("Zvuk koji se pusti kad se CO2 line graph zatvori/spusti.")]
    [SerializeField] private AudioClip despawnSound;

    [Tooltip("Spoji Audio Source s AirQualityTracker objekta.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Refresh")]
    [SerializeField] private float graphRefreshInterval = 1f;

    [Header("Physics / Layer")]
    [SerializeField] private string visualizationLayerName = "Visualization";
    [SerializeField] private bool ensureColliderExists = true;

    [Header("Debug")]
    [SerializeField] private bool logSamples = false;
    [SerializeField] private bool logClicks = false;
    [SerializeField] private bool logRejectedAdjacentClicks = true;

    private Texture2D heatmapTexture;
    private Material runtimeMaterial;

    private float cellWidth;
    private float cellHeight;
    private Vector3 gridOrigin;
    private float simulationStartTime;

    private readonly Dictionary<Vector2Int, List<CO2Sample>> cellHistory =
        new Dictionary<Vector2Int, List<CO2Sample>>();

    private readonly Dictionary<Vector2Int, GraphInstance> openGraphs =
        new Dictionary<Vector2Int, GraphInstance>();

    private readonly List<Vector2Int> graphOpenOrder =
        new List<Vector2Int>();

    private float graphRefreshTimer = 0f;
    private float expirationRefreshTimer = 0f;

    private bool interactionEnabled = true;
    private bool visualizationVisible = true;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        simulationStartTime = Time.time;

        InitializeGridTexture();
        EnsureCollider();

        if (graphParent == null)
        {
            GameObject root = new GameObject("CO2LineGraphs");
            root.transform.SetParent(transform, true);
            graphParent = root.transform;
        }

        SetVisualizationLayerRecursively(graphParent.gameObject);
    }

    private void OnEnable()
    {
        if (openGraphAction != null)
            openGraphAction.action.Enable();
    }

    private void OnDisable()
    {
        if (openGraphAction != null)
            openGraphAction.action.Disable();
    }

    private void OnValidate()
    {
        gridSizeX = Mathf.Max(1, gridSizeX);
        gridSizeY = Mathf.Max(1, gridSizeY);

        worldWidth = Mathf.Max(0.01f, worldWidth);
        worldHeight = Mathf.Max(0.01f, worldHeight);

        baselineGridX = Mathf.Max(0.01f, baselineGridX);
        baselineGridY = Mathf.Max(0.01f, baselineGridY);

        minCO2Ppm = Mathf.Min(minCO2Ppm, maxCO2Ppm - 1f);
        maxCO2Ppm = Mathf.Max(maxCO2Ppm, minCO2Ppm + 1f);

        historyRetentionSeconds = Mathf.Max(1f, historyRetentionSeconds);
        averageWindowSeconds = Mathf.Max(1f, averageWindowSeconds);

        cellDataLifetimeSeconds = Mathf.Max(1f, cellDataLifetimeSeconds);
        expirationRefreshInterval = Mathf.Max(0.1f, expirationRefreshInterval);

        graphWindowSeconds = Mathf.Max(1f, graphWindowSeconds);

        graphWidth = Mathf.Max(0.1f, graphWidth);
        graphHeight = Mathf.Max(0.1f, graphHeight);
        graphLineWidth = Mathf.Max(0.001f, graphLineWidth);
        axisLineWidth = Mathf.Max(0.001f, axisLineWidth);
        graphRefreshInterval = Mathf.Max(0.05f, graphRefreshInterval);
    }

    private void Update()
    {
        HandleClick();

        if (removeCellsWithNoRecentSamples)
        {
            expirationRefreshTimer += Time.deltaTime;

            if (expirationRefreshTimer >= expirationRefreshInterval)
            {
                expirationRefreshTimer = 0f;
                RemoveExpiredCellData();
            }
        }

        if (openGraphs.Count > 0)
        {
            graphRefreshTimer += Time.deltaTime;

            if (graphRefreshTimer >= graphRefreshInterval)
            {
                graphRefreshTimer = 0f;
                RebuildAllOpenGraphs();
            }
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
    }

    public void SetVisualizationVisible(bool visible)
    {
        visualizationVisible = visible;

        foreach (KeyValuePair<Vector2Int, GraphInstance> pair in openGraphs)
        {
            if (pair.Value != null && pair.Value.root != null)
                SetRenderersAndCollidersVisible(pair.Value.root, visualizationVisible);
        }
    }

    public void AddCO2Sample(Vector3 worldPosition, float co2Ppm)
    {
        if (!TryGetCellIndex(worldPosition, out int gridX, out int gridY))
            return;

        PaintCell(gridX, gridY, co2Ppm);
        ApplyTexture();

        if (logSamples)
            Debug.Log($"CO2 sample | cell=({gridX},{gridY}) | pos={worldPosition} | co2={co2Ppm:F1} ppm");
    }

    public void ClearCO2()
    {
        cellHistory.Clear();
        ClearTexture();
        CloseAllGraphs();
        simulationStartTime = Time.time;
    }

    public bool TryGetCellIndex(Vector3 worldPosition, out int gridX, out int gridY)
    {
        float localX = worldPosition.x - gridOrigin.x;
        float localZ = worldPosition.z - gridOrigin.z;

        gridX = Mathf.FloorToInt(localX / cellWidth);
        gridY = Mathf.FloorToInt(localZ / cellHeight);

        return gridX >= 0 && gridX < gridSizeX &&
               gridY >= 0 && gridY < gridSizeY;
    }

    public Vector3 GetCellCenterWorld(int gridX, int gridY)
    {
        float x = gridOrigin.x + (gridX + 0.5f) * cellWidth;
        float z = gridOrigin.z + (gridY + 0.5f) * cellHeight;

        return new Vector3(x, transform.position.y, z);
    }

    public float GetCellWidth()
    {
        return cellWidth;
    }

    public float GetCellHeight()
    {
        return cellHeight;
    }

    public List<CO2Sample> GetCellHistory(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);

        if (cellHistory.TryGetValue(key, out List<CO2Sample> samples))
            return new List<CO2Sample>(samples);

        return new List<CO2Sample>();
    }

    public float GetRelativeSimulationTime()
    {
        return Time.time - simulationStartTime;
    }

    public bool TryGetAverageCO2ForCell(int gridX, int gridY, out float averageCO2)
    {
        return TryGetAverageCO2ForCell(gridX, gridY, averageWindowSeconds, out averageCO2);
    }

    public bool TryGetLatestCO2ForCell(int gridX, int gridY, out float latestCO2)
    {
        latestCO2 = 0f;

        Vector2Int key = new Vector2Int(gridX, gridY);

        if (!cellHistory.TryGetValue(key, out List<CO2Sample> samples))
            return false;

        RemoveExpiredSamplesFromList(samples);

        if (samples.Count == 0)
        {
            cellHistory.Remove(key);
            ClearCellPixel(gridX, gridY);
            ApplyTexture();
            return false;
        }

        latestCO2 = samples[samples.Count - 1].co2Ppm;
        return true;
    }

    public bool TryGetDisplayedCO2ForCell(int gridX, int gridY, out float displayedCO2)
    {
        displayedCO2 = 0f;

        Vector2Int key = new Vector2Int(gridX, gridY);

        if (!cellHistory.TryGetValue(key, out List<CO2Sample> samples))
            return false;

        RemoveExpiredSamplesFromList(samples);

        if (samples.Count == 0)
        {
            cellHistory.Remove(key);
            ClearCellPixel(gridX, gridY);
            ApplyTexture();
            return false;
        }

        if (useAverageCO2ForCellColor)
        {
            if (TryGetAverageCO2ForCell(gridX, gridY, averageWindowSeconds, out float average))
            {
                displayedCO2 = average;
                return true;
            }

            return false;
        }

        displayedCO2 = samples[samples.Count - 1].co2Ppm;
        return true;
    }

    public Color GetColorForCO2Value(float co2Ppm)
    {
        return GetColorForCO2(co2Ppm);
    }

    private void InitializeGridTexture()
    {
        cellWidth = worldWidth / gridSizeX;
        cellHeight = worldHeight / gridSizeY;

        gridOrigin = transform.position - new Vector3(worldWidth * 0.5f, 0f, worldHeight * 0.5f);

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

    private void ClearTexture()
    {
        if (heatmapTexture == null)
            return;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
                heatmapTexture.SetPixel(x, y, emptyCellColor);
        }

        ApplyTexture();
    }

    private void ApplyTexture()
    {
        if (heatmapTexture != null)
            heatmapTexture.Apply(false);
    }

    private void PaintCell(int gridX, int gridY, float co2Ppm)
    {
        if (gridX < 0 || gridX >= gridSizeX || gridY < 0 || gridY >= gridSizeY)
            return;

        RecordCellSample(gridX, gridY, co2Ppm);

        float displayedCO2 = co2Ppm;

        if (useAverageCO2ForCellColor &&
            TryGetAverageCO2ForCell(gridX, gridY, averageWindowSeconds, out float averageCO2))
        {
            displayedCO2 = averageCO2;
        }

        Color color = GetColorForCO2(displayedCO2);
        heatmapTexture.SetPixel(gridSizeX - 1 - gridX, gridSizeY - 1 - gridY, color);
    }

    private void ClearCellPixel(int gridX, int gridY)
    {
        if (heatmapTexture == null)
            return;

        if (gridX < 0 || gridX >= gridSizeX || gridY < 0 || gridY >= gridSizeY)
            return;

        heatmapTexture.SetPixel(gridSizeX - 1 - gridX, gridSizeY - 1 - gridY, emptyCellColor);
    }

    private void RecordCellSample(int gridX, int gridY, float co2Ppm)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);

        if (!cellHistory.TryGetValue(key, out List<CO2Sample> samples))
        {
            samples = new List<CO2Sample>();
            cellHistory[key] = samples;
        }

        float relativeTime = GetRelativeSimulationTime();

        if (samples.Count > 0)
        {
            CO2Sample last = samples[samples.Count - 1];

            if (Mathf.Abs(last.relativeTime - relativeTime) < 0.01f &&
                Mathf.Abs(last.co2Ppm - co2Ppm) < 0.001f)
            {
                return;
            }
        }

        samples.Add(new CO2Sample(relativeTime, co2Ppm));

        RemoveExpiredSamplesFromList(samples);
    }

    private bool TryGetAverageCO2ForCell(int gridX, int gridY, float windowSeconds, out float averageCO2)
    {
        averageCO2 = 0f;

        Vector2Int key = new Vector2Int(gridX, gridY);

        if (!cellHistory.TryGetValue(key, out List<CO2Sample> samples))
            return false;

        RemoveExpiredSamplesFromList(samples);

        if (samples.Count == 0)
        {
            cellHistory.Remove(key);
            ClearCellPixel(gridX, gridY);
            ApplyTexture();
            return false;
        }

        float currentTime = GetRelativeSimulationTime();
        float minAllowedTime = currentTime - windowSeconds;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            CO2Sample sample = samples[i];

            if (sample.relativeTime < minAllowedTime)
                continue;

            sum += sample.co2Ppm;
            count++;
        }

        if (count == 0)
            return false;

        averageCO2 = sum / count;
        return true;
    }

    private void RemoveExpiredSamplesFromList(List<CO2Sample> samples)
    {
        if (samples == null)
            return;

        float currentTime = GetRelativeSimulationTime();
        float minAllowedTime = currentTime - cellDataLifetimeSeconds;

        for (int i = samples.Count - 1; i >= 0; i--)
        {
            if (samples[i].relativeTime < minAllowedTime)
                samples.RemoveAt(i);
        }
    }

    private void RemoveExpiredCellData()
    {
        if (cellHistory.Count == 0)
            return;

        List<Vector2Int> toRemove = null;

        foreach (KeyValuePair<Vector2Int, List<CO2Sample>> pair in cellHistory)
        {
            List<CO2Sample> samples = pair.Value;

            RemoveExpiredSamplesFromList(samples);

            if (samples == null || samples.Count == 0)
            {
                if (toRemove == null)
                    toRemove = new List<Vector2Int>();

                toRemove.Add(pair.Key);
            }
        }

        if (toRemove == null || toRemove.Count == 0)
            return;

        for (int i = 0; i < toRemove.Count; i++)
        {
            Vector2Int cell = toRemove[i];

            cellHistory.Remove(cell);
            ClearCellPixel(cell.x, cell.y);
        }

        ApplyTexture();
        RebuildAllOpenGraphs();
    }

    private Color GetColorForCO2(float co2Ppm)
    {
        float t = Mathf.InverseLerp(minCO2Ppm, maxCO2Ppm, co2Ppm);
        t = Mathf.Clamp01(t);

        Color result;

        if (gradientColorCount == 2)
        {
            result = Color.Lerp(lowCO2Color, highCO2Color, t);
        }
        else if (t <= 0.5f)
        {
            result = Color.Lerp(lowCO2Color, middleCO2Color, t / 0.5f);
        }
        else
        {
            result = Color.Lerp(middleCO2Color, highCO2Color, (t - 0.5f) / 0.5f);
        }

        result.a = 1f;
        return result;
    }

    private void HandleClick()
    {
        if (!interactionEnabled || !visualizationVisible)
            return;

        if (openGraphAction == null || !openGraphAction.action.WasPressedThisFrame())
            return;

        if (TryGetCellFromCursor(out int cursorGridX, out int cursorGridY))
        {
            ToggleOrCreateGraph(cursorGridX, cursorGridY);
            return;
        }

        if (!TryGetCellFromRay(out int rayGridX, out int rayGridY))
            return;

        ToggleOrCreateGraph(rayGridX, rayGridY);
    }

    private bool TryGetCellFromCursor(out int gridX, out int gridY)
    {
        gridX = -1;
        gridY = -1;

        if (co2CellCursor == null || !co2CellCursor.HasValidCell)
            return false;

        Vector2Int cell = co2CellCursor.CurrentCell;
        gridX = cell.x;
        gridY = cell.y;
        return true;
    }

    private bool TryGetCellFromRay(out int gridX, out int gridY)
    {
        gridX = -1;
        gridY = -1;

        if (rayOrigin == null)
        {
            Debug.LogWarning("CO2GridLineGraph: Ray Origin nije postavljen, a CO2 cursor nema validnu ćeliju.");
            return false;
        }

        Vector3 rayStart = rayOrigin.position;
        Vector3 rayDirection = rayOrigin.forward.normalized;

        if (debugRay)
            Debug.DrawRay(rayStart, rayDirection * raycastDistance, Color.green, 0.2f);

        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            rayDirection,
            raycastDistance,
            gridLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            CO2GridLineGraph hitGrid = hit.collider.GetComponentInParent<CO2GridLineGraph>();

            if (hitGrid != this)
                continue;

            return TryGetCellIndex(hit.point, out gridX, out gridY);
        }

        return false;
    }

    private void ToggleOrCreateGraph(int gridX, int gridY)
    {
        if (useInteractionBounds && interactionBounds != null)
        {
            Vector3 cellCenter = GetCellCenterWorld(gridX, gridY);

            if (!interactionBounds.IsCO2CellAllowed(cellCenter, GetCellWidth(), GetCellHeight()))
            {
                if (logRejectedAdjacentClicks)
                    Debug.Log($"CO2 graph nije otvoren jer ćelija ({gridX},{gridY}) nije dovoljno unutar sobe.");

                return;
            }
        }

        Vector2Int cell = new Vector2Int(gridX, gridY);

        if (openGraphs.TryGetValue(cell, out GraphInstance existing))
        {
            if (toggleGraphOnSameCell)
                CloseGraph(cell);

            return;
        }

        if (!allowOrthogonalAdjacentGraphs && HasOrthogonalAdjacentOpenGraph(cell, out Vector2Int adjacentCell))
        {
            if (ignoreClickWhenAdjacentGraphExists)
            {
                if (logRejectedAdjacentClicks)
                {
                    Debug.Log(
                        $"CO2 graf nije otvoren na ćeliji ({gridX},{gridY}) jer već postoji susjedni graf na ćeliji ({adjacentCell.x},{adjacentCell.y})."
                    );
                }

                return;
            }

            CloseGraph(adjacentCell);
        }

        EnforceMaxOpenGraphs();

        GameObject graphRoot = BuildGraph(gridX, gridY, true);

        if (graphRoot == null)
            return;

        GraphInstance instance = new GraphInstance
        {
            root = graphRoot,
            cell = cell
        };

        openGraphs[cell] = instance;
        graphOpenOrder.Add(cell);

        PlaySpawnSound(graphRoot.transform.position);

        if (logClicks)
            Debug.Log($"CO2 line graph otvoren na ćeliji ({gridX},{gridY}). Ukupno otvorenih: {openGraphs.Count}");
    }

    private bool HasOrthogonalAdjacentOpenGraph(Vector2Int newCell, out Vector2Int adjacentCell)
    {
        foreach (Vector2Int openCell in openGraphs.Keys)
        {
            int dx = Mathf.Abs(openCell.x - newCell.x);
            int dy = Mathf.Abs(openCell.y - newCell.y);

            if (dx + dy == 1)
            {
                adjacentCell = openCell;
                return true;
            }
        }

        adjacentCell = new Vector2Int(-1, -1);
        return false;
    }

    private void EnforceMaxOpenGraphs()
    {
        if (maxOpenGraphs <= 0)
            return;

        while (openGraphs.Count >= maxOpenGraphs && graphOpenOrder.Count > 0)
        {
            Vector2Int oldest = graphOpenOrder[0];
            CloseGraph(oldest);
        }
    }

    private void CloseGraph(Vector2Int cell, bool playSound = true, bool animateClose = true)
    {
        if (openGraphs.TryGetValue(cell, out GraphInstance instance))
        {
            if (instance.root != null)
            {
                if (playSound)
                    PlayDespawnSound(instance.root.transform.position);

                StartGraphCloseAnimation(instance.root, animateClose);
            }

            openGraphs.Remove(cell);
        }

        graphOpenOrder.Remove(cell);
    }

    private void StartGraphCloseAnimation(GameObject graphRoot, bool animateClose)
    {
        if (graphRoot == null)
            return;

        if (!animateClose || !animateGraphClose)
        {
            Destroy(graphRoot);
            return;
        }

        CO2GraphCloseAnimator closeAnimator = graphRoot.GetComponent<CO2GraphCloseAnimator>();

        if (closeAnimator == null)
            closeAnimator = graphRoot.AddComponent<CO2GraphCloseAnimator>();

        closeAnimator.Init(graphCloseDuration, graphCloseDrop, fadeGraphOnClose);
    }

    private void CloseAllGraphs()
    {
        List<Vector2Int> cells = new List<Vector2Int>(openGraphs.Keys);

        for (int i = 0; i < cells.Count; i++)
            CloseGraph(cells[i], false, false);
    }

    private void RebuildAllOpenGraphs()
    {
        List<Vector2Int> cells = new List<Vector2Int>(openGraphs.Keys);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];

            if (!openGraphs.TryGetValue(cell, out GraphInstance instance))
                continue;

            if (instance.root != null)
                Destroy(instance.root);

            GameObject rebuilt = BuildGraph(cell.x, cell.y, false);

            if (rebuilt == null)
            {
                openGraphs.Remove(cell);
                graphOpenOrder.Remove(cell);
                continue;
            }

            instance.root = rebuilt;
            openGraphs[cell] = instance;
        }
    }

    private GameObject BuildGraph(int gridX, int gridY, bool animate)
    {
        Vector3 cellCenter = GetCellCenterWorld(gridX, gridY);
        Vector3 graphAnchor = cellCenter;

        if (useInteractionBounds && interactionBounds != null)
        {
            bool gotAnchor = interactionBounds.TryGetCO2GraphAnchor(
                cellCenter,
                GetCellWidth(),
                GetCellHeight(),
                out graphAnchor
            );

            if (!gotAnchor)
            {
                if (logRejectedAdjacentClicks)
                    Debug.Log($"CO2 graph nije otvoren jer nije pronađena sigurna pozicija za ćeliju ({gridX},{gridY}).");

                return null;
            }
        }

        GameObject root = new GameObject($"CO2LineGraphRoot_{gridX}_{gridY}");
        root.transform.SetParent(graphParent, true);
        root.transform.position = graphAnchor + Vector3.up * graphVerticalOffset;

        root.AddComponent<WorldLabelBillboard>();
        SetVisualizationLayerRecursively(root);

        CreatePanel(root.transform);
        CreateAxes(root.transform);
        CreateLabels(root.transform);
        CreateGraphLine(root.transform, gridX, gridY);

        if (animate)
        {
            root.transform.localScale = Vector3.one * 0.01f;
            CO2GraphPopupAnimator animator = root.AddComponent<CO2GraphPopupAnimator>();
            animator.Init(Vector3.one, 0.35f);
        }

        SetRenderersAndCollidersVisible(root, visualizationVisible);

        return root;
    }

    private void CreatePanel(Transform parent)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "CO2GraphPanel";
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = new Vector3(0f, graphHeight * 0.5f, 0.03f);
        panel.transform.localScale = new Vector3(graphWidth + 0.18f, graphHeight + 0.20f, 0.01f);

        Collider col = panel.GetComponent<Collider>();

        if (col != null)
            Destroy(col);

        Renderer renderer = panel.GetComponent<Renderer>();
        renderer.material = CreateUnlitRuntimeMaterial(panelColor);
    }

    private void CreateAxes(Transform parent)
    {
        GameObject axes = new GameObject("CO2GraphAxes");
        axes.transform.SetParent(parent, false);

        LineRenderer lr = axes.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 3;
        lr.startWidth = axisLineWidth;
        lr.endWidth = axisLineWidth;
        lr.material = CreateUnlitRuntimeMaterial(axisColor);
        lr.startColor = axisColor;
        lr.endColor = axisColor;

        Vector3 topLeft = new Vector3(-graphWidth * 0.5f, graphHeight, -0.02f);
        Vector3 bottomLeft = new Vector3(-graphWidth * 0.5f, 0f, -0.02f);
        Vector3 bottomRight = new Vector3(graphWidth * 0.5f, 0f, -0.02f);

        lr.SetPosition(0, topLeft);
        lr.SetPosition(1, bottomLeft);
        lr.SetPosition(2, bottomRight);
    }

    private void CreateLabels(Transform parent)
    {
        CreateText(
            parent,
            "CO2",
            new Vector3(-graphWidth * 0.5f, graphHeight + 0.09f, -0.03f),
            labelFontSize,
            labelCharacterSize
        );

        if (showDateLabel)
        {
            CreateText(
                parent,
                DateTime.Now.ToString("dd/MM/yyyy"),
                new Vector3(0f, graphHeight + dateLabelVerticalOffset, -0.03f),
                dateLabelFontSize,
                dateLabelCharacterSize,
                TextAnchor.MiddleCenter,
                TextAlignment.Center
            );
        }

        CreateText(
            parent,
            $"{maxCO2Ppm:F0}",
            new Vector3(-graphWidth * 0.5f - 0.19f, graphHeight, -0.03f),
            labelFontSize - 6,
            labelCharacterSize * 0.85f
        );

        CreateText(
            parent,
            $"{minCO2Ppm:F0}",
            new Vector3(-graphWidth * 0.5f - 0.19f, 0f, -0.03f),
            labelFontSize - 6,
            labelCharacterSize * 0.85f
        );

        CreateRollingTimeLabels(parent);
    }

    private void CreateRollingTimeLabels(Transform parent)
    {
        DateTime now = DateTime.Now;
        DateTime minusHalf = now.AddSeconds(-graphWindowSeconds * 0.5f);
        DateTime minusFull = now.AddSeconds(-graphWindowSeconds);

        float leftX = -graphWidth * 0.5f;
        float middleX = 0f;
        float rightX = graphWidth * 0.5f;

        float labelY = -0.09f;
        float labelZ = -0.03f;

        CreateText(
            parent,
            minusFull.ToString("HH:mm:ss"),
            new Vector3(leftX, labelY, labelZ),
            labelFontSize - 8,
            labelCharacterSize * 0.8f,
            TextAnchor.MiddleLeft,
            TextAlignment.Left
        );

        CreateText(
            parent,
            minusHalf.ToString("HH:mm:ss"),
            new Vector3(middleX, labelY, labelZ),
            labelFontSize - 8,
            labelCharacterSize * 0.8f,
            TextAnchor.MiddleCenter,
            TextAlignment.Center
        );

        CreateText(
            parent,
            now.ToString("HH:mm:ss"),
            new Vector3(rightX, labelY, labelZ),
            labelFontSize - 8,
            labelCharacterSize * 0.8f,
            TextAnchor.MiddleRight,
            TextAlignment.Right
        );
    }

    private void CreateGraphLine(Transform parent, int gridX, int gridY)
    {
        List<CO2Sample> history = GetCellHistory(gridX, gridY);

        if (history == null || history.Count == 0)
        {
            CreateText(
                parent,
                "No CO2 data",
                new Vector3(-graphWidth * 0.25f, graphHeight * 0.5f, -0.04f),
                labelFontSize,
                labelCharacterSize
            );

            return;
        }

        float now = GetRelativeSimulationTime();
        float minTime = now - graphWindowSeconds;

        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < history.Count; i++)
        {
            CO2Sample sample = history[i];

            if (sample.relativeTime < minTime)
                continue;

            float xT = Mathf.InverseLerp(minTime, now, sample.relativeTime);
            float yT = Mathf.InverseLerp(minCO2Ppm, maxCO2Ppm, sample.co2Ppm);

            xT = Mathf.Clamp01(xT);
            yT = Mathf.Clamp01(yT);

            float x = Mathf.Lerp(-graphWidth * 0.5f, graphWidth * 0.5f, xT);
            float y = Mathf.Lerp(0f, graphHeight, yT);

            points.Add(new Vector3(x, y, -0.04f));
        }

        if (points.Count == 0)
        {
            CreateText(
                parent,
                "No recent CO2 data",
                new Vector3(-graphWidth * 0.33f, graphHeight * 0.5f, -0.04f),
                labelFontSize,
                labelCharacterSize
            );

            return;
        }

        if (points.Count == 1)
            points.Add(points[0] + new Vector3(0.02f, 0f, 0f));

        CO2Sample latest = history[history.Count - 1];

        Color currentGraphColor = useCurrentCO2ColorForGraphLine
            ? GetColorForCO2(latest.co2Ppm)
            : fallbackLineColor;

        currentGraphColor.a = 1f;

        GameObject lineObject = new GameObject("CO2GraphLine");
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Count;
        line.startWidth = graphLineWidth;
        line.endWidth = graphLineWidth;
        line.material = CreateUnlitRuntimeMaterial(currentGraphColor);
        line.startColor = currentGraphColor;
        line.endColor = currentGraphColor;

        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i]);

        Vector3 latestPoint = points[points.Count - 1];

        CreateLatestPoint(parent, latestPoint, currentGraphColor);

        CreateText(
            parent,
            $"{latest.co2Ppm:F0} ppm",
            new Vector3(graphWidth * 0.5f + 0.06f, Mathf.Clamp(latestPoint.y, 0f, graphHeight), -0.04f),
            labelFontSize - 4,
            labelCharacterSize * 0.9f,
            TextAnchor.MiddleLeft,
            TextAlignment.Left
        );
    }

    private void CreateLatestPoint(Transform parent, Vector3 localPosition, Color pointColor)
    {
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.name = "CO2LatestPoint";
        point.transform.SetParent(parent, false);
        point.transform.localPosition = localPosition;
        point.transform.localScale = Vector3.one * 0.045f;

        Collider col = point.GetComponent<Collider>();

        if (col != null)
            Destroy(col);

        Renderer renderer = point.GetComponent<Renderer>();
        renderer.material = CreateUnlitRuntimeMaterial(pointColor);
    }

    private TextMesh CreateText(
        Transform parent,
        string text,
        Vector3 localPosition,
        int fontSize,
        float characterSize,
        TextAnchor anchor = TextAnchor.MiddleLeft,
        TextAlignment alignment = TextAlignment.Left)
    {
        GameObject obj = new GameObject("CO2GraphLabel");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;

        TextMesh textMesh = obj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = characterSize;
        textMesh.anchor = anchor;
        textMesh.alignment = alignment;
        textMesh.color = textColor;

        if (labelFont != null)
        {
            textMesh.font = labelFont;

            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();

            if (renderer != null && labelFont.material != null)
                renderer.material = labelFont.material;
        }

        return textMesh;
    }

    private Material CreateUnlitRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
        }

        material.renderQueue = 3000;

        return material;
    }

    private void PlaySpawnSound(Vector3 position)
    {
        if (spawnSound == null)
            return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }
        else
        {
            AudioSource.PlayClipAtPoint(spawnSound, position);
        }
    }

    private void PlayDespawnSound(Vector3 position)
    {
        if (despawnSound == null)
            return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(despawnSound);
        }
        else
        {
            AudioSource.PlayClipAtPoint(despawnSound, position);
        }
    }

    private void EnsureCollider()
    {
        if (!ensureColliderExists)
            return;

        Collider existing = GetComponent<Collider>();

        if (existing != null)
            return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            return;
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(worldWidth, 0.02f, worldHeight);
        box.center = Vector3.zero;
    }

    private void SetVisualizationLayerRecursively(GameObject root)
    {
        if (root == null)
            return;

        int layer = LayerMask.NameToLayer(visualizationLayerName);

        if (layer != -1)
            root.layer = layer;

        foreach (Transform child in root.transform)
            SetVisualizationLayerRecursively(child.gameObject);
    }

    private void SetRenderersAndCollidersVisible(GameObject root, bool visible)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = visible;
        }
    }
}

public class CO2GraphPopupAnimator : MonoBehaviour
{
    private Vector3 targetScale = Vector3.one;
    private float duration = 0.35f;
    private float timer = 0f;

    public void Init(Vector3 target, float animationDuration)
    {
        targetScale = target;
        duration = Mathf.Max(0.01f, animationDuration);
        timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        transform.localScale = Vector3.Lerp(Vector3.one * 0.01f, targetScale, eased);

        if (t >= 1f)
            Destroy(this);
    }
}


public class CO2GraphCloseAnimator : MonoBehaviour
{
    private float duration = 0.28f;
    private float dropDistance = 0.08f;
    private bool fadeRenderers = true;
    private float timer = 0f;

    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer[] renderers;
    private Material[] materials;
    private Color[] originalColors;
    private Color[] originalBaseColors;
    private bool[] hasColorProperty;
    private bool[] hasBaseColorProperty;

    public void Init(float animationDuration, float closeDropDistance, bool shouldFadeRenderers)
    {
        duration = Mathf.Max(0.05f, animationDuration);
        dropDistance = Mathf.Max(0f, closeDropDistance);
        fadeRenderers = shouldFadeRenderers;
        timer = 0f;

        startScale = transform.localScale;
        startPosition = transform.position;

        CO2GraphPopupAnimator popupAnimator = GetComponent<CO2GraphPopupAnimator>();
        if (popupAnimator != null)
            Destroy(popupAnimator);

        CacheRendererMaterials();
    }

    private void CacheRendererMaterials()
    {
        renderers = GetComponentsInChildren<Renderer>(true);

        if (renderers == null)
        {
            materials = new Material[0];
            originalColors = new Color[0];
            originalBaseColors = new Color[0];
            hasColorProperty = new bool[0];
            hasBaseColorProperty = new bool[0];
            return;
        }

        materials = new Material[renderers.Length];
        originalColors = new Color[renderers.Length];
        originalBaseColors = new Color[renderers.Length];
        hasColorProperty = new bool[renderers.Length];
        hasBaseColorProperty = new bool[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            materials[i] = renderers[i].material;

            if (materials[i] == null)
                continue;

            hasColorProperty[i] = materials[i].HasProperty("_Color");
            hasBaseColorProperty[i] = materials[i].HasProperty("_BaseColor");

            if (hasColorProperty[i])
                originalColors[i] = materials[i].GetColor("_Color");

            if (hasBaseColorProperty[i])
                originalBaseColors[i] = materials[i].GetColor("_BaseColor");
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / duration);
        float eased = EaseInBack(t);

        transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.01f, eased);
        transform.position = startPosition + Vector3.down * (dropDistance * eased);

        if (fadeRenderers)
            ApplyFade(1f - eased);

        if (t >= 1f)
            Destroy(gameObject);
    }

    private void ApplyFade(float alphaMultiplier)
    {
        if (materials == null)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            Material mat = materials[i];

            if (mat == null)
                continue;

            if (hasColorProperty[i])
            {
                Color c = originalColors[i];
                c.a *= alphaMultiplier;
                mat.SetColor("_Color", c);
            }

            if (hasBaseColorProperty[i])
            {
                Color c = originalBaseColors[i];
                c.a *= alphaMultiplier;
                mat.SetColor("_BaseColor", c);
            }
        }
    }

    private float EaseInBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }
}

