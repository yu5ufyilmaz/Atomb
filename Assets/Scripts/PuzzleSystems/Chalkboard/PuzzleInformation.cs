using System;
using System.Collections.Generic;

// Aynı kimlik ve yuva bilgisi kitap, defter ve fiziksel pano arasında korunur.
public static class PuzzleInformation
{
    public static List<NotebookBlockData> NotebookNotes(NotebookUI notebook)
    {
        var notes = new List<NotebookBlockData>();
        if (notebook == null || notebook.workspacePages == null) return notes;
        foreach (var page in notebook.workspacePages)
            if (page != null && page.blocks != null)
                notes.AddRange(page.blocks.FindAll(n => n != null && n.blockType == NotebookBlockType.RuleClue));
        return notes;
    }

    public static List<string> KnownQuestIDs(ChalkboardManager board)
    {
        var ids = new List<string>();
        if (board != null)
        {
            if (board.practiceQuest != null) ids.Add(ID(board.practiceQuest));
            foreach (var quest in board.practicePuzzles)
                if (quest != null && !ids.Contains(ID(quest))) ids.Add(ID(quest));
        }
        return ids;
    }

    public static int ExpectedCount(string id, ChalkboardManager board) => Clues(id, board).Count;

    public static string Title(string id, ChalkboardManager board)
    {
        var practice = board != null ? board.FindPracticeQuest(id) : null;
        if (practice != null) return practice.title;
        return "Bağlantısı belirsiz bilgiler";
    }

    public static bool TryResolve(NotebookBlockData note, ChalkboardManager board)
    {
        if (note == null || note.blockType != NotebookBlockType.RuleClue) return false;
        var ids = string.IsNullOrEmpty(note.questID) ? KnownQuestIDs(board) : new List<string> { note.questID };
        if (!string.IsNullOrEmpty(note.questID) && note.ruleIndex >= 0 && note.ruleIndex < ExpectedCount(note.questID, board)) return true;
        string foundID = null; int foundIndex = -1, matches = 0;
        foreach (var id in ids)
        {
            var clues = Clues(id, board);
            for (int i = 0; i < clues.Count; i++)
                if (!string.IsNullOrWhiteSpace(note.description) && string.Equals(note.description.Trim(), (clues[i] ?? "").Trim(), StringComparison.Ordinal))
                { foundID = id; foundIndex = i; matches++; }
        }
        if (matches != 1) return false;
        note.questID = foundID; note.ruleIndex = foundIndex;
        return true;
    }

    public static NotebookBlockData Find(List<NotebookBlockData> notes, string questID, int slot, ChalkboardManager board)
    {
        foreach (var note in notes)
            if (TryResolve(note, board) && note.questID == questID && note.ruleIndex == slot) return note;
        return null;
    }

    public static int FoundCount(List<NotebookBlockData> notes, string questID, ChalkboardManager board)
    {
        int found = 0;
        for (int slot = 0; slot < ExpectedCount(questID, board); slot++) if (Find(notes, questID, slot, board) != null) found++;
        return found;
    }

    private static List<string> Clues(string id, ChalkboardManager board)
    {
        var practice = board != null ? board.FindPracticeQuest(id) : null;
        if (practice != null) return practice.clueNotes ?? new List<string>();
        return new List<string>();
    }
    private static string ID(QuestBlueprintSO quest) => string.IsNullOrEmpty(quest.questID) ? quest.name : quest.questID;
}
