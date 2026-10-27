using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEngine;

public static class ResearchGuidanceChecks
{
    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Araştırma Görünümünü Yakala")]
    public static void CapturePlay()
    {
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Laboratuvarda Play gerekli.");
        ResearchPuzzleTrial.Active.StartCoroutine(CaptureFlow());
    }

    private static IEnumerator CaptureFlow()
    {
        var game = GameManager.Instance; game.StartGameMode();
        if (game.activeInteraction != null) game.activeInteraction.ForceExit();
        float end = Time.realtimeSinceStartup + 15;
        while (game.activeInteraction != null && Time.realtimeSinceStartup < end) yield return null;
        Require(game.activeInteraction == null, "Önceki etkileşim kapanmadı.");
        var book = ResearchPuzzleTrial.Active.books[0]; book.Interact();
        end = Time.realtimeSinceStartup + 15;
        while ((!book.isOpen || book.isAnimating) && Time.realtimeSinceStartup < end) yield return null;
        yield return null; ResearchTrialBookChecks.CheckReadability();
        ScreenCapture.CaptureScreenshot("Temp/research-book-goal.png"); yield return null;
        book.ForceExit();
        while (game.activeInteraction != null) yield return null;
        Debug.Log("RESEARCH VISUAL CAPTURE PASS: Temp/research-book-goal.png");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Araştırma Hedef Görseli")]
    public static void TargetRendering()
    {
        var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(LaboratoryBlindPackSetup.Folder + "/Archive02.asset");
        Require(asset != null && asset.definition.kind == PuzzleKind.Signal, "B-29 signal asset missing.");
        var template = AssetDatabase.LoadAssetAtPath<FormulaItemSO>("Assets/Scripts/Scriptables/Formulas/SignalPartTemplate.asset");
        var content = SignalTrialContent.Create(template, asset.definition.Clone(), PuzzleBookDistribution.Build(asset.definition, 4, 17));
        var host = new GameObject("Target serialization regression", typeof(RectTransform), typeof(CanvasRenderer));
        try
        {
            var graphic = host.AddComponent<QuestDiagramGraphic>();
            var data = content.Quests[0].CreateQuest(); // Includes the actual Unity JSON save/clone boundary.
            graphic.SetQuest(data);
            Require(host.GetComponentInChildren<SignalTrialGraphic>() != null && host.GetComponentInChildren<MixtureGraphic>() == null,
                "RED: B-29 signal target rendered as an empty mixture vessel after serialization.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); content.Dispose(); }
        Debug.Log("RESEARCH TARGET RENDERING PASS: serialized B-29 displays its signal target.");
    }

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Araştırma Dağıtımı")]
    public static void Distribution()
    {
        var puzzles = Enumerable.Range(1, 3).Select(i => AssetDatabase.LoadAssetAtPath<PuzzleDefinitionSO>(LaboratoryBlindPackSetup.Folder + "/Archive" + i.ToString("D2") + ".asset").definition.Clone()).ToList();
        var profiles = Enumerable.Range(0, 47).Select(i => PuzzleBookProfile.Default(i, "profile/" + i, true)).ToList();
        string originals = string.Join("|", puzzles.Select(p => p.Fingerprint()));
        var different = new HashSet<string>();
        for (int seed = 0; seed < 60; seed++)
        {
            var pages = PuzzleBookDistribution.Build(puzzles, profiles, seed);
            CheckPlacement(puzzles, profiles, pages);
            string snapshot = Snapshot(pages);
            Require(snapshot == Snapshot(PuzzleBookDistribution.Build(puzzles, profiles, seed)), "Same seed changed placement or directions.");
            different.Add(snapshot);
        }
        Require(different.Count > 50, "Different seeds do not change books/page slots.");
        Require(originals == string.Join("|", puzzles.Select(p => p.Fingerprint())), "Distribution mutated authored goals.");
        for (int seed = 0; seed < 20; seed++) foreach (PuzzleKind kind in Enum.GetValues(typeof(PuzzleKind)))
        {
            var settings = new PuzzleDefinition { kind = kind, rules = PuzzleRules.ForKind(kind) };
            Require(PuzzleGenerator.TryGenerate(settings, seed, out var generated, out string issue), issue);
            generated.targetInFirstBook = seed % 2 == 0;
            var single = new List<PuzzleDefinition> { generated };
            CheckPlacement(single, profiles, PuzzleBookDistribution.Build(single, profiles, seed));
        }
        bool rejected = false;
        try { PuzzleBookDistribution.Build(puzzles, profiles.Take(3).Select(p => new PuzzleBookProfile { bookID = p.bookID, title = p.title, topics = PuzzleBookTopic.Foundations }).ToList(), 1); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Missing eligible slots silently placed clues into wrong books.");
        Debug.Log("RESEARCH DISTRIBUTION PASS: 60 authored three-goal seeds, 40 generated goals, eligible bounded slots, no filler duplicates, deterministic placement, accurate post-placement references, capacity rejection and immutable assets.");
    }

    private static string Snapshot(List<BookWorkspacePage>[] pages) => string.Join("|", pages.SelectMany(b => b).Select(p => JsonUtility.ToJson(p)));

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Ortak Konulu Kitap Kapasitesi")]
    public static void OverlappingCapacity()
    {
        var puzzles = new List<PuzzleDefinition> { PuzzleDefinition.Legacy() };
        var books = new List<PuzzleBookProfile> {
            new PuzzleBookProfile { bookID = "basics", title = "Temeller", topics = PuzzleBookTopic.Foundations, pageCount = 2 },
            new PuzzleBookProfile { bookID = "general", title = "Genel Arşiv", topics = PuzzleBookTopic.All, pageCount = 6 }
        };
        for (int seed = 0; seed < 24; seed++) CheckPlacement(puzzles, books, PuzzleBookDistribution.Build(puzzles, books, seed));
        Debug.Log("RESEARCH OVERLAPPING CAPACITY PASS: 24 seeds use flexible and subject-only books without false exhaustion.");
    }

    private static void CheckPlacement(List<PuzzleDefinition> puzzles, List<PuzzleBookProfile> profiles, List<BookWorkspacePage>[] pages)
    {
        var notes = pages.SelectMany(b => b).SelectMany(p => p.blocks).ToList();
        Require(notes.Select(n => n.instanceID).Distinct().Count() == notes.Count, "Duplicated clue identities.");
        Require(!notes.Any(n => n.instanceID.Contains("/copy/")), "Irrelevant books received duplicate filler clues.");
        var targets = notes.Where(n => n.blockType == NotebookBlockType.Quest).ToList();
        Require(targets.Count == puzzles.Count, "Goal was lost or duplicated.");
        Require(pages.Count(b => b.Any(p => p.blocks.Any(n => n.blockType == NotebookBlockType.Quest))) == puzzles.Count, "Goals share a book.");
        if (puzzles[0].targetInFirstBook) Require(pages[0][0].blocks.Any(n => n.questID == puzzles[0].id), "First goal moved out of the starting book.");
        for (int b = 0; b < pages.Length; b++) foreach (var page in pages[b])
        {
            Require(page.pageIndex < profiles[b].pageCount && page.blocks.Count <= 2, "Book capacity exceeded.");
            foreach (var note in page.blocks)
            {
                Require(note.bookRegion.xMin >= .08f && note.bookRegion.xMax <= .93f && note.bookRegion.yMin >= .1f && note.bookRegion.yMax <= .86f, "Card extends beyond safe page margin.");
                foreach (var other in page.blocks) if (other != note) Require(!note.bookRegion.Overlaps(other.bookRegion), "Page slots overlap.");
                if (note.blockType == NotebookBlockType.Quest) continue;
                var owner = targets.First(t => t.sourceReferences.Any(r => r.noteID == note.instanceID));
                var puzzle = puzzles.First(p => p.id == owner.questID);
                Require((profiles[b].topics & PuzzleBookDistribution.TopicFor(note, puzzle.kind)) != 0, "Clue is in a book with the wrong subject.");
            }
        }
        foreach (var target in targets)
        {
            var puzzle = puzzles.First(p => p.id == target.questID);
            Require(target.sourceReferences.Count > 0, "Goal has no research directions.");
            foreach (var source in target.sourceReferences)
            {
                int book = profiles.FindIndex(b => b.bookID == source.bookID);
                Require(book >= 0 && profiles[book].title == source.bookTitle && source.pageIndex < pages[book].Count, "Source names a missing book/page.");
                Require(pages[book][source.pageIndex].blocks.Any(n => n.instanceID == source.noteID), "Source does not match actual placement.");
                Require(target.description.Contains(source.bookTitle), "Goal omits its source book.");
            }
            foreach (var part in PuzzleParts.ForKind(puzzle.kind).Where(p => puzzle.rules.Allows(p.id)))
                Require(target.sourceReferences.Any(r => notes.Any(n => n.instanceID == r.noteID && n.blockType == NotebookBlockType.PartClue && n.itemID == part.id)), "Goal cannot find an allowed part through its sources.");
            var copy = JsonUtility.FromJson<NotebookPageSaveData>(JsonUtility.ToJson(new NotebookPageSaveData { blocks = new List<NotebookBlockData> { target } }));
            Require(JsonUtility.ToJson(copy.blocks[0]) == JsonUtility.ToJson(target), "Serialized source references changed.");
        }
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
