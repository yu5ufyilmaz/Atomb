using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// Işık bakımı: bake durumunu raporla, aynı rotada önce/sonra görüntü al, sahnenin kendi lightmap ve occlusion verisini üret.
public static class LightingBakeChecks
{
    const string ScenePath = "Assets/Scenes/scn_LaboratorySynthesis.unity";
    const string SceneFolder = "Assets/Scenes/scn_LaboratorySynthesis";
    const string Out = "outputs/lighting";

    static void RequireEditScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("scn_LaboratorySynthesis açık ve Play kapalı olmalı.");
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static IEnumerable<T> All<T>() where T : Component =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    static bool Lightmapped(MeshRenderer r) =>
        GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI) && r.receiveGI == ReceiveGI.Lightmaps;
    static bool HasLightmap(Renderer r) => r.lightmapIndex >= 0 && r.lightmapIndex < 0xFFFE;

    [MenuItem("Senzora/Işık/Denetim Raporu")]
    public static void Audit()
    {
        RequireEditScene();
        Directory.CreateDirectory(Out);
        var lines = new List<string>();
        Lightmapping.TryGetLightingSettings(out var s);
        lines.Add($"LightingSettings: {(s != null ? AssetDatabase.GetAssetPath(s) : "yok")}");
        if (s != null)
            lines.Add($"  backend {s.lightmapper}, mixed {s.mixedBakeMode}, directional {s.directionalityMode}, res {s.lightmapResolution}/m, max {s.lightmapMaxSize}, padding {s.lightmapPadding}, compression {s.lightmapCompression}, AO {s.ao} ({s.aoMaxDistance} m), samples direct/indirect/env {s.directSampleCount}/{s.indirectSampleCount}/{s.environmentSampleCount}, bounces {s.maxBounces}, filter {s.filteringMode}, denoiser {s.denoiserTypeDirect}/{s.denoiserTypeIndirect}/{s.denoiserTypeAO}");
        lines.Add($"LightingData: {AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)}, lightmaps {LightmapSettings.lightmaps.Length}, mode {LightmapSettings.lightmapsMode}, probes {(LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0)}");
        var hdrp = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
        if (hdrp != null) lines.Add($"HDRP {hdrp.name}: shadowmask {hdrp.currentPlatformRenderPipelineSettings.supportShadowMask}, probe system {hdrp.currentPlatformRenderPipelineSettings.lightProbeSystem}");
        lines.Add($"Occlusion: data {StaticOcclusionCulling.umbraDataSize} B, smallest occluder {StaticOcclusionCulling.smallestOccluder}, smallest hole {StaticOcclusionCulling.smallestHole}, backface {StaticOcclusionCulling.backfaceThreshold}");
        foreach (var sky in All<StaticLightingSky>())
            lines.Add($"StaticLightingSky: {PathOf(sky.transform)} profile {(sky.profile != null ? AssetDatabase.GetAssetPath(sky.profile) : "yok")} sky id {sky.staticLightingSkyUniqueID} active {sky.isActiveAndEnabled}");

        lines.Add(""); lines.Add("LIGHTS");
        foreach (var light in All<Light>().OrderBy(l => PathOf(l.transform)))
        {
            var hd = light.GetComponent<HDAdditionalLightData>();
            var source = PrefabUtility.GetCorrespondingObjectFromSource(light.gameObject);
            var baked = light.bakingOutput;
            lines.Add($"  {PathOf(light.transform)} | {(light.gameObject.activeInHierarchy && light.enabled ? "on" : "OFF")} | {light.type} {light.lightmapBakeType} | {light.intensity:0.#} | range {light.range:0.#} | shadows {light.shadows}"
                + (hd != null ? $" {hd.shadowUpdateMode} res {(hd.shadowResolution.useOverride ? "o" + hd.shadowResolution.@override : "L" + hd.shadowResolution.level)} vol {hd.volumetricDimmer:0.##} fade {hd.shadowFadeDistance:0}" : "")
                + $" | baked {baked.isBaked} {baked.lightmapBakeType} {baked.mixedLightingMode}" + (source != null ? $" | prefab {source.name}" : ""));
        }
        var lights = All<Light>().ToList();
        lines.Add($"  total {lights.Count}: " + string.Join(", ", lights.GroupBy(l => l.lightmapBakeType).Select(g => $"{g.Key} {g.Count()}")) + $"; Mixed/Baked not in bake: {lights.Count(l => l.lightmapBakeType != LightmapBakeType.Realtime && !l.bakingOutput.isBaked)}");

        var renderers = All<MeshRenderer>().ToList();
        var mapped = renderers.Where(Lightmapped).ToList();
        var missing = mapped.Where(r => r.gameObject.activeInHierarchy && r.enabled && !HasLightmap(r)).ToList();
        var noUv2 = mapped.Where(r => { var m = r.GetComponent<MeshFilter>()?.sharedMesh; return m != null && !m.HasVertexAttribute(VertexAttribute.TexCoord1); }).ToList();
        var noMesh = mapped.Where(r => r.GetComponent<MeshFilter>()?.sharedMesh == null).ToList();
        var flags = renderers.Select(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject)).ToList();
        lines.Add(""); lines.Add($"RENDERERS {renderers.Count}: lightmapped {mapped.Count} (missing lightmap {missing.Count}, no UV2 {noUv2.Count}, no mesh {noMesh.Count}), probe-lit GI static {renderers.Count(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI) && r.receiveGI == ReceiveGI.LightProbes)}, dynamic {flags.Count(f => !f.HasFlag(StaticEditorFlags.ContributeGI))}");
        lines.Add($"  occluder static {flags.Count(f => f.HasFlag(StaticEditorFlags.OccluderStatic))}, occludee static {flags.Count(f => f.HasFlag(StaticEditorFlags.OccludeeStatic))}, batching static {flags.Count(f => f.HasFlag(StaticEditorFlags.BatchingStatic))}");
        // Tek obje tek atlas sayfasına sığmak zorunda; sığmayan obje küçültülür ve bulanıklaşır.
        float res = s != null ? s.lightmapResolution : 64;
        foreach (var r in mapped.OrderByDescending(Area).Take(8))
            lines.Add($"  largest: {PathOf(r.transform)} {Area(r):0} m², needs {Mathf.Sqrt(Area(r) * r.scaleInLightmap) * res:0}² texels (scale {r.scaleInLightmap}), lightmap {r.lightmapIndex} scaleOffset {r.lightmapScaleOffset}");
        foreach (var r in missing.Take(60)) lines.Add($"  missing lightmap: {PathOf(r.transform)}");
        foreach (var r in noUv2) lines.Add($"  no UV2: {PathOf(r.transform)} ({r.GetComponent<MeshFilter>().sharedMesh.name})");
        // Statik işaretli ama oyunda hareket eden objeler bake'te ve occlusion'da yanlış yerde kalır.
        foreach (var r in renderers.Where(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject) != 0 && (r.GetComponentInParent<Animator>(true) != null || r.GetComponentInParent<Rigidbody>(true) != null || r.GetComponentInParent<IInteractable>(true) != null)).Take(80))
            lines.Add($"  static but movable/interactive: {PathOf(r.transform)} flags {GameObjectUtility.GetStaticEditorFlags(r.gameObject)}");

        lines.Add(""); lines.Add("PROBES");
        foreach (var g in All<LightProbeGroup>()) lines.Add($"  probe group {PathOf(g.transform)}: {g.probePositions.Length} probes, active {g.gameObject.activeInHierarchy}");
        foreach (var p in All<HDProbe>()) lines.Add($"  reflection {PathOf(p.transform)}: {p.mode}, active {p.isActiveAndEnabled}, baked {(p.bakedTexture != null ? AssetDatabase.GetAssetPath(p.bakedTexture) : "yok")}");

        File.WriteAllLines(Out + "/audit.txt", lines);
        Debug.Log($"LIGHTING AUDIT: {lights.Count} lights, {mapped.Count} lightmapped, {missing.Count} missing -> {Out}/audit.txt");
    }

    static float Area(MeshRenderer r)
    {
        var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null) return 0;
        var v = mesh.vertices; var t = mesh.triangles; var m = r.transform.localToWorldMatrix; float area = 0;
        for (int i = 0; i + 2 < t.Length; i += 3)
            area += Vector3.Cross(m.MultiplyPoint3x4(v[t[i + 1]]) - m.MultiplyPoint3x4(v[t[i]]), m.MultiplyPoint3x4(v[t[i + 2]]) - m.MultiplyPoint3x4(v[t[i]])).magnitude * .5f;
        return area;
    }

    // UV2'si olmayan lightmap'li mesh'lerde Unity doku UV'sini (UV0) kullanır: çakışan ya da 0-1 dışına taşan UV bake'te leke ve sızıntı yapar.
    [MenuItem("Senzora/Işık/UV2 Analizi")]
    public static void UvAudit()
    {
        RequireEditScene();
        var lines = new List<string> { "model | mesh | renderers | uv0 bounds | overlap % | generateSecondaryUV" };
        foreach (var g in All<MeshRenderer>().Where(Lightmapped).Select(r => r.GetComponent<MeshFilter>()?.sharedMesh)
                     .Where(m => m != null && !m.HasVertexAttribute(VertexAttribute.TexCoord1)).GroupBy(m => m))
        {
            var mesh = g.Key; string path = AssetDatabase.GetAssetPath(mesh);
            var uv = mesh.uv; var tris = mesh.triangles;
            var min = uv.Length > 0 ? new Vector2(uv.Min(v => v.x), uv.Min(v => v.y)) : Vector2.zero;
            var max = uv.Length > 0 ? new Vector2(uv.Max(v => v.x), uv.Max(v => v.y)) : Vector2.zero;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            lines.Add($"{path} | {mesh.name} | {g.Count()} | ({min.x:0.##},{min.y:0.##})-({max.x:0.##},{max.y:0.##}) | {Overlap(uv, tris):0.0} | {(importer != null ? importer.generateSecondaryUV.ToString() : "not a model")}");
        }
        Directory.CreateDirectory(Out);
        File.WriteAllLines(Out + "/uv.txt", lines);
        Debug.Log($"LIGHTING UV AUDIT: {lines.Count - 1} meshes -> {Out}/uv.txt");
    }

    // Kaplanan 256² texel merkezlerinden birden fazla üçgene düşenlerin oranı (0-1 dışı UV sarılarak sayılır).
    static float Overlap(Vector2[] uv, int[] tris)
    {
        const int N = 256; var hits = new byte[N * N];
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            Vector2 a = uv[tris[t]] * N, b = uv[tris[t + 1]] * N, c = uv[tris[t + 2]] * N;
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            if (Mathf.Abs(area) < 1e-6f) continue;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y));
            if ((x1 - x0) * (y1 - y0) > N * N * 4) continue;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float w0 = ((b.x - p.x) * (c.y - p.y) - (c.x - p.x) * (b.y - p.y)) / area;
                    float w1 = ((c.x - p.x) * (a.y - p.y) - (a.x - p.x) * (c.y - p.y)) / area;
                    if (w0 <= 0 || w1 <= 0 || w0 + w1 >= 1) continue;
                    int i = ((y % N + N) % N) * N + (x % N + N) % N;
                    if (hits[i] < 255) hits[i]++;
                }
        }
        int covered = hits.Count(h => h > 0), multi = hits.Count(h => h > 1);
        return covered == 0 ? 0 : 100f * multi / covered;
    }

    // Doku UV'si çakışan modellere ayrı lightmap UV'si üret (hiçbir materyal UV1 okumuyor, doku görünümü değişmez).
    [MenuItem("Senzora/Işık/Lightmap UV Üret")]
    public static void GenerateLightmapUvs()
    {
        RequireEditScene();
        var paths = All<MeshRenderer>().Where(Lightmapped).Select(r => r.GetComponent<MeshFilter>()?.sharedMesh)
            .Where(m => m != null && !m.HasVertexAttribute(VertexAttribute.TexCoord1)).Select(AssetDatabase.GetAssetPath).Distinct().ToList();
        var lines = new List<string>();
        foreach (var path in paths)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) { lines.Add("skip (not a model): " + path); continue; }
            var clock = Stopwatch.StartNew();
            importer.generateSecondaryUV = true;
            importer.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
            importer.secondaryUVMinLightmapResolution = 32; // bina 2048'lik sayfaya ~32 texel/m ile sığıyor
            importer.SaveAndReimport();
            lines.Add($"UV2 on: {path} ({clock.Elapsed.TotalSeconds:0} s)");
        }
        Directory.CreateDirectory(Out);
        File.WriteAllLines(Out + "/uv-generate.txt", lines.Prepend($"UV GENERATE DONE: {paths.Count} models"));
        Debug.Log($"LIGHTMAP UV: {paths.Count} models");
    }

    // Açılıp kapanan ya da hareket eden objeler occluder olarak bake'lenirse açık kapının arkası görünmez olur.
    [MenuItem("Senzora/Işık/Occlusion Hazırlığı")]
    public static void PrepareOcclusion()
    {
        RequireEditScene();
        const StaticEditorFlags occlusion = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
        var lines = new List<string>();
        var marked = All<MeshRenderer>().Where(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & occlusion) != 0).ToList();
        foreach (var r in marked.Where(r => r.GetComponentInParent<Animator>(true) != null || r.GetComponentInParent<Rigidbody>(true) != null).ToList())
        {
            Undo.RecordObject(r.gameObject, "Occlusion prep");
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~occlusion);
            lines.Add("cleared (moves): " + PathOf(r.transform));
            marked.Remove(r);
        }

        // Kalan occluder'lara dışarıdan referans veren script/timeline'ları listele: SetActive ile kapatılan duvar bake'te kalır.
        var refs = new Dictionary<GameObject, List<string>>();
        void Add(GameObject go, string by) { if (!refs.TryGetValue(go, out var l)) refs[go] = l = new List<string>(); l.Add(by); }
        foreach (var mb in All<MonoBehaviour>())
        {
            if (mb == null) continue;
            var it = new SerializedObject(mb).GetIterator();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var go = it.objectReferenceValue is GameObject g ? g : (it.objectReferenceValue as Component)?.gameObject;
                    if (go != null && go.scene.IsValid() && !mb.transform.IsChildOf(go.transform) && !go.transform.IsChildOf(mb.transform))
                        Add(go, $"{mb.GetType().Name}.{it.propertyPath} @ {PathOf(mb.transform)}");
                }
        }
        foreach (var d in All<UnityEngine.Playables.PlayableDirector>())
            if (d.playableAsset != null)
                foreach (var output in d.playableAsset.outputs)
                {
                    var b = d.GetGenericBinding(output.sourceObject);
                    var go = b is GameObject g ? g : (b as Component)?.gameObject;
                    if (go != null) Add(go, $"Timeline {d.playableAsset.name}/{output.streamName} @ {PathOf(d.transform)}");
                }
        foreach (var r in marked.Where(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.OccluderStatic)))
            for (var t = r.transform; t != null; t = t.parent)
                if (refs.TryGetValue(t.gameObject, out var by))
                {
                    lines.Add($"referenced occluder: {PathOf(r.transform)} via {t.name} <- {string.Join("; ", by.Distinct().Take(4))}");
                    break;
                }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory(Out);
        File.WriteAllLines(Out + "/occlusion-prep.txt", lines.Prepend($"OCCLUSION PREP: {lines.Count(l => l.StartsWith("cleared"))} cleared, {marked.Count} still marked"));
        Debug.Log($"OCCLUSION PREP -> {Out}/occlusion-prep.txt");
    }

    [MenuItem("Senzora/Işık/Görüntü Turu Önce (Play)")] static void TourBefore() => StartTour("before");
    [MenuItem("Senzora/Işık/Görüntü Turu Sonra (Play)")] static void TourAfter() => StartTour("after");

    static void StartTour(string label)
    {
        if (!Application.isPlaying || GameManager.Instance == null || GameManager.Instance.isGameStarted)
            throw new InvalidOperationException("Ana menüde taze Play gerekli.");
        var menu = Object.FindFirstObjectByType<MainMenuManager>();
        menu.StartCoroutine(Tour(menu, label));
    }

    // Her oda merkezinde dört yön: görüntü + kare süresi. Önce/sonra aynı rota, aynı kalite.
    static IEnumerator Tour(MainMenuManager menu, string label)
    {
        string dir = $"{Out}/{label}";
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
        File.WriteAllText(dir + "/frames.txt", "RUNNING\n");
        Application.runInBackground = true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        SaveManager.Instance.sessionOnly = true; // Yeni Oyun yolu gerçek kaydı silmesin
        var game = GameManager.Instance;
        var movement = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        menu.PlayGame(); yield return null;
        menu.ChooseTutorial(false);
        float deadline = Time.realtimeSinceStartup + 120;
        while ((!game.isGameStarted || movement.IsFrozen) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!game.isGameStarted || movement.IsFrozen) { File.AppendAllText(dir + "/frames.txt", "FAIL oyun başlamadı\n"); yield break; }
        if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = true;
        if (PressureSystemManager.Instance != null) PressureSystemManager.Instance.isSystemActive = false;
        if (BreakerBox.Instance != null) BreakerBox.Instance.isSystemActive = false; // ölçerken sigorta atmasın
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;

        var body = movement.GetComponent<CharacterController>();
        var spots = new List<(string name, Vector3 pos)> { ("Spawn", movement.transform.position) };
        foreach (var room in Object.FindObjectsByType<RoomManager>(FindObjectsSortMode.InstanceID))
        {
            var c = room.GetComponent<BoxCollider>().bounds.center;
            spots.Add((room.name, new Vector3(c.x, movement.transform.position.y, c.z)));
        }
        var lines = new List<string> { $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, screen {Screen.width}x{Screen.height}" };
        var all = new List<float>(); var timings = new FrameTiming[1]; int index = 0;
        foreach (var spot in spots)
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                body.enabled = false; movement.transform.position = spot.pos; body.enabled = true; Physics.SyncTransforms();
                for (int i = 0; i < 30; i++) { movement.ForceCameraRotation(yaw, 8f); yield return null; }
                var cpu = new List<float>(); var gpu = new List<float>();
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    cpu.Add(Time.unscaledDeltaTime * 1000f);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add((float)timings[0].gpuFrameTime);
                }
                ScreenCapture.CaptureScreenshot($"{dir}/{index++:00}-{spot.name}-{yaw}.png");
                yield return null; yield return null;
                all.AddRange(cpu);
                lines.Add($"{spot.name}/{yaw}: frame median {Median(cpu):0.00} ms, gpu median {(gpu.Count > 0 ? Median(gpu).ToString("0.00") : "n/a")} ms");
            }
        lines.Insert(0, $"TOUR {label.ToUpper()} DONE: {index} views, frame median {Median(all):0.00} ms ({1000f / Median(all):0} fps), p95 {P95(all):0.00} ms");
        File.WriteAllLines(dir + "/frames.txt", lines);
        Debug.Log(lines[0]);
    }

    static float Median(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
    static float P95(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s[Mathf.Min(s.Count - 1, (int)(s.Count * .95f))]; }

    static readonly List<string> bakeLog = new List<string>();
    static readonly Stopwatch bakeClock = new Stopwatch();

    // Sahne şimdiye kadar scn_labratory'nin 17 Eylül bake'ini ödünç kullanıyordu; kendi klasörüne, kendi ayarıyla bake et.
    [MenuItem("Senzora/Işık/Sahneyi Bake Et")]
    public static void Bake()
    {
        RequireEditScene();
        if (Lightmapping.isRunning) throw new InvalidOperationException("Bake zaten çalışıyor.");
        if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "scn_LaboratorySynthesis");
        string path = SceneFolder + "/scn_LaboratorySynthesis.lighting";
        if (AssetDatabase.LoadAssetAtPath<LightingSettings>(path) == null)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(Lightmapping.lightingSettings), path);
        var s = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        // Eski bake 4/32/8 örnekle alınmış; denoiser karanlık köşelerde leke bırakıyor. Çözünürlük ve mod aynı kalır, görünüm değişmez.
        s.directSampleCount = 32; s.indirectSampleCount = 256; s.environmentSampleCount = 128;
        s.lightmapCompression = LightmapCompression.HighQuality; // aynı BC6H boyutu, karanlık geçişlerde daha az bant
        s.lightmapMaxSize = 2048; // 4144 m² bina tek sayfaya sığmak zorunda; 1024'te ~16 texel/m'ye düşüyordu
        EditorUtility.SetDirty(s); AssetDatabase.SaveAssetIfDirty(s);
        Lightmapping.lightingSettings = s;
        // UV üretimi küçük osiloskop panellerinde "pack margin" yüzünden atlandı; UV'siz yüzey lightmap'te tek texel'e düşer, probtan ışık alsın.
        var probeLit = All<MeshRenderer>().Where(Lightmapped).Where(r => { var m = r.GetComponent<MeshFilter>()?.sharedMesh; return m != null && !m.HasVertexAttribute(VertexAttribute.TexCoord1); }).ToList();
        foreach (var r in probeLit) { Undo.RecordObject(r, "Probe lit"); r.receiveGI = ReceiveGI.LightProbes; }
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

        Directory.CreateDirectory(Out);
        File.WriteAllText(Out + "/bake.txt", "RUNNING " + DateTime.Now.ToString("HH:mm:ss") + $", {probeLit.Count} renderers switched to probes\n");

        // HDRP Static Lighting Sky ancak bir kare çizildikten sonra bake'e hazır olur; Unity arka plandayken Scene view çizmiyor.
        var warm = new GameObject("SkyWarmup") { hideFlags = HideFlags.HideAndDontSave };
        var cam = warm.AddComponent<Camera>();
        cam.cameraType = CameraType.SceneView; // HDRP statik gökyüzünü yalnız Scene view kamerasında zorla günceller
        cam.targetTexture = RenderTexture.GetTemporary(256, 256, 24);
        for (int i = 0; i < 3; i++) cam.Render();
        RenderTexture.ReleaseTemporary(cam.targetTexture);
        Object.DestroyImmediate(warm);
        StartBake(); // delayCall Unity arka plandayken tetiklenmiyor
    }

    static void StartBake()
    {
        bakeLog.Clear(); bakeClock.Restart();
        Application.logMessageReceivedThreaded += CollectBakeLog;
        Lightmapping.bakeCompleted += BakeDone;
        if (!Lightmapping.BakeAsync()) { BakeCleanup(); File.AppendAllText(Out + "/bake.txt", "FAIL BakeAsync başlamadı\n"); }
    }

    static void CollectBakeLog(string message, string stack, LogType type)
    {
        if (type == LogType.Log) return;
        lock (bakeLog) bakeLog.Add($"{type}: {message.Split('\n')[0]}");
    }

    static void BakeCleanup()
    {
        Application.logMessageReceivedThreaded -= CollectBakeLog;
        Lightmapping.bakeCompleted -= BakeDone;
    }

    static void BakeDone()
    {
        BakeCleanup();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var lines = new List<string>
        {
            $"BAKE DONE in {bakeClock.Elapsed.TotalMinutes:0.0} min: {LightmapSettings.lightmaps.Length} lightmaps, data {AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)}, probes {(LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0)}",
        };
        lock (bakeLog) lines.AddRange(bakeLog.GroupBy(l => l).Select(g => g.Count() > 1 ? $"{g.Key} (x{g.Count()})" : g.Key));
        File.WriteAllLines(Out + "/bake.txt", lines);
        Debug.Log(lines[0]);
    }

    // Builds/ .gitignore'da. Boş klasöre development olmayan build; uyarıları dosyaya yaz.
    [MenuItem("Senzora/Işık/Temiz Build")]
    public static void ReleaseBuild()
    {
        const string folder = "Builds/Senzora_Isik";
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = folder + "/Senzora.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        var warnings = report.steps.SelectMany(s => s.messages).Where(m => m.type != LogType.Log).Select(m => $"{m.type}: {m.content.Split('\n')[0]}").Distinct();
        Directory.CreateDirectory(Out);
        File.WriteAllLines(Out + "/release-build.txt", warnings.Prepend($"{report.summary.result} {report.summary.totalTime} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} size={report.summary.totalSize / 1048576} MB"));
        Debug.Log($"RELEASE BUILD: {report.summary.result}");
    }

    [MenuItem("Senzora/Işık/Occlusion Bake")]
    public static void Occlusion()
    {
        RequireEditScene();
        var clock = Stopwatch.StartNew();
        bool ok = StaticOcclusionCulling.Compute();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        string line = $"OCCLUSION {(ok ? "DONE" : "FAILED")} in {clock.Elapsed.TotalSeconds:0} s: {StaticOcclusionCulling.umbraDataSize / 1024} KB";
        Directory.CreateDirectory(Out);
        File.WriteAllText(Out + "/occlusion.txt", line + "\n");
        Debug.Log(line);
    }
}
