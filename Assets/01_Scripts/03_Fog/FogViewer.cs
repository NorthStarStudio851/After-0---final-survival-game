using UnityEngine;

/// <summary>
/// The bubble the player carries. It is not part of the light map on purpose - a moving source
/// would force the texture to redraw every frame. Instead the position goes straight to the
/// shader as a global, and the fog adds it on top of whatever the poles already cleared.
/// </summary>
[ExecuteAlways]
public class FogViewer : MonoBehaviour
{
    private static readonly int ViewerProperty = Shader.PropertyToID("_FogViewer");

    [Header("Radius (metres)")]
    [SerializeField] private float dayRadius = 13f;
    [SerializeField] private float nightRadius = 6.5f;

    [Tooltip("Seconds to travel between the two, so dusk is not a snap")]
    [SerializeField] private float changeSpeed = 1.5f;

    [Header("Time of day")]
    [Tooltip("0 = full day, 1 = full night. Wire the day cycle here when it exists")]
    [Range(0f, 1f)]
    [SerializeField] private float nightAmount;

    private float currentRadius;

    public float CurrentRadius => currentRadius;

    /// <summary>Called by the day cycle. 0 = day, 1 = night.</summary>
    public void SetNightAmount(float value) => nightAmount = Mathf.Clamp01(value);

    private void OnEnable()
    {
        currentRadius = TargetRadius();
        Push();
    }

    private void OnDisable()
    {
        // Leaving a stale bubble behind would light a hole in the middle of nowhere
        Shader.SetGlobalVector(ViewerProperty, new Vector4(0f, 0f, 0f, 0f));
    }

    private void LateUpdate()
    {
        float wanted = TargetRadius();

        currentRadius = Application.isPlaying
            ? Mathf.MoveTowards(currentRadius, wanted, changeSpeed * Time.deltaTime)
            : wanted;

        Push();
    }

    private float TargetRadius() => Mathf.Lerp(dayRadius, nightRadius, nightAmount);

    private void Push()
    {
        Vector3 p = transform.position;
        Shader.SetGlobalVector(ViewerProperty, new Vector4(p.x, p.y, p.z, currentRadius));
    }

    private void OnValidate()
    {
        dayRadius = Mathf.Max(0f, dayRadius);
        nightRadius = Mathf.Max(0f, nightRadius);
        changeSpeed = Mathf.Max(0.01f, changeSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, currentRadius > 0f ? currentRadius : dayRadius);
    }
}
