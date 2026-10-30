using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-time authoring migration. Runtime icons use serialized binding IDs, not sprite-name rules.
public static class ContentInputSetup
{
    [MenuItem("Senzora/İçerik Tablosu/Geliştirici/Tuş Görsellerini Bağla")]
    public static void BindIcons()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play modundan çıkın.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Önce açık sahneleri kaydedin.");
        var inputs = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ContentWorkbookProject.InputPath);
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); var scene = SceneManager.GetSceneByPath(path);
            bool temporary = !scene.IsValid() || !scene.isLoaded;
            if (temporary) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool changed = false;
            try
            {
                foreach (var controls in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ControlsUIManager>(true)))
                {
                    var panel = new SerializedObject(controls).FindProperty("mainCanvasObj").objectReferenceValue as GameObject;
                    if (panel == null) continue;
                    foreach (var image in panel.GetComponentsInChildren<Image>(true))
                    {
                        if (image.sprite == null || image.GetComponent<InputBindingIcon>() != null) continue;
                        string name = image.sprite.name, action = null, control = null;
                        bool book = image.GetComponentsInParent<Transform>(true).Any(t => t.name == "Book");
                        bool hiding = image.GetComponentsInParent<Transform>(true).Any(t => t.name == "HidingSpot");
                        switch (name)
                        {
                            case "F": action = "Cancel"; control = "<Keyboard>/f"; break;
                            case "Tab": action = "ToggleNotebook"; control = "<Keyboard>/tab"; break;
                            case "A": action = book ? "PreviousPage" : "DeviceLeft"; control = "<Keyboard>/a"; break;
                            case "D": action = book ? "NextPage" : "DeviceRight"; control = "<Keyboard>/d"; break;
                            case "W": action = "DeviceUp"; control = "<Keyboard>/w"; break;
                            case "S": action = "DeviceDown"; control = "<Keyboard>/s"; break;
                            case "Enter": action = "Confirm"; control = "<Keyboard>/enter"; break;
                            case "MouseScroll": action = "Scroll"; control = "<Mouse>/scroll"; break;
                            case "MouseLeftButton": action = hiding ? "Peek" : "Interact"; control = "<Mouse>/leftButton"; break;
                            case "MouseRightButton":
                                // These two old pictures contradicted the actual left-click behavior.
                                action = hiding ? "Peek" : book ? "Interact" : "SecondaryInteract";
                                control = hiding || book ? "<Mouse>/leftButton" : "<Mouse>/rightButton";
                                if (hiding || book) image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/UIButtons/MouseLeftButton.png");
                                break;
                        }
                        if (action == null) continue;
                        var target = inputs.FindAction("Player/" + action, true);
                        var binding = target.bindings.FirstOrDefault(b => b.path == control);
                        if (binding.id == Guid.Empty) continue;
                        var icon = image.gameObject.AddComponent<InputBindingIcon>(); icon.bindingID = binding.id.ToString(); icon.artworkControlPath = control;
                        EditorUtility.SetDirty(icon); changed = true; count++;
                    }
                }
                if (changed) { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Sahne kaydedilemedi: " + path); }
            }
            finally { if (temporary) EditorSceneManager.CloseScene(scene, true); }
        }
        Debug.Log("CONTENT INPUT ICONS PASS: " + count + " authored binding icons connected.");
    }
}
