using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

    private class VisualNoisePoint
    {
        public Vector3 worldPoint;
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

    [Range(6, 32)]
    [SerializeField] private int tubeSegments = 20;

    [Tooltip("Za lijep kraj ostavi uključeno. Disk je jako mali jer se cijev sužava prema kraju.")]
    [SerializeField] private bool capEnds = true;

    [Header("Visual Smoothing")]
    [SerializeField] private bool useVisualSmoothing = true;

    [Tooltip("Koliko interpoliranih točaka ide između dva stvarna samplea.")]
    [Range(1, 32)]
    [SerializeField] private int interpolationStepsPerSegment = 12;

    [Tooltip("Ovo ostavi false. Kad je true, može stvoriti čudan vertikalni/horizontalni završetak ako podaci kasne.")]
    [SerializeField] private bool addLiveHoldPoint = false;

    [Tooltip("Ako su sampleovi udaljeni, automatski dodaje još točaka.")]
    [SerializeField] private float maxVisualSegmentLength = 0.04f;

    [Tooltip("Dodatno omekšavanje putanje. Ne radi overshoot, za razliku od CatmullRom krivulje.")]
    [Range(0, 4)]
    [SerializeField] private int chaikinSmoothingIterations = 2;

    [Header("Smooth Trail Ends")]
    [SerializeField] private bool smoothEnds = true;

    [Tooltip("Koliko sekundi od kraja cijev postepeno postaje tanja.")]
    [SerializeField] private float endFadeSeconds = 3f;

    [Range(0f, 1f)]
    [SerializeField] private float minimumEndRadiusFactor = 0.03f;

    [SerializeField] private bool fadeAlphaAtEnds = true;

    [Range(0f, 1f)]
    [SerializeField] private float minimumEndAlphaFactor = 0.15f;

    [Header("Sampling")]
    [SerializeField] private float minSampleInterval = 0.15f;
    [SerializeField] private float verticalNudge = 0.02f;

    [Header("Appearance")]
    [SerializeField] private bool useDefaultNoiseGradient = true;
    [SerializeField] private Gradient noiseGradient;
    [SerializeField] private bool rebuildEveryFrame = true;
    [SerializeField] private bool visible = true;

    [Header("Visibility Animation")]
    [Tooltip("Kad se vizualizacija vlage pali/gasi preko switchera, trail se lagano pojavljuje/nestaje umjesto instantnog enable/disable.")]
    [SerializeField] private bool animateVisibilityChanges = true;

    [SerializeField] private float visibilityFadeDuration = 0.35f;

    [Tooltip("Tijekom pojavljivanja trail je malo tanji pa djeluje kao da izrasta iz putanje.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float visibilityStartRadiusFactor = 0.35f;

    [Tooltip("Ako je uključeno, collider traila se gasi odmah kad vizualizacija nestaje, da se ne može hoverati po nevidljivom objektu.")]
    [SerializeField] private bool disableColliderWhileHidden = true;

    [Header("Physics / Hover")]
    [SerializeField] private bool createHoverMeshCollider = true;

    [SerializeField] private string visualizationLayerName = "Visualization";
    [SerializeField] private string playerLayerName = "Avatar";
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

    private string externalGradientSignature = "";

    private float visibilityAnimationValue = 1f;
    private float visibilityAnimationTarget = 1f;
    private float lastVisibilityAnimationValue = -1f;

    private void Reset()
    {
        noiseGradient = CreateDefaultNoiseGradient();
    }

    private void OnValidate()
    {
        historySeconds = Mathf.Max(1f, historySeconds);
        timeHeight = Mathf.Max(0.1f, timeHeight);
        minSampleInterval = Mathf.Max(0.02f, minSampleInterval);
        tubeRadius = Mathf.Max(0.005f, tubeRadius);
        tubeSegments = Mathf.Clamp(tubeSegments, 6, 32);
        timeAxisWidth = Mathf.Max(0.001f, timeAxisWidth);
        timeTickRadius = Mathf.Max(0.005f, timeTickRadius);
        endFadeSeconds = Mathf.Max(0.1f, endFadeSeconds);
        maxVisualSegmentLength = Mathf.Max(0.01f, maxVisualSegmentLength);
        visibilityFadeDuration = Mathf.Max(0.01f, visibilityFadeDuration);

        minNoiseDb = Mathf.Min(minNoiseDb, maxNoiseDb - 0.01f);
        maxNoiseDb = Mathf.Max(maxNoiseDb, minNoiseDb + 0.01f);

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
        mesh.indexFormat = IndexFormat.UInt32;
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

        visibilityAnimationValue = visible ? 1f : 0f;
        visibilityAnimationTarget = visibilityAnimationValue;
        lastVisibilityAnimationValue = visibilityAnimationValue;

        meshRenderer.enabled = visible;

        if (meshCollider != null)
            meshCollider.enabled = visible || !disableColliderWhileHidden;

        CreateLabelRootIfNeeded();
        CreateTimeAxisMaterialIfNeeded();
    }

    private void Update()
    {
        RemoveOldSamples();
        bool visibilityChangedThisFrame = UpdateVisibilityAnimation();

        if (rollingTimeWindow && rebuildEveryFrame)
            RebuildMesh();
        else if (visibilityChangedThisFrame)
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
            Debug.Log($"NoiseTrail sample | pos={worldPosition}, value={noiseDb:F1}, count={samples.Count}");

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
        visibilityAnimationTarget = visible ? 1f : 0f;

        if (!animateVisibilityChanges)
        {
            visibilityAnimationValue = visibilityAnimationTarget;
            lastVisibilityAnimationValue = visibilityAnimationValue;
        }

        if (meshRenderer != null && visible)
            meshRenderer.enabled = true;

        if (meshCollider != null)
            meshCollider.enabled = visible || !disableColliderWhileHidden;

        if (!visible && !animateVisibilityChanges)
        {
            if (meshRenderer != null)
                meshRenderer.enabled = false;

            SetLabelObjectsVisible(false);
        }
        else
        {
            SetLabelObjectsVisible(IsVisuallyVisible());
        }

        RebuildMesh();
    }

    public bool IsVisible()
    {
        return visible;
    }

    public bool TryGetClosestHoverInfo(Vector3 worldPoint, out NoiseHoverInfo info)
    {
        info = default;

        if (samples.Count == 0 || !visible || visibilityAnimationValue <= 0.05f)
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

    public void ApplyExternalValueGradient(
        float minValue,
        float maxValue,
        Color lowColor,
        Color middleColor,
        Color highColor,
        bool rebuildImmediately = true)
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
        noiseGradient = CreateThreeColorValueGradient(lowColor, middleColor, highColor);

        if (rebuildImmediately)
            RebuildMesh();
    }

    private bool UpdateVisibilityAnimation()
    {
        float previous = visibilityAnimationValue;

        if (!animateVisibilityChanges)
        {
            visibilityAnimationValue = visibilityAnimationTarget;
        }
        else
        {
            float speed = 1f / Mathf.Max(0.01f, visibilityFadeDuration);
            visibilityAnimationValue = Mathf.MoveTowards(
                visibilityAnimationValue,
                visibilityAnimationTarget,
                Time.deltaTime * speed
            );
        }

        if (meshRenderer != null)
        {
            if (visibilityAnimationValue > 0.001f)
                meshRenderer.enabled = true;
            else if (!visible)
                meshRenderer.enabled = false;
        }

        if (meshCollider != null && disableColliderWhileHidden)
            meshCollider.enabled = visible && visibilityAnimationValue > 0.95f;

        bool changed = Mathf.Abs(previous - visibilityAnimationValue) > 0.0005f ||
                       Mathf.Abs(lastVisibilityAnimationValue - visibilityAnimationValue) > 0.0005f;

        lastVisibilityAnimationValue = visibilityAnimationValue;
        return changed;
    }

    private bool IsVisuallyVisible()
    {
        return visibilityAnimationValue > 0.01f;
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

        List<VisualNoisePoint> visualPoints = BuildVisualPoints();

        if (visualPoints.Count < 2)
        {
            RefreshMeshCollider();
            return;
        }

        int ringCount = visualPoints.Count;
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
        Vector3[] tangents = new Vector3[ringCount];
        Vector3[] normals = new Vector3[ringCount];
        Vector3[] binormals = new Vector3[ringCount];

        for (int i = 0; i < ringCount; i++)
            points[i] = visualPoints[i].worldPoint;

        BuildParallelTransportFrames(points, tangents, normals, binormals);

        Color[] ringColors = new Color[ringCount];

        for (int i = 0; i < ringCount; i++)
        {
            float endFactor = GetSmoothEndFactor(visualPoints[i].time);
            float visibilityRadiusFactor = Mathf.Lerp(visibilityStartRadiusFactor, 1f, visibilityAnimationValue);
            float ringRadius = tubeRadius * Mathf.Lerp(minimumEndRadiusFactor, 1f, endFactor) * visibilityRadiusFactor;

            Color sampleColor = GetNoiseColor(visualPoints[i].noiseDb);

            if (fadeAlphaAtEnds)
                sampleColor.a *= Mathf.Lerp(minimumEndAlphaFactor, 1f, endFactor);

            sampleColor.a *= visibilityAnimationValue;

            ringColors[i] = sampleColor;

            for (int s = 0; s < tubeSegments; s++)
            {
                float angle = (Mathf.PI * 2f * s) / tubeSegments;

                Vector3 offset =
                    normals[i] * Mathf.Cos(angle) * ringRadius +
                    binormals[i] * Mathf.Sin(angle) * ringRadius;

                int vertexIndex = i * tubeSegments + s;

                vertices[vertexIndex] = transform.InverseTransformPoint(points[i] + offset);
                colors[vertexIndex] = sampleColor;
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

            colors[startCenterIndex] = ringColors[0];
            colors[endCenterIndex] = ringColors[ringCount - 1];

            for (int s = 0; s < tubeSegments; s++)
            {
                int sNext = (s + 1) % tubeSegments;

                triangles[triangleCursor++] = startCenterIndex;
                triangles[triangleCursor++] = s;
                triangles[triangleCursor++] = sNext;
            }

            int endRingStart = (ringCount - 1) * tubeSegments;

            for (int s = 0; s < tubeSegments; s++)
            {
                int sNext = (s + 1) % tubeSegments;

                triangles[triangleCursor++] = endCenterIndex;
                triangles[triangleCursor++] = endRingStart + sNext;
                triangles[triangleCursor++] = endRingStart + s;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        RefreshMeshCollider();
    }

    private List<VisualNoisePoint> BuildVisualPoints()
    {
        List<VisualNoisePoint> result = new List<VisualNoisePoint>();

        if (samples.Count == 0)
            return result;

        List<NoiseSample> source = new List<NoiseSample>(samples);

        if (addLiveHoldPoint && source.Count > 0)
        {
            NoiseSample last = source[source.Count - 1];

            if (Time.time - last.time > 0.02f)
            {
                source.Add(new NoiseSample
                {
                    worldPosition = last.worldPosition,
                    noiseDb = last.noiseDb,
                    time = Time.time
                });
            }
        }

        if (!useVisualSmoothing || source.Count < 2)
        {
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(new VisualNoisePoint
                {
                    worldPoint = GetSpatioTemporalPoint(source[i]),
                    noiseDb = source[i].noiseDb,
                    time = source[i].time
                });
            }

            return result;
        }

        for (int i = 0; i < source.Count - 1; i++)
        {
            NoiseSample a = source[i];
            NoiseSample b = source[i + 1];

            Vector3 aWorld = GetSpatioTemporalPoint(a);
            Vector3 bWorld = GetSpatioTemporalPoint(b);

            float segmentDistance = Vector3.Distance(aWorld, bWorld);

            int distanceBasedSteps = maxVisualSegmentLength > 0.001f
                ? Mathf.CeilToInt(segmentDistance / maxVisualSegmentLength)
                : 1;

            int steps = Mathf.Max(interpolationStepsPerSegment, distanceBasedSteps);
            steps = Mathf.Clamp(steps, 1, 64);

            for (int step = 0; step < steps; step++)
            {
                float t = step / (float)steps;

                result.Add(new VisualNoisePoint
                {
                    worldPoint = Vector3.Lerp(aWorld, bWorld, t),
                    noiseDb = Mathf.Lerp(a.noiseDb, b.noiseDb, t),
                    time = Mathf.Lerp(a.time, b.time, t)
                });
            }
        }

        NoiseSample lastSource = source[source.Count - 1];

        result.Add(new VisualNoisePoint
        {
            worldPoint = GetSpatioTemporalPoint(lastSource),
            noiseDb = lastSource.noiseDb,
            time = lastSource.time
        });

        for (int i = 0; i < chaikinSmoothingIterations; i++)
            result = ChaikinSmooth(result);

        return result;
    }

    private List<VisualNoisePoint> ChaikinSmooth(List<VisualNoisePoint> input)
    {
        if (input == null || input.Count < 3)
            return input;

        List<VisualNoisePoint> output = new List<VisualNoisePoint>();
        output.Add(input[0]);

        for (int i = 0; i < input.Count - 1; i++)
        {
            VisualNoisePoint a = input[i];
            VisualNoisePoint b = input[i + 1];

            output.Add(new VisualNoisePoint
            {
                worldPoint = Vector3.Lerp(a.worldPoint, b.worldPoint, 0.25f),
                noiseDb = Mathf.Lerp(a.noiseDb, b.noiseDb, 0.25f),
                time = Mathf.Lerp(a.time, b.time, 0.25f)
            });

            output.Add(new VisualNoisePoint
            {
                worldPoint = Vector3.Lerp(a.worldPoint, b.worldPoint, 0.75f),
                noiseDb = Mathf.Lerp(a.noiseDb, b.noiseDb, 0.75f),
                time = Mathf.Lerp(a.time, b.time, 0.75f)
            });
        }

        output.Add(input[input.Count - 1]);
        return output;
    }

    private void BuildParallelTransportFrames(
        Vector3[] points,
        Vector3[] tangents,
        Vector3[] normals,
        Vector3[] binormals)
    {
        int count = points.Length;

        for (int i = 0; i < count; i++)
            tangents[i] = GetTubeTangent(points, i);

        Vector3 reference = Vector3.up;

        if (Mathf.Abs(Vector3.Dot(tangents[0], reference)) > 0.92f)
            reference = Vector3.right;

        normals[0] = Vector3.Cross(tangents[0], reference).normalized;

        if (normals[0].sqrMagnitude < 0.000001f)
            normals[0] = Vector3.forward;

        binormals[0] = Vector3.Cross(tangents[0], normals[0]).normalized;

        for (int i = 1; i < count; i++)
        {
            Vector3 previousTangent = tangents[i - 1];
            Vector3 currentTangent = tangents[i];

            Vector3 rotationAxis = Vector3.Cross(previousTangent, currentTangent);
            float axisMagnitude = rotationAxis.magnitude;

            if (axisMagnitude > 0.000001f)
            {
                rotationAxis /= axisMagnitude;

                float dot = Mathf.Clamp(Vector3.Dot(previousTangent, currentTangent), -1f, 1f);
                float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

                Quaternion transportRotation = Quaternion.AngleAxis(angle, rotationAxis);
                normals[i] = transportRotation * normals[i - 1];
            }
            else
            {
                normals[i] = normals[i - 1];
            }

            normals[i] = Vector3.ProjectOnPlane(normals[i], currentTangent).normalized;

            if (normals[i].sqrMagnitude < 0.000001f)
                normals[i] = normals[i - 1];

            binormals[i] = Vector3.Cross(currentTangent, normals[i]).normalized;
        }
    }

    private float GetSmoothEndFactor(float sampleTime)
    {
        if (!smoothEnds)
            return 1f;

        float age = Mathf.Clamp(Time.time - sampleTime, 0f, historySeconds);

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

    private Gradient CreateThreeColorValueGradient(Color lowColor, Color middleColor, Color highColor)
    {
        Gradient g = new Gradient();

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(lowColor, 0.00f),
            new GradientColorKey(middleColor, 0.50f),
            new GradientColorKey(highColor, 1.00f)
        };

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(0.85f, 0.00f),
            new GradientAlphaKey(1.00f, 0.50f),
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

        timeAxisMaterial = CreateUnlitRuntimeMaterial(timeAxisColor);
    }

    private Material CreateUnlitRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        SetMaterialColor(material, color);

        return material;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
        }
    }

    private void UpdateLabelsAndAxis()
    {
        CreateLabelRootIfNeeded();

        bool shouldShowAnything = IsVisuallyVisible() && samples.Count > 0 && (showTimeLabels || showDateLabel || showTimeAxis);

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

        labelObject.SetActive(IsVisuallyVisible());

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
            timeAxisLine.numCapVertices = 8;
            timeAxisLine.numCornerVertices = 8;
        }

        timeAxisObject.SetActive(IsVisuallyVisible() && showTimeAxis);

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

        tickObject.SetActive(IsVisuallyVisible() && showTimeAxis);
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

    public float GetMinNoiseDb()
    {
        return minNoiseDb;
    }

    public float GetMaxNoiseDb()
    {
        return maxNoiseDb;
    }

    public float GetMiddleNoiseDb()
    {
        return (minNoiseDb + maxNoiseDb) * 0.5f;
    }

    public Color GetColorForNoiseDb(float noiseDb)
    {
        return GetNoiseColor(noiseDb);
    }
}