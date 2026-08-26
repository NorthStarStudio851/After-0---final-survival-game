using UnityEngine;

public enum FogStage
{
    Locked = 0,
    MissionsDone = 1,
    GeneratorConnected = 2
}

public class FogSystem : MonoBehaviour
{
    private const int SectorCount = 8;

    [Header("Terrain Reference")]
    [SerializeField] private Terrain terrain;

    [Header("Radii (meters)")]
    [SerializeField] private float startRadius = 40f;
    [SerializeField] private float missionRadius = 100f;
    [SerializeField] private float cardinalMaxRadius = 135f;
    [SerializeField] private float diagonalMaxRadius = 197f;

    [Header("Fit To Terrain")]
    [SerializeField] private float edgeMargin = 15f;

    [Header("Shape")]
    [Tooltip("2 = circle, 4 = squircle, 8 = square with soft corners, 16 = hard square")]
    [SerializeField] private float shapeExponent = 8f;
    [Tooltip("Rotates the square; 45 lines it up with a camera at yaw 45")]
    [SerializeField] private float shapeRotation = 45f;

    [Header("Edge Shape")]
    [SerializeField] private float wobbleStrength = 0.05f;
    [SerializeField] private float wobbleFrequency = 1.6f;
    [SerializeField] private float rippleStrength = 0.02f;
    [SerializeField] private float rippleFrequency = 3.5f;
    [SerializeField] private float wobbleDriftSpeed = 0.02f;

    [Header("Animation")]
    [SerializeField] private float retractSpeed = 8f;

    [Header("Sectors (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SV, 6=V, 7=NV)")]
    [SerializeField] private FogStage[] sectorStages = new FogStage[SectorCount];

    [Header("Gizmos")]
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private int gizmoSegments = 192;
    [SerializeField] private bool drawSectorLines = true;

    private float[] currentRadius;
    private float[] targetRadius;

    private float SectorAngleSize => 360f / SectorCount;

    // In the editor Unity restores private arrays as empty ones, so length is the only honest test
    private bool HasRuntimeRadii => currentRadius != null && currentRadius.Length == SectorCount
                                    && targetRadius != null && targetRadius.Length == SectorCount;

    private void Awake()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;

        EnsureStageArray();

        currentRadius = new float[SectorCount];
        targetRadius = new float[SectorCount];

        // No animation on load: fog starts exactly where the saved stages say it should be
        for (int i = 0; i < SectorCount; i++)
        {
            targetRadius[i] = StageRadius(i, sectorStages[i]);
            currentRadius[i] = targetRadius[i];
        }
    }

    private void Update()
    {
        if (!HasRuntimeRadii) return;

        float step = retractSpeed * Time.deltaTime;

        for (int i = 0; i < SectorCount; i++)
        {
            currentRadius[i] = Mathf.MoveTowards(currentRadius[i], targetRadius[i], step);
        }
    }

    // --- Setup helpers: right click the component header to run these ---

    [ContextMenu("Center on terrain")]
    private void CenterOnTerrain()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;
        Vector3 center = new Vector3(origin.x + size.x * 0.5f, origin.y, origin.z + size.z * 0.5f);

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(transform, "Center fog on terrain");
#endif
        transform.position = center;
        MarkChanged(transform);
    }

    [ContextMenu("Fit radii to terrain")]
    private void FitRadiiToTerrain()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 size = terrain.terrainData.size;
        float half = Mathf.Min(size.x, size.z) * 0.5f;

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Fit fog radii to terrain");
#endif
        // On a square the corners already reach further, so the sides get the full margin
        cardinalMaxRadius = Mathf.Max(startRadius, half - edgeMargin);
        diagonalMaxRadius = Mathf.Max(cardinalMaxRadius, half * Mathf.Sqrt(2f) - edgeMargin);

        MarkChanged(this);
    }

    [ContextMenu("Reset sectors to locked")]
    private void ResetSectorsToLocked()
    {
        EnsureStageArray();

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Reset fog sectors");
#endif
        for (int i = 0; i < SectorCount; i++)
        {
            sectorStages[i] = FogStage.Locked;

            if (HasRuntimeRadii) targetRadius[i] = startRadius;
        }

        MarkChanged(this);
    }

    // Without this the editor throws the change away at the next recompile
    private void MarkChanged(Object target)
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;

        UnityEditor.EditorUtility.SetDirty(target);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    // --- Public API: this is what the mission system calls ---

    public void SetSectorStage(int sector, FogStage stage)
    {
        if (sector < 0 || sector >= SectorCount) return;

        EnsureStageArray();
        sectorStages[sector] = stage;

        if (HasRuntimeRadii)
        {
            targetRadius[sector] = StageRadius(sector, stage);
        }
    }

    public void AdvanceSector(int sector)
    {
        if (sector < 0 || sector >= SectorCount) return;

        FogStage current = GetStage(sector);
        if (current == FogStage.GeneratorConnected) return;

        SetSectorStage(sector, current + 1);
    }

    public FogStage GetStage(int sector)
    {
        if (sectorStages == null || sector < 0 || sector >= sectorStages.Length) return FogStage.Locked;
        return sectorStages[sector];
    }

    // Compass angle from the fog centre to a world position: 0 = north (+Z), grows clockwise
    public float GetAngleTo(Vector3 worldPosition)
    {
        Vector3 offset = worldPosition - transform.position;
        return Mathf.Repeat(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, 360f);
    }

    // Which sector a world position falls into, 0..7
    public int GetSectorAt(Vector3 worldPosition)
    {
        return Mathf.RoundToInt(GetAngleTo(worldPosition) / SectorAngleSize) % SectorCount;
    }

    // True while the position is still in the safe zone, false once it is inside the fog
    public bool IsInsideClearZone(Vector3 worldPosition)
    {
        return DistanceIntoFog(worldPosition) <= 0f;
    }

    // Negative = safe, positive = how many meters deep into the fog
    public float DistanceIntoFog(Vector3 worldPosition)
    {
        Vector3 offset = worldPosition - transform.position;
        offset.y = 0f;

        return offset.magnitude - SampleRadius(GetAngleTo(worldPosition));
    }

    // --- The core math: radius blends smoothly between neighbouring sectors ---

    public float SampleRadius(float angleDegrees)
    {
        if (float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees)) return RadiusOfSector(0);

        float scaled = Mathf.Repeat(angleDegrees, 360f) / SectorAngleSize;

        int lower = Mathf.FloorToInt(scaled) % SectorCount;
        int upper = (lower + 1) % SectorCount;

        float t = scaled - Mathf.Floor(scaled);
        t = t * t * (3f - 2f * t); // smoothstep, otherwise the sectors meet in hard pizza-slice edges

        float radius = Mathf.Lerp(RadiusOfSector(lower), RadiusOfSector(upper), t);

        return radius * ShapeFactor(angleDegrees) * (1f + EdgeOffset(angleDegrees));
    }

    // Superellipse: 2 gives a circle, higher values push the outline towards a square
    private float ShapeFactor(float angleDegrees)
    {
        if (shapeExponent <= 2.001f) return 1f;

        float radians = (angleDegrees - shapeRotation) * Mathf.Deg2Rad;

        float cos = Mathf.Abs(Mathf.Cos(radians));
        float sin = Mathf.Abs(Mathf.Sin(radians));

        float sum = Mathf.Pow(cos, shapeExponent) + Mathf.Pow(sin, shapeExponent);

        return Mathf.Pow(sum, -1f / shapeExponent);
    }

    // Perlin sampled around a circle is seamless by construction, so the edge never shows a joint
    private float EdgeOffset(float angleDegrees)
    {
        if (wobbleStrength <= 0f && rippleStrength <= 0f) return 0f;

        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        float drift = Application.isPlaying ? Time.time * wobbleDriftSpeed : 0f;

        float lobes = Mathf.PerlinNoise(cos * wobbleFrequency + 32.7f + drift,
                                        sin * wobbleFrequency + 11.3f + drift) - 0.5f;

        float teeth = Mathf.PerlinNoise(cos * rippleFrequency + 71.1f - drift,
                                        sin * rippleFrequency + 54.9f - drift) - 0.5f;

        return lobes * 2f * wobbleStrength + teeth * 2f * rippleStrength;
    }

    public Vector3 GetBoundaryPoint(float angleDegrees)
    {
        return PointAt(angleDegrees, SampleRadius(angleDegrees));
    }

    private Vector3 PointAt(float angleDegrees, float radius)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector3 center = transform.position;

        // Angle 0 points north (+Z) and grows clockwise, same as a compass
        float x = center.x + Mathf.Sin(radians) * radius;
        float z = center.z + Mathf.Cos(radians) * radius;
        float y = center.y;

        if (terrain != null)
        {
            y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        return new Vector3(x, y, z);
    }

    private float RadiusOfSector(int sector)
    {
        sector = ((sector % SectorCount) + SectorCount) % SectorCount;

        // In play mode the animated value, in the editor the value the stage implies
        if (HasRuntimeRadii) return currentRadius[sector];

        return StageRadius(sector, GetStage(sector));
    }

    private float StageRadius(int sector, FogStage stage)
    {
        switch (stage)
        {
            case FogStage.MissionsDone:
                return missionRadius;
            case FogStage.GeneratorConnected:
                return IsCardinal(sector) ? cardinalMaxRadius : diagonalMaxRadius;
            default:
                return startRadius;
        }
    }

    private bool IsCardinal(int sector)
    {
        return sector % 2 == 0;
    }

    private void EnsureStageArray()
    {
        if (sectorStages != null && sectorStages.Length == SectorCount) return;

        FogStage[] resized = new FogStage[SectorCount];

        if (sectorStages != null)
        {
            int copyCount = Mathf.Min(sectorStages.Length, SectorCount);
            for (int i = 0; i < copyCount; i++) resized[i] = sectorStages[i];
        }

        sectorStages = resized;
    }

    private void OnValidate()
    {
        EnsureStageArray();
        gizmoSegments = Mathf.Max(8, gizmoSegments);
        edgeMargin = Mathf.Max(0f, edgeMargin);
        shapeExponent = Mathf.Clamp(shapeExponent, 2f, 24f);
        wobbleStrength = Mathf.Max(0f, wobbleStrength);
        rippleStrength = Mathf.Max(0f, rippleStrength);
    }

    private void OnDrawGizmos()
    {
        if (sectorStages == null || sectorStages.Length != SectorCount) return;
        if (gizmoSegments < 8) return;
        if (terrain == null) terrain = Terrain.activeTerrain;

        Gizmos.color = gizmoColor;

        float step = 360f / gizmoSegments;
        Vector3 previous = GetBoundaryPoint(0f);

        for (int i = 1; i <= gizmoSegments; i++)
        {
            Vector3 point = GetBoundaryPoint(i * step);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        if (!drawSectorLines) return;

        for (int i = 0; i < SectorCount; i++)
        {
            float angle = i * SectorAngleSize;
            Gizmos.DrawLine(transform.position, GetBoundaryPoint(angle));
        }
    }
}