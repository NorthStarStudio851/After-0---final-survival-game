using System.Collections;
using UnityEngine;

public class OvermapTransition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FogExit fogExit;
    [SerializeField] private CanvasGroup fadeScreen;

    [Header("Destination")]
    [SerializeField] private string overmapSceneName = "02_Overmap";

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.4f;

    private bool running;

    private void OnEnable()
    {
        if (fadeScreen != null)
        {
            fadeScreen.alpha = 0f;
            fadeScreen.blocksRaycasts = false;
        }

        if (fogExit != null) fogExit.OnExitTriggered.AddListener(BeginExit);
    }

    private void OnDisable()
    {
        if (fogExit != null) fogExit.OnExitTriggered.RemoveListener(BeginExit);
    }

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