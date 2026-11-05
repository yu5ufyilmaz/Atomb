using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

// 1940-50'ler eski kamera / yıpranmış film görünümü: tek obje + tek profil; Kaldır menüsü ikisini de siler.
public static class VintageCameraLookSetup
{
    const string Name = "VintageCameraLook";
    const string ProfilePath = "Assets/Scenes/scn_LaboratorySynthesis/VintageCameraLook.asset";
    const string PendingKey = "VintageCameraLook.Preview";

    static void RequireEditScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path != LaboratorySynthesisSetup.ScenePath)
            throw new InvalidOperationException("scn_LaboratorySynthesis açık ve Play kapalı olmalı.");
    }

    static GameObject FindRoot() => Array.Find(EditorSceneManager.GetActiveScene().GetRootGameObjects(), g => g.name == Name);

    static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;
        component = profile.Add<T>();
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile); // Add() alone leaves the component unsaved
        return component;
    }

    static void Drop<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component)) return;
        profile.Remove<T>();
        AssetDatabase.RemoveObjectFromAsset(component);
        Object.DestroyImmediate(component, true);
    }

    [MenuItem("Senzora/Kamera/Eski Kamera Görünümü Kur")]
    public static void Install()
    {
        RequireEditScene();
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }

        // Components from earlier versions: drop them from the asset, not just from the list.
        Drop<ColorAdjustments>(profile); Drop<LiftGammaGain>(profile); Drop<SplitToning>(profile); Drop<PaniniProjection>(profile);
        var bloom = Get<Bloom>(profile); bloom.active = true;
        bloom.threshold.Override(0.8f); bloom.intensity.Override(0.45f); bloom.scatter.Override(0.9f); bloom.tint.Override(Color.white);
        var lens = Get<LensDistortion>(profile); lens.active = true;
        // HDRP: positive intensity = barrel (tan curve in UberPost DistortUV), negative = pincushion.
        lens.intensity.Override(0.12f); lens.xMultiplier.Override(1f); lens.yMultiplier.Override(1f); lens.center.Override(new Vector2(0.5f, 0.5f)); lens.scale.Override(1.03f);
        var aberration = Get<ChromaticAberration>(profile); aberration.active = true;
        aberration.intensity.Override(0.15f);
        // Custom tier: the quality-tier sample count (6) draws the spectral LUT as stepped RGB bands.
        aberration.quality.Override(ScalableSettingLevelParameter.LevelCount);
        var samples = new SerializedObject(aberration);
        samples.FindProperty("m_MaxSamples.m_OverrideState").boolValue = true; samples.FindProperty("m_MaxSamples.m_Value").intValue = 24;
        samples.ApplyModifiedPropertiesWithoutUndo();
        var grain = Get<FilmGrain>(profile); grain.active = true;
        grain.type.Override(FilmGrainLookup.Large01); grain.intensity.Override(0.35f); grain.response.Override(0.6f);
        var vignette = Get<Vignette>(profile); vignette.active = true;
        vignette.intensity.Override(0.28f); vignette.smoothness.Override(0.7f); vignette.roundness.Override(1f); vignette.rounded.Override(false); vignette.color.Override(Color.black);
        foreach (var c in profile.components) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); // SaveAssets would also flush unrelated dirty TMP font atlases

        var go = FindRoot() ?? new GameObject(Name);
        go.layer = 0; go.SetActive(true);
        var volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 10; volume.weight = 1; volume.sharedProfile = profile;
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go); // the earlier VintageCameraInstability component
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log($"ESKİ KAMERA GÖRÜNÜMÜ KURULDU: {Name} (global, öncelik 10) -> {ProfilePath}, {profile.components.Count} efekt");
    }

    [MenuItem("Senzora/Kamera/Eski Kamera Görünümünü Kaldır")]
    public static void Remove()
    {
        RequireEditScene();
        var go = FindRoot();
        if (go != null) Object.DestroyImmediate(go);
        bool asset = AssetDatabase.DeleteAsset(ProfilePath);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log($"ESKİ KAMERA GÖRÜNÜMÜ KALDIRILDI: obje {(go != null ? "silindi" : "yoktu")}, profil {(asset ? "silindi" : "yoktu")}");
    }

    // Outside Play: arm, enter Play, and start the capture once the scene is running.
    [MenuItem("Senzora/Kamera/Eski Kamera Görünümü Önizle")]
    public static void Preview()
    {
        if (Application.isPlaying) { BeginPreview(); return; }
        RequireEditScene();
        if (FindRoot() == null) throw new InvalidOperationException("Önce Eski Kamera Görünümü Kur.");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void WatchPlay() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.EraseBool(PendingKey);
        BeginPreview();
    };

    static void BeginPreview()
    {
        if (GameManager.Instance == null || SaveManager.Instance == null) throw new InvalidOperationException("Laboratuvarda Play gerekli.");
        SaveManager.Instance.sessionOnly = true; // Yeni Oyun yolu gerçek kaydı silmesin
        GameManager.Instance.StartCoroutine(PreviewFlow());
    }

    static IEnumerator PreviewFlow()
    {
        var look = FindRoot();
        if (look == null) throw new InvalidOperationException(Name + " sahnede yok.");
        yield return ResearchGuidanceChecks.EnterNormalGame(false);
        var player = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        float deadline = Time.realtimeSinceStartup + 60;
        while (player.IsFrozen && Time.realtimeSinceStartup < deadline) yield return null;
        if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = true;
        if (PressureSystemManager.Instance != null) PressureSystemManager.Instance.isSystemActive = false;
        if (BreakerBox.Instance != null) BreakerBox.Instance.isSystemActive = false;

        var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        var sizeIndex = view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var oldSize = sizeIndex.GetValue(view);
        PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "VintageLook");
        string dir = $"outputs/vintage-look/{DateTime.Now:yyyyMMdd-HHmm}";
        Directory.CreateDirectory(dir);
        float yaw = player.IntendedLookYaw;
        foreach (bool on in new[] { true, false })
        {
            look.SetActive(on);
            for (int shot = 0; shot < 3; shot++)
            {
                for (int i = 0; i < 30; i++) { player.ForceCameraRotation(yaw + shot * 120, 8f); yield return null; }
                ScreenCapture.CaptureScreenshot($"{dir}/{(on ? "on" : "off")}_{shot + 1}.png");
                yield return null; yield return null;
            }
        }
        look.SetActive(true);
        Debug.Log($"ESKİ KAMERA ÖNİZLEME BİTTİ: 6 kare {Screen.width}x{Screen.height} -> {dir}");
        sizeIndex.SetValue(view, oldSize);
        EditorApplication.isPlaying = false;
    }
}
