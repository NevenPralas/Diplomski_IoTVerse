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
        public SphereCollider bubbleCollider;

        public float latestNoiseDb;
        public float targetDiameter;
        public float currentDiameter;
        public float stableDisplayDiameter;
        public float lastUpdateTime;

        public float spawnStartTime;
        public bool isDespawning;
        public float despawnStartTime;
        public float lastDisplayDiameter;

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

    public struct NoiseBubbleHoverInfo
    {
        public Vector3 worldPoint;
        public Vector3 bubbleCenter;
        public float noiseDb;
        public float ageSeconds;
        public Vector2Int cell;
    }

    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;
    [SerializeField] private Transform bubblesParent;
    [SerializeField] private Material bubbleMaterialTemplate;

    [Header("Noise Mapping")]
    [SerializeField] private float minNoiseDb = 30f;
    [SerializeField] private float maxNoiseDb = 85f;
    [SerializeField] private bool useAverageOfRecentSamples = false;
    [SerializeField] private float historySeconds = 60f;

    [Header("Bubble Size")]
    [SerializeField] private float minBubbleDiameter = 0.12f;
    [SerializeField] private float maxBubbleDiameter = 0.55f;
    [SerializeField] private float sizeSmoothSpeed = 8f;
    [SerializeField] private bool clampToCellSize = true;

    [Range(0.1f, 1.5f)]
    [SerializeField] private float maxCellFill = 0.85f;

    [Header("Bubble Placement")]
    [SerializeField] private float verticalOffset = 0.18f;
    [SerializeField] private bool snapToCellCenter = true;

    [Header("Spawn / Despawn Animation")]
    [Tooltip("Nova kugla se pri prvom pojavljivanju elegantno poveća od 0 do svoje vrijednosti.")]
    [SerializeField] private bool animateBubbleSpawn = true;

    [SerializeField] private float spawnDuration = 0.32f;

    [Tooltip("Mali overshoot pri pojavljivanju, da kugla izgleda kao mekani balon.")]
    [Range(0f, 0.35f)]
    [SerializeField] private float spawnOvershoot = 0.12f;

    [Tooltip("Kad kugla istekne nakon removeAfterSeconds, ne nestane odmah nego se smanji i izblijedi.")]
    [SerializeField] private bool animateBubbleDespawn = true;

    [SerializeField] private float despawnDuration = 0.35f;

    [Tooltip("Kratki pojačani sjaj tijekom pojavljivanja nove kugle.")]
    [SerializeField] private bool spawnGlowBoost = true;

    [SerializeField] private float spawnGlowMultiplier = 1.8f;

    [Header("Active Bubble Pulse")]
    [Tooltip("Koliko dugo nakon zadnjeg mjerenja ćelija vrijedi kao aktualna.")]
    [SerializeField] private float activePulseTimeoutSeconds = 1.25f;

    [Tooltip("Aktualna kugla pulsira brže i življe.")]
    [SerializeField] private float activePulseAmplitude = 0.06f;

    [SerializeField] private float activePulseAmplitudeAtMaxNoise = 0.14f;
    [SerializeField] private float activeMinPulseSpeed = 1.2f;
    [SerializeField] private float activeMaxPulseSpeed = 4.5f;

    [Header("Old / Inactive Bubble Breathing")]
    [Tooltip("Stare kugle nisu statične nego sporo dišu.")]
    [SerializeField] private bool animateInactiveBubbles = true;

    [Tooltip("Sporo titranje starih kugli. Manje vrijednosti = mirnije.")]
    [SerializeField] private float inactivePulseAmplitude = 0.025f;

    [SerializeField] private float inactivePulseAmplitudeAtMaxNoise = 0.055f;

    [Tooltip("Stare kugle titraju puno sporije od aktualne.")]
    [SerializeField] private float inactiveMinPulseSpeed = 0.18f;

    [SerializeField] private float inactiveMaxPulseSpeed = 0.65f;

    [Tooltip("Kad je uključeno, svaka ćelija ima malo drugačiju fazu titranja.")]
    [SerializeField] private bool randomizePulsePhasePerCell = true;

    [Header("Old / Inactive Bubble Look")]
    [Tooltip("Stare kugle zadržavaju RGB boju vrijednosti, ali su prozirnije.")]
    [Range(0f, 1f)]
    [SerializeField] private float inactiveAlphaMultiplier = 0.48f;

    [Tooltip("Aktualne kugle imaju puniji alpha.")]
    [Range(0f, 1f)]
    [SerializeField] private float activeAlphaMultiplier = 1.0f;

    [Tooltip("Stare kugle slabije svijetle.")]
    [Range(0f, 1f)]
    [SerializeField] private float inactiveEmissionMultiplier = 0.35f;

    [Tooltip("Aktualne kugle jače svijetle.")]
    [Range(0f, 2f)]
    [SerializeField] private float activeEmissionMultiplier = 1.0f;

    [Header("Color Mapping")]
    [SerializeField] private bool useDefaultNoiseGradient = true;
    [SerializeField] private Gradient noiseGradient;
    [SerializeField] private float emissionIntensity = 1.2f;

    [Range(0f, 1f)]
    [SerializeField] private float bubbleAlpha = 0.75f;

    [Header("Lifetime")]
    [Tooltip("Ako je true, posjećene ćelije ostaju zauvijek. Ako je false, brišu se nakon removeAfterSeconds.")]
    [SerializeField] private bool keepVisitedCellsVisible = false;

    [SerializeField] private float removeAfterSeconds = 60f;

    [Header("Hover / Physics")]
    [SerializeField] private bool removeBubbleColliders = false;
    [SerializeField] private bool bubbleColliderIsTrigger = true;
    [SerializeField] private string visualizationLayerName = "Visualization";

    [Header("Debug")]
    [SerializeField] private bool logAddedSamples = false;
    [SerializeField] private bool logRemovedBubbles = false;

    private readonly Dictionary<Vector2Int, NoiseCellData> cells =
        new Dictionary<Vector2Int, NoiseCellData>();

    private string externalGradientSignature = "";

    public float MinNoiseDb => minNoiseDb;
    public float MaxNoiseDb => maxNoiseDb;

    private void Reset()
    {
        noiseGradient = CreateDefaultNoiseGradient();
        keepVisitedCellsVisible = false;
        removeAfterSeconds = 60f;
        removeBubbleColliders = false;
        bubbleColliderIsTrigger = true;
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
        activePulseTimeoutSeconds = Mathf.Max(0.05f, activePulseTimeoutSeconds);
        spawnDuration = Mathf.Max(0.01f, spawnDuration);
        despawnDuration = Mathf.Max(0.01f, despawnDuration);
        spawnGlowMultiplier = Mathf.Max(1f, spawnGlowMultiplier);

        minBubbleDiameter = Mathf.Max(0.01f, minBubbleDiameter);
        maxBubbleDiameter = Mathf.Max(minBubbleDiameter, maxBubbleDiameter);

        sizeSmoothSpeed = Mathf.Max(0.1f, sizeSmoothSpeed);

        activeMinPulseSpeed = Mathf.Max(0.01f, activeMinPulseSpeed);
        activeMaxPulseSpeed = Mathf.Max(activeMinPulseSpeed, activeMaxPulseSpeed);

        inactiveMinPulseSpeed = Mathf.Max(0.01f, inactiveMinPulseSpeed);
        inactiveMaxPulseSpeed = Mathf.Max(inactiveMinPulseSpeed, inactiveMaxPulseSpeed);

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
        cellData.isDespawning = false;

        RemoveOldSamples(cellData);

        float displayNoise = noiseDb;

        if (useAverageOfRecentSamples && TryGetAverageNoise(cellData, out float averageNoise))
            displayNoise = averageNoise;

        cellData.latestNoiseDb = displayNoise;
        cellData.targetDiameter = GetDiameterForNoise(displayNoise);

        // Kad je robot upravo na toj ćeliji, odmah osvježi kao aktivnu kuglu.
        UpdateBubbleMaterial(cellData, displayNoise, true);

        if (logAddedSamples)
        {
            Debug.Log(
                $"NoiseBubbleGrid sample | cell=({gridX},{gridY}) " +
                $"world={worldPosition} | noise={noiseDb:F1} dBA | display={displayNoise:F1} dBA"
            );
        }
    }

    public void ApplyExternalNoiseGradient(
        float minValue,
        float maxValue,
        Color lowColor,
        Color middleColor,
        Color highColor,
        bool updateExistingBubblesImmediately = true)
    {
        minValue = Mathf.Min(minValue, maxValue - 0.01f);
        maxValue = Mathf.Max(maxValue, minValue + 0.01f);

        lowColor.a = 1f;
        middleColor.a = 1f;
        highColor.a = 1f;

        string signature =
            minValue.ToString("F3") + "|" +
            maxValue.ToString("F3") + "|" +
            ColorUtility.ToHtmlStringRGBA(lowColor) + "|" +
            ColorUtility.ToHtmlStringRGBA(middleColor) + "|" +
            ColorUtility.ToHtmlStringRGBA(highColor);

        if (signature == externalGradientSignature)
            return;

        externalGradientSignature = signature;

        minNoiseDb = minValue;
        maxNoiseDb = maxValue;

        useDefaultNoiseGradient = false;
        noiseGradient = CreateThreeColorNoiseGradient(lowColor, middleColor, highColor);

        if (updateExistingBubblesImmediately)
            RefreshExistingBubbleVisuals();
    }

    public void ClearBubbles()
    {
        foreach (KeyValuePair<Vector2Int, NoiseCellData> pair in cells)
        {
            if (pair.Value != null && pair.Value.bubbleObject != null)
                Destroy(pair.Value.bubbleObject);
        }

        cells.Clear();

        if (bubblesParent != null)
        {
            List<GameObject> oldChildren = new List<GameObject>();

            foreach (Transform child in bubblesParent)
            {
                if (child != null && child.name.StartsWith("NoiseBubble_"))
                    oldChildren.Add(child.gameObject);
            }

            for (int i = 0; i < oldChildren.Count; i++)
            {
                if (oldChildren[i] != null)
                    Destroy(oldChildren[i]);
            }
        }
    }

    public void SetVisible(bool visible)
    {
        if (bubblesParent != null)
            bubblesParent.gameObject.SetActive(visible);
    }

    public bool TryGetClosestBubbleHoverInfo(Vector3 worldPoint, out NoiseBubbleHoverInfo info)
    {
        info = default;

        if (cells.Count == 0)
            return false;

        NoiseCellData closest = null;
        float bestDistanceSqr = float.MaxValue;

        foreach (KeyValuePair<Vector2Int, NoiseCellData> pair in cells)
        {
            NoiseCellData cell = pair.Value;

            if (cell == null || cell.bubbleObject == null || cell.isDespawning)
                continue;

            Vector3 center = cell.bubbleObject.transform.position;
            float radius = Mathf.Max(0.01f, cell.bubbleObject.transform.lossyScale.x * 0.5f);
            float distanceSqr = (center - worldPoint).sqrMagnitude;

            if (distanceSqr <= radius * radius * 4f && distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                closest = cell;
            }
        }

        if (closest == null)
            return false;

        info = new NoiseBubbleHoverInfo
        {
            worldPoint = worldPoint,
            bubbleCenter = closest.bubbleObject.transform.position,
            noiseDb = closest.latestNoiseDb,
            ageSeconds = Mathf.Max(0f, Time.time - closest.lastUpdateTime),
            cell = closest.cell
        };

        return true;
    }

    private NoiseCellData CreateCellData(Vector2Int cellKey, int gridX, int gridY, Vector3 originalWorldPosition)
    {
        Vector3 position;

        if (snapToCellCenter)
            position = heatmap.GetCellCenterWorld(gridX, gridY);
        else
            position = originalWorldPosition;

        position.y += verticalOffset;

        GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = $"NoiseBubble_{gridX}_{gridY}";
        bubble.transform.SetParent(bubblesParent, true);
        bubble.transform.position = position;
        bubble.transform.localScale = animateBubbleSpawn ? Vector3.zero : Vector3.one * minBubbleDiameter;

        ApplyVisualizationLayerRecursively(bubble);

        SphereCollider sphereCollider = bubble.GetComponent<SphereCollider>();

        if (removeBubbleColliders)
        {
            if (sphereCollider != null)
                Destroy(sphereCollider);

            sphereCollider = null;
        }
        else
        {
            if (sphereCollider == null)
                sphereCollider = bubble.AddComponent<SphereCollider>();

            sphereCollider.isTrigger = bubbleColliderIsTrigger;
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
            bubbleCollider = sphereCollider,
            latestNoiseDb = minNoiseDb,
            targetDiameter = minBubbleDiameter,
            currentDiameter = minBubbleDiameter,
            stableDisplayDiameter = minBubbleDiameter,
            lastDisplayDiameter = minBubbleDiameter,
            spawnStartTime = Time.time,
            isDespawning = false,
            despawnStartTime = -1f,
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

            float age = Time.time - cellData.lastUpdateTime;
            bool isActiveCell = age <= activePulseTimeoutSeconds && !cellData.isDespawning;

            cellData.currentDiameter = Mathf.Lerp(
                cellData.currentDiameter,
                cellData.targetDiameter,
                Time.deltaTime * sizeSmoothSpeed
            );

            float normalized = GetNormalizedNoise(cellData.latestNoiseDb);

            float pulseSpeed;
            float amplitude;

            if (isActiveCell)
            {
                pulseSpeed = Mathf.Lerp(activeMinPulseSpeed, activeMaxPulseSpeed, normalized);
                amplitude = Mathf.Lerp(activePulseAmplitude, activePulseAmplitudeAtMaxNoise, normalized);
            }
            else if (animateInactiveBubbles && !cellData.isDespawning)
            {
                pulseSpeed = Mathf.Lerp(inactiveMinPulseSpeed, inactiveMaxPulseSpeed, normalized);
                amplitude = Mathf.Lerp(inactivePulseAmplitude, inactivePulseAmplitudeAtMaxNoise, normalized);
            }
            else
            {
                pulseSpeed = 0f;
                amplitude = 0f;
            }

            float phase = randomizePulsePhasePerCell
                ? GetStableCellPhase(cellData.cell)
                : 0f;

            float pulse = 1f;

            if (pulseSpeed > 0f && amplitude > 0f)
            {
                pulse =
                    1f +
                    Mathf.Sin((Time.time + phase) * pulseSpeed * Mathf.PI * 2f) * amplitude;
            }

            float displayDiameter = Mathf.Max(0.01f, cellData.currentDiameter * pulse);

            float spawnFactor = GetBubbleSpawnFactor(cellData);
            float despawnFactor = GetBubbleDespawnFactor(cellData);

            float animatedDiameter = displayDiameter * spawnFactor * despawnFactor;
            animatedDiameter = Mathf.Max(0.001f, animatedDiameter);

            cellData.stableDisplayDiameter = cellData.currentDiameter;
            cellData.lastDisplayDiameter = animatedDiameter;
            cellData.bubbleObject.transform.localScale = Vector3.one * animatedDiameter;

            float visualAlphaMultiplier = spawnFactor * despawnFactor;
            float visualEmissionMultiplier = 1f;

            if (spawnGlowBoost && animateBubbleSpawn && spawnFactor < 1f && !cellData.isDespawning)
                visualEmissionMultiplier = Mathf.Lerp(spawnGlowMultiplier, 1f, spawnFactor);

            // RGB ostaje ista vrijednost buke, ali alpha/emission nose stanje: nova / aktualna / stara / nestajanje.
            UpdateBubbleMaterial(cellData, cellData.latestNoiseDb, isActiveCell, visualAlphaMultiplier, visualEmissionMultiplier);
        }
    }

    private void RefreshExistingBubbleVisuals()
    {
        foreach (KeyValuePair<Vector2Int, NoiseCellData> pair in cells)
        {
            NoiseCellData cellData = pair.Value;

            if (cellData == null)
                continue;

            float age = Time.time - cellData.lastUpdateTime;
            bool isActiveCell = age <= activePulseTimeoutSeconds;

            cellData.targetDiameter = GetDiameterForNoise(cellData.latestNoiseDb);
            UpdateBubbleMaterial(cellData, cellData.latestNoiseDb, isActiveCell, 1f, 1f);
        }
    }

    private void UpdateBubbleMaterial(
        NoiseCellData cellData,
        float noiseDb,
        bool isActiveCell,
        float visualAlphaMultiplier = 1f,
        float visualEmissionMultiplier = 1f)
    {
        if (cellData == null || cellData.bubbleMaterial == null)
            return;

        Color color = GetColorForNoise(noiseDb);

        float alphaMultiplier = isActiveCell
            ? activeAlphaMultiplier
            : inactiveAlphaMultiplier;

        float emissionMultiplier = isActiveCell
            ? activeEmissionMultiplier
            : inactiveEmissionMultiplier;

        color.a = bubbleAlpha * alphaMultiplier * Mathf.Clamp01(visualAlphaMultiplier);

        if (cellData.bubbleMaterial.HasProperty("_BaseColor"))
            cellData.bubbleMaterial.SetColor("_BaseColor", color);

        if (cellData.bubbleMaterial.HasProperty("_Color"))
            cellData.bubbleMaterial.SetColor("_Color", color);

        if (emissionIntensity > 0f && cellData.bubbleMaterial.HasProperty("_EmissionColor"))
        {
            cellData.bubbleMaterial.EnableKeyword("_EMISSION");
            cellData.bubbleMaterial.SetColor("_EmissionColor", color * emissionIntensity * emissionMultiplier * visualEmissionMultiplier);
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
            RemoveOldSamples(pair.Value);
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

            if (animateBubbleDespawn && !cellData.isDespawning)
            {
                StartBubbleDespawn(cellData);
                continue;
            }

            if (cellData.isDespawning && Time.time - cellData.despawnStartTime < despawnDuration)
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

                if (logRemovedBubbles)
                    Debug.Log($"NoiseBubbleGrid removed expired bubble at cell {key}");
            }
        }
    }

    private void StartBubbleDespawn(NoiseCellData cellData)
    {
        if (cellData == null || cellData.isDespawning)
            return;

        cellData.isDespawning = true;
        cellData.despawnStartTime = Time.time;

        if (cellData.bubbleCollider != null)
            cellData.bubbleCollider.enabled = false;
    }

    private float GetBubbleSpawnFactor(NoiseCellData cellData)
    {
        if (!animateBubbleSpawn || cellData == null)
            return 1f;

        float t = Mathf.Clamp01((Time.time - cellData.spawnStartTime) / spawnDuration);

        if (t >= 1f)
            return 1f;

        float eased = EaseOutBack(t);
        float overshootLimited = Mathf.Lerp(1f, 1f + spawnOvershoot, Mathf.Sin(t * Mathf.PI));

        return Mathf.Clamp(eased * overshootLimited, 0f, 1f + spawnOvershoot);
    }

    private float GetBubbleDespawnFactor(NoiseCellData cellData)
    {
        if (!animateBubbleDespawn || cellData == null || !cellData.isDespawning)
            return 1f;

        float t = Mathf.Clamp01((Time.time - cellData.despawnStartTime) / despawnDuration);
        float eased = EaseInCubic(t);

        return Mathf.Clamp01(1f - eased);
    }

    private float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
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

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(new Color(0.1f, 0.35f, 1f, 1f), 0f),
            new GradientColorKey(new Color(0.8f, 0.0f, 1f, 1f), 0.55f),
            new GradientColorKey(new Color(1f, 0.05f, 0.0f, 1f), 1f)
        };

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(0.75f, 0f),
            new GradientAlphaKey(0.85f, 1f)
        };

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    private Gradient CreateThreeColorNoiseGradient(Color lowColor, Color middleColor, Color highColor)
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(lowColor, 0f),
            new GradientColorKey(middleColor, 0.5f),
            new GradientColorKey(highColor, 1f)
        };

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(0.75f, 0f),
            new GradientAlphaKey(0.85f, 0.5f),
            new GradientAlphaKey(1.00f, 1f)
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