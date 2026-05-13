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
    [Tooltip("Donja granica buke za normalizaciju boje/širine.")]
    [SerializeField] private float minNoiseDb = 30f;

    [Tooltip("Gornja granica buke za normalizaciju boje/širine.")]
    [SerializeField] private float maxNoiseDb = 85f;

    [Tooltip("Minimalna širina traga za tihe vrijednosti.")]
    [SerializeField] private float minRibbonWidth = 0.04f;

    [Tooltip("Maksimalna širina traga za glasne vrijednosti.")]
    [SerializeField] private float maxRibbonWidth = 0.16f;

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
        minRibbonWidth = Mathf.Max(0.005f, minRibbonWidth);
        maxRibbonWidth = Mathf.Max(minRibbonWidth, maxRibbonWidth);

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
        mesh.name = "SpatioTemporalNoiseTrailMesh";
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

        int vertexCount = samples.Count * 2;
        int segmentCount = samples.Count - 1;
        int triangleIndexCount = segmentCount * 6;

        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[triangleIndexCount];

        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 pointWorld = GetSpatioTemporalPoint(samples[i]);
            Vector3 sideWorld = GetSideDirection(i);
            float width = GetRibbonWidth(samples[i].noiseDb);

            Vector3 leftWorld = pointWorld - sideWorld * width * 0.5f;
            Vector3 rightWorld = pointWorld + sideWorld * width * 0.5f;

            int v = i * 2;

            vertices[v] = transform.InverseTransformPoint(leftWorld);
            vertices[v + 1] = transform.InverseTransformPoint(rightWorld);

            Color sampleColor = GetNoiseColor(samples[i].noiseDb);

            colors[v] = sampleColor;
            colors[v + 1] = sampleColor;
        }

        int t = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            int a = i * 2;
            int b = a + 1;
            int c = a + 2;
            int d = a + 3;

            triangles[t++] = a;
            triangles[t++] = c;
            triangles[t++] = b;

            triangles[t++] = b;
            triangles[t++] = c;
            triangles[t++] = d;
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

    private Vector3 GetSideDirection(int index)
    {
        Vector3 direction;

        if (samples.Count < 2)
        {
            return Vector3.right;
        }

        if (index == 0)
        {
            direction = samples[1].worldPosition - samples[0].worldPosition;
        }
        else if (index == samples.Count - 1)
        {
            direction = samples[index].worldPosition - samples[index - 1].worldPosition;
        }
        else
        {
            direction = samples[index + 1].worldPosition - samples[index - 1].worldPosition;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        direction.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;

        if (side.sqrMagnitude < 0.0001f)
        {
            side = Vector3.right;
        }

        return side;
    }

    private float NormalizeNoise(float noiseDb)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(minNoiseDb, maxNoiseDb, noiseDb));
    }

    private float GetRibbonWidth(float noiseDb)
    {
        float t = NormalizeNoise(noiseDb);
        return Mathf.Lerp(minRibbonWidth, maxRibbonWidth, t);
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
        c.a = Mathf.Lerp(0.60f, 1.00f, t);

        return c;
    }

    private Gradient CreateDefaultNoiseGradient()
    {
        Gradient g = new Gradient();

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(new Color(0.00f, 0.65f, 1.00f), 0.00f), // cyan/plavo
            new GradientColorKey(new Color(0.25f, 0.00f, 1.00f), 0.25f), // ljubičasto
            new GradientColorKey(new Color(0.90f, 0.00f, 1.00f), 0.50f), // magenta
            new GradientColorKey(new Color(1.00f, 0.65f, 0.00f), 0.75f), // narančasto
            new GradientColorKey(new Color(1.00f, 0.00f, 0.05f), 1.00f)  // crveno
        };

        GradientAlphaKey[] alphaKeys =
        {
            new GradientAlphaKey(0.60f, 0.00f),
            new GradientAlphaKey(0.80f, 0.50f),
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
        Gizmos.DrawWireSphere(baseCenter, 0.05f);
        Gizmos.DrawWireSphere(topCenter, 0.05f);
    }
}