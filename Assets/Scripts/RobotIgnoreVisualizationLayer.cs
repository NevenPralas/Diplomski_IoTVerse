using System.Collections.Generic;
using UnityEngine;

public class RobotIgnoreVisualizationLayer : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private string robotLayerName = "Robot";
    [SerializeField] private string visualizationLayerName = "Visualization";
    [SerializeField] private string playerLayerName = "Avatar";

    [Tooltip("Dodatni layeri koje robot treba ignorirati. Ovdje obavezno dodaj CO2Grid ako CO2Grid ima MeshCollider za raycast.")]
    [SerializeField]
    private string[] additionalIgnoredLayerNames = new string[]
    {
        "CO2Grid"
    };

    [Header("Ignore")]
    [SerializeField] private bool ignoreVisualization = true;
    [SerializeField] private bool ignorePlayer = true;
    [SerializeField] private bool ignoreAdditionalLayers = true;

    [Header("Runtime Refresh")]
    [Tooltip("Ako je uključeno, skripta ponovno primjenjuje excludeLayers nakon kratkog vremena. Korisno ako se colliders dodaju kasnije u runtimeu.")]
    [SerializeField] private bool reapplyAfterStart = true;

    [SerializeField] private float reapplyDelaySeconds = 0.25f;

    private void Awake()
    {
        Apply();
    }

    private void Start()
    {
        if (reapplyAfterStart)
            Invoke(nameof(Apply), Mathf.Max(0.01f, reapplyDelaySeconds));
    }

    [ContextMenu("Apply Ignore Layers")]
    public void Apply()
    {
        int robotLayer = LayerMask.NameToLayer(robotLayerName);

        if (robotLayer == -1)
        {
            Debug.LogError("Robot layer ne postoji: " + robotLayerName);
            return;
        }

        List<int> ignoredLayers = new List<int>();

        if (ignoreVisualization)
            TryAddLayer(ignoredLayers, visualizationLayerName, true);

        if (ignorePlayer)
            TryAddLayer(ignoredLayers, playerLayerName, true);

        if (ignoreAdditionalLayers && additionalIgnoredLayerNames != null)
        {
            for (int i = 0; i < additionalIgnoredLayerNames.Length; i++)
                TryAddLayer(ignoredLayers, additionalIgnoredLayerNames[i], false);
        }

        LayerMask excludeMask = 0;

        for (int i = 0; i < ignoredLayers.Count; i++)
        {
            int ignoredLayer = ignoredLayers[i];
            Physics.IgnoreLayerCollision(robotLayer, ignoredLayer, true);
            excludeMask |= 1 << ignoredLayer;
        }

        foreach (ArticulationBody body in GetComponentsInChildren<ArticulationBody>(true))
        {
            if (body != null)
                body.excludeLayers |= excludeMask;
        }

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null)
                col.excludeLayers |= excludeMask;
        }

        Debug.Log($"Robot ignore applied. Robot={robotLayerName}, ignored mask={excludeMask.value}");
    }

    private void TryAddLayer(List<int> layers, string layerName, bool warnIfMissing)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return;

        int layer = LayerMask.NameToLayer(layerName.Trim());

        if (layer == -1)
        {
            if (warnIfMissing)
                Debug.LogWarning("Layer ne postoji: " + layerName);

            return;
        }

        if (!layers.Contains(layer))
            layers.Add(layer);
    }
}
