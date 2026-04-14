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

    [Header("Surface Time Bands")]
    [SerializeField] private Material bandMaterial;
    [SerializeField] private int visibleSeconds = 60;
    [SerializeField] private float refreshInterval = 1f;
    [SerializeField] private float shellExpand = 0.015f;
    [SerializeField] private float shellAlpha = 0.9f;
    [SerializeField] private float verticalBandGap = 0.004f;
    [SerializeField] private bool paintTopCap = false;

    [Header("Band Visual Tuning")]
    [SerializeField] private bool useEmissionForBands = true;
    [SerializeField] private float emissionIntensity = 1.2f;

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
        CreateSurfaceBands(activeColumnRoot.transform, gridX, gridY, cellW, cubeHeight, cellD);
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

    private void CreateSurfaceBands(Transform parent, int gridX, int gridY, float cellW, float height, float cellD)
    {
        List<ShaderGridHeatmap.CellTemperatureSample> history = heatmap.GetCellHistory(gridX, gridY);

        if (history == null || history.Count == 0)
            return;

        float currentTime = heatmap.GetRelativeSimulationTime();
        float oldestVisibleTime = Mathf.Max(0f, currentTime - visibleSeconds);

        float sliceHeight = height / visibleSeconds;
        float bandHeight = Mathf.Max(0.002f, sliceHeight - verticalBandGap);

        float faceThickness = Mathf.Max(0.004f, shellExpand);

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

            CreateBandOnAllSides(
                parent,
                secondIndex,
                yCenter,
                bandHeight,
                cellW,
                cellD,
                faceThickness,
                c);

            if (paintTopCap)
            {
                CreateTopCapBand(
                    parent,
                    secondIndex,
                    yCenter,
                    bandHeight,
                    cellW,
                    cellD,
                    faceThickness,
                    c);
            }
        }
    }

    private void CreateBandOnAllSides(
        Transform parent,
        int index,
        float yCenter,
        float bandHeight,
        float cellW,
        float cellD,
        float faceThickness,
        Color color)
    {
        float frontZ = (cellD * 0.5f) + (faceThickness * 0.5f);
        float sideX = (cellW * 0.5f) + (faceThickness * 0.5f);

        CreateBandPiece(
            parent,
            $"Band_{index}_Front",
            new Vector3(0f, yCenter, frontZ),
            new Vector3(cellW + faceThickness, bandHeight, faceThickness),
            color);

        CreateBandPiece(
            parent,
            $"Band_{index}_Back",
            new Vector3(0f, yCenter, -frontZ),
            new Vector3(cellW + faceThickness, bandHeight, faceThickness),
            color);

        CreateBandPiece(
            parent,
            $"Band_{index}_Left",
            new Vector3(-sideX, yCenter, 0f),
            new Vector3(faceThickness, bandHeight, cellD + faceThickness),
            color);

        CreateBandPiece(
            parent,
            $"Band_{index}_Right",
            new Vector3(sideX, yCenter, 0f),
            new Vector3(faceThickness, bandHeight, cellD + faceThickness),
            color);
    }

    private void CreateTopCapBand(
        Transform parent,
        int index,
        float yCenter,
        float bandHeight,
        float cellW,
        float cellD,
        float faceThickness,
        Color color)
    {
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = $"Band_{index}_TopCap";
        cap.transform.SetParent(parent, false);
        cap.transform.localPosition = new Vector3(0f, yCenter + (bandHeight * 0.5f) - (faceThickness * 0.5f), 0f);
        cap.transform.localScale = new Vector3(cellW + faceThickness, faceThickness, cellD + faceThickness);

        Collider col = cap.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer renderer = cap.GetComponent<Renderer>();
        renderer.material = CreateRuntimeBandMaterial(color);
    }

    private void CreateBandPiece(Transform parent, string pieceName, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = pieceName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;

        Collider col = piece.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer renderer = piece.GetComponent<Renderer>();
        renderer.material = CreateRuntimeBandMaterial(color);
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