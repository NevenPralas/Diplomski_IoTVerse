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

    [Header("References")]
    [SerializeField] private Material trailMaterial;

    [Header("Time Mapping")]
    [Tooltip("Koliko sekundi povijesti se prikazuje u 3D putanji.")]
    [SerializeField] private float historySeconds = 60f;

    [Tooltip("Visina 3D putanje koja predstavlja historySeconds.")]
    [SerializeField] private float timeHeight = 1.6f;

    [Tooltip("Y pozicija najnovijeg uzorka.")]
    [SerializeField] private float baseHeight = 0.05f;

    [Tooltip("Ako je uključeno, stariji uzorci se guraju prema gore kako vrijeme prolazi.")]
    [SerializeField] private bool rollingTimeWindow = true;

    [Header("Noise Mapping")]
    [Tooltip("Donja granica buke za normalizaciju boje.")]
    [SerializeField] private float minNoiseDb = 30f;

    [Tooltip("Gornja granica buke za normalizaciju boje.")]
    [SerializeField] private float maxNoiseDb = 85f;

    [Header("Tube Geometry")]
    [Tooltip("Radijus 3D cijevi. Ovo je konstantna debljina traga.")]
    [SerializeField] private float tubeRadius = 0.055f;

    [Tooltip("Broj segmenata kruga. 8 je dobro za performanse, 12 izgleda glađe.")]
    [Range(3, 24)]
    [SerializeField] private int tubeSegments = 10;

    [Tooltip("Ako je uključeno, zatvara početak i kraj cijevi.")]
    [SerializeField] private bool capEnds = true;

    [Header("Sampling")]
    [Tooltip("Minimalni vremenski razmak između dva uzorka.")]
    [SerializeField] private float minSampleInterval = 0.35f;

    [Tooltip("Mali pomak prema gore da se mesh ne reže s podom.")]
    [SerializeField] private float verticalNudge = 0.02f;

    [Header("Appearance")]
    [Tooltip("Ako je uključeno, koristi automatski gradient: plavo -> ljubičasto -> magenta -> narančasto -> crveno.")]
    [SerializeField] private bool useDefaultNoiseGradient = true;

    [SerializeField] private Gradient noiseGradient;

    [SerializeField] private bool rebuildEveryFrame = true;
    [SerializeField] private bool visible = true;

    [Header("Debug")]
    [SerializeField] private bool logSamples = false;

    private readonly List<NoiseSample> samples = new List<NoiseSample>();

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private float lastSampleTime = -999f;

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

        if (useDefaultNoiseGradient)
        {
            noiseGradient = CreateDefaultNoiseGradient();
        }
    }

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh();
        mesh.name = "SpatioTemporalNoiseTrailTubeMesh";
        mesh.MarkDynamic();

        meshFilter.sharedMesh = mesh;

        if (trailMaterial != null)
        {
            meshRenderer.sharedMaterial = trailMaterial;
        }

        if (useDefaultNoiseGradient || noiseGradient == null)
        {
            noiseGradient = CreateDefaultNoiseGradient();
        }

        meshRenderer.enabled = visible;
    }

    private void Update()
    {
        RemoveOldSamples();

        if (rollingTimeWindow && rebuildEveryFrame)
        {
            RebuildMesh();
        }
    }

    public void AddSample(Vector3 worldPosition, float noiseDb)
    {
        if (Time.time - lastSampleTime < minSampleInterval)
        {
            return;
        }

        lastSampleTime = Time.time;

        NoiseSample sample = new NoiseSample
        {
            worldPosition = worldPosition,
            noiseDb = noiseDb,
            time = Time.time
        };

        samples.Add(sample);

        if (logSamples)
        {
            Debug.Log(
                $"NoiseTrail sample | pos={worldPosition}, noise={noiseDb:F1} dBA, count={samples.Count}"
            );
        }

        RemoveOldSamples();
        RebuildMesh();
    }

    public void ClearTrail()
    {
        samples.Clear();

        if (mesh != null)
        {
            mesh.Clear();
        }
    }

    public void SetVisible(bool isVisible)
    {
        visible = isVisible;

        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }
    }

    public bool IsVisible()
    {
        return visible;
    }

    private void RemoveOldSamples()
    {
        float now = Time.time;

        for (int i = samples.Count - 1; i >= 0; i--)
        {
            float age = now - samples[i].time;

            if (age > historySeconds)
            {
                samples.RemoveAt(i);
            }
        }
    }

    private void RebuildMesh()
    {
        if (mesh == null)
        {
            return;
        }

        mesh.Clear();

        if (samples.Count < 2)
        {
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

        for (int i = 0; i < ringCount; i++)
        {
            points[i] = GetSpatioTemporalPoint(samples[i]);
        }

        for (int i = 0; i < ringCount; i++)
        {
            Vector3 tangent = GetTubeTangent(points, i);
            BuildRingBasis(tangent, out Vector3 normal, out Vector3 binormal);

            Color sampleColor = GetNoiseColor(samples[i].noiseDb);

            for (int s = 0; s < tubeSegments; s++)
            {
                float angle = (Mathf.PI * 2f * s) / tubeSegments;

                Vector3 offset =
                    normal * Mathf.Cos(angle) * tubeRadius +
                    binormal * Mathf.Sin(angle) * tubeRadius;

                int vertexIndex = i * tubeSegments + s;

                vertices[vertexIndex] = transform.InverseTransformPoint(points[i] + offset);
                colors[vertexIndex] = sampleColor;
            }
        }

        int triangleCursor = 0;

        // Bočne plohe cijevi.
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

        // Zatvaranje početka i kraja cijevi.
        if (capEnds)
        {
            int startCenterIndex = tubeVertexCount;
            int endCenterIndex = tubeVertexCount + 1;

            vertices[startCenterIndex] = transform.InverseTransformPoint(points[0]);
            vertices[endCenterIndex] = transform.InverseTransformPoint(points[ringCount - 1]);

            colors[startCenterIndex] = GetNoiseColor(samples[0].noiseDb);
            colors[endCenterIndex] = GetNoiseColor(samples[ringCount - 1].noiseDb);

            // Start cap
            for (int s = 0; s < tubeSegments; s++)
            {
                int sNext = (s + 1) % tubeSegments;

                triangles[triangleCursor++] = startCenterIndex;
                triangles[triangleCursor++] = sNext;
                triangles[triangleCursor++] = s;
            }

            // End cap
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
        {
            tangent = Vector3.forward;
        }
        else if (index == 0)
        {
            tangent = points[1] - points[0];
        }
        else if (index == points.Length - 1)
        {
            tangent = points[index] - points[index - 1];
        }
        else
        {
            tangent = points[index + 1] - points[index - 1];
        }

        if (tangent.sqrMagnitude < 0.000001f)
        {
            tangent = Vector3.up;
        }

        return tangent.normalized;
    }

    private void BuildRingBasis(Vector3 tangent, out Vector3 normal, out Vector3 binormal)
    {
        Vector3 reference = Vector3.up;

        // Ako je tangent skoro okomit, Vector3.up nije dobra referenca.
        if (Mathf.Abs(Vector3.Dot(tangent, reference)) > 0.92f)
        {
            reference = Vector3.right;
        }

        normal = Vector3.Cross(tangent, reference).normalized;

        if (normal.sqrMagnitude < 0.000001f)
        {
            normal = Vector3.forward;
        }

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
        {
            gradientToUse = CreateDefaultNoiseGradient();
        }

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