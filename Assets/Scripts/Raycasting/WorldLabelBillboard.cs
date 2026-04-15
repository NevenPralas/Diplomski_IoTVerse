using UnityEngine;

public class WorldLabelBillboard : MonoBehaviour
{
    [SerializeField] private bool lockXRotation = true;
    [SerializeField] private bool lockZRotation = true;

    private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 direction = transform.position - targetCamera.transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);

        Vector3 euler = lookRotation.eulerAngles;

        if (lockXRotation)
            euler.x = 0f;

        if (lockZRotation)
            euler.z = 0f;

        transform.rotation = Quaternion.Euler(euler);
    }
}