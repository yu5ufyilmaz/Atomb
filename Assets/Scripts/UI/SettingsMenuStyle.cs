using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "Senzora/Settings Menu Style")]
public sealed class SettingsMenuStyle : ScriptableObject
{
    public TMP_FontAsset font, headingFont;
    public Sprite barOutside, barInside, box, check, arrow, qualityFrame;
    public AudioMixer mixer;
    public AudioMixerGroup voice, effects;
    // Dark minimal palette (3 Nov redesign): bone text in three steps, one dim blood-red accent for focus.
    public static readonly Color Ink = new Color(.95f, .93f, .88f), Muted = new Color(.72f, .7f, .65f), Faint = new Color(.5f, .49f, .45f);
    public static readonly Color Accent = new Color(.64f, .18f, .18f), FillDim = new Color(.6f, .59f, .55f);
    // Opaque surfaces: translucent white blends in linear space in game and turns far lighter than in the editor renders.
    public static readonly Color Surface = new Color(.1f, .1f, .095f), Raised = new Color(.24f, .24f, .23f), Hairline = new Color(.2f, .2f, .19f), RowFocus = new Color(.12f, .12f, .115f);
    // States brighten the opaque surface; ghosts are invisible until hovered or selected.
    public static void SurfaceColors(Selectable control, bool ghost = false)
    {
        var colors = control.colors; colors.normalColor = ghost ? new Color(1, 1, 1, 0) : Color.white; colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1); colors.selectedColor = new Color(1.9f, 1.9f, 1.9f, 1);
        colors.pressedColor = new Color(2.4f, 2.4f, 2.4f, 1); colors.disabledColor = ghost ? new Color(1, 1, 1, 0) : new Color(.7f, .7f, .7f, 1); control.colors = colors;
    }
    public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    public Image Image(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color color)
    {
        var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false; return image;
    }
    public TMP_Text Label(Transform parent, string name, float x, float y, float w, float h, string text, float size = 30)
    {
        var label = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset; label.fontSize = size; label.enableAutoSizing = false;
        label.fontStyle = FontStyles.Normal; label.color = new Color(.88f, .88f, .85f); label.textWrappingMode = TextWrappingModes.Normal;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.text = text; label.raycastTarget = false; return label;
    }
    public Button Button(Transform parent, string name, float x, float y, float w, float h, string text, Action clicked, float fontSize = 28)
    {
        var image = Image(parent, name, x, y, w, h, null, new Color(1, 1, 1, .015f)); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, 1, 1, 4); colors.selectedColor = new Color(1, 1, 1, 5); colors.pressedColor = new Color(.7f,.7f,.65f,6); button.colors = colors;
        var label = Label(image.transform, "Label", 12, 0, w - 24, h, text, fontSize);
        button.onClick.AddListener(() => clicked?.Invoke()); return button;
    }
    public Slider Slider(Transform parent, string name, float value, float min, float max, Action<float> changed)
    {
        var rect = Rect(parent, name, 560, 17, 340, 42); var slider = rect.gameObject.AddComponent<Slider>();
        var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        Image(rect, "Track", 0, 19, 340, 4, null, Raised);
        var area = Rect(rect, "FillArea", 0, 19, 340, 4);
        var fill = Image(area, "Fill", 0, 0, 340, 4, null, FillDim); Stretch(fill.rectTransform);
        var handleArea = Rect(rect, "HandleArea", 0, 0, 340, 42);
        var handle = Image(handleArea, "Handle", 0, 8, 6, 26, null, Ink); handle.raycastTarget = true;
        handle.rectTransform.pivot = new Vector2(.5f,.5f); handle.rectTransform.anchoredPosition = Vector2.zero; handle.rectTransform.sizeDelta = new Vector2(6,-16);
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.minValue = min; slider.maxValue = max; slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v => changed(v)); return slider;
    }
    // Segmented off/on control instead of the X box: both states stay readable at a glance.
    public Toggle Toggle(Transform parent, string name, bool value, Action<bool> changed, string offText, string onText)
    {
        var rect = Rect(parent, name, 560, 15, 240, 46); var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
        var background = Image(rect, "Box", 0, 0, 240, 46, null, Surface); background.raycastTarget = true;
        SurfaceColors(toggle);
        Image(rect, "OffMark", 4, 4, 114, 38, null, Raised);
        var mark = Image(rect, "Check", 122, 4, 114, 38, null, Raised);
        foreach (var (part, text, x) in new[] { ("Off", offText, 0f), ("On", onText, 120f) }) { var label = Label(rect, part, x, 0, 120, 46, text, 21); label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap; }
        toggle.targetGraphic = background; toggle.graphic = mark; toggle.SetIsOnWithoutNotify(value); mark.canvasRenderer.SetAlpha(value?1:0); SyncToggle(toggle);
        toggle.onValueChanged.AddListener(v => { SyncToggle(toggle); changed(v); }); return toggle;
    }
    public static void SyncToggle(Toggle toggle)
    {
        toggle.transform.Find("OffMark").GetComponent<Image>().canvasRenderer.SetAlpha(toggle.isOn ? 0 : 1);
        toggle.transform.Find("Off").GetComponent<TMP_Text>().color = toggle.isOn ? Faint : Ink; toggle.transform.Find("On").GetComponent<TMP_Text>().color = toggle.isOn ? Ink : Faint;
    }
    public TMP_Dropdown Dropdown(Transform parent, string name, string[] options, int value, Action<int> changed)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(560, -10); rect.sizeDelta = new Vector2(436, 56);
        foreach (var label in go.GetComponentsInChildren<TMP_Text>(true)) { label.font = font; label.fontSize = 25; label.fontStyle = FontStyles.Normal; label.color = Ink; label.enableAutoSizing = false; }
        var image = go.GetComponent<Image>(); image.sprite = null; image.color = Surface;
        var templateImage = go.transform.Find("Template").GetComponent<Image>(); templateImage.sprite = null; templateImage.color = new Color(.075f,.075f,.07f,.99f);
        var templateRect = (RectTransform)templateImage.transform; templateRect.sizeDelta = new Vector2(templateRect.sizeDelta.x,280);
        var itemRect = (RectTransform)go.transform.Find("Template/Viewport/Content/Item"); itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x,52);
        var contentRect = (RectTransform)itemRect.parent; contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x,52);
        // The default list paints light grey rows under light text; rows now stay dark and the checkmark is an accent bar.
        var item = itemRect.GetComponent<Toggle>(); SurfaceColors(item, true); item.targetGraphic.color = Surface; var tick = (RectTransform)itemRect.Find("Item Checkmark"); tick.GetComponent<Image>().color = Accent; tick.sizeDelta = new Vector2(4, 28); tick.anchoredPosition = new Vector2(4, 0);
        foreach (var bar in go.transform.Find("Template").GetComponentsInChildren<Scrollbar>(true)) { bar.GetComponent<Image>().color = Surface; bar.handleRect.GetComponent<Image>().color = FillDim; }
        var arrowImage = go.transform.Find("Arrow").GetComponent<Image>(); arrowImage.sprite = arrow; arrowImage.color = Muted;
        var dropdown = go.GetComponent<TMP_Dropdown>(); SurfaceColors(dropdown); dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
        var caption = dropdown.captionText.rectTransform; caption.offsetMin = new Vector2(18, caption.offsetMin.y); caption.offsetMax = new Vector2(-48, caption.offsetMax.y);
        dropdown.ClearOptions(); dropdown.AddOptions(new System.Collections.Generic.List<string>(options)); dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Math.Max(0, options.Length - 1))); dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(i => changed(i)); return dropdown;
    }
}
