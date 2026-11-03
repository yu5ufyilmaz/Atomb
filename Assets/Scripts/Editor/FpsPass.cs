using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

// FPS paketi 1: aynı rotada önce/sonra ölç, araya içerik ayarlarını uygula (oynanış koduna dokunmaz).
public static class FpsPass
{
    [MenuItem("Senzora/FPS/Measure Before (Play)")] private static void Before() => Measure("before");
    [MenuItem("Senzora/FPS/Measure After (Play)")] private static void After() => Measure("after");

    private static void Measure(string label)
    {
        if (!Application.isPlaying || GameManager.Instance == null || SaveManager.Instance == null)
            throw new InvalidOperationException("Laboratuvarda Play gerekli.");
        SaveManager.Instance.sessionOnly = true; // Yeni Oyun yolu gerçek kaydı silmesin
        GameManager.Instance.StartCoroutine(MeasureFlow(label));
    }

    private static IEnumerator MeasureFlow(string label)
    {
        yield return ResearchGuidanceChecks.EnterNormalGame();
        if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = true;
        if (PressureSystemManager.Instance != null) PressureSystemManager.Instance.isSystemActive = false;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        var player = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        var body = player.GetComponent<CharacterController>();
        var spots = new List<(string name, Vector3 pos)> { ("Spawn", player.transform.position) };
        foreach (var room in Object.FindObjectsByType<RoomManager>(FindObjectsSortMode.InstanceID))
        {
            var c = room.GetComponent<BoxCollider>().bounds.center;
            spots.Add((room.name, new Vector3(c.x, player.transform.position.y, c.z)));
        }
        var all = new List<float>(); var lines = new List<string>(); var timings = new FrameTiming[1];
        // "track": shader varyantlarını üç kalite seviyesinde de görmek için rotayı her seviyede yürü.
        foreach (int quality in label == "track" ? new[] { 0, 1, 2 } : new[] { -1 })
        {
        if (quality >= 0) QualitySettings.SetQualityLevel(quality, true);
        foreach (var spot in spots)
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                body.enabled = false;
                player.transform.SetPositionAndRotation(spot.pos, Quaternion.Euler(0, yaw, 0));
                body.enabled = true; Physics.SyncTransforms();
                for (int i = 0; i < 20; i++) yield return null; // ısınma, streaming ve cached gölgeler otursun
                var cpu = new List<float>(); var gpu = new List<float>();
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    cpu.Add(Time.unscaledDeltaTime * 1000f);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add((float)timings[0].gpuFrameTime);
                }
                all.AddRange(cpu);
                lines.Add($"{spot.name}/{yaw}: frame median {Median(cpu):0.00} ms, p95 {P95(cpu):0.00} ms, gpu median {(gpu.Count > 0 ? Median(gpu).ToString("0.00") : "n/a")} ms");
            }
        }
        string summary = $"FPS {label.ToUpper()}: {spots.Count * 4} views, frame median {Median(all):0.00} ms ({1000f / Median(all):0} fps), p95 {P95(all):0.00} ms, worst {all.Max():0.00} ms";
        Directory.CreateDirectory("Temp");
        File.WriteAllLines($"Temp/fps-{label}.txt", lines.Prepend(summary));
        Debug.Log(summary + $" -> Temp/fps-{label}.txt");
    }

    private static float Median(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
    private static float P95(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s[Mathf.Min(s.Count - 1, (int)(s.Count * .95f))]; }

    [MenuItem("Senzora/FPS/Apply Pass 1")]
    private static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/scn_LaboratorySynthesis.unity")
            throw new InvalidOperationException("scn_LaboratorySynthesis açık ve Play kapalı olmalı.");

        // 1) Standard->HDRP dönüşümünden kalan 0,4 mm'lik pixel displacement görünmüyor ama her pikselde ray-march yapıyor.
        int materials = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null || mat.shader.name != "HDRP/Lit" || !mat.HasProperty("_DisplacementMode")) continue;
            if (Mathf.RoundToInt(mat.GetFloat("_DisplacementMode")) != 2) continue;
            mat.SetFloat("_DisplacementMode", 0);
            HDShaderUtils.ResetMaterialKeywords(mat);
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat); materials++;
        }

        // 2) Kitap Animator'ları kapak hareketini zaten elle örnekliyor; görünmezken idle pozu basmasın.
        int animators = 0;
        foreach (var animator in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Animator>(true)))
        {
            var controller = animator.runtimeAnimatorController;
            if (controller == null || (controller.name != "newBookAnim" && controller.name != "BookModel")) continue;
            if (animator.cullingMode == AnimatorCullingMode.CullCompletely) continue;
            Undo.RecordObject(animator, "FPS pass"); animator.cullingMode = AnimatorCullingMode.CullCompletely; animators++;
        }

        // 3) Her kare gölge çizen sahne spotları (fener hariç) uzaktayken gölge haritası üretmesin.
        int lights = 0;
        foreach (var data in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HDAdditionalLightData>(true)))
        {
            var light = data.GetComponent<Light>();
            if (light == null || light.shadows == LightShadows.None || data.shadowUpdateMode != ShadowUpdateMode.EveryFrame) continue;
            if (data.name.IndexOf("Flash", StringComparison.OrdinalIgnoreCase) >= 0 || data.shadowFadeDistance <= 20) continue;
            Undo.RecordObject(data, "FPS pass"); data.shadowFadeDistance = 20; lights++;
        }

        // 4) Volumetrik fog High -> Medium.
        string fog = "yok";
        foreach (var volume in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>(true)))
            if (volume.sharedProfile != null && volume.sharedProfile.TryGet(out Fog f) && f.quality.value > (int)ScalableSettingLevelParameter.Level.Medium)
            {
                f.quality.value = (int)ScalableSettingLevelParameter.Level.Medium;
                EditorUtility.SetDirty(volume.sharedProfile); AssetDatabase.SaveAssetIfDirty(volume.sharedProfile); fog = volume.sharedProfile.name;
            }

        // SaveAssets değil: Play'de dinamik TMP atlaslarına eklenen glifler de kaydedilirdi.
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log($"FPS PASS 1 APPLIED: {materials} displacement materials, {animators} book animators, {lights} shadow fade lights, fog medium: {fog}");
    }

    // Kaynakta kalan ama shader'ın okumadığı texture kayıtları (Standard'dan dönüşüm artığı) build'e girip VRAM'de yer tutuyor.
    [MenuItem("Senzora/FPS/Pass 3 - Clean Unused Material Textures")]
    private static void CleanMaterialTextures()
    {
        int materials = 0, slots = 0, heights = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader") continue;
            var so = new SerializedObject(mat);
            var envs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            int removed = 0;
            for (int i = envs.arraySize - 1; i >= 0; i--)
            {
                var entry = envs.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("first").stringValue;
                bool hasTexture = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue != null;
                // Displacement kapalıyken HDRP Lit height map'i örneklemiyor.
                bool unusedHeight = name == "_HeightMap" && mat.shader.name == "HDRP/Lit" && mat.HasProperty("_DisplacementMode") && Mathf.RoundToInt(mat.GetFloat("_DisplacementMode")) == 0;
                if (!hasTexture || (mat.shader.FindPropertyIndex(name) >= 0 && !unusedHeight)) continue;
                if (unusedHeight) { entry.FindPropertyRelative("second.m_Texture").objectReferenceValue = null; heights++; }
                else envs.DeleteArrayElementAtIndex(i);
                removed++;
            }
            if (removed == 0) continue;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (mat.shader.name == "HDRP/Lit") HDShaderUtils.ResetMaterialKeywords(mat);
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
            materials++; slots += removed;
        }
        Debug.Log($"FPS PASS 3 MATERIALS: {materials} materials, {slots} texture slots cleared ({heights} unused height maps)");
    }

    // Shader'lar ilk kullanımda derlenip ilk oynayışta 100–330 ms takılma yapıyor: kullanılanları topla, açılışta ısıt.
    private static readonly Type ShaderUtilType = typeof(ShaderUtil);
    private const string VariantsPath = "Assets/Settings/SenzoraShaderVariants.shadervariants";
    [MenuItem("Senzora/FPS/Pass 3 - Begin Shader Tracking")]
    private static void BeginTracking()
    {
        ShaderUtilType.GetMethod("ClearCurrentShaderVariantCollection", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Debug.Log("SHADER TRACKING CLEARED: şimdi Play'de 'Senzora/FPS/Pass 3 - Track Route (Play)' çalıştır.");
    }
    [MenuItem("Senzora/FPS/Pass 3 - Track Route (Play)")] private static void TrackRoute() => Measure("track");
    [MenuItem("Senzora/FPS/Pass 3 - Save Shader Variants And Preload")]
    private static void SaveTracking()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play kapalı olmalı.");
        int count = (int)ShaderUtilType.GetMethod("GetCurrentShaderVariantCollectionVariantCount", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        ShaderUtilType.GetMethod("SaveCurrentShaderVariantCollection", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { VariantsPath });
        AssetDatabase.ImportAsset(VariantsPath);
        var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantsPath);
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var preloaded = graphics.FindProperty("m_PreloadedShaders");
        bool present = false;
        for (int i = 0; i < preloaded.arraySize; i++) present |= preloaded.GetArrayElementAtIndex(i).objectReferenceValue == collection;
        if (!present) { preloaded.arraySize++; preloaded.GetArrayElementAtIndex(preloaded.arraySize - 1).objectReferenceValue = collection; graphics.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
        Debug.Log($"SHADER VARIANTS SAVED: {count} variants, {collection.shaderCount} shaders -> {VariantsPath}, preloaded={!present || present}");
    }

    // Tüm 2048'lik texture'lar aynı anda VRAM'de (2,1–2,9 GB ölçüldü); uzak/görünmeyen yüzeyler düşük mip ile kalsın.
    [MenuItem("Senzora/FPS/Pass 3 - Enable Texture Streaming")]
    private static void EnableStreaming()
    {
        int current = QualitySettings.GetQualityLevel();
        int[] budgets = { 2048, 1536, 1024 }; // High Fidelity, Balanced, Performant
        for (int q = 0; q < QualitySettings.names.Length; q++)
        {
            QualitySettings.SetQualityLevel(q, false);
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = budgets[Mathf.Min(q, budgets.Length - 1)];
        }
        QualitySettings.SetQualityLevel(current, false);
        AssetDatabase.SaveAssets();
        int textures = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || !importer.mipmapEnabled || importer.streamingMipmaps) continue;
                importer.streamingMipmaps = true; importer.SaveAndReimport(); textures++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        File.WriteAllText("Temp/streaming-done.txt", textures.ToString());
        Debug.Log($"TEXTURE STREAMING: quality budgets 2048/1536/1024 MB, {textures} textures marked");
    }

    // Builds/ .gitignore'da. Frame Timing Stats yalnız bu build için açılır, sonra eski haline döner.
    [MenuItem("Senzora/FPS/Build Dev Bench")]
    private static void BuildDevBench() => BuildDevBench("Builds/DevBench/Senzora.exe", true);

    // Çökme deneyi: aynı bench, incremental GC kapalı (ProjectSettings build sonrası eski haline döner).
    [MenuItem("Senzora/FPS/Build Dev Bench (GC Incremental Off)")]
    private static void BuildDevBenchGcOff() => BuildDevBench("Builds/DevBench-GCOff/Senzora.exe", false);

    private static void BuildDevBench(string location, bool incrementalGc)
    {
        bool timing = PlayerSettings.enableFrameTimingStats;
        bool gc = PlayerSettings.gcIncremental;
        try
        {
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.gcIncremental = incrementalGc;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = location,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            File.WriteAllText("Temp/devbench-build.txt", $"{location} gcIncremental={incrementalGc} {report.summary.result} {report.summary.totalTime} errors={report.summary.totalErrors} size={report.summary.totalSize / 1048576} MB");
            Debug.Log($"DEV BENCH BUILD: {report.summary.result} in {report.summary.totalTime}");
        }
        finally { PlayerSettings.enableFrameTimingStats = timing; PlayerSettings.gcIncremental = gc; AssetDatabase.SaveAssets(); } // build kaydettiği geçici değerleri diske geri yaz
    }

    [MenuItem("Senzora/FPS/Apply Pass 2")]
    private static void ApplyPass2()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/scn_LaboratorySynthesis.unity")
            throw new InvalidOperationException("scn_LaboratorySynthesis açık ve Play kapalı olmalı.");

        // 1) fillAmount her kare değişiyor; ayrı canvas, kök HUD canvas'ının tamamını yeniden batch'letmesin.
        var bars = new[] { "StamineBar", "HandLightBar", "PressureBar" };
        int canvases = 0;
        foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)))
            if (bars.Contains(t.name) && t.GetComponent<Canvas>() == null) { Undo.AddComponent<Canvas>(t.gameObject); canvases++; }

        // 2) Uzun müzik/ambiyans/loop'lar açılışta ham PCM'e açılıp RAM'de tutuluyor ve ilk Play'de çözülüyordu.
        var streamed = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || clip.length < 30f || !(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming) continue;
            settings.loadType = AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            streamed.Add($"{Path.GetFileName(path)} ({clip.length:0}s)");
        }

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log($"FPS PASS 2 APPLIED: {canvases} HUD bar canvases, streaming: {string.Join(", ", streamed)}");
    }
}
