using UnityEngine;
using UnityEngine.Events;

public class FogExit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FogSystem fogSystem;
    [SerializeField] private Transform player;

    [Header("Thresholds (meters)")]
    [SerializeField] private float warningDistance = 10f;
    [SerializeField] private float fullEntryDepth = 2f;

    [Header("Timing (seconds)")]
    [SerializeField] private float exitDelay = 2f;

    [Header("Re-entry")]
    [SerializeField] private float reentryInset = 5f;

    [Header("Events")]
    public UnityEvent OnExitTriggered;

    // Read by the UI: 0 = safe, 1 = about to leave the map
    public float ExitProgress { get; private set; }

    // Read by the screen effect: 0 = far from the edge, 1 = fully in the fog
    public float WarningStrength { get; private set; }

    public bool IsFullyInFog { get; private set; }

    // Kept between scenes so the overmap knows where to put the player back
    public static bool HasExitRecord { get; private set; }
    public static float LastExitAngle { get; private set; }

    private bool exitFired;

    private void Reset()
    {
        fogSystem = FindFirstObjectByType<FogSystem>();
    }

    private void Update()
    {
        if (fogSystem == null || player == null) return;

        float depth = fogSystem.DistanceIntoFog(player.position);

        WarningStrength = Mathf.InverseLerp(-warningDistance, fullEntryDepth, depth);
        IsFullyInFog = depth >= fullEntryDepth;

        if (!IsFullyInFog)
        {
            ExitProgress = 0f;
            exitFired = false;
            return;
        }

        if (exitFired) return;

        ExitProgress += Time.deltaTime / Mathf.Max(0.01f, exitDelay);

        if (ExitProgress < 1f) return;

        ExitProgress = 1f;
        exitFired = true;

        LastExitAngle = fogSystem.GetAngleTo(player.position);
        HasExitRecord = true;

        OnExitTriggered.Invoke();
    }

    // Where the player should re-appear when coming back from the overmap
    public Vector3 GetReentryPoint()
    {
        float angle = HasExitRecord ? LastExitAngle : 0f;

        Vector3 boundary = fogSystem.GetBoundaryPoint(angle);
        Vector3 center = fogSystem.transform.position;

        Vector3 inward = center - boundary;
        inward.y = 0f;

        // A few meters inside, otherwise the countdown starts again the moment he lands
        return boundary + inward.normalized * reentryInset;
    }

    [ContextMenu("Test: mut jucatorul la punctul de reintrare")]
    private void TestReentry()
    {
        if (player != null) player.position = GetReentryPoint();
    }
}