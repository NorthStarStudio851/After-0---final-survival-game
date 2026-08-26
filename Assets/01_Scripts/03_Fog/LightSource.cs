using UnityEngine;

public enum PoleKind
{
    TorchPole = 0,
    LightPole = 1
}

/// <summary>
/// One pole. Put this on the prefab and it takes care of itself: it tells the LightMap
/// it exists, and tells it again when it goes away.
/// </summary>
[ExecuteAlways]
public class LightSource : MonoBehaviour
{
    [Header("Kind")]
    [SerializeField] private PoleKind kind = PoleKind.TorchPole;

    [Header("Radius (metres)")]
    [SerializeField] private float torchPoleRadius = 21f;
    [SerializeField] private float lightPoleRadius = 35f;

    [Header("Power")]
    [Tooltip("Light poles go dark without a generator; torch poles ignore this")]
    [SerializeField] private bool powered = true;

    private Vector3 lastPosition;
    private float lastRadius;

    public PoleKind Kind => kind;

    public float Radius
    {
        get
        {
            if (kind == PoleKind.LightPole) return powered ? lightPoleRadius : 0f;
            return torchPoleRadius;
        }
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        lastRadius = Radius;

        if (LightMap.Instance != null) LightMap.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (LightMap.Instance != null) LightMap.Instance.Unregister(this);
    }

#if UNITY_EDITOR
    // Only for dragging one around in the scene view. Poles never move once the game runs,
    // so this must not survive into a build - 57 of them polling every frame is pure waste.
    private void Update()
    {
        if (Application.isPlaying) return;
        if (LightMap.Instance == null) return;

        if (lastPosition == transform.position && Mathf.Approximately(lastRadius, Radius)) return;

        lastPosition = transform.position;
        lastRadius = Radius;

        LightMap.Instance.MarkDirty();
    }
#endif

    public void SetPowered(bool value)
    {
        if (powered == value) return;

        powered = value;

        if (LightMap.Instance != null) LightMap.Instance.MarkDirty();
    }

    private void OnValidate()
    {
        torchPoleRadius = Mathf.Max(0f, torchPoleRadius);
        lightPoleRadius = Mathf.Max(0f, lightPoleRadius);

        if (LightMap.Instance != null) LightMap.Instance.MarkDirty();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == PoleKind.LightPole
            ? new Color(0.49f, 0.78f, 1f, 0.9f)
            : new Color(1f, 0.85f, 0.35f, 0.9f);

        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
