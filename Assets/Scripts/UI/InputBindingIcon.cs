using TMPro;
using UnityEngine;
using UnityEngine.UI;

// An authored keyboard/mouse picture keeps its artwork until the actual binding changes.
[RequireComponent(typeof(Image))]
public sealed class InputBindingIcon : MonoBehaviour
{
    public string bindingID;
    public string artworkControlPath;
    Image artwork;
    TMP_Text replacement;
    void OnEnable()
    {
        GameInput.BindingsChanged += Refresh;
        GameLocalization.LanguageChanged += Refresh;
        if (Application.isPlaying) Refresh();
    }
    void OnDisable()
    {
        GameInput.BindingsChanged -= Refresh;
        GameLocalization.LanguageChanged -= Refresh;
    }
    public void Refresh()
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(bindingID)) return;
        if (artwork == null) artwork = GetComponent<Image>();
        string actual = GameInput.BindingPath(bindingID);
        bool original = string.Equals(actual, artworkControlPath, System.StringComparison.OrdinalIgnoreCase);
        artwork.enabled = original;
        if (original) { if (replacement != null) replacement.gameObject.SetActive(false); return; }
        if (replacement == null)
        {
            var caption = GetComponentInChildren<TMP_Text>(true);
            var host = new GameObject("Current Binding", typeof(RectTransform), typeof(TextMeshProUGUI));
            host.transform.SetParent(transform, false);
            replacement = host.GetComponent<TMP_Text>();
            if (caption != null) { replacement.font = caption.font; replacement.color = caption.color; }
            replacement.alignment = TextAlignmentOptions.Center;
            replacement.enableAutoSizing = true; replacement.fontSizeMin = 10; replacement.fontSizeMax = 24;
            replacement.textWrappingMode = TextWrappingModes.NoWrap; replacement.raycastTarget = false;
            var rect = replacement.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
        replacement.gameObject.SetActive(true); replacement.text = GameInput.PathDisplayName(actual);
    }
}
