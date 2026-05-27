using System;
using System.Collections.Generic;
using UnityEngine;

public class NoiseBubbleGrid : MonoBehaviour
{
    [Serializable]
    private class NoiseCellData
    {
        public Vector2Int cell;
        public GameObject bubbleObject;
        public Renderer bubbleRenderer;
        public Material bubbleMaterial;

        public float latestNoiseDb;
        public float targetDiameter;
        public float currentDiameter;
        public float lastUpdateTime;

        public readonly List<NoiseSample> samples = new List<NoiseSample>();
    }

    [Serializable]
    private class NoiseSample
    {
        public float time;
        public float noiseDb;

        public NoiseSample(float time, float noiseDb)
        {
            this.time = time;
            this.noiseDb = noiseDb;
        }
    }

    [Header("References")]
    [Tooltip("Grid objekt koji već ima ShaderGridHeatmap. Koristi se samo za računanje ćelija i centra ćelije.")]
    [SerializeField] private ShaderGridHeatmap heatmap;

    [Tooltip("Opcionalni parent za sve balone. Ako je prazno, skripta sama napravi root objekt.")]
    [SerializeField] private Transform bubblesParent;

    [Tooltip("Opcionalni materijal za balone. Ako je prazno, skripta sama napravi URP/Lit materijal.")]
    [SerializeField] private Material bubbleMaterialTemplate;

    [Header("Noise Mapping")]
    [Tooltip("Donja granica buke za mapiranje boje i veličine.")]
    [SerializeField] private float minNoiseDb = 30f;

    [Tooltip("Gornja granica buke za mapiranje boje i veličine.")]
    [SerializeField] private float maxNoiseDb = 85f;

    [Tooltip("Ako je uključeno, balon koristi prosjek mjerenja u zadnjih historySeconds umjesto zadnje vrijednosti.")]
    [SerializeField] private bool useAverageOfRecentSamples = false;

    [Tooltip("Vremenski prozor za povijest po ćeliji. 60 = zadnja minuta.")]
    [SerializeField] private float historySeconds = 60f;

    [Header("Bubble Size")]
    [Tooltip("Najmanji promjer balona za minNoiseDb.")]
    [SerializeField] private float minBubbleDiameter = 0.12f;

    [Tooltip("Najveći promjer balona za maxNoiseDb.")]
    [SerializeField] private float maxBubbleDiameter = 0.55f;

    [Tooltip("Koliko brzo balon prelazi prema novoj veličini kad dođe novo mjerenje.")]
    [SerializeField] private float sizeSmoothSpeed = 8f;

    [Tooltip("Ako je uključeno, balon ne smije biti veći od ćelije.")]
    [SerializeField] private bool clampToCellSize = true;

    [Tooltip("Koliko posto ćelije maksimalno smije zauzeti balon ako je clampToCellSize uključen.")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float maxCellFill = 0.85f;

    [Header("Bubble Placement")]
    [Tooltip("Visina balona iznad poda/grida.")]
    [SerializeField] private float verticalOffset = 0.18f;

    [Tooltip("Ako je uključeno, balon se pozicionira točno na centar ćelije.")]
    [SerializeField] private bool snapToCellCenter = true;

    [Header("Pulse / Titranje")]
    [Tooltip("Osnovna amplituda titranja. Veća buka dodatno pojača titranje.")]
    [SerializeField] private float pulseAmplitude = 0.06f;

    [Tooltip("Dodatna amplituda titranja kod najveće buke.")]
    [SerializeField] private float pulseAmplitudeAtMaxNoise = 0.14f;

    [Tooltip("Brzina titranja kod najniže buke.")]
    [SerializeField] private float minPulseSpeed = 1.2f;

    [Tooltip("Brzina titranja kod najveće buke.")]
    [SerializeField] private float maxPulseSpeed = 4.5f;

    [Tooltip("Ako je uključeno, svaki balon ima malo drugačiju fazu titranja da scena ne izgleda umjetno sinkronizirano.")]
    [SerializeField] private bool randomizePulsePhasePerCell = true;

    [Header("Color Mapping")]
    [Tooltip("Ako je uključeno, koristi se default gradijent: plavo -> magenta -> crveno.")]
    [SerializeField] private bool useDefaultNoiseGradient = true;

    [SerializeField] private Gradient noiseGradient;

    [Tooltip("Intenzitet emisije boje. 0 znači bez emisije.")]
    [SerializeField] private float emissionIntensity = 1.2f;

    [Tooltip("Alfa boje. Radi samo ako materijal/shader podržava transparentnost.")]
    [Range(0f, 1f)]
    [SerializeField] private float bubbleAlpha = 0.75f;

    [Header("Lifetime")]
    [Tooltip("Ako je uključeno, jednom stvoreni baloni ostaju i nakon što robot ode iz ćelije.")]
    [SerializeField] private bool keepVisitedCellsVisible = true;

    [Tooltip("Ako keepVisitedCellsVisible nije uključen, balon nestaje kad je stariji od ovoliko sekundi.")]
    [SerializeField] private float removeAfterSeconds = 60f;

    [Header("Physics / Layers")]
    [SerializeField] private string visualizationLayerName = "Visualization";

    [Tooltip("Ako je uključeno, baloni nemaju collidere i neće smetati robotu/playeru.")]
    [SerializeField] private bool removeBubbleColliders = true;

    [Header("Debug")]
    [SerializeField] private bool logAddedSamples = false;

    private readonly Dictionary<Vector2Int, NoiseCellData> cells = new Dictionary<Vector2Int, NoiseCellData>();

    private void Reset()
    {
        noiseGradient = CreateDefaultNoiseGradient();
    }

    private void Awake()
    {
        if (useDefaultNoiseGradient || noiseGradient == null)
            noiseGradient = CreateDefaultNoiseGradient();

        if (bubblesParent == null)
        {
            GameObject root = new GameObject("NoiseBubbleGrid_Bubbles");
            root.transform.SetParent(transform, false);
            bubblesParent = root.transform;
        }

        ApplyVisualizationLayerRecursively(bubblesParent.gameObject);
    }

    private void OnValidate()
    {
        minNoiseDb = Mathf.Min(minNoiseDb, maxNoiseDb - 0.01f);
        maxNoiseDb = Mathf.Max(maxNoiseDb, minNoiseDb + 0.01f);

        historySeconds = Mathf.Max(1f, historySeconds);
        removeAfterSeconds = Mathf.Max(1f, removeAfterSeconds);

        minBubbleDiameter = Mathf.Max(0.01f, minBubbleDiameter);
        maxBubbleDiameter = Mathf.Max(minBubbleDiameter, maxBubbleDiameter);

        sizeSmoothSpeed = Mathf.Max(0.1f, sizeSmoothSpeed);

        minPulseSpeed = Mathf.Max(0.01f, minPulseSpeed);
        maxPulseSpeed = Mathf.Max(minPulseSpeed, maxPulseSpeed);

        if (useDefaultNoiseGradient)
            noiseGradient = CreateDefaultNoiseGradient();
    }

    private void Update()
    {
        RemoveOldSamplesFromCellHistories();
        UpdateAllBubbles();

        if (!keepVisitedCellsVisible)
            RemoveExpiredBubbles();
    }

    public void AddNoiseSample(Vector3 worldPosition, float noiseDb)
    {
        if (heatmap == null)
        {
            Debug.LogWarning("NoiseBubbleGrid: Heatmap referenca nije postavljena.");
            return;
        }

        if (!heatmap.TryGetCellIndex(worldPosition, out int gridX, out int gridY))
            return;

        Vector2Int cellKey = new Vector2Int(gridX, gridY);

        if (!cells.TryGetValue(cellKey, out NoiseCellData cellData))
        {
            cellData = CreateCellData(cellKey, gridX, gridY, worldPosition);
            cells[cellKey] = cellData;
        }

        float now = Time.time;

        cellData.samples.Add(new NoiseSample(now, noiseDb));
        cellData.lastUpdateTime = now;

        RemoveOldSamples(cellData);

        float displayNoise = noiseDb;

        if (useAverageOfRecentSamples && TryGetAverageNoise(cellData, out float averageNoise))
            displayNoise = averageNoise;

        cellData.latestNoiseDb = displayNoise;
        cellData.targetDiameter = GetDiameterForNoise(displayNoise);

        UpdateBubbleMaterial(cellData, displayNoise);

        if (logAddedSamples)
        {
            Debug.Log(
                $"NoiseBubbleGrid sample | cell=({gridX},{gridY}) " +
                $"noise={noiseDb:F1} dBA display={displayNoise:F1} dBA"
            );
        }
    }

    public void ClearBubbles()
    {
        foreach (KeyValuePair<Vector2Int, NoiseCellData> pair in cells)
        {
            if (pair.Value != null && pair.Value.bubbleObject != null)
                Destroy(pair.Value.bubbleObject);
        }

        cells.Clear();
    }

    public void SetVisible(bool visible)
    {
        if (bubblesParent != null)
            bubblesParent.gameObject.SetActive(visible);
    }

    private NoiseCellData CreateCellData(Vector2Int cellKey, int gridX, int gridY, Vector3 originalWorldPosition)
    {
        Vector3 position;

        if (snapToCellCenter)
        {
            position = heatmap.GetCellCenterWorld(gridX, gridY);
        }
        else
        {
            position = originalWorldPosition;
        }

        position.y += verticalOffset;

        GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = $"NoiseBubble_{gridX}_{gridY}";
        bubble.transform.SetParent(bubblesParent, true);
        bubble.transform.position = position;
        bubble.transform.localScale = Vector3.one * minBubbleDiameter;

        ApplyVisualizationLayerRecursively(bubble);

        if (removeBubbleColliders)
        {
            Collider col = bubble.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
        }

        Renderer renderer = bubble.GetComponent<Renderer>();
        Material mat = CreateBubbleMaterial();
        renderer.material = mat;

        return new NoiseCellData
        {
            cell = cellKey,
            bubbleObject = bubble,
            bubbleRenderer = renderer,
            bubbleMaterial = mat,
            latestNoiseDb = minNoiseDb,
            targetDiameter = minBubbleDiameter,
            currentDiameter = minBubbleDiameter,
            lastUpdateTime = Time.time
        };
    }

    private void UpdateAllBubbles()
    {
        foreach (KeyValuePair<Vector2Int, NoiseCellData> pair in cells)
        {
            NoiseCellData cellData = pair.Value;

            if (cellData == null || cellData.bubbleObject == null)
                continue;

            float normalized = GetNormalizedNoise(cellData.latestNoiseDb);

            float pulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, normalized);
            float amplitude = Mathf.Lerp(pulseAmplitude, pulseAmplitudeAtMaxNoise, normalized);

            float phase = randomizePulsePhasePerCell
                ? GetStableCellPhase(cellData.cell)
                : 0f;

            float pulse =
                1f + Mathf.Sin((Time.time + phase) * pulseSpeed * Mathf.PI * 2f) * amplitude;

            cellData.currentDiameter = Mathf.Lerp(
                cellData.currentDiameter,
                cellData.targetDiameter,
                Time.deltaTime * sizeSmoothSpeed
            );

            float finalDiameter = Mathf.Max(0.01f, cellData.currentDiameter * pulse);
            cellData.bubbleObject.transform.localScale = Vector3.one * finalDiameter;
        }
    }

    private void UpdateBubbleMaterial(NoiseCellData cellData, float noiseDb)
    {
        if (cellData == null || cellData.bubbleMaterial == null)
            return;

        Color color = GetColorForNoise(noiseDb);
        color.a = bubbleAlpha;

        if (cellData.bubbleMaterial.HasProperty("_BaseColor"))
            cellData.bubbleMaterial.SetColor("_BaseColor", color);

        if (cellData.bubbleMaterial.HasProperty("_Color"))
            cellData.bubbleMaterial.SetColor("_Color", color);

        if (emissionIntensity > 0f && cellData.bubbleMaterial.HasProperty("_EmissionColor"))
        {
            cellData.bubbleMaterial.EnableKeyword("_EMISSION");
            cellData.bubbleMaterial.SetColor("_EmissionColor", color * emissionIntensity);
        }
    }

    private float GetDiameterForNoise(float noiseDb)
    {
        float t = GetNormalizedNoise(noiseDb);
        float diameter = Mathf.Lerp(minBubbleDiameter, maxBubbleDiameter, t);

        if (clampToCellSize && heatmap != null)
        {
            float maxAllowed = Mathf.Min(heatmap.GetCellWidth(), heatmap.GetCellHeight()) * maxCellFill;
            diameter = Mathf.Min(diameter, maxAllowed);
        }

        return diameter;
    }

    private Color GetColorForNoise(float noiseDb)
    {
        float t = GetNormalizedNoise(noiseDb);

        if (noiseGradient != null)
            return noiseGradient.Evaluate(t);

        return Color.Lerp(Color.blue, Color.red, t);
    }

    private float GetNormalizedNoise(float noiseDb)
    {
        if (Mathf.Approximately(minNoiseDb, maxNoiseDb))
            return 1f;

        return Mathf.Clamp01(Mathf.InverseLerp(minNoiseDb, maxNoiseDb, noiseDb));
    }

    private bool TryGetAverageNoise(NoiseCellData cellData, out float averageNoise)
    {
        averageNoise = 0f;

        if (cellData == null || cellData.samples.Count == 0)
            return false;

        float minAllowedTime = Time.time - historySeconds;
        float sum = 0f;
        int count = 0;

        for (int i = 0; i < cellData.samples.Count; i++)
        {
            NoiseSample sample = cellData.samples[i];

            if (sample.time < minAllowedTime)
                continue;

            sum += sample.noiseDb;
            count++;
        }

        if (count == 0)
            return false;

        averageNoise = sum / count;
        return true;
    }

    private void RemoveOldSamplesFromCellHistories()
    {
        foreach (KeyValuePair<Vector2Int, NoiseCellData> pair in cells)
        {
            RemoveOldSamples(pair.Value);
        }
    }

    private void RemoveOldSamples(NoiseCellData cellData)
    {
        if (cellData == null)
            return;

        float minAllowedTime = Time.time - historySeconds;

        for (int i = cellData.samples.Count - 1; i >= 0; i--)
        {
            if (cellData.samples[i].time < minAllowedTime)
                cellData.samples.RemoveAt(i);
        }
    }

    private void RemoveExpiredBubbles()
    {
        List<Vector2Int> toRemove = null;

        foreach (KeyValuePair<Vector2Int, NoiseCellData> pair in cells)
        {
            NoiseCellData cellData = pair.Value;

            if (cellData == null)
                continue;

            if (Time.time - cellData.lastUpdateTime <= removeAfterSeconds)
                continue;

            if (toRemove == null)
                toRemove = new List<Vector2Int>();

            toRemove.Add(pair.Key);
        }

        if (toRemove == null)
            return;

        for (int i = 0; i < toRemove.Count; i++)
        {
            Vector2Int key = toRemove[i];

            if (cells.TryGetValue(key, out NoiseCellData cellData))
            {
                if (cellData.bubbleObject != null)
                    Destroy(cellData.bubbleObject);

                cells.Remove(key);
            }
        }
    }

    private Material CreateBubbleMaterial()
    {
        Material material;

        if (bubbleMaterialTemplate != null)
        {
            material = new Material(bubbleMaterialTemplate);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
        }

        return material;
    }

    private Gradient CreateDefaultNoiseGradient()
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(new Color(0.1f, 0.35f, 1f, 1f), 0f),
            new GradientColorKey(new Color(0.8f, 0.0f, 1f, 1f), 0.55f),
            new GradientColorKey(new Color(1f, 0.05f, 0.0f, 1f), 1f)
        };

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(0.75f, 0f),
            new GradientAlphaKey(0.85f, 1f)
        };

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    private float GetStableCellPhase(Vector2Int cell)
    {
        int hash = cell.x * 73856093 ^ cell.y * 19349663;
        hash = Mathf.Abs(hash);

        return (hash % 1000) / 1000f;
    }

    private void ApplyVisualizationLayerRecursively(GameObject root)
    {
        if (root == null)
            return;

        int layer = LayerMask.NameToLayer(visualizationLayerName);

        if (layer == -1)
            return;

        root.layer = layer;

        foreach (Transform child in root.transform)
            ApplyVisualizationLayerRecursively(child.gameObject);
    }
}