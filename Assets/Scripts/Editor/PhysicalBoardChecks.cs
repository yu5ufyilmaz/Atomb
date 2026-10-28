using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PhysicalBoardChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Fiziksel Tahta Deneyimi")]
    public static void All()
    {
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Araştırma sahnesinde Play gerekli.");
        ResearchPuzzleTrial.Active.StartCoroutine(CheckFlow());
    }

    public static void Controls() { NotebookControls(); PhysicalControls(); }

    private static IEnumerator CheckFlow()
    {
        yield return ResearchGuidanceChecks.EnterNormalGame();
        var interaction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        Require(interaction != null, "Gerçek tahta etkileşimi bulunamadı.");
        interaction.Interact();
        float deadline = Time.realtimeSinceStartup + 15;
        while ((!ChalkboardManager.Instance.isMachineActive || NotebookWorkspaceController.Instance == null
            || !NotebookWorkspaceController.Instance.IsActive) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(ChalkboardManager.Instance.isMachineActive, "Tahtaya giriş tamamlanmadı.");
        yield return null;
        var failures = new List<string>();
        foreach (Action check in new Action[] { NotebookControls, PhysicalControls, CompactGoals })
            try { check(); } catch (Exception error) { failures.Add(check.Method.Name + ": " + error.GetBaseException().Message); }

        var board = ChalkboardManager.Instance;
        var documents = board.EnsureDocuments();
        documents.SetDocumentPanelOpen(true, true);
        NotebookWorkspaceController.Instance.ToggleNotebook();
        yield return new WaitForSecondsRealtime(.35f);
        ScreenCapture.CaptureScreenshot("Temp/physical-board-open.png"); yield return null;
        interaction.ForceExit();
        deadline = Time.realtimeSinceStartup + 15;
        while (GameManager.Instance.activeInteraction != null && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        try
        {
            Require(!board.isMachineActive && GameManager.Instance.activeInteraction == null, "Tahtadan çıkış tamamlanmadı.");
            Require(documents.IsDocumentPanelOpen, "Tahtadan çıkınca açık belgeler kendiliğinden kapandı.");
            var shade = Field<RectTransform>(documents, "shadeViewport");
            Require(shade.gameObject.activeInHierarchy && shade.rect.height > 0, "Açık belge panosu dünyada görünmüyor.");
            var snapshot = new SynthesisBoardSaveData(); documents.SaveLayout(snapshot);
            Require(snapshot.documentShadeOpen, "Tahtadan sonra kayıt açık belge durumunu kaybetti.");
            Require(!Field<CanvasGroup>(documents, "input").blocksRaycasts, "Uzaktaki belge panosu oyuncu girdisini yakalıyor.");
            var meter = Field<CanvasGroup>(SignalExperiment.Active, "outputInput");
            Require(meter.gameObject.activeInHierarchy && meter.alpha == 1 && !meter.blocksRaycasts,
                "Ölçüm cihazı çıkıştan sonra görünmeli, girdi almamalı.");
        }
        catch (Exception error) { failures.Add("ExitPersistence: " + error.GetBaseException().Message); }

        interaction.Interact();
        deadline = Time.realtimeSinceStartup + 15;
        while ((!board.isMachineActive || !NotebookWorkspaceController.Instance.IsActive) && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        try
        {
            Require(documents.IsDocumentPanelOpen && Field<CanvasGroup>(documents, "input").blocksRaycasts,
                "Tekrar giriş belgelerin açık halini ve etkileşimini korumadı.");
        }
        catch (Exception error) { failures.Add("Reentry: " + error.GetBaseException().Message); }
        Require(failures.Count == 0, string.Join("\n", failures));
        Debug.Log("PHYSICAL BOARD EXPERIENCE PASS: Tab/×, physical world controls, compact scrollable goals, visible open documents after real exit/reentry and saved open state.");
    }

    private static void NotebookControls()
    {
        var workspace = NotebookWorkspaceController.Instance;
        var window = Field<RectTransform>(workspace, "notebookWindow");
        var root = Field<GameObject>(workspace, "root");
        Require(!root.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("DEFTERİ AÇ") || t.text.Contains("DEFTERİ KAPAT")),
            "Tab ve × yanında gereksiz Defteri Aç/Kapat ekran düğmesi var.");
        Require(root.transform.Find("Araştırma Araçları") == null, "Belge düğmesi hâlâ ekran araç çubuğunda.");
        if (!Field<bool>(workspace, "notebookExpanded")) workspace.ToggleNotebook();
        Invoke(workspace, "UpdateNotebookPopup", 1f);
        var close = window.Find("×").GetComponent<Button>();
        close.onClick.Invoke();
        Invoke(workspace, "UpdateNotebookPopup", 1f);
        Require(!window.gameObject.activeSelf, "Defter × düğmesi defteri kapatmadı.");
        workspace.ToggleNotebook(); // The same action bound to Tab while at a workstation.
        Invoke(workspace, "UpdateNotebookPopup", 1f);
        Require(window.gameObject.activeSelf && Field<CanvasGroup>(workspace, "notebookVisibility").blocksRaycasts,
            "Tab eylemi defteri tekrar kullanılabilir açmadı.");
    }

    private static void PhysicalControls()
    {
        var board = ChalkboardManager.Instance;
        var documents = board.EnsureDocuments();
        var handle = Field<RectTransform>(documents, "shadeToggle");
        Require(handle.gameObject.activeInHierarchy && handle.GetComponentInParent<Canvas>().renderMode == RenderMode.WorldSpace,
            "Belgeler gerçek tahtadaki tutamaktan açılmalı.");
        bool open = documents.IsDocumentPanelOpen;
        handle.GetComponent<Button>().onClick.Invoke();
        Require(documents.IsDocumentPanelOpen != open, "Fiziksel belge tutamağı çalışmıyor.");
        documents.SetDocumentPanelOpen(open, true);
        Require(board.boardTransform.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name == "Belge Panosu Arkalığı"),
            "Belge panosunun fiziksel arkalığı yok.");
        var meter = board.boardTransform.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Deneyi Ölç");
        Require(meter != null && meter.GetComponentInParent<Canvas>().renderMode == RenderMode.WorldSpace,
            "DENEYİ ÖLÇ gerçek tahtada bir cihaz düğmesi olmalı.");
        Require(meter.transform.parent.GetComponentsInChildren<MeshRenderer>(true).Length > 0, "Ölçüm cihazının fiziksel gövdesi yok.");
        var overlay = Field<GameObject>(NotebookWorkspaceController.Instance, "boardControls");
        Require(overlay == null || !overlay.activeInHierarchy, "Ekranda ikinci bir deney araç çubuğu var.");
        var feedback = Field<TextMeshProUGUI>(SignalExperiment.Active, "deviceFeedback");
        feedback.text = "";
        meter.onClick.Invoke();
        Require(!string.IsNullOrEmpty(feedback.text), "Fiziksel ölçüm düğmesi ölçüm veya deney doğrulama geri bildirimi üretmedi.");
    }

    private static void CompactGoals()
    {
        var trial = ResearchPuzzleTrial.Active;
        var board = trial.Board;
        var documents = board.EnsureDocuments();
        var previous = board.PinnedQuest;
        var previousPractice = board.practiceQuest;
        var previousPuzzle = trial.CurrentPuzzle;
        var layout = new SynthesisBoardSaveData(); documents.SaveLayout(layout);
        try
        {
            foreach (bool initiallyOpen in new[] { false, true })
            foreach (var puzzle in trial.Puzzles)
            {
                documents.SetDocumentPanelOpen(initiallyOpen, true);
                Require(board.TryPinQuest(trial.Content.Quests.Find(q => q.questID == puzzle.id).CreateQuest()), "Gerçek hedef sabitlenemedi.");
                documents.SetDocumentPanelOpen(true, true);
                Canvas.ForceUpdateCanvases();
                var card = Field<RectTransform>(documents, "targetArea");
                Require(card.rect.width <= 290 && card.rect.height <= 270, "Tahta hedefi dev defter kartı boyutunda: " + card.rect.size);
                var bounds = ((RectTransform)card.parent).rect;
                Require(bounds.Contains(card.anchoredPosition - card.rect.size / 2) && bounds.Contains(card.anchoredPosition + card.rect.size / 2),
                    "Hedef kartı belge panosunun dışına taşıyor.");
                var dropHint = Field<RectTransform>(documents, "informationArea");
                Require(!dropHint.gameObject.activeSelf || !new Rect(card.anchoredPosition - card.rect.size / 2, card.rect.size)
                    .Overlaps(new Rect(dropHint.anchoredPosition - dropHint.rect.size / 2, dropHint.rect.size)),
                    "Kayıt bırakma ipucu hedef metnini örtüyor.");
                var scroll = card.GetComponentInChildren<ScrollRect>(true);
                Require(scroll != null && scroll.vertical && scroll.content != null, "Kompakt hedefte kaynak metni kaydırılamıyor.");
                var hint = Field<TextMeshProUGUI>(documents, "hint"); hint.ForceMeshUpdate();
                Require(hint.rectTransform.rect.height >= hint.preferredHeight - 1 && !hint.isTextTruncated,
                    "Kompakt hedef kaynak yönlendirmelerini kaybetti.");
                Require(scroll.content.rect.height >= hint.preferredHeight - 1, "Kaydırma içeriği bütün yönlendirmeleri kapsamıyor.");
                if (scroll.content.rect.height > scroll.viewport.rect.height + 5)
                {
                    float before = scroll.content.anchoredPosition.y;
                    scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -3) });
                    Require(scroll.content.anchoredPosition.y > before + 1, "Hedefin uzun kaynak yönlendirmeleri tekerlek ile okunamıyor.");
                }
            }
        }
        finally
        {
            var restore = previous ?? trial.Content.Quests.Find(q => q.questID == previousPuzzle.id)?.CreateQuest();
            if (restore != null) board.TryPinQuest(restore);
            if (previous == null) typeof(ChalkboardManager).GetField("<PinnedQuest>k__BackingField", Private).SetValue(board, null);
            board.practiceQuest = previousPractice; documents.LoadLayout(layout); documents.Refresh();
        }
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
