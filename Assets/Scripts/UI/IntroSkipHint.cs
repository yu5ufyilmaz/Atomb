using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bottom-right "[E] Hold to skip" during the opening; a thin red line fills while the key is held.</summary>
public sealed class IntroSkipHint : MonoBehaviour
{
    const float Margin = 64, Delay = 1, Fade = .5f;
    CanvasGroup group;
    RectTransform fill;
    float age, progress;
    bool closing;

    public static IntroSkipHint Create()
    {
        var style = Resources.Load<SettingsMenuStyle>("Settings/SettingsMenuStyle");
        var hint = new GameObject("Intro Skip Hint", typeof(RectTransform)).AddComponent<IntroSkipHint>();
        var canvas = hint.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2500;
        var scaler = hint.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        hint.group = hint.gameObject.AddComponent<CanvasGroup>(); hint.group.alpha = 0; hint.group.blocksRaycasts = false;

        // Anchored to the bottom-right corner; children are laid out leftwards from the right edge.
        var root = SettingsMenuStyle.Rect(hint.transform, "Hint", 0, 0, 0, 40);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1, 0); root.anchoredPosition = new Vector2(-Margin, Margin);
        var label = style.Label(root, "Label", 0, 0, 400, 34, "", 20); label.color = SettingsMenuStyle.Muted; label.textWrappingMode = TextWrappingModes.NoWrap;
        GameLocalization.Set(label, "Hold to skip");
        float labelWidth = Mathf.Ceil(label.GetPreferredValues(label.text).x);
        var key = style.Image(root, "Key", 0, 2, 0, 30, null, SettingsMenuStyle.Raised);
        var glyph = style.Label(key.transform, "Glyph", 0, 0, 0, 30, GameInput.ResolveHints("{input:SkipIntro}"), 17);
        glyph.alignment = TextAlignmentOptions.Center; glyph.color = SettingsMenuStyle.Ink; glyph.textWrappingMode = TextWrappingModes.NoWrap;
        float keyWidth = Mathf.Max(30, Mathf.Ceil(glyph.GetPreferredValues(glyph.text).x) + 16), width = keyWidth + 12 + labelWidth;
        root.sizeDelta = new Vector2(width, 40);
        key.rectTransform.sizeDelta = glyph.rectTransform.sizeDelta = new Vector2(keyWidth, 30);
        label.rectTransform.anchoredPosition = new Vector2(keyWidth + 12, 0); label.rectTransform.sizeDelta = new Vector2(labelWidth, 34);
        style.Image(root, "Track", 0, 38, width, 2, null, SettingsMenuStyle.Hairline);
        hint.fill = style.Image(root, "Fill", 0, 38, width, 2, null, SettingsMenuStyle.Accent).rectTransform; hint.fill.localScale = new Vector3(0, 1, 1);
        return hint;
    }

    public void SetProgress(float value) { progress = Mathf.Clamp01(value); }
    public void Close() { closing = true; }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        fill.localScale = new Vector3(progress, 1, 1);
        float target = closing ? 0 : age < Delay ? 0 : 1;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / (closing ? Fade * .5f : Fade));
        if (closing && group.alpha <= 0) Destroy(gameObject);
    }
}
