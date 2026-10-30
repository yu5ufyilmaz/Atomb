using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class SettingsMenuSetup
{
    public const string PrefabPath = "Assets/Resources/Settings/SettingsMenu.prefab";
    public const string StylePath = "Assets/Resources/Settings/SettingsMenuStyle.asset";
    [MenuItem("Senzora/Ayarlar/Menüyü Kur")]
    public static void Run()
    {
        try { Setup(); File.WriteAllText("Temp/settings-setup.result", "PASS " + DateTime.Now.ToString("s")); }
        catch (Exception error) { File.WriteAllText("Temp/settings-setup.result", "FAIL " + error); Debug.LogException(error); }
    }
    static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before setup.");
        Directory.CreateDirectory("Assets/Resources/Settings"); AssetDatabase.Refresh();
        var style = AssetDatabase.LoadAssetAtPath<SettingsMenuStyle>(StylePath);
        if (style == null) { style = ScriptableObject.CreateInstance<SettingsMenuStyle>(); AssetDatabase.CreateAsset(style, StylePath); }
        string font = "Assets/Sprites/UI/MainMenu/Font/static/";
        style.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(font + "DMSans-Medium SDF.asset");
        style.headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(font + "DMSans-Thin SDF.asset");
        if (style.font == null || style.headingFont == null) throw new InvalidDataException("Existing DM Sans fonts were not found.");
        string art = "Assets/Sprites/UI/UI3/SettingsMenuScreen/";
        Sprite Sprite(string name)
        {
            string path = art + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Missing settings artwork: " + path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidDataException("Settings artwork could not be loaded: " + path);
            return sprite;
        }
        style.barInside = Sprite("BarInside"); style.barOutside = Sprite("BarOutside"); style.box = Sprite("Box"); style.check = Sprite("x"); style.arrow = Sprite("QualityArrow"); style.qualityFrame = Sprite("QualityFrame");
        style.mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/MainMixer.mixer");
        style.voice = style.mixer.FindMatchingGroups("Voice").SingleOrDefault(g => g.name == "Voice"); style.effects = style.mixer.FindMatchingGroups("SFX").Single(g => g.name == "SFX");
        if (style.voice == null) throw new InvalidDataException("Voice mixer group was not imported.");
        EditorUtility.SetDirty(style); AssetDatabase.SaveAssetIfDirty(style);
        var temp = new GameObject("SettingsMenu", typeof(RectTransform)); temp.SetActive(false);
        try { SettingsMenuStyle.Stretch((RectTransform)temp.transform); temp.AddComponent<SettingsMenuView>().style = style; temp.SetActive(true); PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath); }
        finally { UnityEngine.Object.DestroyImmediate(temp); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        string[] scenes = { "scn_PressAnyKeyMenu", "scn_MainMenu", "MainMenu", "scn_labratory", "scn_labratoryfast", "scn_LaboratorySynthesis" };
        foreach (string name in scenes)
        {
            string path = "Assets/Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save the open scene before setup: " + path);
        }
        Directory.CreateDirectory("Temp/SettingsMenu/backups");
        foreach (string name in scenes)
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var menu in roots.SelectMany(r => r.GetComponentsInChildren<MainMenuManager>(true)))
                {
                    var serialized = new SerializedObject(menu); var reference = serialized.FindProperty("settingsPanel");
                    if (reference.objectReferenceValue != null) continue;
                    var main = serialized.FindProperty("mainMenuPanel").objectReferenceValue as GameObject;
                    var canvas = main != null ? main.GetComponentInParent<Canvas>() : menu.GetComponentInParent<Canvas>(); if (canvas == null) continue;
                    var panel = new GameObject("SettingsPanel", typeof(RectTransform)); panel.SetActive(false); panel.transform.SetParent(canvas.transform, false);
                    SettingsMenuStyle.Stretch((RectTransform)panel.transform); panel.AddComponent<SettingsUIManager>(); reference.objectReferenceValue = panel; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var adapters = roots.SelectMany(r => r.GetComponentsInChildren<SettingsUIManager>(true)).ToArray();
                foreach (var adapter in adapters)
                {
                    foreach (Transform child in adapter.transform) if (child.GetComponent<SettingsMenuView>() == null) child.gameObject.SetActive(false);
                    var view = adapter.GetComponentInChildren<SettingsMenuView>(true);
                    if (view == null) { var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, adapter.transform); child.SetActive(true); }
                    if (adapter.transform is RectTransform rect) { rect.localScale = Vector3.one; SettingsMenuStyle.Stretch(rect); }
                }
                foreach (var manager in roots.SelectMany(r => r.GetComponentsInChildren<SettingsManager>(true))) { manager.mainAudioMixer = style.mixer; manager.voiceGroup = style.voice; manager.effectsGroup = style.effects; }
                foreach (var subtitles in roots.SelectMany(r => r.GetComponentsInChildren<GlobalSubtitleManager>(true))) subtitles.EnsurePresentationPanel();
                var voiceSources = new HashSet<AudioSource>(roots.SelectMany(r => r.GetComponentsInChildren<DialogAction>(true)).Where(d => d.audioSource != null).Select(d => d.audioSource));
                foreach (var source in roots.SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)))
                    if (voiceSources.Contains(source)) source.outputAudioMixerGroup = style.voice;
                    else if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = style.effects;
                foreach (var system in roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)))
                {
                    foreach (var old in system.GetComponents<StandaloneInputModule>()) UnityEngine.Object.DestroyImmediate(old);
                    var module = system.GetComponent<InputSystemUIInputModule>(); if (module == null) module = system.gameObject.AddComponent<InputSystemUIInputModule>(); module.AssignDefaultActions();
                }
                string backup = "Temp/SettingsMenu/backups/" + name + ".unity"; if (!File.Exists(backup)) File.Copy(path, backup);
                EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                Debug.Log("SETTINGS SETUP: " + name + ", panels=" + adapters.Length);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets(); Debug.Log("SETTINGS MENU SETUP PASS");
    }
}
