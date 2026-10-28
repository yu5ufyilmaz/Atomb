#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

// Editor diagnostics only. Never configures a live research session or writes a save file.
public static class BookPagePerformanceAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ScenePath = "Assets/Scenes/scn_LaboratorySynthesis.unity";
    private const int Seed = 27102026, Warmup = 4, Samples = 21, Cycles = 3;
    private const double TurnBudgetMs = 4;

    [Serializable] public class Stats
    {
        public int count;
        public double median, p95, max;
        public static Stats From(List<double> values)
        {
            values.Sort();
            return values.Count == 0 ? new Stats() : new Stats { count = values.Count,
                median = values[values.Count / 2], p95 = values[Math.Max(0, (int)Math.Ceiling(values.Count * .95) - 1)], max = values[values.Count - 1] };
        }
    }
    [Serializable] public class Entry
    {
        public string role, bookID, book, issue, contentHash;
        public int pages, blocks, graphics, activeGraphics, textVertices, canvasVertices, triangles;
        public bool canTurn;
        public double firstPrepareMs, firstPrepareCanvasMs;
        public Stats reopenMs, beginMs, turnMs, canvasMs, turnAndCanvasMs, endMs, steadyAllocatedBytes;
    }
    [Serializable] public class Report
    {
        public string utc, unity, source, note;
        public int seed, poolGoals, profileCount;
        public bool allocationCounterVerified;
        public double turnBudgetMs = TurnBudgetMs;
        public List<Entry> books = new List<Entry>();
    }

    [MenuItem("Senzora/Book Motion/Audit Page Turn CPU")]
    public static void Run() => RunSnapshot("Temp/book-performance-audit.json");
    [MenuItem("Senzora/Book Motion/Optimization Capture Before")]
    public static void RunBefore() => RunSnapshot("Temp/book-optimization-before.json");
    [MenuItem("Senzora/Book Motion/Optimization Capture After")]
    public static void RunAfter() => RunSnapshot("Temp/book-optimization-after.json");

    private static void RunSnapshot(string path)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run the CPU audit outside Play mode.");
        var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
            seed = Seed, source = ScenePath, allocationCounterVerified = AllocationCounterWorks(),
            note = "Editor CPU microbenchmark, not game FPS/GPU time. Actual saved simultaneous pool and scene book identities; deterministic distribution. Three full turns, each with four warmup and 21 measured UpdateTurn+ForceUpdateCanvases samples. All authored content retained; indexed meshes and hidden-ink culling are allowed. Cold Prepare is one sample; reopen/begin/end include canvas flush. Allocation counter is calibrated with a retained 4096-byte allocation; false means inconclusive, never zero-allocation proof." };
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var trial = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ResearchPuzzleTrial>(true)).Single();
            var profiles = trial.books.Select((b, i) => b.GetComponent<PuzzleBookIdentity>().Profile(i)).ToList();
            if (trial.simultaneousPuzzles == null) throw new InvalidOperationException("Saved simultaneous goal pool is missing.");
            var assets = trial.simultaneousPuzzles.Playable(out string error);
            if (error != null) throw new InvalidOperationException(error);
            var definitions = assets.Select(a => a.definition.Clone()).ToList();
            var pages = PuzzleBookDistribution.Build(definitions, profiles, Seed);
            report.poolGoals = definitions.Count; report.profileCount = profiles.Count;
            if (definitions.Count != 3 || profiles.Count != 47) throw new InvalidOperationException("Expected the saved three-goal/47-book configuration.");
            foreach (var sample in Representatives(trial.books, profiles, pages))
            {
                var book = trial.books[sample.index];
                book.clueData = null; book.synthesisPages = pages[sample.index];
                book.totalPages = Mathf.Max(2, (book.synthesisPages.Count + 1) / 2 * 2);
                book.usePhysicalPageBlocks = true;
                var entry = new Entry { role = sample.role, bookID = profiles[sample.index].bookID,
                    book = profiles[sample.index].title, pages = book.totalPages,
                    contentHash = Fingerprint(book.synthesisPages),
                    blocks = book.synthesisPages.Sum(p => p.blocks.Count), canTurn = book.totalPages > 2 || book.allowLoop };
                report.books.Add(entry);
                Measure(book, entry);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Write(path, report);
        Debug.Log("BOOK CPU " + path + "\n" + string.Join("\n", report.books.Select(e =>
            $"{e.role}/{e.book}: pages={e.pages}, blocks={e.blocks}, graphics={e.graphics}, vertices={e.canvasVertices}, cold={e.firstPrepareMs:F2}ms, reopen={e.reopenMs.median:F2}ms, begin={e.beginMs.median:F2}ms, turn+canvas={e.turnAndCanvasMs.median:F2}/{e.turnAndCanvasMs.p95:F2}/{e.turnAndCanvasMs.max:F2}ms median/p95/max, allocation={e.steadyAllocatedBytes.median:F0}B (verified={report.allocationCounterVerified}), issue={e.issue}")));
    }

    private struct Sample { public int index; public string role; }
    private static List<Sample> Representatives(InteractableBook[] books, List<PuzzleBookProfile> profiles, List<BookWorkspacePage>[] pages)
    {
        var result = new List<Sample>();
        foreach (var group in new[] { PuzzleBookTopic.SignalSamples | PuzzleBookTopic.SignalProcessing,
            PuzzleBookTopic.ColorMaterials | PuzzleBookTopic.MixtureProcessing, PuzzleBookTopic.Foundations })
        {
            int index = Enumerable.Range(0, books.Length).Where(i => (profiles[i].topics & group) != 0)
                .OrderByDescending(i => pages[i].Count > 2 || books[i].allowLoop)
                .ThenByDescending(i => pages[i].Sum(p => p.blocks.Sum(n => (n.description?.Length ?? 0) + (n.title?.Length ?? 0) + 200)))
                .First();
            if (!result.Exists(s => s.index == index)) result.Add(new Sample { index = index,
                role = group == PuzzleBookTopic.Foundations ? "heavy-foundations" : (group & PuzzleBookTopic.SignalSamples) != 0 ? "heavy-signal" : "heavy-color" });
        }
        int empty = Array.FindIndex(pages, p => p.All(page => page.blocks.Count == 0));
        if (empty >= 0) result.Add(new Sample { index = empty, role = "empty" });
        return result;
    }

    private static void Measure(InteractableBook book, Entry entry)
    {
        if (book.bookSkinnedMeshRenderer == null || book.pageTurnMaterial == null)
            throw new InvalidOperationException("Saved physical book references are missing: " + entry.book);
        var original = book.pageTurnMaterial;
        var material = new Material(original);
        book.pageTurnMaterial = material;
        try
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/NewBookAnim/newBookOpened.anim");
            clip.SampleAnimation(book.gameObject, 0);
            book.isOpen = true;
            SetIndices(book, 0);
            var paper = book.GetComponent<BookPageBlockSurface>() ?? book.gameObject.AddComponent<BookPageBlockSurface>();
            Field(paper, "book").SetValue(paper, book);
            entry.firstPrepareMs = Timed(paper.PreparePresentation);
            entry.firstPrepareCanvasMs = Timed(Canvas.ForceUpdateCanvases);
            var reopen = new List<double>(); var begin = new List<double>(); var end = new List<double>();
            var turn = new List<double>(); var canvasTimes = new List<double>(); var combined = new List<double>(); var allocations = new List<double>();
            for (int cycle = 0; cycle < Cycles + 1; cycle++)
            {
                SetIndices(book, 0);
                double reopenTime = Timed(() => { paper.PreparePresentation(); Canvas.ForceUpdateCanvases(); });
                if (cycle > 0) reopen.Add(reopenTime);
                if (!entry.canTurn) continue;
                bool started = false;
                double beginTime = Timed(() => { started = paper.BeginTurn(1); Canvas.ForceUpdateCanvases(); });
                if (!started) throw new InvalidOperationException("Representative physical book refused to turn: " + entry.book);
                if (cycle > 0) begin.Add(beginTime);
                for (int sample = 0; sample < Warmup + Samples; sample++)
                {
                    // Sample both front and reverse faces, with the same visible content every run.
                    float progress = .08f + .84f * sample / (Warmup + Samples - 1);
                    long bytes = GC.GetAllocatedBytesForCurrentThread();
                    long start = Stopwatch.GetTimestamp();
                    paper.UpdateTurn(progress);
                    long middle = Stopwatch.GetTimestamp();
                    Canvas.ForceUpdateCanvases();
                    long stop = Stopwatch.GetTimestamp();
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                    if (cycle > 0 && sample >= Warmup)
                    {
                        turn.Add(Milliseconds(middle - start)); canvasTimes.Add(Milliseconds(stop - middle));
                        combined.Add(Milliseconds(stop - start)); allocations.Add(allocated);
                    }
                }
                CountGeometry(paper, entry);
                SetIndices(book, 2);
                double endTime = Timed(() => { paper.EndTurn(); Canvas.ForceUpdateCanvases(); });
                if (cycle > 0) end.Add(endTime);
            }
            if (!entry.canTurn) { entry.issue = "Saved content has one spread; only presentation/reopen measured."; CountGeometry(paper, entry); }
            entry.reopenMs = Stats.From(reopen); entry.beginMs = Stats.From(begin); entry.endMs = Stats.From(end);
            entry.turnMs = Stats.From(turn); entry.canvasMs = Stats.From(canvasTimes);
            entry.turnAndCanvasMs = Stats.From(combined); entry.steadyAllocatedBytes = Stats.From(allocations);
        }
        finally { book.pageTurnMaterial = original; UnityEngine.Object.DestroyImmediate(material); }
    }

    private static void CountGeometry(BookPageBlockSurface paper, Entry entry)
    {
        var surface = (Canvas)Field(paper, "surface").GetValue(paper);
        var graphics = surface.GetComponentsInChildren<Graphic>(true);
        entry.graphics = graphics.Length;
        entry.activeGraphics = graphics.Count(g => g.isActiveAndEnabled && !g.canvasRenderer.cull);
        entry.textVertices = surface.GetComponentsInChildren<TMP_Text>(true).Sum(t =>
            t.textInfo.meshInfo.Take(t.textInfo.materialCount).Sum(m => m.vertexCount));
        entry.canvasVertices = graphics.Sum(g => g.canvasRenderer.GetMesh()?.vertexCount ?? 0);
        entry.triangles = ((ICollection)Field(paper, "pageTriangles").GetValue(paper)).Count;
    }

    // A fixed CPU gate makes the pre-fix bottleneck red. Relative comparison prevents later regressions.
    [MenuItem("Senzora/Book Motion/Optimization Check Before Budget")]
    public static void CheckBeforeBudget()
    {
        var before = JsonUtility.FromJson<Report>(File.ReadAllText("Temp/book-optimization-before.json"));
        var failures = before.books.Where(e => e.canTurn && e.turnAndCanvasMs.median > TurnBudgetMs)
            .Select(e => $"{e.role}: warm turn+canvas {e.turnAndCanvasMs.median:F2}ms exceeds {TurnBudgetMs}ms CPU budget.").ToArray();
        if (failures.Length > 0) throw new InvalidOperationException("BOOK BEFORE BUDGET FAIL\n" + string.Join("\n", failures));
        Debug.Log("BOOK BEFORE BUDGET PASS: measured warm turn+canvas <=4ms.");
    }

    [MenuItem("Senzora/Book Motion/Optimization Check Regression")]
    public static void CheckRegression()
    {
        var after = JsonUtility.FromJson<Report>(File.ReadAllText("Temp/book-optimization-after.json"));
        var before = JsonUtility.FromJson<Report>(File.ReadAllText("Temp/book-optimization-before.json"));
        var failures = new List<string>();
        if (after.seed != before.seed || after.poolGoals != before.poolGoals || after.profileCount != before.profileCount)
            failures.Add("Workload configuration changed.");
        foreach (var entry in after.books.Where(e => e.canTurn))
        {
            var baseline = before.books.Single(e => e.bookID == entry.bookID);
            if (entry.blocks != baseline.blocks || entry.pages != baseline.pages || entry.contentHash != baseline.contentHash)
                failures.Add(entry.role + ": workload/content shrank.");
            if (entry.turnAndCanvasMs.count < Cycles * Samples || entry.turnAndCanvasMs.median > TurnBudgetMs)
                failures.Add(entry.role + $": warm turn+canvas {entry.turnAndCanvasMs.median:F2}ms exceeds {TurnBudgetMs}ms CPU budget.");
            if (entry.turnAndCanvasMs.median > baseline.turnAndCanvasMs.median * 1.15)
                failures.Add(entry.role + ": median regressed more than 15%.");
            if (after.allocationCounterVerified && entry.steadyAllocatedBytes.max > 0)
                failures.Add(entry.role + $": steady allocation {entry.steadyAllocatedBytes.max:F0}B, expected zero.");
        }
        if (after.books.Count(e => e.canTurn) < 2) failures.Add("Heavy signal and color turn paths were not exercised.");
        if (failures.Count > 0) throw new InvalidOperationException("BOOK OPTIMIZATION FAIL\n" + string.Join("\n", failures));
        Debug.Log("BOOK OPTIMIZATION PASS: unchanged content, warm turn+canvas <=4ms, no >15% regression. Allocation counter verified=" + after.allocationCounterVerified);
    }

    private static bool AllocationCounterWorks()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var bytes = new byte[4096];
        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(bytes);
        return delta >= 4096;
    }
    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    private static string Fingerprint(List<BookWorkspacePage> pages)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", pages.Select(p => JsonUtility.ToJson(p))))));
    }
    private static double Timed(Action action) { long start = Stopwatch.GetTimestamp(); action(); return Milliseconds(Stopwatch.GetTimestamp() - start); }
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, Private)
        ?? throw new MissingFieldException(target.GetType().Name, name);
    private static void SetIndices(InteractableBook book, int left)
    { Field(book, "pageIndexL").SetValue(book, left); Field(book, "pageIndexR").SetValue(book, left + 1); }
    private static void Write(string path, object report)
    { Directory.CreateDirectory("Temp"); File.WriteAllText(path, JsonUtility.ToJson(report, true)); }
}
#endif
