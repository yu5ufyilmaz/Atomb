using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Exercise the same runtime controls and drag handlers as the player. No saved data is changed.
public static class ResearchUiExperienceChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Senzora/Puzzle Atölyesi/Doğrulama/Araştırma Arayüzü")]
    public static void All()
    {
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Araştırma sahnesinde Play gerekli.");
        ResearchPuzzleTrial.Active.StartCoroutine(OpenAndCheck());
    }

    private static IEnumerator OpenAndCheck()
    {
        Application.runInBackground = true;
        yield return ResearchGuidanceChecks.EnterNormalGame();
        var interaction = UnityEngine.Object.FindFirstObjectByType<InteractableChalkboard>();
        Require(interaction != null, "Araştırma tahtası bulunamadı.");
        if (GameManager.Instance.activeInteraction != null && GameManager.Instance.activeInteraction != interaction)
        {
            GameManager.Instance.activeInteraction.ForceExit();
            float exitDeadline = Time.realtimeSinceStartup + 15;
            while (GameManager.Instance.activeInteraction != null && Time.realtimeSinceStartup < exitDeadline) yield return null;
            Require(GameManager.Instance.activeInteraction == null, "Önceki etkileşim kapanmadı.");
        }
        if (!ChalkboardManager.Instance.isMachineActive) interaction.Interact();
        float deadline = Time.realtimeSinceStartup + 15;
        while ((!ChalkboardManager.Instance.isMachineActive || NotebookWorkspaceController.Instance == null || !NotebookWorkspaceController.Instance.IsActive)
            && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        CheckOpenWorkspace();
    }

    private static void CheckOpenWorkspace()
    {
        var workspace = NotebookWorkspaceController.Instance;
        Require(Application.isPlaying && workspace != null && workspace.IsActive && workspace.IsArchiveNotebook,
            "Araştırma tahtasını açıp Play sırasında çalıştır.");
        var failures = new List<string>();
        foreach (Action check in new Action[] { Controls, DragPaper, PartRoles, GoalReadability })
            try { check(); } catch (Exception error) { failures.Add(check.Method.Name + ": " + error.GetBaseException().Message); }
        Require(failures.Count == 0, string.Join("\n", failures));
        Debug.Log("RESEARCH UI EXPERIENCE PASS: persistent clickable controls, opaque screen/world drag paper, catalog-derived part roles, readable source directions.");
    }

    public static void Controls() => PhysicalBoardChecks.Controls();

    public static void DragPaper()
    {
        var workspace = NotebookWorkspaceController.Instance;
        foreach (bool world in new[] { false, true })
        {
            var host = world ? new GameObject("UI check world source", typeof(RectTransform), typeof(Canvas)) : null;
            if (host != null) host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var source = Note(host != null ? host.transform : workspace.DragCanvas.transform,
                new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = "pulse-source", title = "Atım Kaynağı", description = "Giriş yok; bir çıkış." }, world);
            try
            {
                if (!world)
                    Require(source.GetComponent<Image>().color.a < .01f, "Test için defterin gerçek el yazısı görünümü gerekli.");
                var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = new Vector2(Screen.width / 2f, Screen.height / 2f) };
                source.OnBeginDrag(data);
                var floating = Field<RectTransform>(source, "floatingRect");
                Require(floating != null, "Gerçek sürükleme başlamadı.");
                Require(floating.GetComponent<Image>().color.a >= .98f, (world ? "Kitap" : "Defter") + " sürüklemesinde kâğıt zemini kayboluyor.");
                Require(!floating.GetComponent<CanvasGroup>().blocksRaycasts, "Geçici not bırakma hedefini engelliyor.");
                Require(source.gameObject.activeInHierarchy && source.GetComponent<CanvasGroup>().alpha >= .98f, "Notun arşivdeki aslı kayboldu.");
            }
            finally
            {
                Invoke(source, "CancelDrag");
                UnityEngine.Object.DestroyImmediate(source.gameObject);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }

    public static void PartRoles()
    {
        string[] ids = { "pulse-source", "pulse-n12", "pulse-meter", "mix-source", "mix-red", "mix-meter" };
        string[] roles = { "BAŞLATICI", "İŞLEM", "SONLANDIRICI", "BAŞLATICI", "İŞLEM", "SONLANDIRICI" };
        for (int i = 0; i < ids.Length; i++)
            foreach (bool source in new[] { false, true })
            {
                var block = Note(NotebookWorkspaceController.Instance.DragCanvas.transform,
                    new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = ids[i], title = "Parça", description = "Davranış" }, source);
                try
                {
                    var heading = block.transform.Find("Başlık").GetComponent<TMP_Text>();
                    Require(heading.text.Contains(roles[i]), ids[i] + " rolü " + roles[i] + " olarak görünmüyor.");
                    Require(heading.text.Contains("›"), "Parça başlığı akış yönünü göstermiyor.");
                }
                finally { UnityEngine.Object.DestroyImmediate(block.gameObject); }
            }
    }

    public static void GoalReadability()
    {
        var trial = ResearchPuzzleTrial.Active;
        var board = ChalkboardManager.Instance;
        var documents = board.EnsureDocuments();
        var previousQuest = board.PinnedQuest;
        var previousPractice = board.practiceQuest;
        var previousPuzzle = trial.CurrentPuzzle;
        var layout = new SynthesisBoardSaveData(); documents.SaveLayout(layout);
        try
        {
            foreach (var puzzle in trial.Puzzles)
            {
                NotebookBlockData note = null;
                foreach (var book in trial.Content.Books) foreach (var page in book)
                    foreach (var candidate in page.blocks)
                        if (candidate.blockType == NotebookBlockType.Quest && candidate.questID == puzzle.id) note = candidate;
                Require(note != null, "Gerçek hedef kaydı kitaplarda yok: " + puzzle.title);
                var goal = Note(NotebookWorkspaceController.Instance.DragCanvas.transform, note, false);
                try
                {
                    Canvas.ForceUpdateCanvases();
                    var body = goal.transform.Find("Açıklama").GetComponent<TMP_Text>();
                    body.ForceMeshUpdate();
                    Require(((RectTransform)goal.transform).rect.height >= 300, "Hedef kartında kaynak kitap yönlendirmelerine yer yok.");
                    Require(body.rectTransform.rect.height >= body.preferredHeight - 1 && !body.isTextTruncated,
                        puzzle.title + " kaynak yönlendirmeleri sığmıyor: " + note.description.Length + " karakter; alan=" + body.rectTransform.rect.height + "; gereken=" + body.preferredHeight);
                    var diagram = goal.GetComponentInChildren<QuestDiagramGraphic>();
                    Require(diagram == null || diagram.rectTransform.anchorMin.y >= body.rectTransform.anchorMax.y,
                        "Hedef çizimi ile kitap yönlendirmeleri üst üste geliyor.");
                    foreach (var plot in goal.GetComponentsInChildren<SignalTrialGraphic>())
                        Require(plot.transform.parent != goal.transform, "Hedef köküne ikinci bir sinyal çizimi ekleniyor.");
                    foreach (var plot in goal.GetComponentsInChildren<MixtureGraphic>())
                        Require(plot.transform.parent != goal.transform, "Hedef köküne ikinci bir karışım çizimi ekleniyor.");
                    Require(board.TryPinQuest(trial.Content.Quests.Find(q => q.questID == puzzle.id).CreateQuest()), "Gerçek hedef panoya takılamadı.");
                    var hint = Field<TextMeshProUGUI>(documents, "hint"); hint.ForceMeshUpdate();
                    Require(hint.rectTransform.rect.height >= hint.preferredHeight - 1 && !hint.isTextTruncated,
                        puzzle.title + " panodaki kaynak yönlendirmeleri sığmıyor: alan=" + hint.rectTransform.rect.height + "; gereken=" + hint.preferredHeight);
                }
                finally { UnityEngine.Object.DestroyImmediate(goal.gameObject); }
            }
        }
        finally
        {
            var restore = previousQuest ?? trial.Content.Quests.Find(q => q.questID == previousPuzzle.id)?.CreateQuest();
            if (restore != null) board.TryPinQuest(restore);
            if (previousQuest == null) typeof(ChalkboardManager).GetField("<PinnedQuest>k__BackingField", Private).SetValue(board, null);
            board.practiceQuest = previousPractice;
            documents.LoadLayout(layout); documents.Refresh();
        }
    }

    private static NotebookDraggableBlock Note(Transform parent, NotebookBlockData data, bool source)
    {
        var obj = new GameObject("UI check note", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
        obj.transform.SetParent(parent, false);
        var block = obj.GetComponent<NotebookDraggableBlock>(); block.Initialize(data, source); return block;
    }

    private static void RequireReachable(RectTransform control)
    {
        Require(control.gameObject.activeInHierarchy, control.name + " açık/kapalı durumunda kayboldu.");
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, control.position);
        Require(center.x > 0 && center.x < Screen.width && center.y > 0 && center.y < Screen.height, control.name + " ekran dışında.");
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = center }, hits);
        Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(control), control.name + " başka bir arayüzün altında kaldı.");
    }

    private static T Field<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, Private);
        return field != null ? (T)field.GetValue(target) : default;
    }
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
