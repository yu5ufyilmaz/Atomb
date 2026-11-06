using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pointer hook on an interactive control: hover tick on enter, click (or tab) tick on left press (pointer down fires before
// the button's own handler can close the panel and deactivate this hook). Attached by UISoundPlayer or the
// "Senzora/UI/Tık Seslerini Bağla" menu; builders pass UISoundKind.Tab for section/tab buttons.
public sealed class UISoundOnPointer : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    public UISoundKind click = UISoundKind.Click;
    Selectable selectable;

    public static UISoundOnPointer Attach(GameObject target, UISoundKind? click = null)
    {
        var hook = target.GetComponent<UISoundOnPointer>();
        if (hook == null) hook = target.AddComponent<UISoundOnPointer>();
        if (click.HasValue) hook.click = click.Value;
        return hook;
    }
    void Awake() { selectable = GetComponent<Selectable>(); }
    bool Ready => selectable == null || selectable.IsInteractable();
    public void OnPointerEnter(PointerEventData data) { if (Ready) UISoundPlayer.Play(UISoundKind.Hover); }
    public void OnPointerDown(PointerEventData data) { if (Ready && data.button == PointerEventData.InputButton.Left) UISoundPlayer.Play(click); }
}
