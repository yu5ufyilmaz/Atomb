using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>A modal choice before the new-game transition starts.</summary>
public sealed class TutorialChoicePrompt : MonoBehaviour, ICancelHandler
{
    Action<bool> choose;
    Action cancel;
    GameObject previousSelection;
    Button yes;

    public void Show(Action<bool> onChoose, Action onCancel)
    {
        if (yes == null) Build();
        choose = onChoose; cancel = onCancel;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        EventSystem.current?.SetSelectedGameObject(yes.gameObject);
    }
    public void Hide()
    {
        choose = null; cancel = null; gameObject.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }
    void Select(bool tutorial) { var callback = choose; callback?.Invoke(tutorial); }
    void Cancel() { var callback = cancel; callback?.Invoke(); }
    public void OnCancel(BaseEventData eventData) => Cancel();
    void Update()
    {
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) Cancel();
    }

    void Build()
    {
        var style = Resources.Load<SettingsMenuStyle>("Settings/SettingsMenuStyle");
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 3000;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        gameObject.AddComponent<GraphicRaycaster>();
        var shade = style.Image(transform, "Backdrop", 0, 0, 1920, 1080, null, new Color(0, 0, 0, .78f));
        SettingsMenuStyle.Stretch(shade.rectTransform); shade.raycastTarget = true;
        var card = SettingsMenuStyle.Rect(transform, "Question", 0, 0, 840, 408);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f); card.anchoredPosition = Vector2.zero;
        card.gameObject.AddComponent<Image>().color = new Color(.035f, .043f, .036f, 1);
        var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.35f, .37f, .3f, .8f); outline.effectDistance = Vector2.one;
        TMP_Text Label(string name, string text, float y, float height, float size)
        {
            var label = style.Label(card, name, 48, y, 744, height, text, size);
            label.alignment = TextAlignmentOptions.Center; GameLocalization.Set(label, text); return label;
        }
        var eyebrow = Label("Eyebrow", "BEFORE YOU BEGIN", 36, 28, 18); eyebrow.color = new Color(.59f, .61f, .51f);
        var title = Label("Title", "Would you like to play the tutorial?", 83, 90, 34);
        title.font = style.headingFont != null ? style.headingFont : style.font;
        Label("Description", "Learn the controls and survival rules step by step.", 178, 46, 22);
        style.Image(card, "Divider", 48, 244, 744, 1, null, new Color(.32f, .34f, .28f));
        Button Answer(string name, string text, float x, Action clicked)
        {
            var button = style.Button(card, name, x, 268, 360, 64, text, clicked, 21);
            button.image.color = Color.white;
            var colors = button.colors;
            colors.normalColor = new Color(.14f, .17f, .13f, 1);
            colors.highlightedColor = new Color(.24f, .28f, .20f, 1);
            colors.selectedColor = new Color(.27f, .31f, .22f, 1);
            colors.pressedColor = new Color(.10f, .12f, .09f, 1);
            button.colors = colors;
            button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
            GameLocalization.Set(button.GetComponentInChildren<TMP_Text>(), text);
            return button;
        }
        yes = Answer("Yes", "YES, START THE TUTORIAL", 48, () => Select(true));
        var no = Answer("No", "NO, PLAY DIRECTLY", 432, () => Select(false));
        var back = style.Button(card, "Back", 270, 352, 300, 36, "BACK TO MENU", Cancel, 18);
        var backText = back.GetComponentInChildren<TMP_Text>(); backText.alignment = TextAlignmentOptions.Center; GameLocalization.Set(backText, "BACK TO MENU");
        var yesNav = yes.navigation; yesNav.mode = Navigation.Mode.Explicit; yesNav.selectOnRight = no; yesNav.selectOnDown = back; yes.navigation = yesNav;
        var noNav = no.navigation; noNav.mode = Navigation.Mode.Explicit; noNav.selectOnLeft = yes; noNav.selectOnDown = back; no.navigation = noNav;
        var backNav = back.navigation; backNav.mode = Navigation.Mode.Explicit; backNav.selectOnUp = yes; back.navigation = backNav;
    }
}
