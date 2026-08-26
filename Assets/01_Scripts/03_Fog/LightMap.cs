using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The lit part of the world, kept as one small top-down texture over the terrain.
/// Every pole stamps a circle into it; the fog shader reads it to know where to thin out.
/// Gameplay questions go through LightAt/IsLit instead, which stay analytic - no readback,
/// and no dependency on the texture resolution.
/// </summary>
[ExecuteAlways]
public class LightMap : MonoBehaviour
{
    public static LightMap Instance { get; private set; }

    [Header("Terrain")]
    [SerializeField] private Terrain terrain;

    [Header("Texture")]
    [Tooltip("Pixels across the whole terrain. 128 over 255 m is about 2 m per pixel.")]
    [SerializeField] private int resolution = 128;

    [Tooltip("How much of the outer radius fades out, 0 = hard edge")]
    [Range(0f, 0.6f)]
    [SerializeField] private float edgeSoftness = 0.18f;

    [Header("Shader")]
    [SerializeField] private string mapProperty = "_LightMap";
    [SerializeField] private string boundsProperty = "_LightMapBounds";

    private readonly List<LightSource> sources = new List<LightSource>();

    private Texture2D map;
    private byte[] levels;

    private Vector3 origin;
    private float worldSize = 255f;
    private bool dirty;

    /// <summary>Fraction of the terrain that is lit, 0..1. Refreshed on every rebuild.</summary>
    public float Coverage { get; private set; }

    public int SourceCount => sources.Count;

    private void OnEnable()
    {
        Instance = this;

        ResolveTerrain();
        BuildTexture();

        // OnEnable order between objects is not guaranteed. A pole that woke up first found
        // Instance still null and gave up, so sweep for those instead of trusting registration.
        AdoptExistingSources();

        dirty = true;
    }

    private void AdoptExistingSources()
    {
        LightSource[] found = FindObjectsByType<LightSource>(FindObjectsSortMode.None);

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].isActiveAndEnabled) Register(found[i]);
        }
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;

        if (map == null) return;

        if (Application.isPlaying) Destroy(map);
        else DestroyImmediate(map);

        map = null;
        levels = null;
    }

    private void LateUpdate()
    {
        if (!dirty) return;

        dirty = false;
        Rebuild();
    }

    // --- Registration: LightSource calls these itself ---

    public void Register(LightSource source)
    {
        if (source == null || sources.Contains(source)) return;

        sources.Add(source);
        dirty = true;
    }

    public void Unregister(LightSource source)
    {
        if (sources.Remove(source)) dirty = true;
    }

    /// <summary>Call after moving or resizing a source.</summary>
    public void MarkDirty() => dirty = true;

    // --- Gameplay queries: analytic, so they ignore the texture entirely ---

    /// <summary>1 inside a pole's full radius, fading to 0 at its edge.</summary>
    public float LightAt(Vector3 worldPosition)
    {
        float best = 0f;

        for (int i = 0; i < sources.Count; i++)
        {
            LightSource s = sources[i];
            if (s == null || !s.isActiveAndEnabled) continue;

            float radius = s.Radius;
            if (radius <= 0f) continue;

            Vector3 offset = s.transform.position - worldPosition;
            offset.y = 0f;

            float value = Falloff(offset.magnitude / radius);
            if (value > best) best = value;

            if (best >= 1f) return 1f;
        }

        return best;
    }

    public bool IsLit(Vector3 worldPosition) => LightAt(worldPosition) > 0.5f;

    /// <summary>Positive = metres of light left before the fog, negative = already in it.</summary>
    public float DistanceIntoLight(Vector3 worldPosition)
    {
        float best = float.NegativeInfinity;

        for (int i = 0; i < sources.Count; i++)
        {
            LightSource s = sources[i];
            if (s == null || !s.isActiveAndEnabled) continue;

            Vector3 offset = s.transform.position - worldPosition;
            offset.y = 0f;

            float margin = s.Radius - offset.magnitude;
            if (margin > best) best = margin;
        }

        return float.IsNegativeInfinity(best) ? -9999f : best;
    }

    // --- Building the texture ---

    private void ResolveTerrain()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;

        if (terrain != null)
        {
            Vector3 size = terrain.terrainData.size;
            origin = terrain.transform.position;
            worldSize = Mathf.Max(size.x, size.z);
        }
        else
        {
            origin = Vector3.zero;
            worldSize = 255f;
        }
    }

    private void BuildTexture()
    {
        resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 32, 512);

        if (map != null && map.width == resolution) return;

        if (map != null)
        {
            if (Application.isPlaying) Destroy(map);
            else DestroyImmediate(map);
        }

        map = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
        {
            name = "Light Map",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        levels = new byte[resolution * resolution];
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (map == null || levels == null) BuildTexture();
        if (map == null) return;

        ResolveTerrain();

        System.Array.Clear(levels, 0, levels.Length);

        for (int i = 0; i < sources.Count; i++) Stamp(sources[i]);

        map.SetPixelData(levels, 0);
        map.Apply(false, false);

        PushToShaders();
        MeasureCoverage();
    }

    private void Stamp(LightSource source)
    {
        if (source == null || !source.isActiveAndEnabled) return;

        float radius = source.Radius;
        if (radius <= 0f) return;

        float metresPerPixel = worldSize / resolution;
        Vector3 centre = source.transform.position;

        float localX = centre.x - origin.x;
        float localZ = centre.z - origin.z;

        int minX = Mathf.Max(0, Mathf.FloorToInt((localX - radius) / metresPerPixel));
        int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt((localX + radius) / metresPerPixel));
        int minY = Mathf.Max(0, Mathf.FloorToInt((localZ - radius) / metresPerPixel));
        int maxY = Mathf.Min(resolution - 1, Mathf.CeilToInt((localZ + radius) / metresPerPixel));

        for (int y = minY; y <= maxY; y++)
        {
            float pz = (y + 0.5f) * metresPerPixel - localZ;

            for (int x = minX; x <= maxX; x++)
            {
                float px = (x + 0.5f) * metresPerPixel - localX;

                float value = Falloff(Mathf.Sqrt(px * px + pz * pz) / radius);
                if (value <= 0f) continue;

                int index = y * resolution + x;
                byte written = (byte)(value * 255f);

                // Circles overlap constantly, so the brightest one wins
                if (written > levels[index]) levels[index] = written;
            }
        }
    }

    // 1 in the middle, easing to 0 across the outer edgeSoftness of the radius
    private float Falloff(float normalised)
    {
        if (normalised >= 1f) return 0f;
        if (edgeSoftness <= 0.001f) return 1f;

        float t = Mathf.Clamp01((1f - normalised) / edgeSoftness);
        return t * t * (3f - 2f * t);
    }

    private void PushToShaders()
    {
        if (map == null) return;

        Shader.SetGlobalTexture(mapProperty, map);

        // xy = terrain corner, z = size in metres, w = 1/size so the shader skips a divide
        Shader.SetGlobalVector(boundsProperty,
            new Vector4(origin.x, origin.z, worldSize, 1f / Mathf.Max(0.01f, worldSize)));
    }

    private void MeasureCoverage()
    {
        if (levels == null || levels.Length == 0)
        {
            Coverage = 0f;
            return;
        }

        int lit = 0;
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] > 127) lit++;
        }

        Coverage = (float)lit / levels.Length;
    }

    private void OnValidate()
    {
        resolution = Mathf.Clamp(resolution, 32, 512);
        dirty = true;
    }

    private void OnDrawGizmosSelected()
    {
        ResolveTerrain();

        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.6f);
        Vector3 centre = origin + new Vector3(worldSize * 0.5f, 0f, worldSize * 0.5f);
        Gizmos.DrawWireCube(centre, new Vector3(worldSize, 0.1f, worldSize));

        for (int i = 0; i < sources.Count; i++)
        {
            LightSource s = sources[i];
            if (s == null) continue;

            Gizmos.DrawWireSphere(s.transform.position, s.Radius);
        }
    }
}
