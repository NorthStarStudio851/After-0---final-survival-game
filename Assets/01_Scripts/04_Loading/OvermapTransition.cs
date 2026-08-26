using System.Collections;
using UnityEngine;

/// <summary>
/// Fades the screen and hands over to the loading scene. It does not care what asked for it -
/// a TerrainExit, a button, anything - so hook OnExitTriggered to BeginExit in the Inspector.
/// </summary>
public class OvermapTransition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup fadeScreen;

    [Header("Destination")]
    [SerializeField] private string overmapSceneName = "02_Overmap";

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.4f;

    private bool running;

    private void OnEnable()
    {
        if (fadeScreen == null) return;

        fadeScreen.alpha = 0f;
        fadeScreen.blocksRaycasts = false;
    }

    /// <summary>Wire this to TerrainExit.OnExitTriggered.</summary>
    public void BeginExit()
    {
        if (running) return;

        running = true;
        StartCoroutine(LeaveMap());
    }

    private IEnumerator LeaveMap()
    {
        yield return Fade(1f);

        SceneRouter.GoThroughLoading(overmapSceneName);
    }

    private IEnumerator Fade(float target)
    {
        if (fadeScreen == null) yield break;

        fadeScreen.blocksRaycasts = true;

        float start = fadeScreen.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeScreen.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        fadeScreen.alpha = target;
    }
}
