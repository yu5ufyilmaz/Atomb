using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>HUD polish in the gameplay scenes, keeping its icons and places:
/// rings get a faint track and fade when idle, the flashlight hint shows the real (right) mouse button,
/// both key hints follow rebinding, and the Tab hint says what it opens.</summary>
public static class HudSetup
{
    static readonly string[] Scenes = { "scn_labratory", "scn_labratoryfast", "scn_LaboratorySynthesis" };
    static readonly string[] Rings = { "StamineBar", "HandLightBar", "PressureBar" };

    [MenuItem("Senzora/HUD/Göstergeleri Düzenle")]
    public static void Run()
    {
        try { string log = Setup(); File.WriteAllText("Temp/hud-setup.result", "PASS " + DateTime.Now.ToString("s") + "\n" + log); }
        catch (Exception error) { File.WriteAllText("Temp/hud-setup.result", "FAIL " + error); Debug.LogException(error); }
    }

    static string Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before setup.");
        foreach (string name in Scenes)
        {
            var open = SceneManager.GetSceneByPath("Assets/Scenes/" + name + ".unity");
            if (open.IsValid() && open.isLoaded && open.isDirty) throw new InvalidOperationException("Save the open scene before setup: " + name);
        }
        var inputs = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ContentWorkbookProject.InputPath);
        var rightButton = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/ControlIcons/MouseRightButton.png");
        if (inputs == null || rightButton == null) throw new InvalidDataException("Input actions or MouseRightButton sprite missing.");
        Directory.CreateDirectory("Temp/Hud/backups"); string log = "";
        foreach (string name in Scenes)
        {
            string path = "Assets/Scenes/" + name + ".unity"; if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).FirstOrDefault(t => t.name == "PROGRESSBARS");
                if (hud == null) continue;
                string backup = "Temp/Hud/backups/" + name + ".unity"; if (!File.Exists(backup)) File.Copy(path, backup);
                log += name + ": " + Convert(hud, inputs, rightButton) + "\n";
                EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        return log;
    }

    static string Convert(RectTransform hud, InputActionAsset inputs, Sprite rightButton)
    {
        int rings = 0;
        foreach (string name in Rings)
        {
            var bar = hud.Find(name); if (bar == null) continue;
            var ring = bar.GetComponent<Image>();
            var track = hud.Find(name + "Track")?.GetComponent<Image>();
            if (track == null)
            {
                track = new GameObject(name + "Track", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                track.transform.SetParent(hud, false); track.transform.SetSiblingIndex(bar.GetSiblingIndex());
            }
            var from = (RectTransform)bar; var to = track.rectTransform;
            to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot; to.anchoredPosition = from.anchoredPosition; to.sizeDelta = from.sizeDelta;
            track.sprite = ring.sprite; track.type = Image.Type.Simple; track.color = new Color(1, 1, 1, .14f); track.raycastTarget = false;
            if (bar.GetComponent<CanvasGroup>() == null) bar.gameObject.AddComponent<CanvasGroup>();
            var fade = bar.GetComponent<HudGaugeFade>(); if (fade == null) fade = bar.gameObject.AddComponent<HudGaugeFade>();
            fade.ring = ring; fade.track = track; rings++;
        }
        // The flashlight charges on ChargeFlashlight (right mouse); the old picture showed the left button.
        var charge = hud.Find("PressureButton")?.GetComponent<Image>();
        if (charge != null) { charge.sprite = rightButton; Bind(charge, inputs, "ChargeFlashlight", "<Mouse>/rightButton"); }
        var tab = hud.Find("TabButton")?.GetComponent<Image>();
        if (tab != null)
        {
            Bind(tab, inputs, "ToggleNotebook", "<Keyboard>/tab");
            var label = tab.GetComponentInChildren<TMP_Text>(true);
            // The key picture already names the key; the words say what it opens. Left-aligned beside the icon so longer words never overlap it.
            label.text = "Notebook"; label.horizontalAlignment = HorizontalAlignmentOptions.Left; label.fontSize = 20;
            var r = label.rectTransform; float iconRight = tab.rectTransform.sizeDelta.x * .5f + 10;
            r.anchoredPosition = new Vector2(iconRight + r.sizeDelta.x * .5f, r.anchoredPosition.y);
        }
        return rings + " rings, flashlight hint " + (charge != null) + ", tab hint " + (tab != null);
    }

    static void Bind(Image image, InputActionAsset inputs, string action, string control)
    {
        var binding = inputs.FindAction("Player/" + action, true).bindings.FirstOrDefault(b => b.path == control);
        if (binding.id == Guid.Empty) throw new InvalidDataException(action + " has no " + control + " binding.");
        var icon = image.GetComponent<InputBindingIcon>(); if (icon == null) icon = image.gameObject.AddComponent<InputBindingIcon>();
        icon.bindingID = binding.id.ToString(); icon.artworkControlPath = control; EditorUtility.SetDirty(icon);
    }
}
