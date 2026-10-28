#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

// Samples the real scene, camera and InteractableBook coroutines. Requires a session-only Play run.
public static class BookOptimizationChecks
{
    [Serializable] public class FrameEntry
    {
        public string bookID, role, phase;
        public int graphics, textVertices, maxDrawCalls, maxVertices;
        public BookPagePerformanceAudit.Stats frameMs, mainThreadMs, gpuMs, allocatedBytes;
    }
    [Serializable] private class LiveReport
    {
        public string utc, note;
        public int goals, books;
        public List<FrameEntry> phases = new List<FrameEntry>();
    }
    private static IEnumerator running;
    private static int lastFrame;
    private static ProfilerRecorder mainThread, gpu, gc;
    private static InteractableBook current;
    private static Transform camera;
    private static Vector3 cameraPosition;
    private static Quaternion cameraRotation;
    private static bool cameraMoved;

    [MenuItem("Senzora/Book Motion/Optimization Live Frames Before (Play)")]
    public static void LiveBefore() => Start("Temp/book-optimization-live-before.json");
    [MenuItem("Senzora/Book Motion/Optimization Live Frames After (Play)")]
    public static void LiveAfter() => Start("Temp/book-optimization-live-after.json");

    private static void Start(string path)
    {
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        if (!Application.isPlaying || running != null) throw new InvalidOperationException("Start once in a session-only Play run.");
        var saves = UnityEngine.Object.FindObjectsByType<SaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (saves.Any(s => !s.sessionOnly)) throw new InvalidOperationException("Live audit requires all SaveManager.sessionOnly=true before Play to protect user saves.");
        var trial = ResearchPuzzleTrial.Active;
        if (trial == null || trial.Content == null || trial.Puzzles.Count != 3 || trial.books.Length != 47)
            throw new InvalidOperationException("Configure the real saved three-goal/47-book research scene before sampling.");
        if (GameManager.Instance == null || GameManager.Instance.isGamePaused || Camera.main == null)
            throw new InvalidOperationException("An unpaused scene with its actual player camera is required.");
        if (GameManager.Instance.activeInteraction != null)
            throw new InvalidOperationException("Exit the current interaction before starting live sampling.");
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
        gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        running = Run(trial, path);
        lastFrame = -1;
        EditorApplication.update += Tick;
        Debug.Log("BOOK LIVE AUDIT started. Keep Game view visible and do not interact until completion.");
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (!Application.isPlaying) { Stop(); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try { if (!running.MoveNext()) Stop(); }
        catch (Exception error) { Stop(); Debug.LogException(error); }
    }

    [MenuItem("Senzora/Book Motion/Optimization Stop Live Frames")]
    public static void Stop()
    {
        EditorApplication.update -= Tick;
        (running as IDisposable)?.Dispose(); running = null;
        if (cameraMoved && camera != null) camera.SetPositionAndRotation(cameraPosition, cameraRotation);
        cameraMoved = false;
        if (current != null && Application.isPlaying) current.ForceExit();
        current = null;
        mainThread.Dispose(); gpu.Dispose(); gc.Dispose();
    }

    private static IEnumerator Run(ResearchPuzzleTrial trial, string path)
    {
        var report = new LiveReport { utc = DateTime.UtcNow.ToString("O"), goals = trial.Puzzles.Count, books = trial.books.Length,
            note = "Full live scene and actual InteractableBook.Interact/NextPage/PreviousPage/ForceExit coroutines; camera motion at the live open pose. Frame intervals include editor/render/scheduling overhead and are not isolated book CPU costs or a standalone-player FPS claim. Profiler samples report prior complete frames; -1 means unavailable. Allocations are whole-frame, not attributed solely to the book. Live random session is reported as configured; use fixed-seed CPU snapshots for controlled comparisons." };
        var idle = new FrameEntry { role = "full-scene", phase = "idle" };
        var idleFrames = Sample(idle, null, 30); while (idleFrames.MoveNext()) yield return null;
        report.phases.Add(idle);
        var samples = new List<(InteractableBook book, string role)>();
        foreach (var mask in new[] { PuzzleBookTopic.SignalSamples | PuzzleBookTopic.SignalProcessing,
            PuzzleBookTopic.ColorMaterials | PuzzleBookTopic.MixtureProcessing, PuzzleBookTopic.Foundations })
        {
            var book = trial.books.Where(b => (b.GetComponent<PuzzleBookIdentity>().topics & mask) != 0)
                .OrderByDescending(b => b.totalPages > 2 || b.allowLoop)
                .ThenByDescending(b => b.synthesisPages.Sum(p => p.blocks.Sum(n => (n.description?.Length ?? 0) + 200))).First();
            if (!samples.Exists(s => s.book == book)) samples.Add((book, mask == PuzzleBookTopic.Foundations ? "foundations" : (mask & PuzzleBookTopic.SignalSamples) != 0 ? "signal" : "color"));
        }
        var empty = trial.books.FirstOrDefault(b => b.synthesisPages.All(p => p.blocks.Count == 0));
        if (empty != null) samples.Add((empty, "empty"));
        foreach (var selected in samples)
        {
            current = selected.book;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                var opening = NewEntry(current, selected.role, cycle == 0 ? "first-open" : "reopen");
                current.Interact();
                var frames = Sample(opening, current, 0); while (frames.MoveNext()) yield return null;
                report.phases.Add(opening);
                if (current.totalPages > 2 || current.allowLoop)
                {
                    var forward = NewEntry(current, selected.role, "forward-" + cycle);
                    current.NextPage(); frames = Sample(forward, current, 0); while (frames.MoveNext()) yield return null;
                    report.phases.Add(forward);
                    var backward = NewEntry(current, selected.role, "backward-" + cycle);
                    current.PreviousPage(); frames = Sample(backward, current, 0); while (frames.MoveNext()) yield return null;
                    report.phases.Add(backward);
                }
                if (cycle == 0)
                {
                    camera = current.cameraTransform;
                    if (camera == null) throw new InvalidOperationException("Book has no live view camera.");
                    cameraPosition = camera.position; cameraRotation = camera.rotation; cameraMoved = true;
                    var cameraEntry = NewEntry(current, selected.role, "camera-motion");
                    frames = Sample(cameraEntry, null, 45, true); while (frames.MoveNext()) yield return null;
                    camera.SetPositionAndRotation(cameraPosition, cameraRotation); cameraMoved = false;
                    report.phases.Add(cameraEntry);
                }
                var closing = NewEntry(current, selected.role, "close-" + cycle);
                current.ForceExit(); frames = Sample(closing, current, 0); while (frames.MoveNext()) yield return null;
                report.phases.Add(closing);
            }
        }
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("BOOK LIVE AUDIT " + path + "\n" + string.Join("\n", report.phases.Select(p =>
            $"{p.role}/{p.phase}: frames={p.frameMs.count}, interval median/p95/max={p.frameMs.median:F2}/{p.frameMs.p95:F2}/{p.frameMs.max:F2}ms, main={p.mainThreadMs.median:F2}ms, allocation={p.allocatedBytes.median:F0}B")));
    }

    private static FrameEntry NewEntry(InteractableBook book, string role, string phase) => new FrameEntry
    { bookID = book.GetComponent<PuzzleBookIdentity>().bookID, role = role, phase = phase };

    private static IEnumerator Sample(FrameEntry entry, InteractableBook motion, int frames, bool moveCamera = false)
    {
        var frame = new List<double>(); var cpu = new List<double>(); var gpuTimes = new List<double>(); var allocations = new List<double>();
        double deadline = EditorApplication.timeSinceStartup + 30;
        int index = 0;
        // Yield first so a newly started coroutine reaches a rendered frame before collecting it.
        do
        {
            yield return null;
            frame.Add(Time.unscaledDeltaTime * 1000.0);
            cpu.Add(mainThread.Valid && mainThread.LastValue > 0 ? mainThread.LastValue / 1000000.0 : -1);
            gpuTimes.Add(gpu.Valid && gpu.LastValue > 0 ? gpu.LastValue / 1000000.0 : -1);
            allocations.Add(gc.Valid ? gc.LastValue : -1);
            entry.maxDrawCalls = Math.Max(entry.maxDrawCalls, UnityStats.drawCalls);
            entry.maxVertices = Math.Max(entry.maxVertices, UnityStats.vertices);
            if (moveCamera)
            {
                float t = index * Mathf.PI * 2 / frames;
                camera.SetPositionAndRotation(cameraPosition + cameraRotation * new Vector3(Mathf.Sin(t) * .025f, Mathf.Cos(t) * .012f, 0),
                    cameraRotation * Quaternion.Euler(Mathf.Sin(t) * 2, Mathf.Cos(t) * 3, 0));
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Book phase did not finish: " + entry.phase);
            index++;
        } while (motion != null ? motion.isAnimating : index < frames);
        entry.frameMs = BookPagePerformanceAudit.Stats.From(frame); entry.mainThreadMs = BookPagePerformanceAudit.Stats.From(cpu);
        entry.gpuMs = BookPagePerformanceAudit.Stats.From(gpuTimes); entry.allocatedBytes = BookPagePerformanceAudit.Stats.From(allocations);
        if (motion != null)
        {
            var graphics = motion.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            entry.graphics = graphics.Length;
            entry.textVertices = motion.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Sum(t => t.textInfo.meshInfo.Take(t.textInfo.materialCount).Sum(m => m.vertexCount));
        }
    }

    [MenuItem("Senzora/Book Motion/Optimization Cached Projection Regression")]
    public static void CachedProjectionRegression()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run projection regression outside Play mode.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/P_Book.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab);
        Material material = null;
        const BindingFlags access = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var book = instance.GetComponent<InteractableBook>();
            book.clueData = null; book.synthesisPages = new List<BookWorkspacePage>();
            book.totalPages = 8; book.usePhysicalPageBlocks = book.printNotesOnPages = true;
            material = new Material(book.pageTurnMaterial); book.pageTurnMaterial = material;
            typeof(InteractableBook).GetField("pageIndexL", access).SetValue(book, 2);
            typeof(InteractableBook).GetField("pageIndexR", access).SetValue(book, 3);
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/NewBookAnim/newBookOpened.anim").SampleAnimation(instance, 0);
            var paper = instance.GetComponent<BookPageBlockSurface>() ?? instance.AddComponent<BookPageBlockSurface>();
            typeof(BookPageBlockSurface).GetField("book", access).SetValue(paper, book);
            paper.PreparePresentation();
            var canvas = (Canvas)typeof(BookPageBlockSurface).GetField("surface", access).GetValue(paper);
            var bind = typeof(BookPageBlockSurface).GetMethod("BindInk", access);
            var evaluate = typeof(BookPageBlockSurface).GetMethod("EvaluateInk", access);
            var reference = typeof(BookPageBlockSurface).GetMethod("MapTurningPage", access);
            var restReference = typeof(BookPageBlockSurface).GetMethod("MapPageWorld", access);
            double maximumError = 0;
            int checkedPoints = 0;
            foreach (int direction in new[] { 1, -1 })
            {
                if (!paper.BeginTurn(direction)) throw new InvalidOperationException("Projection regression could not begin turn.");
                foreach (float progress in new[] { 0, .17f, .5f, .83f, 1 })
                {
                    paper.UpdateTurn(progress);
                    var mesh = book.pageFlipRenderer.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] vertices = mesh.vertices; Vector2[] uvs = mesh.uv; int[] triangles = mesh.triangles;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector2 uv = new Vector2(uvs[i].x * 2 - 1, uvs[i].y);
                        Vector3 expected = (Vector3)reference.Invoke(paper, new object[] { uv });
                        CheckProjection(expected, book.pageFlipRenderer.transform.TransformPoint(vertices[i]), "paper grid/reference", ref maximumError);
                        checkedPoints++;
                    }
                    foreach (bool right in new[] { false, true })
                    foreach (Vector2 uv in new[] { new Vector2(.03f, .08f), new Vector2(.21f, .37f),
                        new Vector2(.5f, .5f), new Vector2(.72f, .63f), new Vector2(.96f, .91f) })
                    {
                        object point = bind.Invoke(paper, new object[] { uv, right });
                        var rest = (Vector3)evaluate.Invoke(paper, new[] { point, (object)false, false });
                        var expectedRest = (Vector3)restReference.Invoke(paper, new object[] { uv, right });
                        CheckProjection(expectedRest, canvas.transform.TransformPoint(rest), "stationary barycentric/reference", ref maximumError);
                        // lift=true selects the renderer's ink path; subtract only its intentional ink offset.
                        var curled = (Vector3)evaluate.Invoke(paper, new[] { point, (object)true, true });
                        var inkLift = (Vector3)typeof(BookPageBlockSurface).GetField("inkLift", access).GetValue(paper);
                        Vector2 movingUV = new Vector2(right ? uv.x : 1 - uv.x, uv.y);
                        Vector3 expectedCurl = MeshPoint(vertices, uvs, triangles, movingUV);
                        CheckProjection(book.pageFlipRenderer.transform.TransformPoint(expectedCurl), canvas.transform.TransformPoint(curled - inkLift),
                            "moving ink/rendered paper triangle", ref maximumError);
                        checkedPoints += 2;
                    }
                }
                paper.EndTurn();
            }
            CheckTransparentHoverCache(paper, canvas);
            Debug.Log($"BOOK CACHED PROJECTION PASS: {checkedPoints} forward/backward paper-grid, stationary UV and ink/triangle points; maximum world error={maximumError:G4}m. Geometric correctness only; no FPS claim.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static void CheckTransparentHoverCache(BookPageBlockSurface paper, Canvas canvas)
    {
        var obj = new GameObject("Transparent hover cache regression", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(BookPagePrintedGraphic));
        var mesh = new Mesh();
        try
        {
            obj.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)obj.transform; rect.sizeDelta = new Vector2(500, 700);
            var image = obj.GetComponent<UnityEngine.UI.Image>(); image.color = Color.white;
            var projection = obj.GetComponent<BookPagePrintedGraphic>();
            const BindingFlags access = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(BookPagePrintedGraphic);
            type.GetField("Surface", access).SetValue(projection, paper);
            type.GetField("RegionRect", access).SetValue(projection, rect);
            type.GetField("RegionUV", access).SetValue(projection, new Rect(0, 0, 1, 1));
            using (var vh = new UnityEngine.UI.VertexHelper())
            {
                AddTestQuad(vh, Color.white); projection.ModifyMesh(vh); vh.FillMesh(mesh); image.canvasRenderer.SetMesh(mesh);
                image.color = Color.clear;
                vh.Clear(); AddTestQuad(vh, Color.clear); projection.ModifyMesh(vh); vh.FillMesh(mesh); image.canvasRenderer.SetMesh(mesh);
                type.GetMethod("RefreshProjection", access).Invoke(projection, null);
                if (image.canvasRenderer.GetMesh().colors32.Any(c => c.a != 0))
                    throw new InvalidOperationException("Transparent hover image resurrected its previous cached visible mesh.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(mesh); }
    }

    private static void AddTestQuad(UnityEngine.UI.VertexHelper vh, Color color)
    {
        vh.AddVert(new Vector3(-250, -350, 0), color, Vector2.zero); vh.AddVert(new Vector3(-250, 350, 0), color, Vector2.up);
        vh.AddVert(new Vector3(250, 350, 0), color, Vector2.one); vh.AddVert(new Vector3(250, -350, 0), color, Vector2.right);
        vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
    }

    private static Vector3 MeshPoint(Vector3[] vertices, Vector2[] uvs, int[] triangles, Vector2 uv)
    {
        uv.x = .5f + .5f * uv.x;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector2 ab = uvs[b] - uvs[a], ac = uvs[c] - uvs[a], ap = uv - uvs[a];
            float determinant = ab.x * ac.y - ab.y * ac.x;
            if (Mathf.Abs(determinant) < .00000001f) continue;
            float v = (ap.x * ac.y - ap.y * ac.x) / determinant;
            float w = (ab.x * ap.y - ab.y * ap.x) / determinant;
            if (v >= -.00001f && w >= -.00001f && v + w <= 1.00001f)
                return vertices[a] + (vertices[b] - vertices[a]) * v + (vertices[c] - vertices[a]) * w;
        }
        throw new InvalidOperationException("Turning mesh has no triangle at " + uv);
    }

    private static void CheckProjection(Vector3 expected, Vector3 actual, string label, ref double maximumError)
    {
        double error = Vector3.Distance(expected, actual);
        maximumError = Math.Max(maximumError, error);
        if (error > .0001) throw new InvalidOperationException($"{label}: error={error:G4}m, expected={expected}, actual={actual}");
    }

    [Serializable] private class BreakdownContext
    {
        public int profilerFrame, gameFrame, targetFrameRate, vSyncCount;
        public bool editorActive, applicationFocused, editorPaused, playing, runInBackground;
        public float deltaMs, unscaledDeltaMs, timeScale;
        public double editorTime;
    }
    [Serializable] private class BreakdownMarker
    {
        public string name, path;
        public int calls;
        public double selfMs, inclusiveMs;
        [NonSerialized] public int representative;
        [NonSerialized] public double largestSelf;
    }
    [Serializable] private class BreakdownFrame
    {
        public BreakdownContext context;
        public string thread, threadGroup, root, issue;
        public int threadIndex = -1, sampleCount;
        public double rawFrameMs, rootMs, accountedSelfMs;
        public List<BreakdownMarker> topSelf = new List<BreakdownMarker>();
        public List<BreakdownMarker> disjointInclusive = new List<BreakdownMarker>();
    }
    [Serializable] private class BreakdownReport
    {
        public string utc, unity, note, issue;
        public string deepProfilingState;
        public bool originalProfilerEnabled, originalProfileEditor;
        public List<BreakdownFrame> frames = new List<BreakdownFrame>();
    }
    private static BreakdownReport breakdown;
    private static int lastBreakdownFrame;
    private static double breakdownDeadline;
    private static object cpuArea;
    private static bool originalCpuArea;
    private static MethodInfo setProfilerArea;

    [MenuItem("Senzora/Book Motion/Optimization Live Main Thread Breakdown (Play)")]
    public static void CaptureLiveBreakdown()
    {
        if (!Application.isPlaying || running != null || breakdown != null)
            throw new InvalidOperationException("Run once during Play, after the live animation sampler finishes.");
        breakdown = new BreakdownReport { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
            deepProfilingState = typeof(ProfilerDriver).GetProperty("deepProfiling", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)?.ToString() ?? "unavailable",
            originalProfilerEnabled = ProfilerDriver.enabled, originalProfileEditor = ProfilerDriver.profileEditor,
            note = "30 completed Unity profiler frames from the actual Play/editor state. Capture does not focus, unpause, queue player updates, change FPS/vsync or animate the scene. Context is observed when each completed profiler frame becomes available; it is not an exact within-frame focus timestamp. Only the named Main Thread is analyzed; worker/render thread times are not summed into it. Self time subtracts immediate children exactly once, then aggregates identical marker IDs. Inclusive frontier recursively expands nodes whose self is <=5% of their inclusive time; its selected regions are disjoint, so no parent/child double counting. Top lists omit minor remaining regions; rawFrameMs/rootMs and accountedSelfMs retain totals. Analysis occurs after profiling is restored, avoiding analyzer overhead in the captured frames. Normal profiling adds overhead. This is editor diagnosis, not standalone FPS." };
        try
        {
            // CPU area APIs are editor internals and vary by Unity version; use the installed signatures.
            var getArea = typeof(ProfilerDriver).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetAreaEnabled" && m.GetParameters().Length == 1);
            setProfilerArea = typeof(ProfilerDriver).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "SetAreaEnabled" && m.GetParameters().Length == 2);
            if (getArea != null && setProfilerArea != null)
            {
                cpuArea = Enum.Parse(getArea.GetParameters()[0].ParameterType, "CPU");
                originalCpuArea = (bool)getArea.Invoke(null, new[] { cpuArea });
                setProfilerArea.Invoke(null, new[] { cpuArea, (object)true });
            }
            ProfilerDriver.profileEditor = true;
            ProfilerDriver.enabled = true;
            lastBreakdownFrame = ProfilerDriver.lastFrameIndex;
            breakdownDeadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += TickBreakdown;
            AssemblyReloadEvents.beforeAssemblyReload += FinishBreakdown;
            Debug.Log("BOOK BREAKDOWN capturing 30 completed frames without changing focus or timing settings.");
        }
        catch { FinishBreakdown(); throw; }
    }

    private static void TickBreakdown()
    {
        if (breakdown == null) return;
        try
        {
            int latest = ProfilerDriver.lastFrameIndex;
            if (latest > lastBreakdownFrame)
            {
                // Read only small state here; heavy raw-tree analysis is deferred until capture ends.
                breakdown.frames.Add(new BreakdownFrame { context = new BreakdownContext {
                    profilerFrame = latest, gameFrame = Time.frameCount, editorActive = InternalEditorUtility.isApplicationActive,
                    applicationFocused = Application.isFocused, editorPaused = EditorApplication.isPaused,
                    playing = Application.isPlaying, runInBackground = Application.runInBackground,
                    deltaMs = Time.deltaTime * 1000, unscaledDeltaMs = Time.unscaledDeltaTime * 1000,
                    timeScale = Time.timeScale, targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
                    editorTime = EditorApplication.timeSinceStartup } });
                lastBreakdownFrame = latest;
            }
            if (breakdown.frames.Count >= 30 || !Application.isPlaying || EditorApplication.timeSinceStartup > breakdownDeadline)
                FinishBreakdown();
        }
        catch (Exception error) { breakdown.issue = error.ToString(); FinishBreakdown(); }
    }

    [MenuItem("Senzora/Book Motion/Optimization Finish Live Breakdown")]
    public static void FinishBreakdown()
    {
        if (breakdown == null) return;
        EditorApplication.update -= TickBreakdown;
        AssemblyReloadEvents.beforeAssemblyReload -= FinishBreakdown;
        var report = breakdown; breakdown = null;
        try
        {
            ProfilerDriver.enabled = report.originalProfilerEnabled;
            ProfilerDriver.profileEditor = report.originalProfileEditor;
            if (cpuArea != null && setProfilerArea != null) setProfilerArea.Invoke(null, new[] { cpuArea, (object)originalCpuArea });
            foreach (var frame in report.frames)
            {
                try { AnalyzeMainThread(frame); }
                catch (Exception error) { frame.issue = error.ToString(); }
            }
        }
        finally
        {
            cpuArea = null; setProfilerArea = null;
            System.IO.Directory.CreateDirectory("Temp");
            System.IO.File.WriteAllText("Temp/book-live-breakdown.json", JsonUtility.ToJson(report, true));
        }
        Debug.Log("BOOK BREAKDOWN saved Temp/book-live-breakdown.json; frames=" + report.frames.Count
            + "; profiler state restored. Inspect actual Main Thread self/frontier markers before attributing frame stalls.");
    }

    private static void AnalyzeMainThread(BreakdownFrame output)
    {
        int frame = output.context.profilerFrame;
        for (int thread = 0; thread < 256; thread++)
        {
            using (var raw = ProfilerDriver.GetRawFrameDataView(frame, thread))
            {
                if (!raw.valid) break;
                if (raw.threadName != "Main Thread") continue;
                output.threadIndex = thread; output.thread = raw.threadName; output.threadGroup = raw.threadGroupName;
                output.rawFrameMs = raw.frameTimeMs; output.sampleCount = raw.sampleCount;
                if (raw.sampleCount == 0) { output.issue = "Main Thread has no CPU samples; CPU area may be unavailable."; return; }
                output.root = raw.GetSampleName(0); output.rootMs = raw.GetSampleTimeMs(0);
                int count = raw.sampleCount;
                var end = new int[count]; var parent = new int[count]; var time = new double[count]; var self = new double[count];
                var stack = new Stack<int>();
                var markers = new Dictionary<int, BreakdownMarker>();
                for (int i = 0; i < count; i++)
                {
                    while (stack.Count > 0 && i >= end[stack.Peek()]) stack.Pop();
                    parent[i] = stack.Count > 0 ? stack.Peek() : -1;
                    end[i] = Math.Min(count, i + 1 + raw.GetSampleChildrenCountRecursive(i));
                    time[i] = Math.Max(0, raw.GetSampleTimeMs(i)); self[i] = time[i];
                    if (parent[i] >= 0) self[parent[i]] -= time[i];
                    stack.Push(i);
                }
                for (int i = 0; i < count; i++)
                {
                    self[i] = Math.Max(0, self[i]); output.accountedSelfMs += self[i];
                    int id = raw.GetSampleMarkerId(i);
                    if (!markers.TryGetValue(id, out var marker))
                    { marker = new BreakdownMarker { name = raw.GetSampleName(i), representative = i }; markers.Add(id, marker); }
                    marker.calls++; marker.selfMs += self[i]; marker.inclusiveMs += time[i];
                    if (self[i] > marker.largestSelf) { marker.largestSelf = self[i]; marker.representative = i; }
                }
                output.topSelf = markers.Values.OrderByDescending(m => m.selfMs).Take(20).ToList();
                foreach (var marker in output.topSelf) marker.path = SamplePath(raw, marker.representative, parent);
                var frontier = new List<int>();
                var pending = new Stack<int>(); pending.Push(0);
                while (pending.Count > 0)
                {
                    int i = pending.Pop();
                    if (end[i] > i + 1 && self[i] <= time[i] * .05)
                    { for (int child = i + 1; child < end[i]; child = end[child]) pending.Push(child); }
                    else frontier.Add(i);
                }
                output.disjointInclusive = frontier.OrderByDescending(i => time[i]).Take(20).Select(i => new BreakdownMarker {
                    name = raw.GetSampleName(i), path = SamplePath(raw, i, parent), calls = 1, selfMs = self[i], inclusiveMs = time[i] }).ToList();
                return;
            }
        }
        output.issue = "No completed Main Thread raw view found. Profiler frame may have expired or profiling target lacks editor CPU data.";
    }

    private static string SamplePath(RawFrameDataView raw, int index, int[] parent)
    {
        var parts = new List<string>();
        for (int i = index; i >= 0 && parts.Count < 32; i = parent[i]) parts.Add(raw.GetSampleName(i));
        parts.Reverse(); return string.Join("/", parts);
    }
}
#endif
