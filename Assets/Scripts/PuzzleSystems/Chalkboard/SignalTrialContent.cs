using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime copies are owned by the session; authoring assets are never modified in Play.
public sealed class SignalTrialContent
{
    public List<FormulaItemSO> Parts = new List<FormulaItemSO>();
    public List<QuestBlueprintSO> Quests = new List<QuestBlueprintSO>();
    public List<BookWorkspacePage>[] Books = new List<BookWorkspacePage>[4];

    public static bool OwnsQuest(string id) => id == SignalTrial.QuestID
        || (ResearchPuzzleTrial.Active != null && ResearchPuzzleTrial.Active.Content != null
            && ResearchPuzzleTrial.Active.Content.Quests.Exists(q => q != null && q.questID == id));

    public static SignalTrialContent Create(FormulaItemSO template, PuzzleDefinition puzzle, List<BookWorkspacePage>[] pages)
        => Create(template, new List<PuzzleDefinition> { puzzle }, pages);

    public static SignalTrialContent Create(FormulaItemSO template, List<PuzzleDefinition> puzzles, List<BookWorkspacePage>[] pages)
    {
        var content = new SignalTrialContent { Books = pages };
        var added = new HashSet<string>();
        foreach (var puzzle in puzzles)
        {
        foreach (var part in PuzzleParts.ForKind(puzzle.kind)) if (puzzle.rules.Allows(part.id))
            if (added.Add(part.id)) content.AddPart(template, part.id, part.title, part.inputs + part.outputs, part.description);
        }
        foreach (var puzzle in puzzles)
        {
        string summary = puzzle.description;
        foreach (var book in pages) foreach (var page in book) foreach (var note in page.blocks)
            if (note.blockType == NotebookBlockType.Quest && note.questID == puzzle.id) summary = note.description;
        var quest = content.AddQuest(puzzle.id, puzzle.title, 0, summary);
        quest.puzzleKind = puzzle.kind;
        foreach (var part in content.Parts) if (puzzle.rules.Allows(part.itemID))
        {
            quest.nodes.Add(new QuestNodeData { itemID = part.itemID, isUnknown = false });
            quest.clueParts.Add(part);
        }
        quest.targetPulses = puzzle.kind == PuzzleKind.Signal && puzzle.target != null ? new List<SignalTrial.Pulse>(puzzle.target) : new List<SignalTrial.Pulse>();
        quest.mixtureTarget = puzzle.kind == PuzzleKind.ColorMixture ? puzzle.mixtureTarget?.Copy() : null;
        quest.clueNotes = new List<string>();
        }
        return content;
    }

    public void Dispose()
    {
        foreach (var quest in Quests) DestroyRuntime(quest);
        foreach (var part in Parts) DestroyRuntime(part);
        Quests.Clear(); Parts.Clear();
        foreach (var pages in Books) pages?.Clear();
    }

    internal void AddPart(FormulaItemSO template, string id, string title, int capacity, string description)
    {
        var part = UnityEngine.Object.Instantiate(template);
        part.name = id;
        part.hideFlags = HideFlags.DontSave;
        part.itemID = id;
        part.itemName = title;
        part.description = description;
        part.connectionCapacity = capacity;
        part.isCore = false;
        part.isDecoy = false;
        part.useUniversalNodeRules = true;
        part.connectionAliasID = "";
        part.doubleBondNodes = new List<int>();
        part.initiallyBlockedNodes = new List<int>();
        part.initiallyUnstableNodes = new List<int>();
        Parts.Add(part);
    }

    internal QuestBlueprintSO AddQuest(string id, string title, int partCount, string summary)
    {
        var quest = ScriptableObject.CreateInstance<QuestBlueprintSO>();
        quest.name = quest.questID = id;
        quest.hideFlags = HideFlags.DontSave;
        quest.title = title;
        quest.clueSummary = summary;
        if (id == SignalTrial.QuestID) quest.targetPulses = PuzzleDefinition.Legacy().target;
        quest.output = null;
        for (int i = 0; i < partCount; i++)
        {
            quest.nodes.Add(new QuestNodeData { itemID = Parts[i].itemID, isUnknown = false });
            quest.clueParts.Add(Parts[i]);
        }
        // Genel notlar bağımsız arşivlenir; hedef için bulunacak ipucu yuvaları üretilmez.
        quest.clueNotes = new List<string>();
        Quests.Add(quest);
        return quest;
    }

    private static void DestroyRuntime(UnityEngine.Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(item);
        else UnityEngine.Object.DestroyImmediate(item);
    }
}
