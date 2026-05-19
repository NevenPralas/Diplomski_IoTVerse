using UnityEngine;

public class RobotIgnoreVisualizationLayer : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private string robotLayerName = "Robot";
    [SerializeField] private string visualizationLayerName = "Visualization";
    [SerializeField] private string playerLayerName = "Avatar";

    [Header("Ignore")]
    [SerializeField] private bool ignoreVisualization = true;
    [SerializeField] private bool ignorePlayer = true;

    private void Awake()
    {
        Apply();
    }

    [ContextMenu("Apply Ignore Layers")]
    public void Apply()
    {
        int robotLayer = LayerMask.NameToLayer(robotLayerName);
        int visualizationLayer = LayerMask.NameToLayer(visualizationLayerName);
        int playerLayer = LayerMask.NameToLayer(playerLayerName);

        if (robotLayer == -1)
        {
            Debug.LogError("Robot layer ne postoji: " + robotLayerName);
            return;
        }

        LayerMask excludeMask = 0;

        if (ignoreVisualization)
        {
            if (visualizationLayer == -1)
            {
                Debug.LogWarning("Visualization layer ne postoji: " + visualizationLayerName);
            }
            else
            {
                Physics.IgnoreLayerCollision(robotLayer, visualizationLayer, true);
                excludeMask |= 1 << visualizationLayer;
            }
        }

        if (ignorePlayer)
        {
            if (playerLayer == -1)
            {
                Debug.LogWarning("Player layer ne postoji: " + playerLayerName);
            }
            else
            {
                Physics.IgnoreLayerCollision(robotLayer, playerLayer, true);
                excludeMask |= 1 << playerLayer;
            }
        }

        foreach (ArticulationBody body in GetComponentsInChildren<ArticulationBody>(true))
        {
            body.excludeLayers |= excludeMask;
        }

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            col.excludeLayers |= excludeMask;
        }

        Debug.Log(
            $"Robot ignore applied. Ignoring Visualization={ignoreVisualization}, Player={ignorePlayer}"
        );
    }
}