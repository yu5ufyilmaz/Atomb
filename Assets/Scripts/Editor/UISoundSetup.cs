using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adds the UI tick hook to every Selectable in the open scenes (inactive ones too). Idempotent: existing hooks are kept.
// At runtime UISoundPlayer also sweeps new controls, so this only makes scene-authored buttons explicit.
public static class UISoundSetup
{
    [MenuItem("Senzora/UI/Tık Seslerini Bağla")]
    public static void Attach()
    {
        int added = 0, kept = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded) continue;
            foreach (var selectable in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Selectable>(true)))
            {
                if (selectable.GetComponent<UISoundOnPointer>() != null) { kept++; continue; }
                var hook = Undo.AddComponent<UISoundOnPointer>(selectable.gameObject);
                hook.click = selectable.name.StartsWith("Tab") ? UISoundKind.Tab : UISoundKind.Click; added++;
            }
        }
        if (added > 0) EditorSceneManager.SaveOpenScenes();
        Debug.Log("UI SOUND SETUP: " + added + " hook added, " + kept + " already present.");
    }
}
