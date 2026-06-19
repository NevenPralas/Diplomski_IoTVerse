using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpaceTimeCubeManager : MonoBehaviour
{
    private bool interactionEnabled = true;

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
    }

    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;
    [SerializeField] private GridCellCursor gridCellCursor;

    [Header("Room / Interaction Bounds")]
    [Tooltip("Opcionalni filter koji sprječava otvaranje stupaca izvan sobe. Koristi isto kao GridCellCursor.")]
    [SerializeField] private CellInteractionBounds interactionBounds;
    [SerializeField] private bool useInteractionBounds = true;

    [Header("Input")]
    [SerializeField] private InputActionReference placeColumnAction;

    [Header("Main White Column")]
    [SerializeField] private float cubeHeight = 1.8f;
    [SerializeField] private Material cubeMaterial;
    [SerializeField] private bool animateMainColumn = true;

    [Header("Filled Time Slices")]
    [SerializeField] private Material bandMaterial;
    [SerializeField] private int visibleSeconds = 60;
    [SerializeField] private float refreshInterval = 1f;
    [SerializeField] private float shellExpand = 0.015f;
    [SerializeField] private float shellAlpha = 0.9f;
    [SerializeField] private float verticalBandGap = 0.004f;

    [Header("Band Visual Tuning")]
    [SerializeField] private bool useEmissionForBands = true;
    [SerializeField] private float emissionIntensity = 1.2f;

    [Header("Time Labels")]
    [SerializeField] private bool showTimeLabels = true;
    [SerializeField] private float labelHorizontalOffset = 0.18f;
    [SerializeField] private float labelVerticalNudge = 0.0f;
    [SerializeField] private int labelFontSize = 48;
    [SerializeField] private float labelCharacterSize = 0.02f;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private Font labelFont;

    [Header("Date Label")]
    [SerializeField] private bool showDateLabel = true;
    [SerializeField] private float dateLabelHeightOffset = 0.14f;
    [SerializeField] private int dateLabelFontSize = 52;
    [SerializeField] private float dateLabelCharacterSize = 0.022f;
    [SerializeField] private Color dateLabelColor = Color.white;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip despawnSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Close Animation")]
    [Tooltip("Ako je uključeno, stupac se pri zatvaranju elegantno spusti prema podu umjesto da odmah nestane.")]
    [SerializeField] private bool animateColumnClose = true;

    [SerializeField] private float columnCloseDuration = 0.35f;

    [Tooltip("Ako je uključeno, svi rendereri stupca lagano blijede tijekom spuštanja. Ako shader ne podržava alpha, i dalje radi scale-down animacija.")]
    [SerializeField] private bool fadeColumnOnClose = true;

    [Header("Multiple Columns")]
    [Tooltip("0 znaci neograniceno. Ako stavis npr. 5, moze biti otvoreno najvise 5 stupaca.")]
    [SerializeField] private int maxOpenColumns = 0;

    [Tooltip("Ako kliknes celiju koja vec ima otvoren stupac, stupac se zatvara.")]
    [SerializeField] private bool toggleColumnOnSameCell = true;

    [Header("Physics Isolation")]
    [Tooltip("Runtime stupci i njihove labele idu na ovaj layer. Ostavi prazno ako ne zelis mijenjati layer.")]
    [SerializeField] private string visualizationLayerName = "Visualization";

    private class ColumnInstance
    {
        public GameObject root;
        public Vector2Int cell;
    }

    private readonly Dictionary<Vector2Int, ColumnInstance> openColumns =
        new Dictionary<Vector2Int, ColumnInstance>();

    private readonly List<Vector2Int> columnOpenOrder =
        new List<Vector2Int>();

    private float refreshTimer = 0f;

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
        if (interactionEnabled && placeColumnAction != null && placeColumnAction.action.WasPressedThisFrame())
        {
            TryPlaceAtCursor();
        }

        if (openColumns.Count > 0)
        {
            refreshTimer += Time.deltaTime;

            if (refreshTimer >= refreshInterval)
            {
                refreshTimer = 0f;
                RebuildAllOpenColumns();
            }
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

        if (useInteractionBounds &&
            interactionBounds != null &&
            !interactionBounds.IsTemperatureCellAllowed(heatmap, cell.x, cell.y))
        {
            Debug.Log($"Space-Time stupac nije otvoren jer je ćelija izvan dopuštenog prostora: ({cell.x}, {cell.y})");
            return;
        }

        ToggleOrCreateColumn(cell.x, cell.y);
    }

    private void ToggleOrCreateColumn(int gridX, int gridY)
    {
        if (useInteractionBounds &&
            interactionBounds != null &&
            !interactionBounds.IsTemperatureCellAllowed(heatmap, gridX, gridY))
        {
            Debug.Log($"Space-Time stupac nije otvoren jer je ćelija izvan dopuštenog prostora: ({gridX}, {gridY})");
            return;
        }

        Vector2Int cell = new Vector2Int(gridX, gridY);

        if (openColumns.TryGetValue(cell, out ColumnInstance existingColumn))
        {
            if (toggleColumnOnSameCell)
            {
                CloseColumn(cell);
                Debug.Log($"Space-Time stupac uklonjen s ćelije ({gridX}, {gridY})");
            }

            return;
        }

        EnforceMaxOpenColumns();

        GameObject columnRoot = BuildColumn(gridX, gridY, true);

        ColumnInstance instance = new ColumnInstance
        {
            root = columnRoot,
            cell = cell
        };

        openColumns[cell] = instance;
        columnOpenOrder.Add(cell);

        Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);
        Vector3 soundPosition = new Vector3(cellCenter.x, cellCenter.y + cubeHeight * 0.5f, cellCenter.z);
        PlaySpawnSound(soundPosition);

        Debug.Log($"Space-Time stupac stvoren na ćeliji ({gridX}, {gridY}). Ukupno otvorenih: {openColumns.Count}");
    }

    private void EnforceMaxOpenColumns()
    {
        if (maxOpenColumns <= 0)
            return;

        while (openColumns.Count >= maxOpenColumns && columnOpenOrder.Count > 0)
        {
            Vector2Int oldestCell = columnOpenOrder[0];
            CloseColumn(oldestCell);
        }
    }

    private void CloseColumn(Vector2Int cell)
    {
        if (openColumns.TryGetValue(cell, out ColumnInstance instance))
        {
            if (instance.root != null)
            {
                Vector3 soundPosition = instance.root.transform.position + Vector3.up * (cubeHeight * 0.5f);
                PlayDespawnSound(soundPosition);
                StartColumnCloseAnimation(instance.root);
            }

            openColumns.Remove(cell);
        }

        columnOpenOrder.Remove(cell);
    }

    private void StartColumnCloseAnimation(GameObject columnRoot)
    {
        if (columnRoot == null)
            return;

        if (!animateColumnClose)
        {
            Destroy(columnRoot);
            return;
        }

        SpaceTimeColumnCloseAnimator closeAnimator = columnRoot.GetComponent<SpaceTimeColumnCloseAnimator>();

        if (closeAnimator == null)
            closeAnimator = columnRoot.AddComponent<SpaceTimeColumnCloseAnimator>();

        closeAnimator.Init(columnCloseDuration, fadeColumnOnClose);
    }

    private void RebuildAllOpenColumns()
    {
        if (openColumns.Count == 0)
            return;

        List<Vector2Int> cellsToRebuild = new List<Vector2Int>(openColumns.Keys);

        for (int i = 0; i < cellsToRebuild.Count; i++)
        {
            Vector2Int cell = cellsToRebuild[i];

            if (!openColumns.TryGetValue(cell, out ColumnInstance instance))
                continue;

            if (instance.root != null)
                Destroy(instance.root);

            GameObject rebuiltRoot = BuildColumn(cell.x, cell.y, false);
            instance.root = rebuiltRoot;
            openColumns[cell] = instance;
        }
    }

    private GameObject BuildColumn(int gridX, int gridY, bool playRiseAnimation)
    {
        if (visibleSeconds <= 0)
            visibleSeconds = 1;

        float cellW = heatmap.GetCellWidth();
        float cellD = heatmap.GetCellHeight();
        Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);

        GameObject columnRoot = new GameObject($"SpaceTimeColumnRoot_{gridX}_{gridY}");
        columnRoot.transform.position = cellCenter;
        SetVisualizationLayer(columnRoot);

        CreateMainWhiteColumn(columnRoot.transform, cellW, cubeHeight, cellD, playRiseAnimation);
        CreateFilledTimeSlices(columnRoot.transform, gridX, gridY, cellW, cubeHeight, cellD);

        if (showTimeLabels)
            CreateAdaptiveTimeLabels(columnRoot.transform, cellW);

        if (showDateLabel)
            CreateDateLabel(columnRoot.transform);

        return columnRoot;
    }

    private void CreateMainWhiteColumn(Transform parent, float cellW, float height, float cellD, bool playRiseAnimation)
    {
        GameObject mainColumn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainColumn.name = "MainWhiteColumn";
        SetVisualizationLayer(mainColumn);

        mainColumn.transform.SetParent(parent, false);
        mainColumn.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        mainColumn.transform.localScale = new Vector3(cellW, height, cellD);

        Collider col = mainColumn.GetComponent<Collider>();

        if (col != null)
            Destroy(col);

        Renderer renderer = mainColumn.GetComponent<Renderer>();

        if (cubeMaterial != null)
        {
            renderer.material = new Material(cubeMaterial);
        }
        else
        {
            Material fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallback.color = new Color(1f, 1f, 1f, 0.35f);
            renderer.material = fallback;
        }

        if (animateMainColumn)
        {
            ColumnAnimator animator = mainColumn.GetComponent<ColumnAnimator>();

            if (animator == null)
                animator = mainColumn.AddComponent<ColumnAnimator>();

            if (playRiseAnimation)
                animator.Init(cellW, height, cellD);
            else
                animator.SetInstantState(cellW, height, cellD);
        }
    }

    private void CreateFilledTimeSlices(Transform parent, int gridX, int gridY, float cellW, float height, float cellD)
    {
        List<ShaderGridHeatmap.CellTemperatureSample> history = heatmap.GetCellHistory(gridX, gridY);

        if (history == null || history.Count == 0)
            return;

        float currentTime = heatmap.GetRelativeSimulationTime();

        float sliceHeight = height / visibleSeconds;
        float filledSliceHeight = Mathf.Max(0.002f, sliceHeight - verticalBandGap);

        float sliceWidth = cellW + shellExpand;
        float sliceDepth = cellD + shellExpand;

        for (int secondIndex = 0; secondIndex < visibleSeconds; secondIndex++)
        {
            float bucketEnd = currentTime - secondIndex;
            float bucketStart = bucketEnd - 1f;

            if (bucketEnd < 0f)
                continue;

            bucketStart = Mathf.Max(0f, bucketStart);

            ShaderGridHeatmap.CellTemperatureSample latestSample =
                GetLatestSampleInRange(history, bucketStart, bucketEnd);

            if (latestSample == null)
                continue;

            float yCenter = (secondIndex * sliceHeight) + (sliceHeight * 0.5f);

            Color c = heatmap.GetColorForTemperature(latestSample.temperature);
            c.a = shellAlpha;

            CreateFilledBandPiece(
                parent,
                $"FilledSlice_{secondIndex}",
                new Vector3(0f, yCenter, 0f),
                new Vector3(sliceWidth, filledSliceHeight, sliceDepth),
                c,
                latestSample.temperature,
                latestSample.relativeTime,
                gridX,
                gridY,
                secondIndex,
                heatmap.GetDisplayedValueTitle(),
                heatmap.GetDisplayedValueUnit(),
                heatmap.GetDisplayedValueDecimals());
        }
    }

    private void CreateFilledBandPiece(
        Transform parent,
        string pieceName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        float temperature,
        float relativeTime,
        int gridX,
        int gridY,
        int secondIndex,
        string valueTitle,
        string valueUnit,
        int valueDecimals)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = pieceName;
        SetVisualizationLayer(piece);

        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;

        Collider col = piece.GetComponent<Collider>();

        if (col != null)
        {
            col.isTrigger = true;
        }

        SpaceTimeSliceData data = piece.AddComponent<SpaceTimeSliceData>();
        data.Init(temperature, relativeTime, gridX, gridY, secondIndex, valueTitle, valueUnit, valueDecimals);

        Renderer renderer = piece.GetComponent<Renderer>();
        renderer.material = CreateRuntimeBandMaterial(color);
    }

    private void CreateAdaptiveTimeLabels(Transform parent, float cellW)
    {
        float elapsed = Mathf.Max(0f, heatmap.GetRelativeSimulationTime());

        DateTime now = DateTime.Now;

        float xOffset = (cellW * 0.5f) + labelHorizontalOffset;
        float zOffset = 0f;

        if (elapsed < 30f)
        {
            CreateSingleTimeLabel(
                parent,
                "TimeLabel_NowOnly",
                FormatClockTime(now),
                new Vector3(xOffset, 0f + labelVerticalNudge, zOffset));

            return;
        }

        if (elapsed < visibleSeconds)
        {
            DateTime minus30 = now.AddSeconds(-30);

            CreateSingleTimeLabel(
                parent,
                "TimeLabel_Now",
                FormatClockTime(now),
                new Vector3(xOffset, 0f + labelVerticalNudge, zOffset));

            CreateSingleTimeLabel(
                parent,
                "TimeLabel_Minus30",
                FormatClockTime(minus30),
                new Vector3(xOffset, (cubeHeight * 0.5f) + labelVerticalNudge, zOffset));

            return;
        }

        DateTime nowFinal = now;
        DateTime minus30Final = now.AddSeconds(-(visibleSeconds * 0.5f));
        DateTime minus60Final = now.AddSeconds(-visibleSeconds);

        CreateSingleTimeLabel(
            parent,
            "TimeLabel_Bottom_Now",
            FormatClockTime(nowFinal),
            new Vector3(xOffset, 0f + labelVerticalNudge, zOffset));

        CreateSingleTimeLabel(
            parent,
            "TimeLabel_Middle_Minus30",
            FormatClockTime(minus30Final),
            new Vector3(xOffset, (cubeHeight * 0.5f) + labelVerticalNudge, zOffset));

        CreateSingleTimeLabel(
            parent,
            "TimeLabel_Top_Minus60",
            FormatClockTime(minus60Final),
            new Vector3(xOffset, cubeHeight + labelVerticalNudge, zOffset));
    }

    private void CreateDateLabel(Transform parent)
    {
        DateTime now = DateTime.Now;

        CreateSingleDateLabel(
            parent,
            "DateLabel",
            FormatDate(now),
            new Vector3(0f, cubeHeight + dateLabelHeightOffset, 0f));
    }

    private void CreateSingleTimeLabel(Transform parent, string objectName, string textValue, Vector3 localPosition)
    {
        GameObject labelObj = new GameObject(objectName);
        SetVisualizationLayer(labelObj);

        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = localPosition;
        labelObj.transform.localRotation = Quaternion.identity;
        labelObj.transform.localScale = Vector3.one;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = textValue;
        textMesh.fontSize = labelFontSize;
        textMesh.characterSize = labelCharacterSize;
        textMesh.anchor = TextAnchor.MiddleLeft;
        textMesh.alignment = TextAlignment.Left;
        textMesh.color = labelColor;

        if (labelFont != null)
        {
            textMesh.font = labelFont;

            MeshRenderer renderer = labelObj.GetComponent<MeshRenderer>();

            if (renderer != null && labelFont.material != null)
                renderer.material = labelFont.material;
        }

        labelObj.AddComponent<WorldLabelBillboard>();
    }

    private void CreateSingleDateLabel(Transform parent, string objectName, string textValue, Vector3 localPosition)
    {
        GameObject labelObj = new GameObject(objectName);
        SetVisualizationLayer(labelObj);

        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = localPosition;
        labelObj.transform.localRotation = Quaternion.identity;
        labelObj.transform.localScale = Vector3.one;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = textValue;
        textMesh.fontSize = dateLabelFontSize;
        textMesh.characterSize = dateLabelCharacterSize;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = dateLabelColor;

        if (labelFont != null)
        {
            textMesh.font = labelFont;

            MeshRenderer renderer = labelObj.GetComponent<MeshRenderer>();

            if (renderer != null && labelFont.material != null)
                renderer.material = labelFont.material;
        }

        labelObj.AddComponent<WorldLabelBillboard>();
    }

    private void SetVisualizationLayer(GameObject obj)
    {
        if (obj == null)
            return;

        if (string.IsNullOrWhiteSpace(visualizationLayerName))
            return;

        int layer = LayerMask.NameToLayer(visualizationLayerName);

        if (layer == -1)
            return;

        obj.layer = layer;
    }

    private string FormatClockTime(DateTime timeValue)
    {
        return timeValue.ToString("HH:mm:ss");
    }

    private string FormatDate(DateTime timeValue)
    {
        return timeValue.ToString("dd/MM/yyyy");
    }

    private Material CreateRuntimeBandMaterial(Color color)
    {
        Material mat;

        if (bandMaterial != null)
            mat = new Material(bandMaterial);
        else
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        mat.color = color;

        if (useEmissionForBands)
        {
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emissionIntensity);
            }
        }

        return mat;
    }

    private ShaderGridHeatmap.CellTemperatureSample GetLatestSampleInRange(
        List<ShaderGridHeatmap.CellTemperatureSample> history,
        float start,
        float end)
    {
        ShaderGridHeatmap.CellTemperatureSample result = null;

        for (int i = 0; i < history.Count; i++)
        {
            ShaderGridHeatmap.CellTemperatureSample sample = history[i];

            if (sample.relativeTime >= start && sample.relativeTime < end)
            {
                if (result == null || sample.relativeTime > result.relativeTime)
                    result = sample;
            }
        }

        return result;
    }

    private void PlaySpawnSound(Vector3 position)
    {
        if (spawnSound == null)
            return;

        if (audioSource != null)
            audioSource.PlayOneShot(spawnSound);
        else
            AudioSource.PlayClipAtPoint(spawnSound, position);
    }

    private void PlayDespawnSound(Vector3 position)
    {
        if (despawnSound == null)
            return;

        if (audioSource != null)
            audioSource.PlayOneShot(despawnSound);
        else
            AudioSource.PlayClipAtPoint(despawnSound, position);
    }
}


public class SpaceTimeColumnCloseAnimator : MonoBehaviour
{
    private float duration = 0.35f;
    private bool fadeRenderers = true;
    private float timer = 0f;

    private Vector3 startScale;
    private Renderer[] renderers;
    private Material[] materials;
    private Color[] originalColors;
    private Color[] originalBaseColors;
    private bool[] hasColorProperty;
    private bool[] hasBaseColorProperty;

    public void Init(float animationDuration, bool shouldFadeRenderers)
    {
        duration = Mathf.Max(0.05f, animationDuration);
        fadeRenderers = shouldFadeRenderers;
        timer = 0f;
        startScale = transform.localScale;

        // ColumnAnimator na bijelom stupcu pulsira scale. Za vrijeme zatvaranja ga gasimo,
        // jer sada cijeli root kontrolira elegantno spuštanje prema dolje.
        ColumnAnimator[] columnAnimators = GetComponentsInChildren<ColumnAnimator>(true);
        for (int i = 0; i < columnAnimators.Length; i++)
        {
            if (columnAnimators[i] != null)
                columnAnimators[i].enabled = false;
        }

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
        float eased = EaseInCubic(t);

        // Root je na podu ćelije, zato smanjivanje Y scalea izgleda kao spuštanje odozgo prema dolje.
        float yScale = Mathf.Lerp(startScale.y, 0.001f, eased);
        transform.localScale = new Vector3(startScale.x, yScale, startScale.z);

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

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }
}

