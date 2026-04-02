using UnityEngine;

public class VRAvatarHeadAlign : MonoBehaviour
{
    [Header("References")]
    public Transform xrHead;
    public Transform avatarHead;

    [Header("Settings")]
    public bool followYaw = true;
    public float rotationSmooth = 12f;
    public Vector3 positionOffset = Vector3.zero;

    void LateUpdate()
    {
        if (xrHead == null || avatarHead == null)
            return;

        // 1) Okreni tijelo po Y osi prema smjeru glave
        if (followYaw)
        {
            Vector3 flatForward = xrHead.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSmooth * Time.deltaTime
                );
            }
        }

        // 2) Pomakni cijeli avatar tako da njegova glava dođe na VR glavu
        Vector3 delta = xrHead.position - avatarHead.position;
        transform.position += delta + positionOffset;
    }
}