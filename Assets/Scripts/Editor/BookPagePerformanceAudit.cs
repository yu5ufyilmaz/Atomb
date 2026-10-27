#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

// Diagnostic only: preview scenes and temporary instances; no scene or runtime source is saved.
public static class BookPagePerformanceAudit
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private class Report { public List<Entry> books = new List<Entry>(); public string note; }
    [Serializable] private class Entry
    {
        public string scene, book, role;
        public int pages, blocks, graphics, textVertices, triangles;
        public bool physical, printed, canTurn;
        public double beginMs, turnMedianMs, turnMaxMs, canvasMedianMs, meshOnlyMedianMs, allocationKB;
    }

    [MenuItem("Senzora/Book Motion/Audit Page Turn CPU")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play mode; preview scenes protect the open scene.");
        var report = new Report { note = "Editor CPU microbenchmark; not end-to-end GPU FPS. Median of 9 warm samples per update. Current configured two-page books cannot turn and are not forced to do so." };
        foreach (string path in new[] { "Assets/Scenes/scn_LaboratorySynthesis.unity", "Assets/Scenes/scn_labratory.unity" })
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            SignalTrialContent content = null;
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<InteractableBook>(true)).ToArray();
                var trial = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchPuzzleTrial>(true)).FirstOrDefault();
                if (trial != null && trial.partTemplate != null) content = SignalTrial.CreateContent(trial.partTemplate);
                foreach (var book in all)
                {
                    int index = trial == null ? -1 : Array.IndexOf(trial.books, book);
                    if (index >= 0 && content != null)
                    {
                        book.synthesisPages = content.Books[index];
                        book.totalPages = Mathf.Max(2, (content.Books[index].Count + 1) / 2 * 2);
                        book.usePhysicalPageBlocks = true;
                    }
                    var entry = new Entry { scene = scene.name, book = PathOf(book.transform),
                        role = index < 0 ? "ordinary" : new[] { "questions", "atlas", "principles", "records" }[index],
                        pages = book.totalPages, physical = book.usePhysicalPageBlocks, printed = book.printNotesOnPages,
                        blocks = book.synthesisPages?.Sum(p => p?.blocks?.Count ?? 0) ?? 0,
                        canTurn = book.totalPages > 2 || book.allowLoop };
                    report.books.Add(entry);
                    // Benchmark every research book that can turn, plus representative ordinary paths.
                    if (entry.canTurn && (index >= 0 || report.books.Count(e => e.scene == scene.name && e.role == "ordinary" && e.turnMedianMs > 0) < 3))
                        Measure(book, entry);
                }
            }
            finally { content?.Dispose(); EditorSceneManager.ClosePreviewScene(scene); }
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/book-performance-audit.json", JsonUtility.ToJson(report, true));
        Debug.Log("BOOK PERFORMANCE AUDIT: Temp/book-performance-audit.json\n" + string.Join("\n", report.books.Where(e => e.turnMedianMs > 0 || e.role != "ordinary")
            .Select(e => $"{e.scene}/{e.role}/{e.book}: pages={e.pages}, blocks={e.blocks}, physical={e.physical}, turn={e.turnMedianMs:F2}ms, mesh-only={e.meshOnlyMedianMs:F2}ms, canvas={e.canvasMedianMs:F2}ms, allocation={e.allocationKB:F1}KB")));
    }

    private static void Measure(InteractableBook book, Entry entry)
    {
        if (!book.usePhysicalPageBlocks || book.bookSkinnedMeshRenderer == null || book.pageTurnMaterial == null) return;
        var material = new Material(book.pageTurnMaterial);
        var original = book.pageTurnMaterial;
        book.pageTurnMaterial = material;
        try
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/NewBookAnim/newBookOpened.anim");
            clip.SampleAnimation(book.gameObject, 0);
            typeof(InteractableBook).GetField("pageIndexL", Private).SetValue(book, 0);
            typeof(InteractableBook).GetField("pageIndexR", Private).SetValue(book, 1);
            var paper = book.GetComponent<BookPageBlockSurface>() ?? book.gameObject.AddComponent<BookPageBlockSurface>();
            typeof(BookPageBlockSurface).GetField("book", Private).SetValue(paper, book);
            paper.PreparePresentation();
            var watch = Stopwatch.StartNew();
            bool started = paper.BeginTurn(1);
            entry.beginMs = watch.Elapsed.TotalMilliseconds;
            if (!started) return;
            Canvas.ForceUpdateCanvases();
            var canvas = (Canvas)typeof(BookPageBlockSurface).GetField("surface", Private).GetValue(paper);
            entry.graphics = canvas.GetComponentsInChildren<Graphic>(true).Length;
            entry.textVertices = canvas.GetComponentsInChildren<TMP_Text>(true).Sum(t => t.textInfo.meshInfo.Take(t.textInfo.materialCount).Sum(m => m.vertexCount));
            entry.triangles = ((System.Collections.ICollection)typeof(BookPageBlockSurface).GetField("pageTriangles", Private).GetValue(paper)).Count;
            var full = new List<double>(); var rebuild = new List<double>(); var allocated = new List<double>();
            for (int i = 0; i < 12; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                watch.Restart(); paper.UpdateTurn(.2f + i * .04f); double elapsed = watch.Elapsed.TotalMilliseconds;
                watch.Restart(); Canvas.ForceUpdateCanvases(); double canvasMs = watch.Elapsed.TotalMilliseconds;
                if (i >= 3) { full.Add(elapsed); rebuild.Add(canvasMs); allocated.Add((GC.GetAllocatedBytesForCurrentThread() - before) / 1024.0); }
            }
            var field = typeof(BookPageBlockSurface).GetField("animatedInk", Private);
            var ink = field.GetValue(paper);
            field.SetValue(paper, Array.Empty<Graphic>());
            var mesh = new List<double>();
            for (int i = 0; i < 12; i++)
            { watch.Restart(); paper.UpdateTurn(.2f + i * .04f); if (i >= 3) mesh.Add(watch.Elapsed.TotalMilliseconds); }
            field.SetValue(paper, ink);
            entry.turnMedianMs = Median(full); entry.turnMaxMs = full.Max(); entry.canvasMedianMs = Median(rebuild);
            entry.meshOnlyMedianMs = Median(mesh); entry.allocationKB = Median(allocated);
            paper.EndTurn();
        }
        finally { book.pageTurnMaterial = original; UnityEngine.Object.DestroyImmediate(material); }
    }
    private static double Median(List<double> values) { values.Sort(); return values[values.Count / 2]; }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
#endif
