using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Turns the Animator-driven title-menu buttons into one left-aligned column driven by MainMenuButtonMotion.</summary>
public static class MainMenuButtonSetup
{
    static readonly string[] Scenes = { "scn_PressAnyKeyMenu", "scn_MainMenu", "MainMenu", "scn_labratory", "scn_labratoryfast", "scn_LaboratorySynthesis" };
    static readonly string[] Controllers = { "Assets/Animations/UI/NewGameAnims/PlayButton.controller", "Assets/Animations/UI/QuitAnims/QuitButton.controller" };
    const float BarInTrace = 86.25f, TraceGap = 21, BoldDrop = -1.1f, MaxWidth = 560, MinFont = 32, TextHeight = 90; // tall enough that 48 pt never auto-shrinks on height
    static readonly Vector2 TraceSize = new Vector2(381.5f, 55.5f); // ButtonSelected at half its pixel size, as the buttons drew it

    [MenuItem("Senzora/Ana Menü/Butonları Kur")]
    public static void Run()
    {
        try { string log = Setup(); File.WriteAllText("Temp/main-menu-buttons-setup.result", "PASS " + DateTime.Now.ToString("s") + "\n" + log); }
        catch (Exception error) { File.WriteAllText("Temp/main-menu-buttons-setup.result", "FAIL " + error); Debug.LogException(error); }
    }

    static string Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before setup.");
        var controllers = Controllers.Select(AssetDatabase.LoadAssetAtPath<AnimatorController>).ToArray();
        if (controllers.Any(c => c == null)) throw new InvalidDataException("Menu button controllers not found.");
        foreach (string name in Scenes)
        {
            var open = SceneManager.GetSceneByPath("Assets/Scenes/" + name + ".unity");
            if (open.IsValid() && open.isLoaded && open.isDirty) throw new InvalidOperationException("Save the open scene before setup: " + name);
        }
        Directory.CreateDirectory("Temp/MainMenuButtons/backups"); string log = "";
        foreach (string name in Scenes)
        {
            string path = "Assets/Scenes/" + name + ".unity"; if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var buttons = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true))
                    .Where(b => b.GetComponent<MainMenuButtonMotion>() != null || (b.TryGetComponent<Animator>(out var animator) && controllers.Contains(animator.runtimeAnimatorController))).ToArray();
                if (buttons.Length == 0) continue;
                string backup = "Temp/MainMenuButtons/backups/" + name + ".unity"; if (!File.Exists(backup)) File.Copy(path, backup);
                foreach (var column in buttons.GroupBy(b => b.transform.parent)) log += name + "/" + column.Key.name + ": " + Convert(column.OrderByDescending(b => ((RectTransform)b.transform).anchoredPosition.y).ToArray()) + "\n";
                EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        return log;
    }

    static string Convert(Button[] buttons)
    {
        // The column keeps the top button's current resting text edge and the list's outer rows.
        var parent = (RectTransform)buttons[0].transform.parent;
        float left = RestingLeft(buttons[0], parent);
        float top = ((RectTransform)buttons[0].transform).anchoredPosition.y, bottom = ((RectTransform)buttons[buttons.Length - 1].transform).anchoredPosition.y;
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i]; var rect = (RectTransform)button.transform; var image = button.GetComponent<Image>();
            var thin = rect.Find("TextThin").GetComponent<TMP_Text>(); var bold = rect.Find("TextBold").GetComponent<TMP_Text>();
            var animator = button.GetComponent<Animator>(); if (animator != null) UnityEngine.Object.DestroyImmediate(animator);
            // Most menu scenes stretch the buttons over the panel; a fixed anchor keeps the fitted width a real width.
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = TraceSize;
            var trace = rect.Find("Trace")?.GetComponent<Image>();
            if (trace == null)
            {
                trace = new GameObject("Trace", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); trace.transform.SetParent(rect, false); trace.transform.SetAsFirstSibling();
                trace.sprite = image.sprite;
            }
            trace.raycastTarget = false; trace.color = new Color(1, 1, 1, 0);
            var traceRect = trace.rectTransform; traceRect.anchorMin = traceRect.anchorMax = traceRect.pivot = new Vector2(0, .5f); traceRect.sizeDelta = TraceSize;
            image.sprite = null; image.color = Color.clear; image.raycastTarget = true;
            button.transition = Selectable.Transition.None; button.targetGraphic = image;
            rect.pivot = new Vector2(0, .5f); float y = buttons.Length > 1 ? Mathf.Lerp(top, bottom, i / (buttons.Length - 1f)) : top;
            rect.anchoredPosition = new Vector2(ToAnchored(parent, rect, left), y);
            foreach (var text in new[] { thin, bold })
            {
                var t = text.rectTransform; t.anchorMin = t.anchorMax = t.pivot = new Vector2(0, .5f); t.anchoredPosition = new Vector2(0, text == bold ? BoldDrop : 0); t.sizeDelta = new Vector2(MaxWidth, TextHeight);
                text.horizontalAlignment = HorizontalAlignmentOptions.Left; text.margin = Vector4.zero; text.textWrappingMode = TextWrappingModes.NoWrap;
                if (!text.enableAutoSizing) text.fontSizeMax = text.fontSize; text.fontSize = text.fontSizeMax; text.enableAutoSizing = true; text.fontSizeMin = MinFont; text.raycastTarget = false;
            }
            thin.alpha = 1; bold.alpha = 0;
            var motion = button.GetComponent<MainMenuButtonMotion>(); if (motion == null) motion = button.gameObject.AddComponent<MainMenuButtonMotion>();
            motion.thin = thin; motion.bold = bold; motion.trace = trace;
            traceRect.anchoredPosition = new Vector2(motion.shift - TraceGap - BarInTrace, 0);
            rect.sizeDelta = new Vector2(Mathf.Max(bold.GetPreferredValues(bold.text).x, thin.GetPreferredValues(thin.text).x) + motion.shift + motion.padding, rect.sizeDelta.y);
            EditorUtility.SetDirty(button.gameObject);
        }
        return buttons.Length + " buttons, left=" + left.ToString("0.0");
    }

    // Left edge of the visible resting word in the parent's space, as the player saw it before this setup.
    static float RestingLeft(Button button, RectTransform parent)
    {
        var thin = button.transform.Find("TextThin").GetComponent<TMP_Text>(); thin.ForceMeshUpdate(true, true);
        return parent.InverseTransformPoint(thin.transform.TransformPoint(new Vector3(thin.textBounds.min.x, 0, 0))).x;
    }

    // anchoredPosition.x that puts a left-pivot rect's pivot at parent-space x.
    static float ToAnchored(RectTransform parent, RectTransform rect, float x)
    {
        float anchor = parent.rect.xMin + parent.rect.width * rect.anchorMin.x;
        return x - anchor;
    }
}
