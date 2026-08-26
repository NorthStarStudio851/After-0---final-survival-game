using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class FogBoundaryLine : MonoBehaviour
{
    private const int ProbeCount = 8;

    [Header("References")]
    [SerializeField] private FogSystem fogSystem;

    [Header("Shape")]
    [SerializeField] private int segments = 128;
    [SerializeField] private int mobileSegments = 72;
    [SerializeField] private float inset = 1.5f;
    [SerializeField] private float heightOffset = 0.25f;

    [Header("Updates")]
    [SerializeField] private float updateInterval = 0.05f;
    [SerializeField] private float radiusEpsilon = 0.05f;

    private LineRenderer line;
    private Vector3[] points;
    private readonly float[] lastProbes = new float[ProbeCount];

    private int activeSegments;
    private float nextUpdateTime;

    private void OnEnable()
    {
        if (fogSystem == null) fogSystem = FindFirstObjectByType<FogSystem>();

        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;

        Rebuild();
    }

    private void LateUpdate()
    {
        if (fogSystem == null || line == null) return;

        if (points == null || points.Length != activeSegments)
        {
            Rebuild();
            return;
        }

        if (Time.time < nextUpdateTime) return;
        nextUpdateTime = Time.time + updateInterval;

        if (!BoundaryChanged()) return;

        WritePoints();
        line.SetPositions(points);
        CacheProbes();
    }

    [ContextMenu("Rebuild")]
    private void Rebuild()
    {
        if (fogSystem == null) return;

        activeSegments = Mathf.Max(8, Application.isMobilePlatform ? mobileSegments : segments);

        points = new Vector3[activeSegments];
        line.positionCount = activeSegments;

        WritePoints();
        line.SetPositions(points);

        CacheProbes();
    }

    private void WritePoints()
    {
        // The loop closes itself, so the last point must not repeat the first one
        float step = 360f / activeSegments;

        for (int i = 0; i < activeSegments; i++)
        {
            float angle = i * step;
            float radians = angle * Mathf.Deg2Rad;

            Vector3 boundary = fogSystem.GetBoundaryPoint(angle);
            Vector3 center = fogSystem.transform.position;

            Vector3 inward = new Vector3(center.x - boundary.x, 0f, center.z - boundary.z).normalized;
            Vector3 placed = boundary + inward * inset;

            points[i] = new Vector3(placed.x, boundary.y + heightOffset, placed.z);
        }
    }

    private bool BoundaryChanged()
    {
        for (int i = 0; i < ProbeCount; i++)
        {
            float radius = fogSystem.SampleRadius(i * (360f / ProbeCount));
            if (Mathf.Abs(radius - lastProbes[i]) > radiusEpsilon) return true;
        }

        return false;
    }

    private void CacheProbes()
    {
        for (int i = 0; i < ProbeCount; i++)
        {
            lastProbes[i] = fogSystem.SampleRadius(i * (360f / ProbeCount));
        }
    }

    private void OnValidate()
    {
        segments = Mathf.Max(8, segments);
        mobileSegments = Mathf.Max(8, mobileSegments);
        updateInterval = Mathf.Max(0f, updateInterval);
    }
}