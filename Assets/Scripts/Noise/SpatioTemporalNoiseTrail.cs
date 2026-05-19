using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SpatioTemporalNoiseTrail : MonoBehaviour
{
    [System.Serializable]
    private class NoiseSample
    {
        public Vector3 worldPosition;
        public float noiseDb;
        public float time;
    }

    public struct NoiseHoverInfo
    {
        public Vector3 worldPoint;
        public float noiseDb;
        public float ageSeconds;
        public DateTime clockTime;
    }

    [Header("References")]
    [SerializeField] private Material trailMaterial;

    [Header("Time Mapping")]
    [SerializeField] private float historySeconds = 60f;
    [SerializeField] private float timeHeight = 2.2f;
    [SerializeField] private float baseHeight = 0.3f;
    [SerializeField] private bool rollingTimeWindow = true;

    [Header("Noise Mapping")]
    [SerializeField] private float minNoiseDb = 30f;
    [SerializeField] private float maxNoiseDb = 85f;

    [Header("Tube Geometry")]
    [SerializeField] private float tubeRadius = 0.055f;

    [Range(3, 24)]
    [SerializeField] private int tubeSegments = 10;

    [Tooltip("Ako je uključeno, dodaje zatvorene capove. Za ljepši glatki kraj preporuka: false.")]
    [SerializeField] private bool capEnds = false;

    [Header("Smooth Trail Ends")]
    [SerializeField] private bool smoothEnds = true;

    [Tooltip("Koliko sekundi na početku i kraju vremenskog prozora se krivulja postepeno sužava.")]
    [SerializeField] private float endFadeSeconds = 4f;

    [Tooltip("Najmanji radijus na samom kraju krivulje. 0.05 znači skoro u špic.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumEndRadiusFactor = 0.05f;

    [Tooltip("Ako je uključeno, krajevi osim suženja postaju i prozirniji.")]
    [SerializeField] private bool fadeAlphaAtEnds = true;

    [Range(0f, 1f)]
    [SerializeField] private float minimumEndAlphaFactor = 0.1f;

    [Header("Sampling")]
    [SerializeField] private float minSampleInterval = 0.25f;
    [SerializeField] private float verticalNudge = 0.02f;

    [Header("Appearance")]
    [SerializeField] private bool useDefaultNoiseGradient = true;
    [SerializeField] private Gradient noiseGradient;
    [SerializeField] private bool rebuildEveryFrame = true;
    [SerializeField] private bool visible = true;

    [Header("Physics / Hover")]
    [SerializeField] private bool createHoverMeshCollider = true;

    [Tooltip("Layer za noise trail. Stavi Visualization.")]
    [SerializeField] private string visualizationLayerName = "Visualization";

    [Tooltip("Layer avatara/igrača. Noise trail neće fizički kolajdati s ovim layerom.")]
    [SerializeField] private string playerLayerName = "Player";

    [Tooltip("Layer robota. Noise trail neće fizički kolajdati s ovim layerom.")]
    [SerializeField] private string robotLayerName = "Robot";

    [SerializeField] private bool ignorePlayerAndRobotCollisions = true;

    [Header("Time Labels")]
    [SerializeField] private bool showTimeLabels = true;
    [SerializeField] private bool showDateLabel = true;
    [SerializeField] private bool showTimeAxis = true;
    [SerializeField] private float labelHorizontalOffset = 0.35f;
    [SerializeField] private float labelDepthOffset = 0.0f;
    [SerializeField] private float labelVerticalNudge = 0.0f;
    [SerializeField] private float axisOffsetFromLabels = 0.08f;
    [SerializeField] private int labelFontSize = 48;
    [SerializeField] private float labelCharacterSize = 0.022f;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private Font labelFont;

    [Header("Date Label")]
    [SerializeField] private float dateLabelHeightOffset = 0.18f;
    [SerializeField] private int dateLabelFontSize = 52;
    [SerializeField] private float dateLabelCharacterSize = 0.024f;
    [SerializeField] private Color dateLabelColor = Color.white;

    [Header("Time Axis Visual")]
    [SerializeField] private Color timeAxisColor = Color.white;
    [SerializeField] private float timeAxisWidth = 0.01f;
    [SerializeField] private float timeTickRadius = 0.035f;

    [Header("Debug")]
    [SerializeField] private bool logSamples = false;

    private readonly List<NoiseSample> samples = new List<NoiseSample>();

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private float lastSampleTime = -999f;

    private GameObject labelRoot;

    private GameObject nowLabelObject;
    private GameObject minus30LabelObject;
    private GameObject minus60LabelObject;
    private GameObject dateLabelObject;

    private TextMesh nowLabelText;
    private TextMesh minus30LabelText;
    private TextMesh minus60LabelText;
    private TextMesh dateLabelText;

    private GameObject timeAxisObject;
    private LineRenderer timeAxisLine;

    private GameObject bottomTickObject;
    private GameObject middleTickObject;
    private GameObject topTickObject;

    private Material timeAxisMaterial;

    private void Reset()
    {
        noiseGradient = CreateDefaultNoiseGradient();
    }

    private void OnValidate()
    {
        historySeconds = Mathf.Max(1f, historySeconds);
        timeHeight = Mathf.Max(0.1f, timeHeight);
        minSampleInterval = Mathf.Max(0.05f, minSampleInterval);
        tubeRadius = Mathf.Max(0.005f, tubeRadius);
        tubeSegments = Mathf.Clamp(tubeSegments, 3, 24);
        timeAxisWidth = Mathf.Max(0.001f, timeAxisWidth);
        timeTickRadius = Mathf.Max(0.005f, timeTickRadius);
        endFadeSeconds = Mathf.Max(0.1f, endFadeSeconds);

        if (useDefaultNoiseGradient)
            noiseGradient = CreateDefaultNoiseGradient();
    }

    private void Awake()
    {
        SetVisualizationLayer(gameObject);

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh();
        mesh.name = "SpatioTemporalNoiseTrailTubeMesh";
        mesh.MarkDynamic();

        meshFilter.sharedMesh = mesh;

        if (createHoverMeshCollider)
        {
            meshCollider = GetComponent<MeshCollider>();

            if (meshCollider == null)
                meshCollider = gameObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;

            ApplyColliderCollisionFilters();
        }

        if (trailMaterial != null)
            meshRenderer.sharedMaterial = trailMaterial;

        if (useDefaultNoiseGradient || noiseGradient == null)
            noiseGradient = CreateDefaultNoiseGradient();

        meshRenderer.enabled = visible;

        CreateLabelRootIfNeeded();
        CreateTimeAxisMaterialIfNeeded();
    }

    private void Update()
    {
        RemoveOldSamples();

        if (rollingTimeWindow && rebuildEveryFrame)
            RebuildMesh();

        UpdateLabelsAndAxis();
    }

    public void AddSample(Vector3 worldPosition, float noiseDb)
    {
        if (Time.time - lastSampleTime < minSampleInterval)
            return;

        lastSampleTime = Time.time;

        NoiseSample sample = new NoiseSample
        {
            worldPosition = worldPosition,
            noiseDb = noiseDb,
            time = Time.time
        };

        samples.Add(sample);

        if (logSamples)
            Debug.Log($"NoiseTrail sample | pos={worldPosition}, noise={noiseDb:F1} dBA, count={samples.Count}");

        RemoveOldSamples();
        RebuildMesh();
        UpdateLabelsAndAxis();
    }

    public void ClearTrail()
    {
        samples.Clear();

        if (mesh != null)
            mesh.Clear();

        RefreshMeshCollider();
        SetLabelObjectsVisible(false);
    }

    public void SetVisible(bool isVisible)
    {
        visible = isVisible;

        if (meshRenderer != null)
            meshRenderer.enabled = visible;

        if (meshCollider != null)
            meshCollider.enabled = visible;

        SetLabelObjectsVisible(visible);
    }

    public bool IsVisible()
    {
        return visible;
    }

    public bool TryGetClosestHoverInfo(Vector3 worldPoint, out NoiseHoverInfo info)
    {
        info = default;

        if (samples.Count == 0)
            return false;

        NoiseSample closestSample = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 samplePoint = GetSpatioTemporalPoint(samples[i]);
            float distanceSqr = (samplePoint - worldPoint).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                closestSample = samples[i];
            }
        }

        if (closestSample == null)
            return false;

        float age = Mathf.Clamp(Time.time - closestSample.time, 0f, historySeconds);

        info = new NoiseHoverInfo
        {
            worldPoint = GetSpatioTemporalPoint(closestSample),
            noiseDb = closestSample.noiseDb,
            ageSeconds = age,
            clockTime = DateTime.Now.AddSeconds(-age)
        };

        return true;
    }

    private void RemoveOldSamples()
    {
        float now = Time.time;

        for (int i = samples.Count - 1; i >= 0; i--)
        {
            float age = now - samples[i].time;

            if (age > historySeconds)
                samples.RemoveAt(i);
        }
    }

    private void RebuildMesh()
    {
        if (mesh == null)
            return;

        mesh.Clear();

        if (samples.Count < 2)
        {
            RefreshMeshCollider();
            return;
        }

        int ringCount = samples.Count;
        int verticesPerRing = tubeSegments;

        int tubeVertexCount = ringCount * verticesPerRing;
        int capVertexCount = capEnds ? 2 : 0;
        int totalVertexCount = tubeVertexCount + capVertexCount;

        int sideTriangleCount = (ringCount - 1) * tubeSegments * 2;
        int capTriangleCount = capEnds ? tubeSegments * 2 : 0;
        int totalTriangleIndexCount = (sideTriangleCount + capTriangleCount) * 3;

        Vector3[] vertices = new Vector3[totalVertexCount];
        Color[] colors = new Color[totalVertexCount];
        int[] triangles = new int[totalTriangleIndexCount];

        Vector3[] points = new Vector3[ringCount];
        float[] radiusFactors = new float[ringCount];
        Color[] sampleColors = new Color[ringCount];

        for (int i = 0; i < ringCount; i++)
        {
            points[i] = GetSpatioTemporalPoint(samples[i]);

            float endFactor = GetSmoothEndFactor(samples[i]);

            radiusFactors[i] = Mathf.Lerp(minimumEndRadiusFactor, 1f, endFactor);

            Color c = GetNoiseColor(samples[i].noiseDb);

            if (fadeAlphaAtEnds)
                c.a *= Mathf.Lerp(minimumEndAlphaFactor, 1f, endFactor);

            sampleColors[i] = c;
        }

        for (int i = 0; i < ringCount; i++)
        {
            Vector3 tangent = GetTubeTangent(points, i);
            BuildRingBasis(tangent, out Vector3 normal, out Vector3 binormal);

            float ringRadius = tubeRadius * radiusFactors[i];

            for (int s = 0; s < tubeSegments; s++)
            {
                float angle = (Mathf.PI * 2f * s) / tubeSegments;

                Vector3 offset =
                    normal * Mathf.Cos(angle) * ringRadius +
                    binormal * Mathf.Sin(angle) * ringRadius;

                int vertexIndex = i * tubeSegments + s;

                vertices[vertexIndex] = transform.InverseTransformPoint(points[i] + offset);
                colors[vertexIndex] = sampleColors[i];
            }
        }

        int triangleCursor = 0;

        for (int i = 0; i < ringCount - 1; i++)
        {
            int currentRing = i * tubeSegments;
            int nextRing = (i + 1) * tubeSegments;

            for (int s = 0; s < tubeSegments; s++)
            {
                int sNext = (s + 1) % tubeSegments;

                int a = currentRing + s;
                int b = currentRing + sNext;
                int c = nextRing + s;
                int d = nextRing + sNext;

                triangles[triangleCursor++] = a;
                triangles[triangleCursor++] = c;
                triangles[triangleCursor++] = b;

                triangles[triangleCursor++] = b;
                triangles[triangleCursor++] = c;
                triangles[triangleCursor++] = d;
            }
        }

        if (capEnds)
        {
            int startCenterIndex = tubeVertexCount;
            int endCenterIndex = tubeVertexCount + 1;

            vertices[startCenterIndex] = transform.InverseTransformPoint(points[0]);
            vertices[endCenterIndex] = transform.InverseTransformPoint(points[ringCount - 1]);

            colors[startCenterIndex] = sampleColors[0];
            colors[endCenterIndex] = sampleColors[ringCount - 1];

            for (int s = 0; s < tubeSegments; s++)
            {
                int sNext = (s + 1) % tubeSegments;

                triangles[triangleCursor++] = startCenterIndex;
                triangles[triangleCursor++] = sNext;
                triangles[triangleCursor++] = s;
            }

            int endRingStart = (ringCount - 1) * tubeSegments;

            for (int s = 0; s < tubeSegments; s++)
            {
                int sNext = (s + 1) % tubeSegments;

                triangles[triangleCursor++] = endCenterIndex;
                triangles[triangleCursor++] = endRingStart + s;
                triangles[triangleCursor++] = endRingStart + sNext;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        RefreshMeshCollider();
    }

    private float GetSmoothEndFactor(NoiseSample sample)
    {
        if (!smoothEnds)
            return 1f;

        float age = Mathf.Clamp(Time.time - sample.time, 0f, historySeconds);

        float newestFactor = Mathf.Clamp01(age / endFadeSeconds);
        float oldestFactor = Mathf.Clamp01((historySeconds - age) / endFadeSeconds);

        float factor = Mathf.Min(newestFactor, oldestFactor);

        return SmoothStep01(factor);
    }

    private float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void RefreshMeshCollider()
    {
        if (meshCollider == null)
            return;

        meshCollider.sharedMesh = null;

        if (mesh != null && samples.Count >= 2)
            meshCollider.sharedMesh = mesh;

        ApplyColliderCollisionFilters();
    }

    private void ApplyColliderCollisionFilters()
    {
        if (meshCollider == null)
            return;

        if (!ignorePlayerAndRobotCollisions)
            return;

        LayerMask excludeMask = 0;

        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        int robotLayer = LayerMask.NameToLayer(robotLayerName);

        if (playerLayer != -1)
            excludeMask |= 1 << playerLayer;

        if (robotLayer != -1)
            excludeMask |= 1 << robotLayer;

        meshCollider.excludeLayers |= excludeMask;
    }

    private Vector3 GetSpatioTemporalPoint(NoiseSample sample)
    {
        float age = Time.time - sample.time;
        float normalizedAge = Mathf.Clamp01(age / historySeconds);

        float y = baseHeight + verticalNudge + normalizedAge * timeHeight;

        Vector3 p = sample.worldPosition;
        p.y = y;

        return p;
    }

    private Vector3 GetTubeTangent(Vector3[] points, int index)
    {
        Vector3 tangent;

        if (points.Length < 2)
            tangent = Vector3.forward;
        else if (index == 0)
            tangent = points[1] - points[0];
        else if (index == points.Length - 1)
            tangent = points[index] - points[index - 1];
        else
            tangent = points[index + 1] - points[index - 1];

        if (tangent.sqrMagnitude < 0.000001f)
            tangent = Vector3.up;

        return tangent.normalized;
    }

    private void BuildRingBasis(Vector3 tangent, out Vector3 normal, out Vector3 binormal)
    {
        Vector3 reference = Vector3.up;

        if (Mathf.Abs(Vector3.Dot(tangent, reference)) > 0.92f)
            reference = Vector3.right;

        normal = Vector3.Cross(tangent, reference).normalized;

        if (normal.sqrMagnitude < 0.000001f)
            normal = Vector3.forward;

        binormal = Vector3.Cross(tangent, normal).normalized;
    }

    private float NormalizeNoise(float noiseDb)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(minNoiseDb, maxNoiseDb, noiseDb));
    }

    private Color GetNoiseColor(float noiseDb)
    {
        float t = NormalizeNoise(noiseDb);

        Gradient gradientToUse = noiseGradient;

        if (useDefaultNoiseGradient || gradientToUse == null)
            gradientToUse = CreateDefaultNoiseGradient();

        Color c = gradientToUse.Evaluate(t);
        c.a = Mathf.Lerp(0.65f, 1.00f, t);

        return c;
    }

    private Gradient CreateDefaultNoiseGradient()
    {
        Gradient g = new Gradient();

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(new Color(0.00f, 0.65f, 1.00f), 0.00f),
            new GradientColorKey(new Color(0.25f, 0.00f, 1.00f), 0.25f),
            new GradientColorKey(new Color(0.90f, 0.00f, 1.00f), 0.50f),
            new GradientColorKey(new Color(1.00f, 0.65f, 0.00f), 0.75f),
            new GradientColorKey(new Color(1.00f, 0.00f, 0.05f), 1.00f)
        };

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(0.65f, 0.00f),
            new GradientAlphaKey(0.85f, 0.50f),
            new GradientAlphaKey(1.00f, 1.00f)
        };

        g.SetKeys(colorKeys, alphaKeys);

        return g;
    }

    private void CreateLabelRootIfNeeded()
    {
        if (labelRoot != null)
            return;

        labelRoot = new GameObject("NoiseTrailLabels");
        labelRoot.transform.SetParent(transform, true);
        SetVisualizationLayer(labelRoot);
    }

    private void CreateTimeAxisMaterialIfNeeded()
    {
        if (timeAxisMaterial != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        timeAxisMaterial = new Material(shader);
        timeAxisMaterial.color = timeAxisColor;
    }

    private void UpdateLabelsAndAxis()
    {
        CreateLabelRootIfNeeded();

        bool shouldShowAnything = visible && samples.Count > 0 && (showTimeLabels || showDateLabel || showTimeAxis);

        SetLabelObjectsVisible(shouldShowAnything);

        if (!shouldShowAnything)
            return;

        CalculateLabelPositions(
            out Vector3 bottomPos,
            out Vector3 middlePos,
            out Vector3 topPos,
            out Vector3 datePos,
            out Vector3 axisBottomPos,
            out Vector3 axisMiddlePos,
            out Vector3 axisTopPos
        );

        DateTime now = DateTime.Now;
        DateTime minus30 = now.AddSeconds(-30);
        DateTime minus60 = now.AddSeconds(-historySeconds);

        if (showTimeLabels)
        {
            CreateOrUpdateTextLabel(ref nowLabelObject, ref nowLabelText, "NoiseTimeLabel_Now",
                FormatClockTime(now), bottomPos, labelFontSize, labelCharacterSize, labelColor, TextAnchor.MiddleLeft);

            CreateOrUpdateTextLabel(ref minus30LabelObject, ref minus30LabelText, "NoiseTimeLabel_Minus30",
                FormatClockTime(minus30), middlePos, labelFontSize, labelCharacterSize, labelColor, TextAnchor.MiddleLeft);

            CreateOrUpdateTextLabel(ref minus60LabelObject, ref minus60LabelText, "NoiseTimeLabel_Minus60",
                FormatClockTime(minus60), topPos, labelFontSize, labelCharacterSize, labelColor, TextAnchor.MiddleLeft);
        }

        if (showDateLabel)
        {
            CreateOrUpdateTextLabel(ref dateLabelObject, ref dateLabelText, "NoiseDateLabel",
                FormatDate(now), datePos, dateLabelFontSize, dateLabelCharacterSize, dateLabelColor, TextAnchor.MiddleCenter);
        }

        if (showTimeAxis)
            UpdateTimeAxis(axisBottomPos, axisMiddlePos, axisTopPos);
    }

    private void CalculateLabelPositions(
        out Vector3 bottomPos,
        out Vector3 middlePos,
        out Vector3 topPos,
        out Vector3 datePos,
        out Vector3 axisBottomPos,
        out Vector3 axisMiddlePos,
        out Vector3 axisTopPos)
    {
        float maxX = transform.position.x;
        float zSum = 0f;
        int count = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 p = GetSpatioTemporalPoint(samples[i]);

            if (i == 0)
                maxX = p.x;
            else
                maxX = Mathf.Max(maxX, p.x);

            zSum += p.z;
            count++;
        }

        float averageZ = count > 0 ? zSum / count : transform.position.z;

        float labelX = maxX + labelHorizontalOffset;
        float labelZ = averageZ + labelDepthOffset;

        float bottomY = baseHeight + verticalNudge + labelVerticalNudge;
        float middleY = baseHeight + verticalNudge + (timeHeight * 0.5f) + labelVerticalNudge;
        float topY = baseHeight + verticalNudge + timeHeight + labelVerticalNudge;

        bottomPos = new Vector3(labelX, bottomY, labelZ);
        middlePos = new Vector3(labelX, middleY, labelZ);
        topPos = new Vector3(labelX, topY, labelZ);

        datePos = new Vector3(labelX, topY + dateLabelHeightOffset, labelZ);

        float axisX = labelX - axisOffsetFromLabels;

        axisBottomPos = new Vector3(axisX, bottomY, labelZ);
        axisMiddlePos = new Vector3(axisX, middleY, labelZ);
        axisTopPos = new Vector3(axisX, topY, labelZ);
    }

    private void CreateOrUpdateTextLabel(
        ref GameObject labelObject,
        ref TextMesh textMesh,
        string objectName,
        string textValue,
        Vector3 worldPosition,
        int fontSize,
        float characterSize,
        Color color,
        TextAnchor anchor)
    {
        if (labelObject == null)
        {
            labelObject = new GameObject(objectName);
            labelObject.transform.SetParent(labelRoot.transform, true);
            SetVisualizationLayer(labelObject);

            textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.anchor = anchor;
            textMesh.alignment = TextAlignment.Left;

            labelObject.AddComponent<WorldLabelBillboard>();
        }

        labelObject.SetActive(visible);

        labelObject.transform.position = worldPosition;
        labelObject.transform.localScale = Vector3.one;

        textMesh.text = textValue;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = characterSize;
        textMesh.color = color;
        textMesh.anchor = anchor;

        if (labelFont != null)
        {
            textMesh.font = labelFont;

            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();

            if (renderer != null && labelFont.material != null)
                renderer.material = labelFont.material;
        }
    }

    private void UpdateTimeAxis(Vector3 bottomPos, Vector3 middlePos, Vector3 topPos)
    {
        CreateTimeAxisMaterialIfNeeded();

        if (timeAxisObject == null)
        {
            timeAxisObject = new GameObject("NoiseTimeAxis");
            timeAxisObject.transform.SetParent(labelRoot.transform, true);
            SetVisualizationLayer(timeAxisObject);

            timeAxisLine = timeAxisObject.AddComponent<LineRenderer>();
            timeAxisLine.useWorldSpace = true;
            timeAxisLine.positionCount = 2;
            timeAxisLine.material = timeAxisMaterial;
            timeAxisLine.startWidth = timeAxisWidth;
            timeAxisLine.endWidth = timeAxisWidth;
            timeAxisLine.numCapVertices = 4;
            timeAxisLine.numCornerVertices = 4;
        }

        timeAxisObject.SetActive(visible && showTimeAxis);

        timeAxisLine.material = timeAxisMaterial;
        timeAxisLine.startColor = timeAxisColor;
        timeAxisLine.endColor = timeAxisColor;
        timeAxisLine.startWidth = timeAxisWidth;
        timeAxisLine.endWidth = timeAxisWidth;

        timeAxisLine.SetPosition(0, bottomPos);
        timeAxisLine.SetPosition(1, topPos);

        CreateOrUpdateTick(ref bottomTickObject, "NoiseTimeAxisTick_Now", bottomPos);
        CreateOrUpdateTick(ref middleTickObject, "NoiseTimeAxisTick_Minus30", middlePos);
        CreateOrUpdateTick(ref topTickObject, "NoiseTimeAxisTick_Minus60", topPos);
    }

    private void CreateOrUpdateTick(ref GameObject tickObject, string objectName, Vector3 worldPosition)
    {
        if (tickObject == null)
        {
            tickObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tickObject.name = objectName;
            tickObject.transform.SetParent(labelRoot.transform, true);
            SetVisualizationLayer(tickObject);

            Collider col = tickObject.GetComponent<Collider>();

            if (col != null)
                Destroy(col);

            Renderer renderer = tickObject.GetComponent<Renderer>();

            if (renderer != null)
                renderer.material = timeAxisMaterial;
        }

        tickObject.SetActive(visible && showTimeAxis);
        tickObject.transform.position = worldPosition;
        tickObject.transform.localScale = Vector3.one * timeTickRadius;
    }

    private void SetLabelObjectsVisible(bool isVisible)
    {
        if (labelRoot != null)
            labelRoot.SetActive(isVisible);

        if (nowLabelObject != null)
            nowLabelObject.SetActive(isVisible && showTimeLabels);

        if (minus30LabelObject != null)
            minus30LabelObject.SetActive(isVisible && showTimeLabels);

        if (minus60LabelObject != null)
            minus60LabelObject.SetActive(isVisible && showTimeLabels);

        if (dateLabelObject != null)
            dateLabelObject.SetActive(isVisible && showDateLabel);

        if (timeAxisObject != null)
            timeAxisObject.SetActive(isVisible && showTimeAxis);

        if (bottomTickObject != null)
            bottomTickObject.SetActive(isVisible && showTimeAxis);

        if (middleTickObject != null)
            middleTickObject.SetActive(isVisible && showTimeAxis);

        if (topTickObject != null)
            topTickObject.SetActive(isVisible && showTimeAxis);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 baseCenter = transform.position + Vector3.up * baseHeight;
        Vector3 topCenter = transform.position + Vector3.up * (baseHeight + timeHeight);

        Gizmos.DrawLine(baseCenter, topCenter);
        Gizmos.DrawWireSphere(baseCenter, tubeRadius);
        Gizmos.DrawWireSphere(topCenter, tubeRadius);
    }
}