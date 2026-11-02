using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
}
