using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FogWallMesh : MonoBehaviour
{
    private const int ProbeCount = 8;
    private const int RingCount = 2;

    [Header("References")]
    [SerializeField] private FogSystem fogSystem;

    [Header("Quality")]
    [SerializeField] private int segments = 192;
    [SerializeField] private int mobileSegments = 96;
    [SerializeField] private float updateInterval = 0.05f;
    [SerializeField] private float radiusEpsilon = 0.05f;

    [Header("Wall")]
    [SerializeField] private float wallHeight = 60f;
    [SerializeField] private float sinkDepth = 3f;
    [SerializeField] private float heightVariation = 14f;
    [SerializeField] private float heightFrequency = 2.4f;

    [Header("UVs")]
    [SerializeField] private float tilingAround = 24f;
    [SerializeField] private float tilingUp = 1f;

    [Header("Culling")]
    [SerializeField] private float boundsRadius = 300f;

    private Mesh mesh;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly float[] lastProbes = new float[ProbeCount];

    private int activeSegments;
    private float nextUpdateTime;

    private int VertexCount => (activeSegments + 1) * RingCount;

    private void OnEnable()
    {
        if (fogSystem == null) fogSystem = FindFirstObjectByType<FogSystem>();
        Rebuild();
    }

    private void OnDisable()
    {
        if (mesh == null) return;

        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);

        mesh = null;
    }

    private void LateUpdate()
    {
        if (fogSystem == null) return;

        if (mesh == null || vertices.Count != VertexCount)
        {
            Rebuild();
            return;
        }

        if (Time.time < nextUpdateTime) return;
        nextUpdateTime = Time.time + updateInterval;

        if (!BoundaryChanged()) return;

        WriteVertexPositions();
        mesh.SetVertices(vertices);
        CacheProbes();
    }

    [ContextMenu("Rebuild")]
    private void Rebuild()
    {
        if (fogSystem == null) return;

        activeSegments = Mathf.Max(8, Application.isMobilePlatform ? mobileSegments : segments);

        if (mesh == null)
        {
            mesh = new Mesh { name = "Fog Wall" };
            mesh.MarkDynamic();

            // Generated every time the component wakes up, so it has no business in the scene file
            mesh.hideFlags = HideFlags.DontSave;
        }

        mesh.Clear();

        WriteVertexPositions();

        // Normals and UVs never change: the wall always faces the centre
        Vector3[] normals = new Vector3[VertexCount];
        Vector2[] uvs = new Vector2[VertexCount];
        int[] triangles = new int[activeSegments * 6];

        float step = 360f / activeSegments;
        int t = 0;

        for (int i = 0; i <= activeSegments; i++)
        {
            float radians = i * step * Mathf.Deg2Rad;
            Vector3 inward = new Vector3(-Mathf.Sin(radians), 0f, -Mathf.Cos(radians));

            int baseIndex = i * RingCount;

            normals[baseIndex] = inward;
            normals[baseIndex + 1] = inward;

            float u = (float)i / activeSegments * tilingAround;

            uvs[baseIndex] = new Vector2(u, 0f);
            uvs[baseIndex + 1] = new Vector2(u, tilingUp);

            if (i == activeSegments) break;

            int nextIndex = baseIndex + RingCount;

            triangles[t++] = baseIndex;
            triangles[t++] = baseIndex + 1;
            triangles[t++] = nextIndex + 1;

            triangles[t++] = baseIndex;
            triangles[t++] = nextIndex + 1;
            triangles[t++] = nextIndex;
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);

        // Fixed bounds, so nothing has to be recalculated while the fog breathes
        mesh.bounds = new Bounds(Vector3.zero,
            new Vector3(boundsRadius * 2f, wallHeight * 4f, boundsRadius * 2f));

        GetComponent<MeshFilter>().sharedMesh = mesh;

        CacheProbes();
    }

    private void WriteVertexPositions()
    {
        if (vertices.Count != VertexCount)
        {
            vertices.Clear();
            for (int i = 0; i < VertexCount; i++) vertices.Add(Vector3.zero);
        }

        float step = 360f / activeSegments;

        for (int i = 0; i <= activeSegments; i++)
        {
            float angle = i * step;
            float radians = angle * Mathf.Deg2Rad;

            // The last ring sits on top of the first one, so the texture has a clean seam
            Vector3 boundary = fogSystem.GetBoundaryPoint(angle);

            Vector3 bottom = new Vector3(boundary.x, boundary.y - sinkDepth, boundary.z);
            Vector3 top = new Vector3(boundary.x, boundary.y + wallHeight + CrestOffset(radians), boundary.z);

            int baseIndex = i * RingCount;

            vertices[baseIndex] = transform.InverseTransformPoint(bottom);
            vertices[baseIndex + 1] = transform.InverseTransformPoint(top);
        }
    }

    // Breaks the flat horizon line at the top of the wall
    private float CrestOffset(float radians)
    {
        if (heightVariation <= 0f) return 0f;

        float n = Mathf.PerlinNoise(Mathf.Cos(radians) * heightFrequency + 5.3f,
                                    Mathf.Sin(radians) * heightFrequency + 91.7f) - 0.5f;

        return n * 2f * heightVariation;
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
        wallHeight = Mathf.Max(1f, wallHeight);
        tilingAround = Mathf.Max(0.01f, tilingAround);
        heightVariation = Mathf.Max(0f, heightVariation);
        updateInterval = Mathf.Max(0f, updateInterval);
        boundsRadius = Mathf.Max(50f, boundsRadius);
    }
}