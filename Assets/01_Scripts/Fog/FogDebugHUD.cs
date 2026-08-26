using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

public class FogDebugHUD : MonoBehaviour
{
    [SerializeField] private FogSystem fogSystem;
    [SerializeField] private FogExit fogExit;
    [SerializeField] private Transform player;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly Key[] SectorKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8
    };

    private GUIStyle style;

    private void Update()
    {
        if (fogSystem == null || Keyboard.current == null) return;

        for (int i = 0; i < SectorKeys.Length; i++)
        {
            if (Keyboard.current[SectorKeys[i]].wasPressedThisFrame)
            {
                fogSystem.AdvanceSector(i);
            }
        }
    }

    private void OnGUI()
    {
        if (fogSystem == null || player == null) return;

        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };

        float depth = fogSystem.DistanceIntoFog(player.position);
        float angle = fogSystem.GetAngleTo(player.position);
        int sector = fogSystem.GetSectorAt(player.position);

        string state = depth <= 0f
            ? $"in siguranta, {-depth:0.0} m pana la ceata"
            : $"IN CEATA, {depth:0.0} m adancime";

        GUI.Label(new Rect(20, 20, 700, 26), $"unghi {angle:0}   sector {sector}   {state}", style);

        if (fogExit == null) return;

        GUI.Label(new Rect(20, 46, 700, 26), $"iesire overmap: {fogExit.ExitProgress * 100f:0}%", style);
        GUI.Box(new Rect(20, 76, 300f, 16), GUIContent.none);
        GUI.Box(new Rect(20, 76, 300f * Mathf.Clamp01(fogExit.ExitProgress), 16), GUIContent.none);
    }
#endif
}