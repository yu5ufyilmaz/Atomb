using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Play check: in every language each machine's control hints keep their words 10 px from the key picture,
/// never overlap and stay inside the bar. Screenshots go to Temp/ControlHints.</summary>
public static class ControlHintChecks
{
    [MenuItem("Senzora/Doğrulama/Kontrol İpuçları (Play)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Start Play at the title menu first.");
        Application.runInBackground = true; if (SaveManager.Instance != null) SaveManager.Instance.sessionOnly = true;
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().StartCoroutine(Checks());
    }

    static IEnumerator Checks()
    {
        string log = "", failure = null, original = GameLocalization.CurrentCode; Directory.CreateDirectory("Temp/ControlHints");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>(); var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        menu.PlayGame(); yield return new WaitForSecondsRealtime(.6f); menu.ChooseTutorial(false);
        float start = Time.realtimeSinceStartup;
        while (!GameManager.Instance.isGameStarted && Time.realtimeSinceStartup - start < 60) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null; }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(1);
        var controls = ControlsUIManager.Instance;
        foreach (var language in GameLocalization.Languages)
        foreach (ControlsUIManager.MachineType type in Enum.GetValues(typeof(ControlsUIManager.MachineType)))
        {
            if (type == ControlsUIManager.MachineType.Generic) continue;
            GameLocalization.SetLanguage(language.code); controls.ShowMachineUI(type); yield return new WaitForSecondsRealtime(.35f);
            var panel = (GameObject)typeof(ControlsUIManager).GetField("currentActivePanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controls);
            try { log += language.code + " " + type + ": " + Measure((RectTransform)panel.transform) + "\n"; }
            catch (Exception e) { failure = language.code + " " + type + ": " + e.Message; }
            ScreenCapture.CaptureScreenshot($"Temp/ControlHints/{language.code}-{type}.png"); yield return null;
            controls.HideControls(); yield return new WaitForSecondsRealtime(.2f);
            if (failure != null) break;
        }
        GameLocalization.SetLanguage(original);
        File.WriteAllText("Temp/control-hints.result", (failure == null ? "PASS " + DateTime.Now.ToString("s") : "FAIL " + failure) + "\n" + log);
    }

    static string Measure(RectTransform panel)
    {
        var spans = new System.Collections.Generic.List<(float left, float right, string name)>();
        foreach (Transform row in panel)
        {
            if (!row.gameObject.activeSelf || row.GetComponent<Image>() == null) continue;
            var icon = (RectTransform)row; var label = row.GetComponentInChildren<TMP_Text>(true);
            float iconLeft = X(panel, icon, icon.rect.xMin), iconRight = X(panel, icon, icon.rect.xMax), right = iconRight;
            if (label != null && label.text.Trim() != "/")
            {
                label.ForceMeshUpdate();
                float words = X(panel, label.rectTransform, label.textBounds.min.x);
                if (Mathf.Abs(words - iconRight - 10) > 2) throw new Exception($"'{label.text}' starts {words - iconRight:0} px from its key, not 10.");
                right = X(panel, label.rectTransform, label.textBounds.max.x);
            }
            if (iconLeft < panel.rect.xMin - 1 || right > panel.rect.xMax + 1) throw new Exception($"'{label?.text}' leaves the bar.");
            spans.Add((iconLeft, right, label != null ? label.text : row.name));
        }
        spans = spans.OrderBy(s => s.left).ToList();
        for (int i = 1; i < spans.Count; i++)
            if (spans[i].left < spans[i - 1].right + 1) throw new Exception($"'{spans[i - 1].name}' runs into '{spans[i].name}'.");
        return string.Join(" | ", spans.Select(s => s.name));
    }

    static float X(RectTransform panel, RectTransform rect, float x) => panel.InverseTransformPoint(rect.TransformPoint(new Vector3(x, 0, 0))).x;
}
