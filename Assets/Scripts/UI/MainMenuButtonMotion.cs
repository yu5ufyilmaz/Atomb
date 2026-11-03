using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Title-menu hover: the thin label turns bold and slides right while the red trace fades in behind it.
/// Replaces the per-button Animator clips whose pixel offsets only fit the English words.</summary>
[RequireComponent(typeof(Button))]
public sealed class MainMenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text thin, bold;
    public Image trace;
    public float shift = 28, duration = .25f, padding = 16;
    Button button;
    RectTransform rect;
    float pose;
    bool inside;
    string fitted;

    void Awake() { button = GetComponent<Button>(); rect = (RectTransform)transform; Apply(); }
    void OnDisable() { inside = false; pose = 0; Apply(); }
    public void OnPointerEnter(PointerEventData data) { inside = true; }
    public void OnPointerExit(PointerEventData data) { inside = false; }

    void LateUpdate()
    {
        // Localization swaps the words at any time; the hover area follows the current word.
        if (!ReferenceEquals(fitted, bold.text)) Fit();
        float target = inside && button.IsInteractable() ? 1 : 0;
        if (pose == target) return;
        pose = Mathf.MoveTowards(pose, target, Time.unscaledDeltaTime / duration); Apply();
    }

    void Fit()
    {
        fitted = bold.text;
        float word = Mathf.Min(Mathf.Max(bold.GetPreferredValues(bold.text).x, thin.GetPreferredValues(thin.text).x), bold.rectTransform.rect.width);
        rect.sizeDelta = new Vector2(word + shift + padding, rect.sizeDelta.y);
    }

    void Apply()
    {
        float eased = 1 - (1 - pose) * (1 - pose);
        thin.alpha = 1 - eased; bold.alpha = eased;
        thin.rectTransform.anchoredPosition = new Vector2(shift * eased, thin.rectTransform.anchoredPosition.y);
        bold.rectTransform.anchoredPosition = new Vector2(shift * eased, bold.rectTransform.anchoredPosition.y);
        if (trace != null) { var color = trace.color; color.a = eased; trace.color = color; }
    }
}
