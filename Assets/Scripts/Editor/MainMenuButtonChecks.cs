using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Play check: in every language the title-menu words share one left edge, the red trace keeps the same gap
/// to the hovered word, and the hover area covers the whole word.</summary>
public static class MainMenuButtonChecks
{
    const float BarInTrace = 86.25f, TraceGap = 21;
    static void Require(bool condition, string message) { if (!condition) throw new Exception("MAIN MENU BUTTONS: " + message); }

    [MenuItem("Senzora/Doğrulama/Ana Menü Butonları (Play)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Start Play at the title menu first.");
        Application.runInBackground = true; if (SaveManager.Instance != null) SaveManager.Instance.sessionOnly = true;
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().StartCoroutine(Checks());
    }

    static IEnumerator Checks()
    {
        string log = "", failure = null, original = GameLocalization.CurrentCode; Directory.CreateDirectory("Temp/MainMenuButtons");
        var buttons = UnityEngine.Object.FindObjectsByType<MainMenuButtonMotion>(FindObjectsSortMode.None).Where(b => b.isActiveAndEnabled)
            .OrderByDescending(b => b.transform.position.y).ToArray();
        var data = new PointerEventData(EventSystem.current);
        foreach (string code in GameLocalization.Languages.Select(l => l.code))
        {
            GameLocalization.SetLanguage(code); yield return null; yield return null;
            try
            {
                Require(buttons.Length >= 4, "title menu buttons missing: " + buttons.Length);
                var lefts = buttons.Select(b => Left(b.thin, b.transform.parent)).ToArray();
                Require(lefts.Max() - lefts.Min() < 1, code + " resting words are not on one left edge: " + string.Join(", ", lefts.Select(l => l.ToString("0.0"))));
            }
            catch (Exception e) { failure = e.Message; }
            if (failure != null) break;
            foreach (var button in buttons)
            {
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler); yield return new WaitForSecondsRealtime(.45f);
                try
                {
                    var parent = button.transform.parent; float word = Left(button.bold, parent), right = Right(button.bold, parent);
                    float bar = parent.InverseTransformPoint(button.trace.rectTransform.TransformPoint(new Vector3(button.trace.rectTransform.rect.xMin + BarInTrace, 0, 0))).x;
                    var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners); float hitRight = parent.InverseTransformPoint(corners[2]).x;
                    log += $"{code} {button.name} '{button.bold.text}' word={word:0.0}..{right:0.0} bar={bar:0.0} hit..{hitRight:0.0} size={button.bold.fontSize:0}\n";
                    Require(button.bold.color.a > .95f && button.thin.color.a < .05f, button.name + " hover must show only the bold label.");
                    Require(Mathf.Abs(word - bar - TraceGap) < 1, $"{code} {button.name} trace gap {word - bar:0.0} instead of {TraceGap}.");
                    Require(right <= hitRight + .5f, $"{code} {button.name} word ends outside its hover area.");
                    // A stretched anchor once turned the fitted width into screen width plus the word.
                    float hitLeft = parent.InverseTransformPoint(corners[0]).x, hitHeight = parent.InverseTransformPoint(corners[1]).y - parent.InverseTransformPoint(corners[0]).y;
                    Require(hitRight - hitLeft <= right - word + 2 * button.shift + button.padding + 2 && hitHeight > 40 && hitHeight < 80, $"{code} {button.name} hover area {hitRight - hitLeft:0}x{hitHeight:0} does not match its word.");
                    Require(!button.bold.isTextOverflowing, $"{code} {button.name} label overflows.");
                }
                catch (Exception e) { failure = e.Message; }
                if (button.name == "PlayButton" || button.name == "Credits") { ScreenCapture.CaptureScreenshot($"Temp/MainMenuButtons/{code}-{button.name}.png"); yield return null; }
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler); yield return new WaitForSecondsRealtime(.45f);
                if (failure == null && !(button.thin.color.a > .95f && button.bold.color.a < .05f)) failure = button.name + " leaving must restore the thin label.";
                if (failure != null) break;
            }
            if (failure != null) break;
        }
        GameLocalization.SetLanguage(original);
        File.WriteAllText("Temp/main-menu-buttons.result", (failure == null ? "PASS " + DateTime.Now.ToString("s") : "FAIL " + failure) + "\n" + log);
    }

    static float Left(TMP_Text text, Transform parent) { text.ForceMeshUpdate(); return parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(text.textBounds.min.x, 0, 0))).x; }
    static float Right(TMP_Text text, Transform parent) { text.ForceMeshUpdate(); return parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(text.textBounds.max.x, 0, 0))).x; }
}
