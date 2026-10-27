using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Owns one immutable puzzle and exact book assignments for the current play session.
public sealed class ResearchPuzzleTrial : MonoBehaviour
{
    public static ResearchPuzzleTrial Active { get; private set; }
    public FormulaItemSO partTemplate;
    public bool allowGameplayScene;
    public InteractableBook[] books = new InteractableBook[4];
    [Header("Puzzle Havuzu / Atölye")]
    public PuzzlePoolSO puzzlePool;
    public PuzzleDefinitionSO previewPuzzle;
    [Tooltip("Birlikte araştırılacak hedefler. Doluysa rastgele tek puzzle seçimi yerine hepsini kitaplara dağıtır.")]
    public PuzzlePoolSO simultaneousPuzzles;
    public SignalTrialContent Content { get; private set; }
    public ChalkboardManager Board { get; private set; }
    public PuzzleDefinition CurrentPuzzle { get; private set; }
    public string SessionID { get; private set; }
    private List<PuzzleDefinition> puzzles = new List<PuzzleDefinition>();
    public IReadOnlyList<PuzzleDefinition> Puzzles => puzzles;
    public readonly HashSet<string> Completed = new HashSet<string>();
    private int distributionSeed;

    public static bool IsResearch(ChalkboardManager board) => Active != null && Active.Board == board && Active.Content != null;

    private void ResolveReferences()
    {
        if (!enabled || (!allowGameplayScene && gameObject.scene.name != "scn_SynthesisBlockout") || partTemplate == null)
            throw new InvalidOperationException("Araştırma sahnesi veya parça şablonu hazır değil.");
        Board = ChalkboardManager.Instance;
        if (Board == null || FormulaCatalog.Instance == null || GetComponent<SignalExperiment>() == null)
            throw new InvalidOperationException("Tahta, katalog veya ölçüm bağlantısı eksik.");
        BookIDs(puzzlePool != null || previewPuzzle != null || simultaneousPuzzles != null);
    }

    private string[] BookIDs(bool strict)
    {
        if (books == null || books.Length == 0) throw new InvalidOperationException("Erişilebilir araştırma kitabı yok.");
        var ids = new string[books.Length]; var unique = new HashSet<string>();
        for (int i = 0; i < books.Length; i++)
        {
            if (books[i] == null || !books[i].gameObject.activeInHierarchy)
                throw new InvalidOperationException("Araştırma kitabı eksik veya etkin değil: " + (i + 1));
            var identity = books[i].GetComponent<PuzzleBookIdentity>();
            if (identity != null && !identity.accessible) throw new InvalidOperationException("Araştırma kitabı erişilebilir olarak işaretlenmemiş: " + books[i].name);
            ids[i] = identity != null ? identity.bookID : strict ? null : "legacy-book/" + i;
            if (string.IsNullOrEmpty(ids[i]) || !unique.Add(ids[i])) throw new InvalidOperationException("Kitap kimliği eksik veya yineleniyor.");
        }
        return ids;
    }

    private List<PuzzleBookProfile> BookProfiles()
    {
        var ids = BookIDs(puzzlePool != null || previewPuzzle != null || simultaneousPuzzles != null);
        var result = new List<PuzzleBookProfile>();
        for (int i = 0; i < books.Length; i++)
            result.Add(books[i].TryGetComponent<PuzzleBookIdentity>(out var identity) ? identity.Profile(i) : PuzzleBookProfile.Default(i, ids[i]));
        return result;
    }

    public bool Configure()
    {
        if (CurrentPuzzle != null) return true;
        try { StartNewSession(); return true; }
        catch (Exception exception) { Debug.LogError("Puzzle oturumu kurulamadı: " + exception.Message, this); return false; }
    }

    public void StartNewSession()
    {
        ResolveReferences();
        int seed = Guid.NewGuid().GetHashCode();
        if (simultaneousPuzzles != null && previewPuzzle == null)
        {
            var assets = simultaneousPuzzles.Playable(out string issue);
            if (issue != null) throw new InvalidOperationException(issue);
            var set = new List<PuzzleDefinition>();
            foreach (var asset in assets) set.Add(asset.definition.Clone());
            distributionSeed = unchecked(seed * 31 + 17);
            var combined = PuzzleBookDistribution.Build(set, BookProfiles(), distributionSeed);
            Install(set, set[0].id, Guid.NewGuid().ToString("N"), combined);
            Completed.Clear(); NotebookUI.Instance?.ResetWorkspace(); return;
        }
        var puzzle = previewPuzzle != null ? previewPuzzle.definition.Clone()
            : puzzlePool != null ? puzzlePool.Choose(seed) : PuzzleDefinition.Legacy();
        string error = PuzzleSimulation.Validate(puzzle);
        if (error != null) throw new InvalidOperationException(error);
        distributionSeed = unchecked(seed * 31 + 17);
        var pages = puzzlePool == null && previewPuzzle == null && books.Length == 4 ? null
            : PuzzleBookDistribution.Build(new List<PuzzleDefinition> { puzzle }, BookProfiles(), distributionSeed);
        Install(puzzle, Guid.NewGuid().ToString("N"), pages);
        Completed.Clear();
        NotebookUI.Instance?.ResetWorkspace();
    }

    private void Install(PuzzleDefinition puzzle, string sessionID, List<BookWorkspacePage>[] pages)
        => Install(new List<PuzzleDefinition> { puzzle }, puzzle.id, sessionID, pages);

    private void Install(List<PuzzleDefinition> set, string activeID, string sessionID, List<BookWorkspacePage>[] pages)
    {
        // Prepare content before touching the live board, so invalid data cannot partially replace a session.
        var content = pages == null ? SignalTrial.CreateContent(partTemplate) : SignalTrialContent.Create(partTemplate, set, pages);
        Board.QuestPinned -= OnQuestPinned;
        Board.ResetBoard();
        var previous = Content;
        puzzles = new List<PuzzleDefinition>(); foreach (var item in set) puzzles.Add(item.Clone());
        Content = content; CurrentPuzzle = puzzles.Find(p => p.id == activeID); SessionID = sessionID; Active = this;
        Board.practiceQuest = Content.Quests.Find(q => q.questID == activeID);
        Board.practicePuzzles = new List<QuestBlueprintSO>(Content.Quests);
        Board.formulaCatalog = new List<FormulaItemSO>(Content.Parts);
        FormulaCatalog.Instance.ReplaceCatalog(Content.Parts);
        for (int i = 0; i < books.Length; i++)
        {
            var book = books[i];
            book.clueData = null; book.synthesisPages = Content.Books[i];
            book.canContainPassword = false; book.usePhysicalPageBlocks = true;
            book.totalPages = Mathf.Max(2, (Content.Books[i].Count + 1) / 2 * 2);
            book.ClearPassword();
            var surface = book.GetComponent<BookPageBlockSurface>() ?? book.gameObject.AddComponent<BookPageBlockSurface>();
            surface.InvalidatePages();
            if (book.printNotesOnPages && book.bookPagesMaterial != null) book.bookPagesMaterial.SetTexture("_PagesTex", Texture2D.whiteTexture);
            book.emissionIntensity = 0;
            foreach (var renderer in book.GetComponentsInChildren<Renderer>(true)) foreach (var material in renderer.materials)
            {
                if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                if (material.HasProperty("_EmissiveIntensity")) material.SetFloat("_EmissiveIntensity", 0);
                material.DisableKeyword("_EMISSION");
            }
            if (pages != null && Content.Books[i].Count > 0)
            {
                var identity = book.GetComponent<PuzzleBookIdentity>();
                if (identity != null) { identity.title = Content.Books[i][0].title; identity.location = Content.Books[i][0].sourceLocation; }
                foreach (var label in book.GetComponentsInChildren<TextMeshPro>(true))
                    if (label.transform.parent.name.StartsWith("Araştırma Kitap Adı")) label.text = Content.Books[i][0].title;
            }
        }
        previous?.Dispose();
        Board.QuestPinned += OnQuestPinned;
        var player = FindFirstObjectByType<SynthesisBlockoutPlayer>();
        if (player != null) player.researchBooks = books;
        GetComponent<SignalExperiment>().Initialize(this);
    }

    public void CaptureSession(GameData data)
    {
        if (CurrentPuzzle == null || data == null) return;
        var saved = new PuzzleSessionData { definition = CurrentPuzzle.Clone(), sessionID = SessionID, distributionSeed = distributionSeed,
            completed = new List<string>(Completed), measurements = GetComponent<SignalExperiment>().CaptureReadings() };
        foreach (var puzzle in puzzles) saved.definitions.Add(puzzle.Clone());
        string[] ids = BookIDs(puzzlePool != null || previewPuzzle != null || simultaneousPuzzles != null);
        for (int i = 0; i < books.Length; i++) saved.books.Add(new PuzzleBookAssignment { bookID = ids[i], pages = Content.Books[i] });
        data.puzzleSession = JsonUtility.FromJson<PuzzleSessionData>(JsonUtility.ToJson(saved));
    }

    public void RestoreSession(GameData data)
    {
        ResolveReferences();
        var saved = data?.puzzleSession;
        if (saved == null)
        {
            var legacy = PuzzleDefinition.Legacy();
            Install(legacy, Guid.NewGuid().ToString("N"), books.Length == 4 ? null : PuzzleBookDistribution.Build(new List<PuzzleDefinition> { legacy }, BookProfiles(), 0));
            Completed.Clear(); return;
        }
        string error = PuzzleSimulation.Validate(saved.definition);
        if (error != null || string.IsNullOrEmpty(saved.sessionID)) throw new InvalidOperationException(error ?? "Kayıt oturum kimliği eksik.");
        var set = saved.definitions != null && saved.definitions.Count > 0 ? saved.definitions : new List<PuzzleDefinition> { saved.definition };
        var puzzleIDs = new HashSet<string>();
        foreach (var puzzle in set)
            if (PuzzleSimulation.Validate(puzzle) is string problem || !puzzleIDs.Add(puzzle.id)) throw new InvalidOperationException("Kayıtlı deney kümesi geçersiz.");
        if (!set.Exists(p => p.id == saved.definition.id && p.Fingerprint() == saved.definition.Fingerprint())) throw new InvalidOperationException("Kayıtlı aktif hedef deney kümesine ait değil.");
        var ids = BookIDs(puzzlePool != null || previewPuzzle != null || simultaneousPuzzles != null);
        if (saved.books == null || saved.books.Count != ids.Length) throw new InvalidOperationException("Kayıttaki kitap sayısı sahneyle uyuşmuyor; kayıt korundu.");
        var pages = new List<BookWorkspacePage>[ids.Length]; var used = new HashSet<string>();
        foreach (var book in saved.books)
        {
            int index = book != null ? Array.IndexOf(ids, book.bookID) : -1;
            if (index < 0 || !used.Add(book.bookID) || book.pages == null) throw new InvalidOperationException("Kayıtlı kitap kimliği bulunamadı veya yineleniyor; yeniden dağıtım yapılmadı.");
            pages[index] = JsonUtility.FromJson<PuzzleBookAssignment>(JsonUtility.ToJson(book)).pages;
        }
        distributionSeed = saved.distributionSeed;
        Install(set, saved.definition.id, saved.sessionID, pages);
        Completed.Clear(); if (saved.completed != null) foreach (string id in saved.completed) Completed.Add(id);
        GetComponent<SignalExperiment>().RestoreReadings(saved.measurements);
    }

    public void MarkCompleted(string id) => Completed.Add(id);
    private void OnQuestPinned(SynthesisQuestData quest)
    {
        if (quest == null) return;
        var puzzle = puzzles.Find(p => p.id == quest.questID);
        if (puzzle != null) CurrentPuzzle = puzzle;
        quest.completed = Completed.Contains(quest.questID);
    }
    private void OnDestroy()
    {
        if (Board != null) Board.QuestPinned -= OnQuestPinned;
        if (Active == this) Active = null;
        Content?.Dispose();
    }
}
