using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TutorialFlowChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Doğrulama/Tutorial ve Kapanan Pano")]
    public static void Core()
    {
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var fixtureScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        var failures = new List<string>();
        try
        {
        foreach (Action check in new Action[] { ClosedPanelReleasesBoard, DisabledBreakerCannotTrip, EarlyInteractionCannotCompleteStep, LessonRequiresCollectedCards, DoorIsReachableAfterBoard })
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(check == (Action)DoorIsReachableAfterBoard ? originalScene : fixtureScene);
            try { check(); Debug.Log("TUTORIAL CHECK PASS: " + check.Method.Name); }
            catch (Exception error) { failures.Add(check.Method.Name + ": " + error.GetBaseException().Message); }
        }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(fixtureScene,true);if(originalScene.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene); }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        Debug.Log("TUTORIAL CORE PASS");
    }

    private static void ClosedPanelReleasesBoard()
    {
        var host = new GameObject("Document panel regression");
        var previous = ChalkboardManager.Instance;
        try
        {
            var board = host.AddComponent<ChalkboardManager>();
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, board);
            board.boardTransform = new GameObject("Board").transform;
            board.boardTransform.SetParent(host.transform);
            var docs = host.AddComponent<ChalkboardDocuments>();
            typeof(ChalkboardDocuments).GetField("board", Private).SetValue(docs, board);
            Call(docs, "InitializeResearch");
            docs.SetDocumentPanelOpen(true, true);
            var back = board.boardTransform.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "Belge Panosu Arkalığı");
            Require(back.gameObject.activeInHierarchy, "Açık panonun gövdesi görünmüyor.");
            var handle = (RectTransform)typeof(ChalkboardDocuments).GetField("targetArea", Private).GetValue(docs);
            var surface = (RectTransform)handle.parent;
            Require(surface.rect.Contains(handle.anchoredPosition + handle.rect.size / 2), "Kapatma tutamağı tahta sınırlarının dışında.");
            handle.GetComponent<Button>().onClick.Invoke();
            docs.SetDocumentPanelOpen(false, true);
            Require(!back.gameObject.activeInHierarchy, "Kapalı panonun fiziksel arkalığı hâlâ parçaları örtüyor.");
            Require(!docs.IsDocumentPanelOpen && handle.gameObject.activeInHierarchy, "Kapalı panonun açma kontrolü kayboldu.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            typeof(ChalkboardManager).GetProperty("Instance").SetValue(null, previous);
        }
    }

    private static void DisabledBreakerCannotTrip()
    {
        var host = new GameObject("Breaker regression");
        var previous = BreakerBox.Instance;
        try
        {
            var breaker = host.AddComponent<BreakerBox>();
            BreakerBox.Instance = breaker;
            breaker.RegisterLight(host.AddComponent<ControllableLight>());
            typeof(BreakerBox).GetField("maxRiskAtFullLoad", Private).SetValue(breaker, 1f);
            breaker.isSystemActive = false;
            Call(breaker, "RunRiskCheck");
            Require(!breaker.IsTripped, "Sistem kapalıyken otomatik risk kontrolü şalteri attırdı.");
        }
        finally { Object.DestroyImmediate(host); BreakerBox.Instance = previous; }
    }

    private static void EarlyInteractionCannotCompleteStep()
    {
        var host = new GameObject("Prerequisite regression");
        var required = new GameObject("Previous step");
        var target = new GameObject("Lesson target");
        try
        {
            var prereq = host.AddComponent<PrerequisiteCondition>();
            prereq.requiredEvent = required.AddComponent<EventLogicController>();
            var condition = host.AddComponent<InteractionCondition>();
            condition.targetInteractable = target;
            Call(condition, "Start");
            Call(condition, "HandlePlayerInteracted", target);
            Require(!condition.IsMet(), "Ön koşuldan önceki tıklama sonraki dersi tamamladı.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(required); Object.DestroyImmediate(target); }
    }

    private static void LessonRequiresCollectedCards()
    {
        var type = Type.GetType("ChalkboardTutorial, Assembly-CSharp");
        Require(type != null, "Gerçek tahta başlangıç dersi yok.");
        var puzzle = (PuzzleDefinition)type.GetMethod("CreatePuzzle").Invoke(null, null);
        Require(PuzzleSimulation.Validate(puzzle) == null && puzzle.solution.parts.Count == 3,
            "Başlangıç deneyinin üç parçalı çözümü hedefi üretmiyor.");
        var host = new GameObject("Lesson collection regression");
        var previous = NotebookUI.Instance;
        try
        {
            var notebook = host.AddComponent<NotebookUI>();
            NotebookUI.Instance = notebook;
            var collected = type.GetMethod("HasCollectedCards");
            Require(!(bool)collected.Invoke(null, new object[] { notebook }), "Boş defter toplama dersini geçti.");
            notebook.EnsureCollectedQuests().Add(new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = puzzle.id });
            foreach (var part in puzzle.solution.parts)
                notebook.EnsureWorkspacePage().blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = part.itemID });
            Require(!(bool)collected.Invoke(null, new object[] { notebook }), "İpucu toplanmadan ders geçildi.");
            notebook.EnsureWorkspacePage().blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.RuleClue,
                questID = puzzle.id, itemID = "tutorial/ports" });
            Require((bool)collected.Invoke(null, new object[] { notebook }), "Gerçek hedef, ipucu ve üç parça toplandığında ders tamamlanmadı.");
        }
        finally { Object.DestroyImmediate(host); NotebookUI.Instance = previous; }
    }

    private static void DoorIsReachableAfterBoard()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != LaboratorySynthesisSetup.ScenePath) return;
        var lesson = Object.FindFirstObjectByType<ChalkboardTutorial>(FindObjectsInactive.Include);
        Require(lesson != null, "Yeni sahne tahta dersine bağlı değil.");
        var controls = lesson.boardStep.GetComponent<PlayerControlAction>();
        var doorLock = lesson.officeDoor.GetComponentInChildren<InteractableDoorLock>();
        Require(controls != null && controls.modifyInteractionRules && controls.enableTutorialMode
            && controls.allowedInteractables.Contains(doorLock.gameObject), "Tahta dersinden sonra kapının kilidi whitelist dışında kalıyor.");
    }

    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
