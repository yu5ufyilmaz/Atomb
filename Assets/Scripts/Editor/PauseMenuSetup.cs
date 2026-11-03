using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Rebuilds the ESC menu as the title menu's column: copies of the title buttons on the same rows, keeping each
/// original button's click wiring, with a quiet "Game Paused" line on the first row.</summary>
public static class PauseMenuSetup
{
    static readonly string[] Scenes = { "scn_labratory", "scn_labratoryfast", "scn_LaboratorySynthesis" };

    [MenuItem("Senzora/Ana Menü/Duraklatma Menüsünü Kur")]
    public static void Run()
    {
        try { string log = Setup(); File.WriteAllText("Temp/pause-menu-setup.result", "PASS " + DateTime.Now.ToString("s") + "\n" + log); }
        catch (Exception error) { File.WriteAllText("Temp/pause-menu-setup.result", "FAIL " + error); Debug.LogException(error); }
    }

    static string Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before setup.");
        foreach (string name in Scenes)
        {
            var open = SceneManager.GetSceneByPath("Assets/Scenes/" + name + ".unity");
            if (open.IsValid() && open.isLoaded && open.isDirty) throw new InvalidOperationException("Save the open scene before setup: " + name);
        }
        Directory.CreateDirectory("Temp/PauseMenu/backups"); string log = "";
        foreach (string name in Scenes)
        {
            string path = "Assets/Scenes/" + name + ".unity"; if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var pause = roots.SelectMany(r => r.GetComponentsInChildren<PauseManager>(true)).FirstOrDefault();
                var menu = roots.SelectMany(r => r.GetComponentsInChildren<MainMenuManager>(true)).FirstOrDefault();
                if (pause == null || menu == null) continue;
                var panel = new SerializedObject(pause).FindProperty("pauseMenuPanel").objectReferenceValue as GameObject;
                var titlePanel = new SerializedObject(menu).FindProperty("mainMenuPanel").objectReferenceValue as GameObject;
                if (panel == null || titlePanel == null) continue;
                string backup = "Temp/PauseMenu/backups/" + name + ".unity"; if (!File.Exists(backup)) File.Copy(path, backup);
                log += name + ": " + Convert((RectTransform)panel.transform, titlePanel) + "\n";
                EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        return log;
    }

    static string Convert(RectTransform panel, GameObject titlePanel)
    {
        var rows = titlePanel.GetComponentsInChildren<MainMenuButtonMotion>(true).Select(b => (RectTransform)b.transform).OrderByDescending(r => r.anchoredPosition.y).ToArray();
        if (rows.Length < 4) throw new InvalidDataException("Title menu column not set up; run Senzora/Ana Menü/Butonları Kur first.");
        var template = rows[0].GetComponent<MainMenuButtonMotion>();
        // Same screen rows as the title menu: both panels share the UI canvas.
        Vector2 Row(int i) => panel.InverseTransformPoint(rows[i].TransformPoint(Vector3.zero));
        var old = panel.Cast<Transform>().Where(t => t.GetComponent<Button>() != null && t.GetComponent<MainMenuButtonMotion>() == null)
            .OrderByDescending(t => ((RectTransform)t).anchoredPosition.y).ToArray();
        if (old.Length == 0) return "already converted";
        for (int i = 0; i < old.Length; i++)
        {
            var original = old[i].GetComponent<Button>(); var label = old[i].GetComponentInChildren<TMP_Text>(true);
            var copy = (GameObject)UnityEngine.Object.Instantiate(template.gameObject, panel); copy.name = old[i].name;
            var rect = (RectTransform)copy.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = Row(i + 1);
            var button = copy.GetComponent<Button>(); var wiring = new SerializedObject(button);
            wiring.CopyFromSerializedProperty(new SerializedObject(original).FindProperty("m_OnClick")); wiring.ApplyModifiedPropertiesWithoutUndo();
            var motion = copy.GetComponent<MainMenuButtonMotion>(); motion.thin.text = motion.bold.text = label != null ? label.text : copy.name;
            rect.sizeDelta = new Vector2(Mathf.Max(motion.bold.GetPreferredValues(motion.bold.text).x, motion.thin.GetPreferredValues(motion.thin.text).x) + motion.shift + motion.padding, rect.sizeDelta.y);
            UnityEngine.Object.DestroyImmediate(old[i].gameObject);
        }
        // The artwork title only had an English picture; a text line on the first row localizes like the rest.
        var artwork = panel.Find("GamePaused"); if (artwork != null) UnityEngine.Object.DestroyImmediate(artwork.gameObject);
        var title = UnityEngine.Object.Instantiate(template.thin, panel); title.name = "Title";
        var t = title.rectTransform; t.anchorMin = t.anchorMax = new Vector2(.5f, .5f); t.pivot = new Vector2(0, .5f); t.anchoredPosition = Row(0);
        title.text = "Game Paused"; title.fontStyle = FontStyles.UpperCase; title.characterSpacing = 6;
        title.fontSize = title.fontSizeMax = 26; title.fontSizeMin = 20; title.color = new Color(.62f, .6f, .56f); title.alpha = 1;
        title.transform.SetAsFirstSibling();
        return old.Length + " buttons";
    }
}
