using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The border of a map, built from the terrain's own size. Drop it on any new terrain,
/// tick which sides let you leave, and it does the rest: walls all round so nobody falls off,
/// a coloured band on the open sides so the player sees the way out from a distance,
/// and the arrival, either on a SpawnPoint or somewhere random along the border.
/// </summary>
[ExecuteAlways]
public class TerrainExit : MonoBehaviour
{
    private const string BuiltName = "Border";

    [Header("Terrain")]
    [Tooltip("Left empty it takes the active terrain, so a new map needs no wiring")]
    [SerializeField] private Terrain terrain;

    [Header("Who")]
    [SerializeField] private string playerTag = "Player";

    [Header("Exit sides")]
    [SerializeField] private bool north = true;
    [SerializeField] private bool east = true;
    [SerializeField] private bool south = true;
    [SerializeField] private bool west = true;

    [Header("Band")]
    [Tooltip("Width of the coloured strip. Walking onto it starts the exit")]
    [SerializeField] private float bandWidth = 9f;
    [SerializeField] private Material bandMaterial;
    [SerializeField] private float bandHeightOffset = 0.15f;

    [Header("Leaving")]
    [Tooltip("Seconds on the band before it fires. Zero means instantly, which is what home uses")]
    [SerializeField] private float holdDuration;

    [Header("Walls")]
    [SerializeField] private float wallHeight = 30f;
    [SerializeField] private float wallThickness = 2f;

    [Header("Events")]
    public UnityEvent OnExitTriggered;

    /// <summary>0 to 1 while the player stands on a band. Read by the UI.</summary>
    public float Progress { get; private set; }

    /// <summary>Where the player stepped off, so he can be put back there on return.</summary>
    public static Vector3 LastExitPoint { get; private set; }
    public static bool HasExitRecord { get; private set; }

    private Transform player;
    private Vector3 centre;
    private float half;          // to the inner edge of the band
    private float outer;         // to the wall
    private bool fired;

    private void OnEnable()
    {
        Measure();
        Build();

        if (Application.isPlaying) PlacePlayer();
    }

    private void OnDisable()
    {
        ClearBuilt();
    }

    // --- Size comes from the terrain, every time, so a new map needs no numbers typed in ---

    private void Measure()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;

        Vector3 size = terrain != null ? terrain.terrainData.size : new Vector3(255f, 0f, 255f);
        Vector3 origin = terrain != null ? terrain.transform.position : Vector3.zero;

        centre = new Vector3(origin.x + size.x * 0.5f, origin.y, origin.z + size.z * 0.5f);

        outer = Mathf.Min(size.x, size.z) * 0.5f;
        half = Mathf.Max(1f, outer - bandWidth);
    }

    // --- Building ---

    [ContextMenu("Rebuild border")]
    public void Build()
    {
        Measure();
        ClearBuilt();

        // Walls sit on the terrain edge on every side, open or closed. An open side still
        // needs one, otherwise a player who walks past the band drops off the world.
        AddWall("N", new Vector3(0f, wallHeight * 0.5f, outer), new Vector3(outer * 2f, wallHeight, wallThickness));
        AddWall("S", new Vector3(0f, wallHeight * 0.5f, -outer), new Vector3(outer * 2f, wallHeight, wallThickness));
        AddWall("E", new Vector3(outer, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, outer * 2f));
        AddWall("V", new Vector3(-outer, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, outer * 2f));

        if (north) AddBand("N", new Vector3(0f, 0f, (half + outer) * 0.5f), new Vector3(outer * 2f, 1f, bandWidth));
        if (south) AddBand("S", new Vector3(0f, 0f, -(half + outer) * 0.5f), new Vector3(outer * 2f, 1f, bandWidth));
        if (east) AddBand("E", new Vector3((half + outer) * 0.5f, 0f, 0f), new Vector3(bandWidth, 1f, outer * 2f));
        if (west) AddBand("V", new Vector3(-(half + outer) * 0.5f, 0f, 0f), new Vector3(bandWidth, 1f, outer * 2f));

        transform.position = centre;
    }

    private Transform NewChild(string label)
    {
        GameObject go = new GameObject($"{BuiltName} {label}") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void AddWall(string label, Vector3 localPosition, Vector3 boxSize)
    {
        Transform t = NewChild($"Wall {label}");
        t.localPosition = localPosition;
        t.gameObject.AddComponent<BoxCollider>().size = boxSize;
    }

    private void AddBand(string label, Vector3 localPosition, Vector3 boxSize)
    {
        Transform t = NewChild($"Band {label}");
        t.localPosition = localPosition + Vector3.up * bandHeightOffset;
        t.localScale = new Vector3(boxSize.x, 1f, boxSize.z);

        MeshFilter filter = t.gameObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = t.gameObject.AddComponent<MeshRenderer>();

        filter.sharedMesh = FlatQuad();
        renderer.sharedMaterial = bandMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh flatQuad;

    // One quad shared by every band. Flat in XZ so the transform stays unrotated.
    private static Mesh FlatQuad()
    {
        if (flatQuad != null) return flatQuad;

        flatQuad = new Mesh { name = "Border Band", hideFlags = HideFlags.DontSave };
        flatQuad.SetVertices(new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
            new Vector3( 0.5f, 0f,  0.5f), new Vector3( 0.5f, 0f, -0.5f)
        });
        flatQuad.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        flatQuad.SetUVs(0, new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f)
        });
        flatQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        flatQuad.RecalculateBounds();

        return flatQuad;
    }

    private void ClearBuilt()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(BuiltName)) continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    // --- Leaving ---

    private void Update()
    {
        if (!Application.isPlaying || fired) return;

        if (player == null && !FindPlayer()) return;

        if (!OnExitBand(player.position))
        {
            Progress = 0f;
            return;
        }

        // Zero hold means home: step on it and you are gone
        if (holdDuration <= 0f)
        {
            Leave();
            return;
        }

        Progress += Time.deltaTime / holdDuration;

        if (Progress >= 1f) Leave();
    }

    private void Leave()
    {
        Progress = 1f;
        fired = true;

        LastExitPoint = player != null ? player.position : centre;
        HasExitRecord = true;

        OnExitTriggered.Invoke();
    }

    /// <summary>True while the position sits on a band belonging to an open side.</summary>
    public bool OnExitBand(Vector3 worldPosition)
    {
        float x = worldPosition.x - centre.x;
        float z = worldPosition.z - centre.z;

        if (north && z > half && Mathf.Abs(x) <= outer) return true;
        if (south && z < -half && Mathf.Abs(x) <= outer) return true;
        if (east && x > half && Mathf.Abs(z) <= outer) return true;
        if (west && x < -half && Mathf.Abs(z) <= outer) return true;

        return false;
    }

    // --- Arriving ---

    private void PlacePlayer()
    {
        if (!FindPlayer()) return;

        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

        if (points.Length > 0)
        {
            SpawnPoint chosen = points[Random.Range(0, points.Length)];
            Move(chosen.Position, chosen.AlignRotation ? chosen.Rotation : player.rotation);
            return;
        }

        Move(RandomBorderPoint(), Quaternion.identity);
    }

    // Landing exactly on the band would mean arriving with one foot already out of the map,
    // and on a map with no hold that is an instant round trip
    private const float ArrivalInset = 6f;

    /// <summary>Somewhere along the border, pulled in far enough not to land on a band.</summary>
    public Vector3 RandomBorderPoint()
    {
        float edge = Mathf.Max(1f, half - ArrivalInset);

        float along = Random.Range(-edge, edge);
        int side = Random.Range(0, 4);

        Vector3 point = side switch
        {
            0 => new Vector3(along, 0f, edge),
            1 => new Vector3(edge, 0f, along),
            2 => new Vector3(along, 0f, -edge),
            _ => new Vector3(-edge, 0f, along)
        };

        point += centre;

        if (terrain != null)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        }

        return point;
    }

    private void Move(Vector3 position, Quaternion rotation)
    {
        // The controller overwrites transform moves, so it has to stand down for one frame
        CharacterController controller = player.GetComponent<CharacterController>();

        if (controller != null) controller.enabled = false;

        player.SetPositionAndRotation(position + Vector3.up * 0.2f, rotation);

        if (controller != null) controller.enabled = true;
    }

    private bool FindPlayer()
    {
        GameObject found = GameObject.FindGameObjectWithTag(playerTag);
        if (found == null) return false;

        player = found.transform;
        return true;
    }

    private void OnValidate()
    {
        bandWidth = Mathf.Max(1f, bandWidth);
        wallHeight = Mathf.Max(1f, wallHeight);
        wallThickness = Mathf.Max(0.1f, wallThickness);
        holdDuration = Mathf.Max(0f, holdDuration);
    }

    private void OnDrawGizmosSelected()
    {
        Measure();

        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.9f);
        Gizmos.DrawWireCube(centre + Vector3.up * 0.5f, new Vector3(outer * 2f, 1f, outer * 2f));

        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.35f);
        Gizmos.DrawWireCube(centre + Vector3.up * 0.5f, new Vector3(half * 2f, 1f, half * 2f));
    }
}
