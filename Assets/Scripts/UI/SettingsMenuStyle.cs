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
        var rect = Rect(parent, name, 700, 16, 350, 42); var slider = rect.gameObject.AddComponent<Slider>();
        var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        Image(rect, "Frame", 0, 5, 350, 31, barOutside, new Color(1,1,1,.8f));
        var area = Rect(rect, "FillArea", 0, 10, 350, 21);
        var fill = Image(area, "Fill", 0, 0, 350, 21, barInside, new Color(1,1,1,.8f)); Stretch(fill.rectTransform);
        var handleArea = Rect(rect, "HandleArea", 0, 0, 350, 42);
        var handle = Image(handleArea, "Handle", 0, 4, 4, 34, null, new Color(.9f,.89f,.82f)); handle.raycastTarget = true;
        handle.rectTransform.pivot = new Vector2(.5f,.5f); handle.rectTransform.anchoredPosition = Vector2.zero; handle.rectTransform.sizeDelta = new Vector2(4,-8);
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.minValue = min; slider.maxValue = max; slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v => changed(v)); return slider;
    }
    public Toggle Toggle(Transform parent, string name, bool value, Action<bool> changed)
    {
        var rect = Rect(parent, name, 1030, 14, 48, 48); var toggle = rect.gameObject.AddComponent<Toggle>(); toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
        var background = Image(rect, "Box", 2, 2, 40, 40, box, new Color(.9f,.9f,.86f)); background.raycastTarget = true;
        var mark = Image(rect, "Check", 9, 9, 26, 26, check, Color.white);
        toggle.targetGraphic = background; toggle.graphic = mark; toggle.SetIsOnWithoutNotify(value); mark.canvasRenderer.SetAlpha(value?1:0); toggle.onValueChanged.AddListener(v => changed(v)); return toggle;
    }
    public TMP_Dropdown Dropdown(Transform parent, string name, string[] options, int value, Action<int> changed)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(650, -10); rect.sizeDelta = new Vector2(430, 58);
        foreach (var label in go.GetComponentsInChildren<TMP_Text>(true)) { label.font = font; label.fontSize = 27; label.fontStyle = FontStyles.Normal; label.color = new Color(.9f,.9f,.86f); label.enableAutoSizing = false; }
        var image = go.GetComponent<Image>(); image.sprite = qualityFrame; image.color = new Color(1,1,1,.6f);
        var templateImage = go.transform.Find("Template").GetComponent<Image>(); templateImage.sprite = null; templateImage.color = new Color(.055f,.065f,.055f,.99f);
        var templateRect = (RectTransform)templateImage.transform; templateRect.sizeDelta = new Vector2(templateRect.sizeDelta.x,280);
        var itemRect = (RectTransform)go.transform.Find("Template/Viewport/Content/Item"); itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x,52);
        var contentRect = (RectTransform)itemRect.parent; contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x,52);
        var arrowImage = go.transform.Find("Arrow").GetComponent<Image>(); arrowImage.sprite = arrow; arrowImage.color = Color.white;
        var dropdown = go.GetComponent<TMP_Dropdown>(); dropdown.captionText.alignment = TextAlignmentOptions.Center;
        dropdown.ClearOptions(); dropdown.AddOptions(new System.Collections.Generic.List<string>(options)); dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Math.Max(0, options.Length - 1))); dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(i => changed(i)); return dropdown;
    }
}
