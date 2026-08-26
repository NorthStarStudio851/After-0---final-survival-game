using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScreenController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private LoadingProgressDisplay display;
    [SerializeField] private CanvasGroup fadeGroup;

    [Header("Behaviour")]
    [SerializeField] private string fallbackScene = "01_BaseScene";
    [SerializeField] private float minimumDisplayTime = 1.2f;
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float holdAtFullDuration = 0.35f;

    private IEnumerator Start()
    {
        if (fadeGroup != null) fadeGroup.alpha = 0f;

        // One frame so the screen is actually visible before loading stalls the main thread
        yield return null;

        yield return FadeIn();

        string target = SceneRouter.ConsumePendingScene();

        if (string.IsNullOrEmpty(target)) target = fallbackScene;

        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogError($"Scena '{target}' nu e in Build Profiles, ma intorc la '{fallbackScene}'.", this);
            target = fallbackScene;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(target);
        load.allowSceneActivation = false;

        float started = Time.unscaledTime;

        // Unity parks the operation at 0.9 until we allow the swap, so that value means "gata"
        while (load.progress < 0.9f || Time.unscaledTime - started < minimumDisplayTime)
        {
            float loaded = Mathf.Clamp01(load.progress / 0.9f);
            float waited = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(0.01f, minimumDisplayTime));

            if (display != null) display.SetProgress(Mathf.Min(loaded, waited));

            yield return null;
        }

        if (display != null) display.SetProgress(1f);

        // Let the bar visibly reach 100 before the scene swaps, otherwise it cuts at 94%
        float holdUntil = Time.unscaledTime + holdAtFullDuration;

        while (Time.unscaledTime < holdUntil)
        {
            yield return null;
        }

        load.allowSceneActivation = true;
    }

    private IEnumerator FadeIn()
    {
        if (fadeGroup == null) yield break;

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        fadeGroup.alpha = 1f;
    }
}