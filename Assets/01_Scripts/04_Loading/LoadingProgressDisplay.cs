using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingProgressDisplay : MonoBehaviour
{
    [Header("Bar (fill in either one)")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;

    [Header("Tip marker")]
    [SerializeField] private RectTransform tipMarker;
    [SerializeField] private float tipOffset = 0f;
    [SerializeField] private bool hideTipWhenEmpty = true;
    [SerializeField] private float tipPulse = 0.12f;
    [SerializeField] private float tipPulseSpeed = 4f;

    [Header("Labels")]
    [SerializeField] private TMP_Text percentLabel;
    [SerializeField] private TMP_Text statusLabel;

    [Header("Text")]
    [SerializeField] private string statusText = "Loading";
    [SerializeField] private int maxDots = 3;
    [SerializeField] private float dotInterval = 0.35f;

    [Header("Motion")]
    [SerializeField] private float fillSmoothing = 6f;

    private float targetProgress;
    private float shownProgress;
    private float nextDotTime;
    private int visibleDots;

    public float ShownProgress => shownProgress;

    public void SetProgress(float value)
    {
        // Never let it walk backwards, a bar that retreats reads as a bug
        targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(value));
    }

    private void OnEnable()
    {
        targetProgress = 0f;
        shownProgress = 0f;
        visibleDots = 0;
        nextDotTime = Time.unscaledTime + dotInterval;

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;
        }

        Apply();
    }

    private void Update()
    {
        // Unscaled, because a loading screen must keep moving even with timeScale at zero
        float step = 1f - Mathf.Exp(-fillSmoothing * Time.unscaledDeltaTime);
        shownProgress = Mathf.Lerp(shownProgress, targetProgress, step);

        if (Time.unscaledTime >= nextDotTime)
        {
            nextDotTime = Time.unscaledTime + dotInterval;
            visibleDots = (visibleDots + 1) % (maxDots + 1);
        }

        Apply();
    }

    private void Apply()
    {
        if (slider != null) slider.SetValueWithoutNotify(shownProgress);
        if (fillImage != null) fillImage.fillAmount = shownProgress;

        MoveTip();

        if (percentLabel != null) percentLabel.text = $"{Mathf.RoundToInt(shownProgress * 100f)}%";

        if (statusLabel != null) statusLabel.text = statusText + BuildDots();
    }

    // Anchors instead of pixels, so the tip lands on the fill edge at any screen width
    private void MoveTip()
    {
        if (tipMarker == null) return;

        bool visible = !hideTipWhenEmpty || shownProgress > 0.001f;

        if (tipMarker.gameObject.activeSelf != visible)
        {
            tipMarker.gameObject.SetActive(visible);
        }

        if (!visible) return;

        Vector2 min = tipMarker.anchorMin;
        Vector2 max = tipMarker.anchorMax;

        min.x = shownProgress;
        max.x = shownProgress;

        tipMarker.anchorMin = min;
        tipMarker.anchorMax = max;

        Vector2 position = tipMarker.anchoredPosition;
        position.x = tipOffset;
        tipMarker.anchoredPosition = position;

        if (tipPulse <= 0f) return;

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * tipPulseSpeed) * tipPulse;
        tipMarker.localScale = new Vector3(1f, pulse, 1f);
    }

    // The hidden dots stay in the string as invisible characters, so the text never changes width
    private string BuildDots()
    {
        if (maxDots <= 0) return string.Empty;

        string shown = new string('.', visibleDots);
        int hidden = maxDots - visibleDots;

        if (hidden <= 0) return shown;

        return shown + "<alpha=#00>" + new string('.', hidden) + "<alpha=#FF>";
    }

    private void OnValidate()
    {
        maxDots = Mathf.Clamp(maxDots, 0, 8);
        dotInterval = Mathf.Max(0.05f, dotInterval);
        fillSmoothing = Mathf.Max(0.1f, fillSmoothing);
        tipPulse = Mathf.Max(0f, tipPulse);
        tipPulseSpeed = Mathf.Max(0f, tipPulseSpeed);
    }
}