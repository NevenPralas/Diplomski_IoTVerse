using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpaceTimeCubeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;
    [SerializeField] private GridCellCursor gridCellCursor;

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
    [SerializeField] private AudioSource audioSource;

    private GameObject activeColumnRoot = null;
    private Vector2Int activeCell = new Vector2Int(-1, -1);
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
        if (placeColumnAction != null && placeColumnAction.action.WasPressedThisFrame())
        {
            TryPlaceAtCursor();
        }

        if (activeColumnRoot != null)
        {
            refreshTimer += Time.deltaTime;

            if (refreshTimer >= refreshInterval)
            {
                refreshTimer = 0f;
                RebuildActiveColumn();
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
        PlaceColumn(cell.x, cell.y);
    }

    private void PlaceColumn(int gridX, int gridY)
    {
        Vector2Int newCell = new Vector2Int(gridX, gridY);

        if (activeCell == newCell && activeColumnRoot != null)
        {
            Destroy(activeColumnRoot);
            activeColumnRoot = null;
            activeCell = new Vector2Int(-1, -1);
            Debug.Log("Stupac uklonjen.");
            return;
        }

        if (activeColumnRoot != null)
        {
            Destroy(activeColumnRoot);
            activeColumnRoot = null;
        }

        activeCell = newCell;
        BuildColumn(gridX, gridY, true);
        refreshTimer = 0f;

        Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);
        Vector3 soundPosition = new Vector3(cellCenter.x, cellCenter.y + cubeHeight * 0.5f, cellCenter.z);
        PlaySpawnSound(soundPosition);

        Debug.Log($"Space-Time stupac stvoren na ćeliji ({gridX}, {gridY})");
    }

    private void RebuildActiveColumn()
    {
        if (activeCell.x < 0 || activeCell.y < 0)
            return;

        if (activeColumnRoot != null)
            Destroy(activeColumnRoot);

        BuildColumn(activeCell.x, activeCell.y, false);
    }

    private void BuildColumn(int gridX, int gridY, bool playRiseAnimation)
    {
        if (visibleSeconds <= 0)
            visibleSeconds = 1;

        float cellW = heatmap.GetCellWidth();
        float cellD = heatmap.GetCellHeight();
        Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);

        activeColumnRoot = new GameObject($"SpaceTimeColumnRoot_{gridX}_{gridY}");
        activeColumnRoot.transform.position = cellCenter;

        CreateMainWhiteColumn(activeColumnRoot.transform, cellW, cubeHeight, cellD, playRiseAnimation);
        CreateFilledTimeSlices(activeColumnRoot.transform, gridX, gridY, cellW, cubeHeight, cellD);

        if (showTimeLabels)
            CreateAdaptiveTimeLabels(activeColumnRoot.transform, cellW);

        if (showDateLabel)
            CreateDateLabel(activeColumnRoot.transform);
    }

    private void CreateMainWhiteColumn(Transform parent, float cellW, float height, float cellD, bool playRiseAnimation)
    {
        GameObject mainColumn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainColumn.name = "MainWhiteColumn";
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
        float oldestVisibleTime = Mathf.Max(0f, currentTime - visibleSeconds);

        float sliceHeight = height / visibleSeconds;
        float filledSliceHeight = Mathf.Max(0.002f, sliceHeight - verticalBandGap);

        float sliceWidth = cellW + shellExpand;
        float sliceDepth = cellD + shellExpand;

        for (int secondIndex = 0; secondIndex < visibleSeconds; secondIndex++)
        {
            float bucketStart = oldestVisibleTime + secondIndex;
            float bucketEnd = bucketStart + 1f;

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
                secondIndex);
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
    int secondIndex)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = pieceName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;

        Collider col = piece.GetComponent<Collider>();

        if (col != null)
        {
            col.isTrigger = true;
        }

        SpaceTimeSliceData data = piece.AddComponent<SpaceTimeSliceData>();
        data.Init(temperature, relativeTime, gridX, gridY, secondIndex);

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
            float currentY = Mathf.Lerp(0f, cubeHeight * 0.5f, elapsed / 30f) + labelVerticalNudge;

            CreateSingleTimeLabel(
                parent,
                "TimeLabel_CurrentOnly",
                FormatClockTime(now),
                new Vector3(xOffset, currentY, zOffset));

            return;
        }

        if (elapsed < visibleSeconds)
        {
            float progress = (elapsed - 30f) / (visibleSeconds - 30f);

            float lowerY = Mathf.Lerp(0f, cubeHeight * 0.5f, progress) + labelVerticalNudge;
            float upperY = Mathf.Lerp(cubeHeight * 0.5f, cubeHeight, progress) + labelVerticalNudge;

            DateTime minus30 = now.AddSeconds(-30);

            CreateSingleTimeLabel(
                parent,
                "TimeLabel_Minus30",
                FormatClockTime(minus30),
                new Vector3(xOffset, lowerY, zOffset));

            CreateSingleTimeLabel(
                parent,
                "TimeLabel_Now",
                FormatClockTime(now),
                new Vector3(xOffset, upperY, zOffset));

            return;
        }

        DateTime minus60Final = now.AddSeconds(-visibleSeconds);
        DateTime minus30Final = now.AddSeconds(-(visibleSeconds * 0.5f));
        DateTime nowFinal = now;

        CreateSingleTimeLabel(
            parent,
            "TimeLabel_Bottom",
            FormatClockTime(minus60Final),
            new Vector3(xOffset, 0f + labelVerticalNudge, zOffset));

        CreateSingleTimeLabel(
            parent,
            "TimeLabel_Middle",
            FormatClockTime(minus30Final),
            new Vector3(xOffset, (cubeHeight * 0.5f) + labelVerticalNudge, zOffset));

        CreateSingleTimeLabel(
            parent,
            "TimeLabel_Top",
            FormatClockTime(nowFinal),
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
}