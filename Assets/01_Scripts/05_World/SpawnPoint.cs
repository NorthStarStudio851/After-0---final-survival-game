using UnityEngine;

/// <summary>
/// Somewhere the player can appear when he arrives on this map. A welcome mat at the base,
/// a bunker door, a dock at the port - same component every time.
/// If a map has none, TerrainExit drops the player somewhere random along the border instead.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Facing")]
    [Tooltip("Turn the player to match this object when he lands on it")]
    [SerializeField] private bool alignRotation = true;

    public Vector3 Position => transform.position;

    public Quaternion Rotation => alignRotation ? transform.rotation : Quaternion.identity;

    public bool AlignRotation => alignRotation;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);

        if (!alignRotation) return;

        Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 2f);
    }
}
