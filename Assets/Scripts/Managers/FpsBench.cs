#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;

// Yalnız Development build + "-fpsbench" argümanı: editördeki Senzora/FPS rotasının aynısını oyuncu build'inde ölçüp çıkar.
public class FpsBench : MonoBehaviour
{
    const string GameScene = "scn_LaboratorySynthesis";
    static readonly string[] Markers =
    {
        "Main Thread", "Render Thread", "BehaviourUpdate", "LateBehaviourUpdate", "FixedBehaviourUpdate",
        "PreLateUpdate.DirectorUpdateAnimationBegin", "PreLateUpdate.DirectorUpdateAnimationEnd",
        "PostLateUpdate.UpdateAllSkinnedMeshes", "PostLateUpdate.PlayerUpdateCanvases", "PostLateUpdate.UpdateAllRenderers",
        "PostLateUpdate.FinishFrameRendering", "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForRenderThread",
        "FixedUpdate.PhysicsFixedUpdate", "GC.Collect", "GC Allocated In Frame",
        "Texture Memory", "Gfx Used Memory", "System Used Memory", "Mesh Memory", "Audio Used Memory",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (Application.isEditor || !(args.Contains("-fpsbench") || args.Contains("-chalkboardloop"))) return;
        var host = new GameObject("FpsBench").AddComponent<FpsBench>();
        DontDestroyOnLoad(host.gameObject);
    }

    IEnumerator Start()
    {
        var lines = new List<string>();
        string output = Path.Combine(Application.persistentDataPath, "fps-bench.txt");
        if (SceneManager.GetActiveScene().name != GameScene)
        {
            SceneManager.LoadScene(GameScene);
            yield return null;
            while (SceneManager.GetActiveScene().name != GameScene) yield return null;
        }
        float loadStart = Time.realtimeSinceStartup;
        yield return null; yield return null;
        var game = GameManager.Instance;
        if (SaveManager.Instance != null) SaveManager.Instance.sessionOnly = true; // Yeni Oyun yolu oyuncunun kaydını silmesin
        var menu = FindFirstObjectByType<MainMenuManager>();
        var sequence = FindFirstObjectByType<InGameMenuController>();
        int loops = LoopCount();
        // Tahta döngüsünde tutorial tahtayı kilitlemesin.
        if (menu != null) { menu.PlayGame(); yield return null; if (menu.IsTutorialChoiceOpen) menu.ChooseTutorial(loops == 0); }
        float end = Time.realtimeSinceStartup + 120;
        while ((game == null || !game.isGameStarted || sequence != null && sequence.enabled) && Time.realtimeSinceStartup < end) yield return null;
        lines.Add($"Game start after menu: {Time.realtimeSinceStartup - loadStart:0.0}s, started={game != null && game.isGameStarted}");
        if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = true;
        if (PressureSystemManager.Instance != null) PressureSystemManager.Instance.isSystemActive = false;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        lines.Add($"Resolution {Screen.width}x{Screen.height} {Screen.fullScreenMode}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, GPU {SystemInfo.graphicsDeviceName} {SystemInfo.graphicsMemorySize} MB, CPU {SystemInfo.processorType}");
        if (loops > 0) { yield return ChalkboardLoop(loops, lines); Application.Quit(); yield break; }

        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        var recorders = new Dictionary<string, ProfilerRecorder>();
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (Markers.Contains(d.Name) && !recorders.ContainsKey(d.Name)) recorders[d.Name] = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default);
        }
        foreach (var r in recorders.Values) r.Start();
        lines.Add("Markers found: " + string.Join(", ", recorders.Keys));

        var player = FindFirstObjectByType<StarterAssets.CharacterController>();
        var body = player.GetComponent<CharacterController>();
        var spots = new List<(string name, Vector3 pos)> { ("Spawn", player.transform.position) };
        foreach (var room in FindObjectsByType<RoomManager>(FindObjectsSortMode.InstanceID))
        {
            var c = room.GetComponent<BoxCollider>().bounds.center;
            spots.Add((room.name, new Vector3(c.x, player.transform.position.y, c.z)));
        }

        var frames = new List<float>(); var series = recorders.Keys.ToDictionary(k => k, k => new List<double>());
        var main = new List<double>(); var render = new List<double>(); var gpu = new List<double>(); var timings = new FrameTiming[1];
        var views = new List<string>(); int spikes = 0; var spikeViews = new List<string>();
        foreach (var spot in spots)
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                body.enabled = false;
                player.transform.SetPositionAndRotation(spot.pos, Quaternion.Euler(0, yaw, 0));
                body.enabled = true; Physics.SyncTransforms();
                for (int i = 0; i < 30; i++) yield return null;
                var viewFrames = new List<float>(); var viewGpu = new List<double>();
                for (int i = 0; i < 120; i++)
                {
                    yield return null;
                    float ms = Time.unscaledDeltaTime * 1000f; viewFrames.Add(ms);
                    if (ms > 33.3f) { spikes++; spikeViews.Add($"{spot.name}/{yaw}:{ms:0}ms"); }
                    foreach (var kv in recorders) series[kv.Key].Add(kv.Value.LastValue);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        main.Add(timings[0].cpuMainThreadFrameTime); render.Add(timings[0].cpuRenderThreadFrameTime);
                        if (timings[0].gpuFrameTime > 0) { gpu.Add(timings[0].gpuFrameTime); viewGpu.Add(timings[0].gpuFrameTime); }
                    }
                }
                frames.AddRange(viewFrames);
                views.Add($"{spot.name}/{yaw}: frame {Median(viewFrames):0.00} ms, p95 {P(viewFrames, .95):0.00}, gpu {(viewGpu.Count > 0 ? Median(viewGpu).ToString("0.00") : "n/a")}");
            }

        lines.Insert(0, $"FPS BENCH (build): {views.Count} views, frame median {Median(frames):0.00} ms ({1000 / Median(frames):0} fps), p95 {P(frames, .95):0.00} ms, p99 {P(frames, .99):0.00} ms, worst {frames.Max():0.0} ms, frames>33ms {spikes}/{frames.Count}");
        lines.Add($"FrameTiming median: cpu main {Median(main):0.00} ms, cpu render {Median(render):0.00} ms, gpu {(gpu.Count > 0 ? Median(gpu).ToString("0.00") : "n/a")} ms");
        foreach (var kv in series)
        {
            bool memory = kv.Key.Contains("Memory") || kv.Key.Contains("Allocated");
            lines.Add(memory ? $"  {kv.Key}: median {Median(kv.Value) / 1048576.0:0.0} MB, max {kv.Value.Max() / 1048576.0:0.0} MB"
                             : $"  {kv.Key}: median {Median(kv.Value) / 1e6:0.000} ms, p95 {P(kv.Value, .95) / 1e6:0.000} ms");
        }
        lines.Add("Spikes: " + string.Join(" ", spikeViews.Take(40)));
        lines.AddRange(views);
        foreach (var r in recorders.Values) r.Dispose();
        File.WriteAllLines(output, lines);
        Application.Quit();
    }

    static int LoopCount()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-chalkboardloop");
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int n) ? n : 0;
    }

    // Oyuncuların tahtadan çıkışta gördüğü çökmeyi tekrar üretmek için: gir, bekle, çık; her turu hemen diske yaz.
    static IEnumerator ChalkboardLoop(int count, List<string> header)
    {
        string path = Path.Combine(Application.persistentDataPath, "chalkboard-loop.txt");
        File.WriteAllLines(path, header);
        var board = FindFirstObjectByType<InteractableChalkboard>();
        var manager = ChalkboardManager.Instance;
        if (board == null || manager == null) { File.AppendAllText(path, "Tahta veya ChalkboardManager yok\n"); yield break; }
        int stuck = 0;
        for (int i = 1; i <= count; i++)
        {
            board.Interact();
            float end = Time.realtimeSinceStartup + 20;
            while (!manager.isMachineActive && Time.realtimeSinceStartup < end) yield return null;
            bool entered = manager.isMachineActive;
            float stay = 0.3f + (i % 5) * 0.4f; // farklı bekleme süreleri: geçiş ortasında çıkış dahil
            end = Time.realtimeSinceStartup + stay;
            while (Time.realtimeSinceStartup < end) yield return null;
            board.ForceExit();
            end = Time.realtimeSinceStartup + 20;
            while ((manager.isMachineActive || GameManager.Instance.activeInteraction != null) && Time.realtimeSinceStartup < end) yield return null;
            bool exited = !manager.isMachineActive && GameManager.Instance.activeInteraction == null;
            if (!entered || !exited) stuck++;
            File.AppendAllText(path, $"cycle {i} entered={entered} exited={exited} t={Time.realtimeSinceStartup:0.0}s mem={System.GC.GetTotalMemory(false) / 1048576}MB\n");
            yield return null; yield return null;
        }
        File.AppendAllText(path, $"DONE {count} cycles, stuck {stuck}\n");
    }

    static float Median(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s[s.Count / 2]; }
    static double Median(List<double> v) { var s = v.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s[s.Count / 2]; }
    static double P(List<double> v, double q) { var s = v.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s[System.Math.Min(s.Count - 1, (int)(s.Count * q))]; }
    static float P(List<float> v, double q) { var s = v.OrderBy(x => x).ToList(); return s.Count == 0 ? 0 : s[System.Math.Min(s.Count - 1, (int)(s.Count * q))]; }
}
#endif
