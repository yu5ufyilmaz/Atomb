using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

// Gerçek kitap yüzeyleri ve defter bırakma yolunu Play içinde denetleyen editör araçları.
public static class ResearchTrialBookChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Araştırma Denemeleri/Canlı/1 Sorular Kitabını Aç")]
    public static void OpenQuestions() => OpenBook(0);
    [MenuItem("Senzora/Araştırma Denemeleri/Canlı/3 Parça Atlasını Aç")]
    public static void OpenAtlas() => OpenBook(1);
    [MenuItem("Senzora/Araştırma Denemeleri/Canlı/4 İlkeleri Aç")]
    public static void OpenPrinciples() => OpenBook(2);
    [MenuItem("Senzora/Araştırma Denemeleri/Canlı/5 Deney Kayıtlarını Aç")]
    public static void OpenRecords() => OpenBook(3);
    [MenuItem("Senzora/Araştırma Denemeleri/Canlı/2 Tahtayı Aç")]
    public static void OpenBoard()
    {
        var player = Player();
        Begin(player, player.board, player.boardView);
    }

    private static void OpenBook(int index)
    {
        var player = Player();
        Begin(player, ResearchPuzzleTrial.Active.books[index], null);
    }

    private static SynthesisBlockoutPlayer Player()
    {
        Check(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Araştırma sahnesi Play modunda hazır olmalı.");
        // Editör arka plandayken de gerçek kitap animasyonu tamamlanabilsin.
        Application.runInBackground = true;
        Check(GameManager.Instance.activeInteraction == null, "Önce açık etkileşimden çık ve animasyonun bitmesini bekle.");
        return UnityEngine.Object.FindFirstObjectByType<SynthesisBlockoutPlayer>();
    }

    private static void Begin(SynthesisBlockoutPlayer player, IInteractable target, Transform pose)
    {
        typeof(SynthesisBlockoutPlayer).GetMethod("Begin", Private).Invoke(player, new object[] { target, pose });
    }

    [MenuItem("Senzora/Araştırma Denemeleri/Canlı/Etkileşimden Çık")]
    public static void Exit()
    {
        if (Application.isPlaying && GameManager.Instance.activeInteraction is IForceExitable exit) exit.ForceExit();
    }

    [MenuItem("Senzora/Araştırma Denemeleri/Canlı/Sonraki Sayfa")]
    public static void NextPage()
    {
        Check(Application.isPlaying && GameManager.Instance.activeInteraction is InteractableBook, "Bir kitap aç.");
        ((InteractableBook)GameManager.Instance.activeInteraction).NextPage();
    }

    [MenuItem("Senzora/Doğrulama/Araştırma Defterden Tahtaya")]
    public static void CopyCollectedToBoard()
    {
        var trial = ResearchPuzzleTrial.Active;
        Check(Application.isPlaying && trial != null && trial.Board.isMachineActive, "Araştırma tahtasını aç.");
        var notebook = NotebookUI.Instance;
        var workspace = NotebookWorkspaceController.Instance;
        var quest = trial.Content.Quests[1];
        workspace.ShowCollectedTargets(); Canvas.ForceUpdateCanvases();
        var target = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
            b => !b.IsSource && b.Data.blockType == NotebookBlockType.Quest && b.Data.questID == quest.questID);
        Check(target != null, "B hedefini önce gerçek kitaptan topla.");
        var docs = trial.Board.EnsureDocuments();
        var targetArea = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", Private).GetValue(docs);
        Check(workspace.TryDropBlock(target, Camera.main.WorldToScreenPoint(targetArea.position)), "Toplanmış hedef gerçek görev alanına bırakılamadı.");
        // Oyuncunun başlığından taşıyabildiği defteri geçici olarak sola al;
        // sağdaki kapalı alanı tahta bırakma noktası sanarak test etme.
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        var window = (RectTransform)page.parent;
        var bounds = (RectTransform)window.parent;
        var previousPosition = window.anchoredPosition;
        window.anchoredPosition = new Vector2(-bounds.rect.width + 290, previousPosition.y);
        try
        {
            foreach (var node in quest.nodes)
            {
                int pageIndex = notebook.workspacePages.FindIndex(p => p.blocks.Exists(b => b.blockType == NotebookBlockType.PartClue && b.itemID == node.itemID));
                Check(pageIndex >= 0, "Parça Atlası önce toplanmalı: " + node.itemID);
                workspace.GoToPage(pageIndex); Canvas.ForceUpdateCanvases();
                var note = Array.Find(UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None),
                    b => !b.IsSource && b.isActiveAndEnabled && b.Data.blockType == NotebookBlockType.PartClue && b.Data.itemID == node.itemID);
                Vector3 point = trial.Board.boardTransform.TransformPoint(new Vector3(node.position.x, node.position.y, .02f));
                Check(note != null && workspace.TryDropBlock(note, Camera.main.WorldToScreenPoint(point)), "Defter parçası tahta alanına bırakılamadı: " + node.itemID);
            }
        }
        finally { window.anchoredPosition = previousPosition; Canvas.ForceUpdateCanvases(); }
        Check(trial.Board.boardTransform.GetComponentsInChildren<DraggableFormula>().Length == 6, "Altı toplanmış parça tahtada yok.");
        Debug.Log("RESEARCH NOTEBOOK TO BOARD PASS: collected target pinned and six atlas notes copied through the actual drop path. No solution was auto-drawn.");
    }

    [MenuItem("Senzora/Doğrulama/Araştırma Kitabı Açık Sayfayı Topla")]
    public static void CollectVisible()
    {
        Check(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Araştırma sahnesinde Play gerekli.");
        var book = GameManager.Instance.activeInteraction as InteractableBook;
        Check(book != null && book.isOpen && !book.isAnimating, "Bir araştırma kitabını açıp animasyonu bekle.");
        var workspace = NotebookWorkspaceController.Instance;
        var notebook = NotebookUI.Instance;
        var page = (RectTransform)typeof(NotebookWorkspaceController).GetField("page", Private).GetValue(workspace);
        var targetArea = (RectTransform)typeof(NotebookWorkspaceController).GetField("targetDropZone", Private).GetValue(workspace);
        var sources = new List<NotebookDraggableBlock>();
        foreach (var note in UnityEngine.Object.FindObjectsByType<NotebookDraggableBlock>(FindObjectsSortMode.None))
            if (note.IsSource && note.isActiveAndEnabled && note.GetComponentInParent<InteractableBook>() == book) sources.Add(note);
        Check(sources.Count > 0, "Fiziksel kitap yüzeyinde kaynak notlar üretilmedi.");
        foreach (var source in sources)
        {
            workspace.GoToPage(notebook.workspacePages.Count);
            Canvas.ForceUpdateCanvases();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
                source.Data.blockType == NotebookBlockType.Quest ? targetArea.position : page.position);
            Check(workspace.TryDropBlock(source, point), "Gerçek kitap notu deftere alınamadı: " + source.Data.title);
        }
        workspace.GoToPage(0);
        Debug.Log("RESEARCH BOOK COLLECT PASS: " + sources.Count + " physical page blocks passed through the real notebook drop path. Targets: " + notebook.collectedQuests.Count);
    }

    [MenuItem("Senzora/Doğrulama/Araştırma Kitabı Okunabilirlik")]
    public static void CheckReadability()
    {
        Canvas.ForceUpdateCanvases();
        var book = GameManager.Instance != null ? GameManager.Instance.activeInteraction as InteractableBook : null;
        Check(Application.isPlaying && book != null && !book.isAnimating, "Kitap açılışını bekle.");
        int blocks = 0;
        foreach (var source in book.GetComponentsInChildren<NotebookDraggableBlock>())
        {
            if (!source.IsSource || !source.isActiveAndEnabled) continue;
            blocks++;
            foreach (var text in source.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "Kitap metni karta sığmıyor: " + source.Data.title + " / " + text.text);
            }
        }
        Check(blocks > 0, "Açık kitapta görünür not yok.");
        Debug.Log("RESEARCH BOOK READABILITY PASS: " + blocks + " visible source blocks fit their text regions.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
