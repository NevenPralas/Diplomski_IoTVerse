using UnityEngine;

public class RobotIgnoreAvatarLayer : MonoBehaviour
{
    public string avatarLayerName = "Player";

    [ContextMenu("Robot Ignore Avatar Layer")]
    public void IgnoreAvatarLayer()
    {
        int avatarLayer = LayerMask.NameToLayer(avatarLayerName);

        if (avatarLayer == -1)
        {
            Debug.LogError("Layer ne postoji: " + avatarLayerName);
            return;
        }

        LayerMask avatarMask = 1 << avatarLayer;

        foreach (ArticulationBody body in GetComponentsInChildren<ArticulationBody>(true))
        {
            body.excludeLayers |= avatarMask;
            Debug.Log("Excluded Avatar layer on: " + GetPath(body.transform));
        }

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            col.excludeLayers |= avatarMask;
            Debug.Log("Excluded Avatar layer on collider: " + GetPath(col.transform));
        }

        Physics.IgnoreLayerCollision(
            LayerMask.NameToLayer("Robot"),
            avatarLayer,
            true
        );
    }

    private string GetPath(Transform t)
    {
        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}