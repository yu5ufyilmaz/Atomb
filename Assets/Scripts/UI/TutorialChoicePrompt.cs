using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>New Game asks in the title menu's own column: the menu words step aside and the choice takes their rows,
/// so the room behind the menu stays in view (3 Nov). Lives under the menu panel and copies its buttons.</summary>
public sealed class TutorialChoicePrompt : MonoBehaviour
{
    const float Slide = 36, Duration = .45f;
    Action<bool> choose;
    Action cancel;
    CanvasGroup choice;
    CanvasGroup[] menuGroups;
    RectTransform[] menuRows;
    float[] menuX;
    float shown;
    bool open, returnToMenu = true;

    public void Show(Action<bool> onChoose, Action onCancel)
    {
        if (choice == null) Build();
        choose = onChoose; cancel = onCancel; open = true; returnToMenu = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Apply();
    }
    // After an answer the choice stays on screen and fades out with the whole menu.
    public void Hide(bool backToMenu = true)
    {
        choose = null; cancel = null; open = false; returnToMenu = backToMenu;
        if (choice != null) Apply();
    }
    void Select(bool tutorial) { var callback = choose; callback?.Invoke(tutorial); }
    void Cancel() { var callback = cancel; callback?.Invoke(); }

    void Update()
    {
        if (open && ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))) Cancel();
        float target = open ? 1 : returnToMenu ? 0 : shown;
        if (choice == null || shown == target) return;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / Duration); Apply();
    }

    // Sequential, not a crossfade: both columns share rows, so overlapping words would smear into each other.
    void Apply()
    {
        float menuOut = Smooth(Mathf.Clamp01(shown * 2)), choiceIn = Smooth(Mathf.Clamp01(shown * 2 - 1));
        bool menuReady = !open && shown <= 0;
        for (int i = 0; i < menuRows.Length; i++)
        {
            menuGroups[i].alpha = 1 - menuOut; menuGroups[i].interactable = menuGroups[i].blocksRaycasts = menuReady;
            menuRows[i].anchoredPosition = new Vector2(menuX[i] - Slide * menuOut, menuRows[i].anchoredPosition.y);
        }
        choice.alpha = choiceIn; choice.interactable = choice.blocksRaycasts = open;
        ((RectTransform)transform).anchoredPosition = new Vector2(Slide * (1 - choiceIn), 0);
    }
    static float Smooth(float t) => t * t * (3 - 2 * t);

    void Build()
    {
        var rect = (RectTransform)transform; SettingsMenuStyle.Stretch(rect);
        choice = gameObject.AddComponent<CanvasGroup>();
        var buttons = transform.parent.GetComponentsInChildren<MainMenuButtonMotion>(true).Where(b => !b.transform.IsChildOf(transform))
            .OrderByDescending(b => ((RectTransform)b.transform).anchoredPosition.y).ToArray();
        menuRows = buttons.Select(b => (RectTransform)b.transform).ToArray(); menuX = menuRows.Select(r => r.anchoredPosition.x).ToArray();
        menuGroups = buttons.Select(b => b.TryGetComponent(out CanvasGroup group) ? group : b.gameObject.AddComponent<CanvasGroup>()).ToArray();
        float x = menuX[0];
        float Row(int i) => menuRows[Mathf.Min(i, menuRows.Length - 1)].anchoredPosition.y;

        // The question is the menu's thin word, smaller and quieter, on the first row.
        var question = Instantiate(buttons[0].thin, transform); question.name = "Question";
        if (question.TryGetComponent(out LocalizedText copied)) DestroyImmediate(copied);
        var q = question.rectTransform; q.anchorMin = q.anchorMax = new Vector2(.5f, .5f); q.pivot = new Vector2(0, .5f); q.anchoredPosition = new Vector2(x, Row(0));
        question.fontSize = question.fontSizeMax = 30; question.fontSizeMin = 22; question.alpha = 1; question.color = new Color(.72f, .69f, .64f);
        GameLocalization.Set(question, "First time in the laboratory?");

        Answer("Yes", "START WITH GUIDANCE", Row(1), () => Select(true));
        Answer("No", "START DIRECTLY", Row(2), () => Select(false));
        Answer("Back", "BACK", Row(menuRows.Length - 1), Cancel);

        void Answer(string name, string text, float y, Action clicked)
        {
            var copy = Instantiate(buttons[0].gameObject, transform); copy.name = name;
            var row = (RectTransform)copy.transform; row.anchoredPosition = new Vector2(x, y);
            var button = copy.GetComponent<Button>(); button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(() => clicked());
            var motion = copy.GetComponent<MainMenuButtonMotion>();
            foreach (var label in new[] { motion.thin, motion.bold })
            {
                if (label.TryGetComponent(out LocalizedText binding)) binding.key = null; // the copy must not keep "New Game"
                GameLocalization.Set(label, text);
            }
        }
    }
}
