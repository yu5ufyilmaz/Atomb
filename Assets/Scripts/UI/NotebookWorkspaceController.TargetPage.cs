using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Araştırma defterinin Hedef sayfası: üstte hedef kartı, altında hedefe özel bilgiler, hedefin parçaları ve
// kitaptan alınan "Kullanılabilir ekipman" notunun yerine geçen görsel numune kartı. Eksikler kesik kutuyla gösterilir.
public partial class NotebookWorkspaceController
{
    private const string EquipmentTitle = "Kullanılabilir ekipman";
    private static readonly Color PlaceholderFill = new Color(.09f, .11f, .095f, .05f), PlaceholderLine = new Color(.09f, .11f, .095f, .35f);

    private static PuzzleDefinition PuzzleOf(string questID)
        => ResearchPuzzleTrial.Active?.Puzzles.FirstOrDefault(p => p.id == questID);

    // Kitap dağıtımı hedefe özel notlara questID yazar; ölçüm kartları ayrı sekmededir.
    private static bool IsTargetNote(NotebookBlockData note)
        => note != null && note.blockType == NotebookBlockType.RuleClue && !string.IsNullOrEmpty(note.questID) && !SignalExperiment.IsMeasurementNote(note);
    private static bool IsEquipmentNote(NotebookBlockData note) => (note?.title ?? "").EndsWith(EquipmentTitle);

    private static List<string> ExpectedTargetNoteTitles(PuzzleDefinition puzzle)
    {
        var titles = new List<string>();
        if (puzzle == null) return titles;
        string[] general = { "rule/target", "rule/ports", "rule/transfer", "rule/coincident", "rule/beforeafter" };
        foreach (var note in PuzzleBookDistribution.Notes(puzzle))
            if (note.blockType == NotebookBlockType.RuleClue && !general.Contains(note.instanceID) && !note.instanceID.StartsWith("rule/ban/"))
                titles.Add(puzzle.title + " / " + note.title);
        if (puzzle.notes != null)
            foreach (var page in puzzle.notes)
                if (page?.blocks != null)
                    foreach (var block in page.blocks)
                        if (block != null && block.blockType == NotebookBlockType.RuleClue) titles.Add(puzzle.title + " / " + block.title);
        return titles;
    }

    private void BuildTargetPage(NotebookBlockData target)
    {
        float y = 8;
        var puzzle = target != null ? PuzzleOf(target.questID) : null;
        var notes = PuzzleInformation.NotebookNotes(notebook);
        var collected = target != null ? notes.FindAll(n => IsTargetNote(n) && n.questID == target.questID) : new List<NotebookBlockData>();
        var equipment = collected.Find(IsEquipmentNote);
        collected.RemoveAll(IsEquipmentNote);

        if (target != null)
        {
            var block = CreateBlock(target, false, targetContent);
            var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(440, 176);
            Top(rect, new Vector2(0, -y)); ApplyArchiveNotePose(rect, target); y += 188;
            // Kart 400 yerine 176 yüksek: hedef görseli başlığın altına, açıklama görselin altına.
            var diagram = block.transform.Find("Hedef Şema") as RectTransform;
            if (diagram != null) { diagram.anchorMin = new Vector2(0, .5f); diagram.anchorMax = new Vector2(1, .78f); }
            var description = block.transform.Find("Açıklama") as RectTransform;
            if (description != null) description.anchorMax = new Vector2(1, .47f);
        }
        else
        {
            var empty = Placeholder("HEDEF YOK", new Vector2(440, 150), new Vector2(0, -y));
            Label(empty, "Kitaptaki hedef notunu deftere bırak.\nTopladığın hedefler bu sayfalarda saklanır.", new Vector2(0, -28), new Vector2(400, 60), 14);
            y += 162;
        }

        y += Heading("HEDEFİN BİLGİLERİ", y);
        var shown = new HashSet<NotebookBlockData>();
        foreach (string title in ExpectedTargetNoteTitles(puzzle))
        {
            var note = collected.Find(n => n.title == title && !shown.Contains(n));
            if (note != null) { shown.Add(note); y += PlaceTargetNote(note, y); }
            else { Placeholder("BİLGİ YOK", new Vector2(440, 52), new Vector2(0, -y)); y += 60; }
        }
        foreach (var note in collected) if (shown.Add(note)) y += PlaceTargetNote(note, y);
        if (shown.Count == 0 && puzzle == null) { Placeholder("BİLGİ YOK", new Vector2(440, 52), new Vector2(0, -y)); y += 60; }

        y += Heading("HEDEFİN PARÇALARI", y);
        var allowed = puzzle != null ? PuzzleParts.ForKind(puzzle.kind).Where(p => puzzle.rules.Allows(p.id)).ToList() : new List<SignalPartLibrary.Part>();
        var archived = ArchivedParts();
        const int columns = 3; const float cellWidth = 142, cellHeight = 104, pitch = 149;
        if (allowed.Count == 0) { Placeholder("PARÇA YOK", new Vector2(440, 52), new Vector2(0, -y)); y += 60; }
        for (int i = 0; i < allowed.Count; i++)
        {
            var position = new Vector2(-pitch + i % columns * pitch, -y - i / columns * (cellHeight + 8));
            var note = archived.Find(n => n.itemID == allowed[i].id);
            if (note != null)
            {
                var block = CreateBlock(note, false, targetContent);
                var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(cellWidth, cellHeight);
                Top(rect, position); ApplyArchiveNotePose(rect, note); CompactPartCard(block);
            }
            else Placeholder("PARÇA YOK", new Vector2(cellWidth, cellHeight), position);
        }
        if (allowed.Count > 0) y += (allowed.Count + columns - 1) / columns * (cellHeight + 8) + 4;

        y += Heading("GEREKLİ NUMUNELER", y);
        if (equipment != null && puzzle != null) PlaceSampleCard(equipment, puzzle, allowed.FindAll(p => p.sample), y);
        else
        {
            var empty = Placeholder("PARÇA BİLGİSİ YOK", new Vector2(440, 104), new Vector2(0, -y));
            Label(empty, "Kitaptaki ekipman notunu deftere al;\nbu deneyin numuneleri burada görünür.", new Vector2(0, -22), new Vector2(400, 44), 13);
        }
        y += 152;
        targetContent.sizeDelta = new Vector2(0, Mathf.Max(targetPanel.rect.height, y));
        targetContent.anchoredPosition = Vector2.zero;
    }

    // Küçük parça kutusu: başlık ve çizim kalır, açıklama metni sığmadığı için gizlenir.
    private static void CompactPartCard(NotebookDraggableBlock block)
    {
        var description = block.transform.Find("Açıklama"); if (description != null) description.gameObject.SetActive(false);
        var drawing = block.GetComponentInChildren<SignalTrialGraphic>()?.rectTransform ?? block.GetComponentInChildren<MixtureGraphic>()?.rectTransform;
        if (drawing != null) { drawing.anchorMin = new Vector2(.06f, .05f); drawing.anchorMax = new Vector2(.94f, .44f); }
    }

    private float PlaceTargetNote(NotebookBlockData note, float y)
    {
        var block = CreateBlock(note, false, targetContent);
        var rect = (RectTransform)block.transform;
        rect.sizeDelta = new Vector2(440, rect.sizeDelta.y);
        Top(rect, new Vector2(0, -y)); ApplyArchiveNotePose(rect, note);
        return rect.sizeDelta.y + 10;
    }

    // Ekipman notu kartın kendisidir (tahtaya sürüklenebilir); yazısı yerine izinli numuneler ve adetleri çizilir.
    private void PlaceSampleCard(NotebookBlockData equipment, PuzzleDefinition puzzle, List<SignalPartLibrary.Part> samples, float y)
    {
        var block = CreateBlock(equipment, false, targetContent);
        var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(440, 140);
        Top(rect, new Vector2(0, -y)); rect.localRotation = Quaternion.identity;
        var description = block.transform.Find("Açıklama"); if (description != null) description.gameObject.SetActive(false);
        var badge = block.transform.Find("Bulmaca Etiketi"); if (badge != null) badge.gameObject.SetActive(false);
        var row = Panel("Numuneler", rect, new Vector2(420, 66), Color.clear);
        row.anchoredPosition = new Vector2(0, -24); row.GetComponent<Image>().raycastTarget = false;
        if (samples.Count == 0)
        {
            var none = Label(row, "", Vector2.zero, new Vector2(400, 40), 13);
            GameLocalization.SetFormat(none, "Numune: {0}–{1} adet", puzzle.rules.minSamples, puzzle.rules.maxSamples);
            return;
        }
        float cell = Mathf.Min(120, 420f / samples.Count);
        for (int i = 0; i < samples.Count; i++)
        {
            var slot = Panel(samples[i].title, row, new Vector2(cell - 6, 66), Color.clear);
            slot.anchoredPosition = new Vector2(-210 + cell * (i + .5f), 0); slot.GetComponent<Image>().raycastTarget = false;
            SignalTrialGraphic.Add(slot, new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = samples[i].id }, new Vector2(.06f, .34f), new Vector2(.94f, 1));
            var rule = puzzle.rules.Find(samples[i].id);
            var count = Label(slot, "", new Vector2(0, -24), new Vector2(cell - 8, 20), 12);
            // Çizim sembolü zaten yazar; altına yalnız adet aralığı gelir.
            GameLocalization.SetFormat(count, "×{0}–{1}", rule != null ? rule.minimum : 0, rule != null ? rule.maximum : 0);
        }
    }

    private float Heading(string text, float y)
    {
        var label = Label(targetContent, text, Vector2.zero, new Vector2(440, 22), 13);
        Top(label.rectTransform, new Vector2(0, -y));
        label.alignment = TMPro.TextAlignmentOptions.Left; label.fontStyle = TMPro.FontStyles.Bold;
        var rule = Panel("Başlık Çizgisi", targetContent, new Vector2(440, 1), PlaceholderLine);
        Top(rule, new Vector2(0, -y - 23)); rule.GetComponent<Image>().raycastTarget = false;
        return 30;
    }

    private RectTransform Placeholder(string text, Vector2 size, Vector2 position)
    {
        var box = Panel("Boş Yuva", targetContent, size, PlaceholderFill);
        Top(box, position); box.GetComponent<Image>().raycastTarget = false;
        var outline = box.gameObject.AddComponent<Outline>(); outline.effectColor = PlaceholderLine; outline.effectDistance = new Vector2(1, -1);
        var label = Label(box, text, new Vector2(0, size.y > 70 ? size.y * .5f - 22 : 0), new Vector2(size.x - 12, 22), 13);
        label.fontStyle = TMPro.FontStyles.Italic; label.color = PlaceholderLine;
        return box;
    }

    private struct PartsPage { public string group; public List<NotebookBlockData> notes; }

    // Parçalar sekmesi role göre sayfalanır: her başlık kendi sayfalarında, sayfa başına PartsPerPage kart.
    private List<PartsPage> PartsPages()
    {
        var pages = new List<PartsPage>(); var notes = ArchivedParts();
        foreach (string group in new[] { "BAŞLATICI", "NUMUNE", "İŞLEM", "SONLANDIRICI", "DİĞER" })
        {
            var members = notes.FindAll(n => PartGroup(n) == group);
            for (int i = 0; i < members.Count; i += PartsPerPage)
                pages.Add(new PartsPage { group = group, notes = members.GetRange(i, Mathf.Min(PartsPerPage, members.Count - i)) });
        }
        if (pages.Count == 0) pages.Add(new PartsPage { group = "", notes = new List<NotebookBlockData>() });
        return pages;
    }

    private static string PartGroup(NotebookBlockData note)
    {
        var part = PuzzleParts.Find(note.itemID);
        if (part == null) return "DİĞER";
        return part.inputs == 0 ? "BAŞLATICI" : part.outputs == 0 ? "SONLANDIRICI" : part.sample ? "NUMUNE" : "İŞLEM";
    }
}
