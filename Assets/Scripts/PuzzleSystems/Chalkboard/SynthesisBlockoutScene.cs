using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        var board = ChalkboardManager.Instance;
        var generator = ProceduralSynthesisGenerator.Instance;
        if (board == null || generator == null) return;
        board.practiceQuest = starterPuzzle;
        board.useProceduralQuests = false;
        generator.RegisterCatalog(starterPuzzle.clueParts);
        board.formulaCatalog = new List<FormulaItemSO>(starterPuzzle.clueParts);
        if (NotebookWorkspaceController.Instance != null)
            NotebookWorkspaceController.Instance.generateProceduralRun = false;
        book.includeProceduralClues = false;
        // Yalnız bu alıştırmanın Play oturumunda kartları kullan; özgün atlas/UV assetine dokunma.
        book.clueData = null;
        var pages = new List<BookWorkspacePage>();
        var instructions = new BookWorkspacePage { pageIndex = 0, title = "BAŞLATICI-X / HEDEF VE KURALLAR" };
        instructions.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.Quest,
            questID = starterPuzzle.CreateQuest().questID, title = starterPuzzle.title,
            description = "?1: nötron üretmeli. ?2: çift bağ tutabilmeli." });
        for (int i = 0; i < starterPuzzle.clueNotes.Count; i++)
            instructions.blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.RuleClue,
                title = i == 0 ? "KRİTİK KURAL" : "OPERASYON / SİLGİ", description = starterPuzzle.clueNotes[i] });
        pages.Add(instructions);
        for (int i = 0; i < starterPuzzle.clueParts.Count; i++)
        {
            int pageIndex = 1 + i / 3;
            if (i % 3 == 0) pages.Add(new BookWorkspacePage { pageIndex = pageIndex, title = "ADAY PARÇALAR" });
            var part = starterPuzzle.clueParts[i];
            pages[pages.Count - 1].blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.PartClue,
                itemID = part.itemID, title = part.itemName, description = part.description });
        }
        book.synthesisPages = pages;
    }
}
