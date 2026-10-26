using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Blockout sahnesinin objeleri artık Edit modunda gerçek GameObject'ler olarak sahnede durur
// (Senzora > Sentez Blockout > Sahneyi Düzenlenebilir Objelerle Oluştur menüsüyle üretilir).
// Bu script yalnızca Play'e özgü işleri yapar: test GameManager'ı, kitap parlaması ve oyun modunu başlatma.
// Awake/Start, sahnedeki diğer scriptlerden önce çalışsın diye düşük sıra kullanılır.
[DefaultExecutionOrder(-1000)]
public class SynthesisBlockoutScene : MonoBehaviour
{
    [Header("Blockout İçeriği")]
    public NotebookData notebookData;
    [Tooltip("Test masasında kullanılan gerçek kitap prefabı (yalnızca sahneyi oluşturan menü komutu kullanır).")]
    public InteractableBook bookPrefab;
    [Tooltip("İlk etkileşim denemelerinde kapalı tutun; sabit parça blokları gösterilir.")]
    public bool enableProceduralQuests;
    [Tooltip("Tek, sabit alıştırma. Doğru çözümde yalnız başarı mesajı verir.")]
    public QuestBlueprintSO starterPuzzle;
    public List<QuestBlueprintSO> additionalPracticePuzzles = new List<QuestBlueprintSO>();

    [Header("Sahne Referansları (menü komutu otomatik atar)")]
    [Tooltip("Sahnedeki test kitabı. Kitabı sildiyseniz veya değiştirdiyseniz buraya yeniden atayın.")]
    public InteractableBook book;

    private GameManager game;

    private void Awake()
    {
        if (GameManager.Instance != null)
        {
            Debug.LogError("Blockout sahnesini tek başına açın. Başka bir sahnenin GameManager'ı zaten çalışıyor.", this);
            return;
        }
        game = new GameObject("Test GameManager").AddComponent<GameManager>();
    }

    private IEnumerator Start()
    {
        if (game == null) yield break;
        if (book == null)
        {
            Debug.LogError("Blockout sahnesinde Book alanı boş. Menüden 'Sahneyi Düzenlenebilir Objelerle Oluştur' komutunu çalıştırın.", this);
            yield break;
        }

        // Bu kısım diğer scriptlerin Start'ından önce çalışır (kitap kendi Start'ında materyalleri kopyalar).
        if (NotebookWorkspaceController.Instance != null)
            NotebookWorkspaceController.Instance.generateProceduralRun = enableProceduralQuests;
        if (ChalkboardManager.Instance != null)
            ChalkboardManager.Instance.useProceduralQuests = enableProceduralQuests;
        book.includeProceduralClues = enableProceduralQuests;
        if (starterPuzzle != null) ConfigureStarterPuzzle();
        GetComponent<ResearchPuzzleTrial>()?.Configure(this);
        book.ClearPassword();
        foreach (var renderer in book.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.materials)
            {
                if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                if (material.HasProperty("_EmissiveIntensity")) material.SetFloat("_EmissiveIntensity", 0);
                material.DisableKeyword("_EMISSION");
            }

        yield return null; // Etkileşim scriptlerinin Start referansları ve GameManager imleci hazır olsun.
        game.StartGameMode();
    }

    private void ConfigureStarterPuzzle()
    {
        // Deneme kamerasındaki bloom, yakın kitap kağıdını ve üstündeki notları yakıyordu.
        var cameraData = Camera.main != null ? Camera.main.GetComponent<HDAdditionalCameraData>() : null;
        if (cameraData != null)
        {
            cameraData.customRenderingSettings = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Bloom, false);
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Bloom] = true;
        }
        var board = ChalkboardManager.Instance;
        var generator = ProceduralSynthesisGenerator.Instance;
        if (board == null || generator == null) return;
        board.practiceQuest = starterPuzzle;
        board.useProceduralQuests = false;
        board.practicePuzzles = new List<QuestBlueprintSO> { starterPuzzle };
        foreach (var puzzle in additionalPracticePuzzles)
            if (puzzle != null && !board.practicePuzzles.Contains(puzzle)) board.practicePuzzles.Add(puzzle);
        board.formulaCatalog = new List<FormulaItemSO>();
        foreach (var puzzle in board.practicePuzzles)
        {
            generator.RegisterCatalog(puzzle.clueParts);
            foreach (var part in puzzle.clueParts)
                if (part != null && !board.formulaCatalog.Contains(part)) board.formulaCatalog.Add(part);
        }
        if (NotebookWorkspaceController.Instance != null)
            NotebookWorkspaceController.Instance.generateProceduralRun = false;
        book.includeProceduralClues = false;
        // Yalnız bu alıştırmanın Play oturumunda kartları kullan; özgün atlas/UV assetine dokunma.
        book.clueData = null;
        var pages = new List<BookWorkspacePage>();
        int start = 0;
        foreach (var puzzle in board.practicePuzzles)
        {
            AddPracticePages(pages, puzzle, start);
            int used = 1 + Mathf.CeilToInt(puzzle.clueParts.Count / 3f);
            start += (used + 1) / 2 * 2;
        }
        book.synthesisPages = pages;
        book.totalPages = Mathf.Max(book.totalPages, start);
    }

    private static void AddPracticePages(List<BookWorkspacePage> pages, QuestBlueprintSO puzzle, int start)
    {
        var instructions = new BookWorkspacePage { pageIndex = start, title = puzzle.title + " / HEDEF VE KURALLAR" };
        instructions.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.Quest,
            questID = puzzle.CreateQuest().questID, title = puzzle.title, description = puzzle.clueSummary });
        for (int i = 0; i < puzzle.clueNotes.Count; i++)
            instructions.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.RuleClue,
                questID = puzzle.CreateQuest().questID, ruleIndex = i,
                title = "İPUCU " + (i + 1), description = puzzle.clueNotes[i] });
        pages.Add(instructions);
        for (int i = 0; i < puzzle.clueParts.Count; i++)
        {
            int pageIndex = start + 1 + i / 3;
            if (i % 3 == 0) pages.Add(new BookWorkspacePage { pageIndex = pageIndex, title = "ADAY PARÇALAR" });
            var part = puzzle.clueParts[i];
            pages[pages.Count - 1].blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.PartClue,
                itemID = part.itemID, title = part.itemName, description = part.description });
        }
    }
}
