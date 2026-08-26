using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Fixed angle")]
    [SerializeField] private float pitch = 50f;
    [SerializeField] private float yaw = 45f;
    [SerializeField] private float distance = 16f;

    [Header("Smoothing")]
    [SerializeField] private float followSmoothTime = 0.18f;

    private Vector3 followVelocity;

    private void OnEnable()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = Vector3.SmoothDamp(transform.position, WantedPosition(), ref followVelocity, followSmoothTime);

        // The angle never changes, so the player always keeps his bearings
        transform.rotation = ViewRotation();
    }

    [ContextMenu("Snap to target")]
    private void SnapToTarget()
    {
        if (target == null) return;

        transform.position = WantedPosition();
        transform.rotation = ViewRotation();
        followVelocity = Vector3.zero;
    }

    private Quaternion ViewRotation()
    {
        return Quaternion.Euler(pitch, yaw, 0f);
    }

    private Vector3 WantedPosition()
    {
        return target.position + targetOffset - ViewRotation() * Vector3.forward * distance;
    }
}