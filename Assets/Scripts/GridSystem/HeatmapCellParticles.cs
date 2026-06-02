using System.Collections.Generic;
using UnityEngine;

public class HeatmapCellParticles : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShaderGridHeatmap heatmap;
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private Transform particlesParent;

    [Header("Placement")]
    [SerializeField] private float verticalOffset = 0.03f;

    [Header("Temperature Range")]
    [SerializeField] private float minTemperature = 18f;
    [SerializeField] private float maxTemperature = 22f;

    [Header("Particle Visibility")]
    [SerializeField, Range(0f, 1f)] private float cellSpawnChance = 0.5f;

    [Header("Color Tuning")]
    [SerializeField] private float alphaMin = 0.3f;
    [SerializeField] private float alphaMax = 0.6f;

    [Header("Physics Isolation")]
    [SerializeField] private string visualizationLayerName = "Visualization";
    [SerializeField] private bool removeCollidersFromParticles = true;
    [SerializeField] private bool removeRigidbodiesFromParticles = true;
    [SerializeField] private bool disableParticleCollisionModule = true;

    private readonly Dictionary<Vector2Int, ParticleSystem> activeParticles = new();
    private readonly HashSet<Vector2Int> blockedCells = new();

    private void Awake()
    {
        if (particlesParent == null)
        {
            GameObject root = new GameObject("HeatmapParticles");
            root.transform.SetParent(transform, false);
            particlesParent = root.transform;
        }

        ApplyVisualizationLayerRecursively(particlesParent.gameObject);
    }

    public void ShowOrUpdateCellParticle(int gridX, int gridY, float temperature)
    {
        if (heatmap == null || particlePrefab == null)
            return;

        Vector2Int key = new Vector2Int(gridX, gridY);

        if (blockedCells.Contains(key))
            return;

        if (!activeParticles.TryGetValue(key, out ParticleSystem ps) || ps == null)
        {
            if (Random.value > cellSpawnChance)
            {
                blockedCells.Add(key);
                return;
            }

            Vector3 cellCenter = heatmap.GetCellCenterWorld(gridX, gridY);
            cellCenter.y += verticalOffset;

            GameObject go = Instantiate(particlePrefab, cellCenter, Quaternion.identity, particlesParent);

            PrepareVisualizationObject(go);

            ps = go.GetComponent<ParticleSystem>();

            if (ps == null)
            {
                Debug.LogWarning("Particle prefab nema ParticleSystem komponentu.");
                Destroy(go);
                return;
            }

            activeParticles[key] = ps;
        }

        UpdateParticleColor(ps, temperature);
    }

    private void PrepareVisualizationObject(GameObject root)
    {
        ApplyVisualizationLayerRecursively(root);

        if (removeCollidersFromParticles)
        {
            foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(col);
            }
        }

        if (removeRigidbodiesFromParticles)
        {
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(rb);
            }
        }

        if (disableParticleCollisionModule)
        {
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.CollisionModule collision = ps.collision;
                collision.enabled = false;

                ParticleSystem.TriggerModule trigger = ps.trigger;
                trigger.enabled = false;
            }
        }
    }

    private void ApplyVisualizationLayerRecursively(GameObject root)
    {
        int layer = LayerMask.NameToLayer(visualizationLayerName);

        if (layer == -1)
        {
            Debug.LogWarning("Layer ne postoji: " + visualizationLayerName);
            return;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
        }
    }

    private void UpdateParticleColor(ParticleSystem ps, float temperature)
    {
        float t = Mathf.InverseLerp(minTemperature, maxTemperature, temperature);

        Color heatColor = heatmap.GetColorForTemperature(temperature);
        heatColor.a = Mathf.Lerp(alphaMin, alphaMax, t);

        var main = ps.main;
        main.startColor = heatColor;

        var colorOverLifetime = ps.colorOverLifetime;
        if (colorOverLifetime.enabled)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(heatColor.r, heatColor.g, heatColor.b), 0f),
                    new GradientColorKey(new Color(heatColor.r, heatColor.g, heatColor.b), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(heatColor.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );

            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }


    public void ClearCellParticle(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);

        if (activeParticles.TryGetValue(key, out ParticleSystem ps))
        {
            if (ps != null)
                Destroy(ps.gameObject);

            activeParticles.Remove(key);
        }

        blockedCells.Remove(key);
    }

    public void ClearAllParticles()
    {
        foreach (var kvp in activeParticles)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }

        activeParticles.Clear();
        blockedCells.Clear();
    }
}