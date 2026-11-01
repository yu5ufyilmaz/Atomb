using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Araştırma defterinin Hedef sayfası: kaydırmasız kontrol listesi.
// Üstte hedef kartı ve hazırlık çubuğu; GEREKENLER (deney koşulu + ekipman notu), İPUCU (puzzle yazarının notları),
// GEREKLİ NUMUNELER (izinli numune çipleri). Eksik not satırında bulunduğu kitabın adı, eksik numunede yalnız "?" yazar.
// Ayrıntılar bu sayfada değil, Parçalar ve Bilgiler sekmelerindeki kartlarda kalır.
public partial class NotebookWorkspaceController
{
    private const string EquipmentTitle = "Kullanılabilir ekipman";
    private static readonly Color PlaceholderFill = new Color(.09f, .11f, .095f, .05f), PlaceholderLine = new Color(.09f, .11f, .095f, .35f);
    private static readonly Color DoneInk = new Color(.06f, .43f, .34f), MissingInk = new Color(.52f, .31f, .04f);

    private sealed class TargetItem { public string title; public NotebookBlockData note; public string book; }

    private static PuzzleDefinition PuzzleOf(string questID)
        => ResearchPuzzleTrial.Active?.Puzzles.FirstOrDefault(p => p.id == questID);

    // Kitap dağıtımı hedefe özel notlara questID yazar; ölçüm kartları ayrı sekmededir.
    private static bool IsTargetNote(NotebookBlockData note)
        => note != null && note.blockType == NotebookBlockType.RuleClue && !string.IsNullOrEmpty(note.questID) && !SignalExperiment.IsMeasurementNote(note);

    private static string ShortTitle(string title)
    {
        int cut = (title ?? "").IndexOf(" / ");
        return cut >= 0 ? title.Substring(cut + 3) : title ?? "";
    }

    // Not hangi kitaptaysa "kitap adı · raf"; dağıtım notları kitap sayfalarına aynı başlık ve kimlikle koyar.
    private static string BookOf(System.Predicate<NotebookBlockData> match)
    {
        var trial = ResearchPuzzleTrial.Active;
        if (trial == null) return null;
        for (int i = 0; i < trial.books.Length; i++)
        {
            var book = trial.books[i];
            if (book == null || book.synthesisPages == null) continue;
            foreach (var page in book.synthesisPages)
                if (page?.blocks != null && page.blocks.Exists(b => b != null && match(b)))
                {
                    var profile = book.TryGetComponent<PuzzleBookIdentity>(out var identity) ? identity.Profile(i) : PuzzleBookProfile.Default(i, "book/" + i);
                    return string.IsNullOrWhiteSpace(profile.location) ? profile.title : profile.title + " · " + profile.location;
                }
        }
        return null;
    }

    // hints=false: deney koşulu ve ekipman notu. hints=true: puzzle'a özel ek notlar.
    private List<TargetItem> ExpectedItems(PuzzleDefinition puzzle, NotebookBlockData target, bool hints)
    {
        var items = new List<TargetItem>();
        if (puzzle == null) return items;
        var collected = target != null ? PuzzleInformation.NotebookNotes(notebook).FindAll(n => IsTargetNote(n) && n.questID == target.questID) : new List<NotebookBlockData>();
        void Add(string title)
        {
            var note = collected.Find(n => n.title == title && !items.Exists(i => i.note == n));
            items.Add(new TargetItem { title = title, note = note, book = note == null ? BookOf(b => b.title == title) : null });
        }
        foreach (var note in PuzzleBookDistribution.Notes(puzzle))
        {
            if (note.blockType != NotebookBlockType.RuleClue) continue;
            string id = note.instanceID ?? "";
            if (hints ? id.StartsWith("rule/custom/") : id == "rule/constraints") Add(puzzle.title + " / " + note.title);
        }
        if (!hints) Add(puzzle.title + " / " + EquipmentTitle);
        if (hints && puzzle.notes != null)
            foreach (var page in puzzle.notes)
                if (page?.blocks != null)
                    foreach (var block in page.blocks)
                        if (block != null && block.blockType == NotebookBlockType.RuleClue) Add(puzzle.title + " / " + block.title);
        return items;
    }

    // Karışım deneyinde şişe çizilen bütün sıvılar (pigment, çözücü, yoğunlaştırıcı) numune gibi davranır.
    private static bool IsSampleLike(SignalPartLibrary.Part part)
        => part != null && (part.sample || (ColorMixtureLibrary.Find(part.id) != null && part.inputs == 1 && part.outputs == 1));

    private static List<SignalPartLibrary.Part> AllowedParts(PuzzleDefinition puzzle)
        => puzzle != null ? PuzzleParts.ForKind(puzzle.kind).Where(p => puzzle.rules.Allows(p.id)).ToList() : new List<SignalPartLibrary.Part>();

    private void BuildTargetPage(NotebookBlockData target)
    {
        float y = 4;
        var puzzle = target != null ? PuzzleOf(target.questID) : null;
        var required = ExpectedItems(puzzle, target, false);
        var hints = ExpectedItems(puzzle, target, true);
        var archived = ArchivedParts();
        // Puzzle tanımı yoksa (eski kayıt, eğitim) en azından toplanmış numuneler görünsün.
        var samples = puzzle != null ? AllowedParts(puzzle).FindAll(IsSampleLike)
            : archived.Select(n => PuzzleParts.Find(n.itemID)).Where(IsSampleLike).ToList();
        int total = required.Count + hints.Count + samples.Count;
        int done = required.Count(i => i.note != null) + hints.Count(i => i.note != null) + samples.Count(s => archived.Exists(n => n.itemID == s.id));

        if (target != null)
        {
            var block = CreateBlock(target, false, targetContent);
            var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(440, 70);
            Top(rect, new Vector2(0, -y)); rect.localRotation = Quaternion.identity;
            CompactTargetCard(block, done, total);
        }
        else
        {
            var empty = Placeholder(targetContent, "HEDEF YOK", new Vector2(440, 70), new Vector2(0, -y));
            Label(empty, "Kitaptaki hedef notunu deftere bırak.", new Vector2(0, -16), new Vector2(400, 20), 12).fontStyle = FontStyles.Italic;
        }
        y += 78;

        // GEREKENLER: yalnız "?" kutuları; numune alınınca kutu açılır. Kitap bilgisi burada yazmaz.
        y += Heading("GEREKENLER", y); y += 4;
        int count = samples.Count == 0 ? 3 : samples.Count;
        int columns = count > 4 ? 5 : 4; float pitch = columns > 4 ? 88 : 112, chipWidth = columns > 4 ? 84 : 100;
        for (int i = 0; i < count; i++)
        {
            var position = new Vector2(-pitch * (columns - 1) / 2 + i % columns * pitch, -y - i / columns * 52);
            var note = i < samples.Count ? archived.Find(n => n.itemID == samples[i].id) : null;
            if (note != null) SampleChip(note, position, chipWidth);
            else Placeholder(targetContent, "?", new Vector2(chipWidth, 48), position);
        }
        y += (count + columns - 1) / columns * 52;

        // İPUCU: deney koşulu, ekipman notu ve puzzle'a özel notlar; eksikte kitap · raf.
        y += Heading("İPUCU", y);
        var items = required.Concat(hints).ToList();
        if (items.Count == 0) y += PlaceholderRow(null, y);
        foreach (var item in items) y += item.note != null ? NoteRow(item.note, y) : PlaceholderRow(item.book, y);

        // NUMUNELERİN YERİ: her numune için kitap · raf; alınınca adı yazılır. Kutular "?" kalır, bilgi burada.
        y += 2; y += Heading("NUMUNELERİN YERİ", y);
        if (samples.Count == 0) y += PlaceholderRow(null, y);
        foreach (var sample in samples)
        {
            bool have = archived.Exists(n => n.itemID == sample.id);
            y += have ? DoneRow(sample.title, y) : PlaceholderRow(BookOf(b => b.blockType == NotebookBlockType.PartClue && b.itemID == sample.id), y);
        }
        targetContent.sizeDelta = new Vector2(0, Mathf.Max(targetPanel.rect.height, y));
        targetContent.anchoredPosition = Vector2.zero;
    }

    // 84 piksellik hedef kartı: solda şema, sağda ad ve hazırlık çubuğu; uzun açıklama gizli.
    private void CompactTargetCard(NotebookDraggableBlock block, int done, int total)
    {
        var rect = (RectTransform)block.transform;
        var description = block.transform.Find("Açıklama"); if (description != null) description.gameObject.SetActive(false);
        // Şemadaki karışım değerleri yazısı 84 piksele sığmaz; ayrıntı hedef kartının büyük halinde kalır.
        foreach (var text in block.GetComponentsInChildren<TextMeshProUGUI>(true)) if (text.name == "Karışım Değerleri") text.gameObject.SetActive(false);
        var diagram = block.transform.Find("Hedef Şema") as RectTransform;
        if (diagram != null) { diagram.anchorMin = new Vector2(.02f, .12f); diagram.anchorMax = new Vector2(.2f, .88f); diagram.offsetMin = diagram.offsetMax = Vector2.zero; }
        var title = block.transform.Find("Başlık") as RectTransform;
        if (title != null) { title.anchorMin = new Vector2(.22f, .5f); title.anchorMax = new Vector2(1, 1); title.offsetMin = new Vector2(0, 0); title.offsetMax = new Vector2(-8, -6); }
        var progress = Label(rect, "", new Vector2(50, -12), new Vector2(320, 18), 12);
        progress.alignment = TextAlignmentOptions.Left; progress.color = done >= total && total > 0 ? DoneInk : ink;
        GameLocalization.SetFormat(progress, "Hazırlık {0} / {1}", done, total);
        var track = Panel("Hazırlık Çubuğu", rect, new Vector2(320, 5), new Color(.09f, .11f, .095f, .15f));
        track.anchoredPosition = new Vector2(50, -26); track.GetComponent<Image>().raycastTarget = false;
        float ratio = total > 0 ? (float)done / total : 0;
        var fill = Panel("Hazırlık Dolgusu", track, new Vector2(320 * ratio, 5), DoneInk);
        fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0, .5f); fill.anchoredPosition = Vector2.zero; fill.GetComponent<Image>().raycastTarget = false;
    }

    // Toplanmış not: tek satır, sürüklenebilir; çerçevesi, bandı ve açıklaması gizli.
    private float NoteRow(NotebookBlockData note, float y)
    {
        var block = CreateBlock(note, false, targetContent);
        var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(440, 20);
        Top(rect, new Vector2(0, -y)); rect.localRotation = Quaternion.identity;
        foreach (string child in new[] { "Açıklama", "Bulmaca Etiketi", "Bant" }) { var t = block.transform.Find(child); if (t != null) t.gameObject.SetActive(false); }
        foreach (var border in block.GetComponentsInChildren<NotebookSketchBorder>(true)) border.enabled = false;
        var background = block.GetComponent<Image>(); if (background != null) background.color = Color.clear;
        var outline = block.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
        var title = block.transform.Find("Başlık")?.GetComponent<TextMeshProUGUI>();
        if (title != null)
        {
            title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(22, 0); title.rectTransform.offsetMax = new Vector2(-4, 0);
            title.alignment = TextAlignmentOptions.Left; title.fontStyle = FontStyles.Normal; title.color = DoneInk;
            if (UsesNotebookSkin && notebookBodyFont != null) title.font = notebookBodyFont;
            title.enableAutoSizing = true; title.fontSizeMin = 10; title.fontSizeMax = 12;
            GameLocalization.Set(title, ShortTitle(note.title));
        }
        Checkbox(y, true); RowLine(y + 20);
        return 21;
    }

    // Eksik satır: solda "Bilinmiyor", sağda notun bulunduğu kitap ve raf.
    private float PlaceholderRow(string book, float y)
    {
        var label = Label(targetContent, "Bilinmiyor", Vector2.zero, new Vector2(160, 20), 12);
        Top(label.rectTransform, new Vector2(-114, -y)); label.alignment = TextAlignmentOptions.Left; label.color = MissingInk;
        var where = Label(targetContent, book ?? "?", Vector2.zero, new Vector2(250, 20), 11);
        Top(where.rectTransform, new Vector2(95, -y)); where.alignment = TextAlignmentOptions.Right; where.color = MissingInk;
        where.enableAutoSizing = true; where.fontSizeMin = 8; where.fontSizeMax = 11;
        Checkbox(y, false); RowLine(y + 20);
        return 21;
    }

    // Alınmış numune satırı: yeşil kutu ve adı (sürüklenecek kart Parçalar sekmesinde).
    private float DoneRow(string text, float y)
    {
        var label = Label(targetContent, text, Vector2.zero, new Vector2(410, 20), 12);
        Top(label.rectTransform, new Vector2(11, -y)); label.alignment = TextAlignmentOptions.Left; label.color = DoneInk;
        Checkbox(y, true); RowLine(y + 20);
        return 21;
    }

    // Yazı tipleri ✓/○ işaretlerini taşımıyor; onay kutusu çizilir: dolu yeşil = defterde, boş = eksik.
    private void Checkbox(float y, bool done)
    {
        var box = Panel("Onay Kutusu", targetContent, new Vector2(11, 11), done ? DoneInk : PlaceholderFill);
        Top(box, new Vector2(-212, -y - 4)); box.GetComponent<Image>().raycastTarget = false;
        if (!done) { var outline = box.gameObject.AddComponent<Outline>(); outline.effectColor = MissingInk; outline.effectDistance = new Vector2(1, -1); }
    }

    private void RowLine(float y)
    {
        var line = Panel("Satır Çizgisi", targetContent, new Vector2(440, 1), PlaceholderLine);
        Top(line, new Vector2(0, -y)); line.GetComponent<Image>().raycastTarget = false;
    }

    // Numune çipi: yalnız çizim (sembol adıyla birlikte); sürüklenebilir.
    private void SampleChip(NotebookBlockData note, Vector2 position, float width)
    {
        var block = CreateBlock(note, false, targetContent);
        var rect = (RectTransform)block.transform; rect.sizeDelta = new Vector2(width, 48);
        Top(rect, position); rect.localRotation = Quaternion.identity;
        foreach (string child in new[] { "Başlık", "Açıklama", "Bulmaca Etiketi" }) { var t = block.transform.Find(child); if (t != null) t.gameObject.SetActive(false); }
        var drawing = block.GetComponentInChildren<SignalTrialGraphic>()?.rectTransform ?? block.GetComponentInChildren<MixtureGraphic>()?.rectTransform;
        if (drawing != null) { drawing.anchorMin = new Vector2(.08f, .1f); drawing.anchorMax = new Vector2(.92f, .9f); drawing.offsetMin = drawing.offsetMax = Vector2.zero; }
        foreach (var text in block.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (text.name == "Karışım Değerleri") { text.textWrappingMode = TextWrappingModes.NoWrap; text.fontSizeMax = 12; }
    }

    private float Heading(string text, float y)
    {
        var label = Label(targetContent, text, Vector2.zero, new Vector2(440, 18), 12);
        Top(label.rectTransform, new Vector2(0, -y));
        label.alignment = TextAlignmentOptions.Left; label.fontStyle = FontStyles.Bold; label.color = new Color(.27f, .27f, .25f);
        var rule = Panel("Başlık Çizgisi", targetContent, new Vector2(440, 1), PlaceholderLine);
        Top(rule, new Vector2(0, -y - 18)); rule.GetComponent<Image>().raycastTarget = false;
        return 22;
    }

    private RectTransform Placeholder(Transform parent, string text, Vector2 size, Vector2 position)
    {
        var box = Panel("Boş Yuva", parent, size, PlaceholderFill);
        Top(box, position); box.GetComponent<Image>().raycastTarget = false;
        var outline = box.gameObject.AddComponent<Outline>(); outline.effectColor = PlaceholderLine; outline.effectDistance = new Vector2(1, -1);
        var label = Label(box, text, new Vector2(0, size.y > 60 ? size.y * .5f - 20 : 0), new Vector2(size.x - 12, 22), 13);
        label.fontStyle = FontStyles.Italic; label.color = PlaceholderLine;
        return box;
    }

    private struct PartsPage { public string group; public List<NotebookBlockData> slots; }
    private static readonly string[] PartGroups = { "BAŞLATICI", "NUMUNE", "İŞLEM", "SONLANDIRICI", "DİĞER" };

    // Parçalar sekmesi role göre sayfalanır: oturumun izin verdiği her parça bir yuvadır; bulunmamışsa yuva boş (null) kalır.
    private List<PartsPage> PartsPages()
    {
        var pages = new List<PartsPage>(); var archived = ArchivedParts();
        var expected = new List<SignalPartLibrary.Part>();
        if (ResearchPuzzleTrial.Active != null)
            foreach (var puzzle in ResearchPuzzleTrial.Active.Puzzles)
                foreach (var part in AllowedParts(puzzle)) if (!expected.Exists(p => p.id == part.id)) expected.Add(part);
        foreach (string group in PartGroups)
        {
            var slots = new List<NotebookBlockData>();
            foreach (var part in expected) if (PartGroupOf(part) == group) slots.Add(archived.Find(n => n.itemID == part.id));
            foreach (var note in archived) if (PartGroup(note) == group && !expected.Exists(p => p.id == note.itemID)) slots.Add(note);
            for (int i = 0; i < slots.Count; i += PartsPerPage)
                pages.Add(new PartsPage { group = group, slots = slots.GetRange(i, Mathf.Min(PartsPerPage, slots.Count - i)) });
        }
        if (pages.Count == 0) pages.Add(new PartsPage { group = "", slots = new List<NotebookBlockData>() });
        return pages;
    }

    private static string PartGroupOf(SignalPartLibrary.Part part)
        => part == null ? "DİĞER" : part.inputs == 0 ? "BAŞLATICI" : part.outputs == 0 ? "SONLANDIRICI" : IsSampleLike(part) ? "NUMUNE" : "İŞLEM";
    private static string PartGroup(NotebookBlockData note) => PartGroupOf(PuzzleParts.Find(note.itemID));
}
