using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static partial class PuzzleBookDistribution
{
    private sealed class Entry
    {
        public NotebookBlockData note;
        public PuzzleBookTopic topic;
        public HashSet<string> quests = new HashSet<string>();
    }

    // Plan the entire assignment before the live session is touched. References are derived last.
    public static List<BookWorkspacePage>[] Build(List<PuzzleDefinition> puzzles, List<PuzzleBookProfile> books, int seed)
    {
        if (puzzles == null || puzzles.Count == 0 || books == null || books.Count < puzzles.Count)
            throw new ArgumentException("Her hedef için ayrı bir erişilebilir kitap gerekli.");
        if (books.Any(b => b == null || string.IsNullOrWhiteSpace(b.bookID) || string.IsNullOrWhiteSpace(b.title) || b.pageCount < 2 || b.pageCount > 64)
            || books.Select(b => b.bookID).Distinct().Count() != books.Count || books.Select(b => b.title).Distinct().Count() != books.Count)
            throw new ArgumentException("Kitap kimliği, benzersiz başlık veya sayfa kapasitesi geçersiz.");
        var random = new System.Random(seed);
        var pages = books.Select(b => Enumerable.Range(0, b.pageCount).Select(i => new BookWorkspacePage {
            pageIndex = i, title = b.title, bookID = b.bookID, sourceLocation = b.location }).ToList()).ToArray();
        var occupied = books.Select(b => new bool[b.pageCount * 2]).ToArray();
        var targets = new Dictionary<string, NotebookBlockData>();
        var entries = new List<Entry>(); var shared = new Dictionary<string, Entry>();
        var targetBooks = Enumerable.Range(0, books.Count).ToList(); Shuffle(targetBooks, random);
        for (int index = 0; index < puzzles.Count; index++)
        {
            var puzzle = puzzles[index];
            if (puzzle == null || string.IsNullOrWhiteSpace(puzzle.id) || targets.ContainsKey(puzzle.id)) throw new ArgumentException("Deney kimliği eksik veya yineleniyor.");
            var notes = Notes(puzzle);
            var target = notes[0]; targets.Add(puzzle.id, target);
            int targetBook = index == 0 && puzzle.targetInFirstBook ? 0 : targetBooks[0];
            targetBooks.Remove(targetBook);
            pages[targetBook][0].blocks.Add(target); occupied[targetBook][0] = occupied[targetBook][1] = true;
            target.bookRegion = new Rect(.09f, .11f, .82f, .74f);
            foreach (var note in notes.Skip(1))
            {
                if (note.instanceID == "rule/target" || note.instanceID.StartsWith("rule/ban/")) continue;
                bool common = note.blockType == NotebookBlockType.PartClue || note.instanceID == "rule/ports" || note.instanceID == "rule/transfer"
                    || note.instanceID == "rule/coincident" || note.instanceID == "rule/beforeafter";
                if (note.blockType == NotebookBlockType.PartClue)
                {
                    var part = PuzzleParts.Find(note.itemID); var rule = puzzle.rules.Find(note.itemID);
                    note.description = (part.inputs == 0 ? "Akışı buradan başlat. " : part.outputs == 0 ? "Son akışı burada ölç. " : "Gelen akışa uygulanır. ")
                        + (string.IsNullOrWhiteSpace(rule.explanation) ? part.description : rule.explanation);
                }
                if (note.instanceID == "rule/constraints") note.description = DescribeRules(puzzle.rules, puzzle.kind);
                AddEntry(note, puzzle, common, entries, shared);
            }
            var equipment = PuzzleParts.ForKind(puzzle.kind).Where(p => puzzle.rules.Allows(p.id))
                .Select(p => p.title + " (" + puzzle.rules.Find(p.id).minimum + "–" + puzzle.rules.Find(p.id).maximum + ")");
            AddEntry(Note("equipment", "Kullanılabilir ekipman", string.Join(", ", equipment) + ". Aralıklar kullanılabilecek adetleri gösterir."), puzzle, false, entries, shared);
            if (puzzle.notes != null) for (int p = 0; p < puzzle.notes.Count; p++)
                if (puzzle.notes[p]?.blocks != null) for (int n = 0; n < puzzle.notes[p].blocks.Count; n++)
                {
                    var custom = puzzle.notes[p].blocks[n]?.Clone(); if (custom == null) continue;
                    if (custom.blockType == NotebookBlockType.Quest) throw new ArgumentException("Ek notlar ikinci bir hedef içeremez.");
                    custom.instanceID = "custom-block/" + p + "/" + n; AddEntry(custom, puzzle, false, entries, shared);
                }
        }
        var locations = new Dictionary<string, PuzzleResearchReference>();
        var candidates = new Dictionary<PuzzleBookTopic, List<int>>();
        Shuffle(entries, random);
        foreach (var entry in entries)
        {
            if (!candidates.TryGetValue(entry.topic, out var eligible))
            {
                eligible = Enumerable.Range(0, books.Count).Where(i => (books[i].topics & entry.topic) != 0).ToList();
                Shuffle(eligible, random); candidates.Add(entry.topic, eligible);
            }
            int candidate = eligible.FindIndex(i => occupied[i].Any(used => !used));
            if (candidate < 0) throw new InvalidOperationException("Uygun kitap sayfa alanı yetersiz: " + entry.topic + ". Kitap konusunu veya sayfa kapasitesini artır.");
            int book = eligible[candidate];
            var slots = Enumerable.Range(0, occupied[book].Length).Where(i => !occupied[book][i]).ToList();
            int slot = slots[random.Next(slots.Count)]; occupied[book][slot] = true;
            int page = slot / 2;
            // Small variation stays inside the tested physical page margins.
            float x = .09f + (float)random.NextDouble() * .015f;
            entry.note.bookRegion = new Rect(x, slot % 2 == 0 ? .53f : .11f, .805f, .32f);
            pages[book][page].blocks.Add(entry.note);
            locations.Add(entry.note.instanceID, new PuzzleResearchReference { noteID = entry.note.instanceID,
                bookID = books[book].bookID, bookTitle = books[book].title, location = books[book].location, pageIndex = page });
        }
        foreach (var puzzle in puzzles)
        {
            var goal = targets[puzzle.id];
            goal.sourceReferences = entries.Where(e => e.quests.Contains(puzzle.id)).Select(e => locations[e.note.instanceID].Clone()).ToList();
            var directions = goal.sourceReferences.GroupBy(r => r.bookID).Select(g => {
                var first = g.First();
                return "• " + first.bookTitle + " · " + first.location + " · s. " + string.Join(", ", g.Select(r => r.pageIndex + 1).Distinct().OrderBy(p => p));
            });
            goal.description = puzzle.description + "\n\nARAŞTIRMA KAYNAKLARI\n" + string.Join("\n", directions)
                + "\nAkışı başlatıcıdan işlemlere, oradan sonlandırıcıya taşı; sonucu referansla karşılaştır.";
        }
        for (int book = 0; book < books.Count; book++)
        {
            int last = pages[book].FindLastIndex(p => p.blocks.Count > 0);
            pages[book].RemoveRange(Math.Max(1, last + 1), pages[book].Count - Math.Max(1, last + 1));
            string contents = string.Join("\n", pages[book].Where(p => p.blocks.Count > 0).Select(p => "s. " + (p.pageIndex + 1) + " · " + string.Join(", ", p.blocks.Select(n => n.title))));
            foreach (var page in pages[book]) page.explanation = "İÇİNDEKİLER\n" + (contents.Length > 0 ? contents : "Bu ciltteki konular için hedef kaydının kaynakçasını takip et.");
        }
        return pages;
    }

    public static PuzzleBookTopic TopicFor(NotebookBlockData note, PuzzleKind kind)
    {
        var part = note.blockType == NotebookBlockType.PartClue ? PuzzleParts.Find(note.itemID) : null;
        if (part != null)
        {
            if (part.inputs == 0 || part.outputs == 0) return PuzzleBookTopic.Foundations;
            return kind == PuzzleKind.ColorMixture ? part.sample ? PuzzleBookTopic.ColorMaterials : PuzzleBookTopic.MixtureProcessing
                : part.sample ? PuzzleBookTopic.SignalSamples : PuzzleBookTopic.SignalProcessing;
        }
        if (note.itemID == "puzzle-rule/ports" || note.itemID == "puzzle-rule/transfer") return PuzzleBookTopic.Foundations;
        return kind == PuzzleKind.ColorMixture ? PuzzleBookTopic.MixtureProcessing : PuzzleBookTopic.SignalProcessing;
    }

    private static void AddEntry(NotebookBlockData note, PuzzleDefinition puzzle, bool common, List<Entry> entries, Dictionary<string, Entry> shared)
    {
        string key = note.instanceID + "/" + note.description;
        if (common && shared.TryGetValue(key, out var existing)) { existing.quests.Add(puzzle.id); return; }
        var entry = new Entry { note = note, topic = TopicFor(note, puzzle.kind) }; entry.quests.Add(puzzle.id);
        note.instanceID = puzzle.id + "/" + note.instanceID;
        if (!common) { note.questID = puzzle.id; note.title = puzzle.title + " / " + note.title; }
        entries.Add(entry); if (common) shared.Add(key, entry);
    }

    private static void Shuffle<T>(List<T> values, System.Random random)
    { for (int i = values.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); } }
}
