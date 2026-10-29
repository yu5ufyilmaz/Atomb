using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TutorialSceneSetup
{
    [MenuItem("Senzora/Tutorial/Yeni Tahta Dersini Bağla")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LaboratorySynthesisSetup.ScenePath)
            throw new InvalidOperationException("scn_LaboratorySynthesis sahnesini Play dışında aç.");
        var manager = Object.FindFirstObjectByType<TutorialManager>();
        var root = manager.tutorialEventsParent;
        var events = root.GetComponentsInChildren<EventLogicController>(true);
        EventLogicController Step(int number) => events.Single(e => e.name.EndsWith("_" + number.ToString("D3")));
        var research = Object.FindFirstObjectByType<ResearchPuzzleTrial>();
        var board = Object.FindFirstObjectByType<InteractableChalkboard>();
        var book = research.books[0];
        var lesson = root.GetComponent<ChalkboardTutorial>() ?? root.AddComponent<ChalkboardTutorial>();
        lesson.research = research; lesson.lessonBook = book;
        lesson.firstStep = Step(1); lesson.collectedStep = Step(2); lesson.boardStep = Step(3);
        lesson.doorStep = Step(4); lesson.terminalReadyStep = Step(9);
        lesson.valveRoomStep = Step(11); lesson.valveStep = Step(12); lesson.finalStep = Step(13);
        var doorHandle = Step(4).GetComponent<InteractionCondition>()?.targetInteractable
            ?? Step(3).GetComponent<LookAction>().lookTarget.gameObject;
        lesson.officeDoor = doorHandle.GetComponent<InteractableDoorLock>().targetDoor;

        var intro = Step(1);
        var highlight = intro.GetComponent<HighlightAction>();
        if (highlight.targetNote != null) highlight.targetNote.gameObject.SetActive(false);
        highlight.targetNote = null; highlight.targetBook = book;
        intro.GetComponent<LookAction>().lookTarget = book.transform;
        intro.GetComponent<PlayerControlAction>().allowedInteractables = new List<GameObject> { book.gameObject };
        intro.GetComponent<DialogAction>().subtitleID = "tut_board_intro";

        Remove<InteractionCondition>(Step(2)); Remove<NoteAction>(Step(2)); Remove<UIAction>(Step(2)); Remove<DialogAction>(Step(2));
        lesson.cardsCollected = Signal(Step(2));
        Step(2).GetComponent<PlayerControlAction>().allowedInteractables = new List<GameObject> { book.gameObject, board.gameObject };
        Remove<InputCondition>(Step(3)); lesson.boardPracticed = Signal(Step(3));
        var doorControls = Step(3).GetComponent<PlayerControlAction>() ?? Step(3).gameObject.AddComponent<PlayerControlAction>();
        doorControls.modifyInteractionRules = true; doorControls.enableTutorialMode = true;
        doorControls.allowedInteractables = new List<GameObject> { doorHandle };
        Remove<InteractionCondition>(Step(4)); lesson.doorLocked = Signal(Step(4));
        Step(5).GetComponent<HidingSpotControlAction>().delay = 0;
        Step(9).GetComponent<PlayerControlAction>().allowedInteractables.AddRange(new[] { book.gameObject, board.gameObject });
        Step(9).GetComponent<PlayerControlAction>().allowedInteractables = Step(9).GetComponent<PlayerControlAction>().allowedInteractables.Distinct().ToList();
        Step(9).GetComponent<DialogAction>().subtitleID = "tut_board_terminal";
        Remove<PasswordCondition>(Step(10)); lesson.terminalAccepted = Signal(Step(10));
        Remove<InteractionCondition>(Step(12)); lesson.valvePracticed = Signal(Step(12));
        Remove<InteractionCondition>(Step(13)); lesson.breakerRestored = Signal(Step(13));
        var doorReminder = Step(14).GetComponent<PrerequisiteCondition>() ?? Step(14).gameObject.AddComponent<PrerequisiteCondition>();
        doorReminder.requiredEvent = Step(4);
        // The old reminder cannot interrupt a machine/board lesson or re-freeze the player.
        Remove<LookAction>(Step(14));

        manager.isTutorialEnabled = true;
        root.SetActive(true);
        foreach (var toggle in Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            for (int i = 0; i < toggle.onValueChanged.GetPersistentEventCount(); i++)
                if (toggle.onValueChanged.GetPersistentTarget(i) == manager) { toggle.SetIsOnWithoutNotify(true); EditorUtility.SetDirty(toggle); }
        EditorUtility.SetDirty(manager); EditorUtility.SetDirty(lesson);
        ConfigureDialogueEvents();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Tutorial bağlantıları kaydedilemedi.");
        Debug.Log("TUTORIAL SCENE CONFIGURED: real collection, board practice, measured Turing submission, actual lock/valve/reset and persistent guidance.");
    }

    [MenuItem("Senzora/Tutorial/Konuşmaları Event Sistemine Bağla")]
    public static void ConfigureDialogueEvents()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play dışında çalıştır.");
        var manager = Object.FindFirstObjectByType<TutorialManager>();
        var root = manager.tutorialEventsParent;
        var lesson = root.GetComponent<ChalkboardTutorial>();
        var events = root.GetComponentsInChildren<EventLogicController>(true);
        EventLogicController Step(int number) => events.Single(e => e.name.EndsWith("_" + number.ToString("D3")));
        foreach (var dialogue in root.GetComponentsInChildren<DialogAction>(true))
            if (!dialogue.followConditions && dialogue.gameObject != lesson.finalStep.gameObject)
            { dialogue.enabled = false; EditorUtility.SetDirty(dialogue); }
        lesson.firstStep.gameObject.SetActive(true);
        var group = root.transform.Find("Konuşma Eventleri");
        if (group == null) { group = new GameObject("Konuşma Eventleri").transform; group.SetParent(root.transform, false); }
        void Cue(string id, int after, int before, ChalkboardTutorialCondition.Context? context = null, float delay = 10)
        {
            var node = group.Find(id);
            if (node == null) { node = new GameObject(id).transform; node.SetParent(group, false); }
            var controller = node.GetComponent<EventLogicController>() ?? node.gameObject.AddComponent<EventLogicController>();
            controller.triggerOnlyOnce = true; controller.requireAllConditions = true;
            var gates = node.GetComponents<PrerequisiteCondition>().ToList();
            while (gates.Count < 2) gates.Add(node.gameObject.AddComponent<PrerequisiteCondition>());
            gates[0].requiredEvent = Step(after); gates[0].requireTriggered = true;
            gates[1].requiredEvent = Step(before); gates[1].requireTriggered = false;
            if (context.HasValue)
            {
                var condition = node.GetComponent<ChalkboardTutorialCondition>() ?? node.gameObject.AddComponent<ChalkboardTutorialCondition>();
                condition.lesson = lesson; condition.context = context.Value;
            }
            var dialogue = node.GetComponent<DialogAction>() ?? node.gameObject.AddComponent<DialogAction>();
            dialogue.enabled = true; dialogue.followConditions = true; dialogue.contextOwner = manager;
            dialogue.subtitleID = "thought_" + id + "_0";
            dialogue.reminderSubtitleIDs = new[] { "thought_" + id + "_1", "thought_" + id + "_2" };
            dialogue.reminderDelay = delay; dialogue.requiredWithoutVoice = true;
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(dialogue);
            foreach (var gate in gates) EditorUtility.SetDirty(gate);
        }
        Cue("book", 1, 2, ChalkboardTutorialCondition.Context.Book);
        Cue("collect", 1, 2, ChalkboardTutorialCondition.Context.Collect, 30);
        Cue("leave_book", 2, 3, ChalkboardTutorialCondition.Context.LeaveBook);
        Cue("board", 2, 3, ChalkboardTutorialCondition.Context.Board);
        Cue("pin", 2, 3, ChalkboardTutorialCondition.Context.Pin, 30);
        Cue("clear_board", 2, 3, ChalkboardTutorialCondition.Context.ClearBoard);
        Cue("build", 2, 3, ChalkboardTutorialCondition.Context.Build, 30);
        Cue("store", 2, 3, ChalkboardTutorialCondition.Context.Store);
        Cue("leave_board", 2, 3, ChalkboardTutorialCondition.Context.LeaveBoard);
        Cue("lock", 3, 4);
        Cue("hide", 4, 5);
        Cue("wait_hidden", 5, 6, delay: 30);
        Cue("leave_hiding", 6, 7);
        Cue("notice_lees", 7, 8);
        Cue("avoid_lees", 8, 9);
        Cue("turing", 9, 10, ChalkboardTutorialCondition.Context.Turing);
        Cue("insert", 9, 10, ChalkboardTutorialCondition.Context.Insert, 30);
        Cue("reader", 9, 10, ChalkboardTutorialCondition.Context.Reader, 30);
        Cue("confirm", 9, 10, ChalkboardTutorialCondition.Context.Confirm);
        Cue("retry_measurement", 9, 10, ChalkboardTutorialCondition.Context.RetryMeasurement);
        Cue("valve_room", 10, 11);
        Cue("valve", 11, 12, ChalkboardTutorialCondition.Context.Valve);
        Cue("leave_valve", 11, 12, ChalkboardTutorialCondition.Context.LeaveValve);
        Cue("wait_power", 12, 13, ChalkboardTutorialCondition.Context.WaitPower);
        Cue("breaker", 12, 13, ChalkboardTutorialCondition.Context.Breaker);
        EditorSceneManager.MarkSceneDirty(root.scene);
        if (!EditorSceneManager.SaveScene(root.scene)) throw new InvalidOperationException("Konuşma eventleri kaydedilemedi.");
        Debug.Log("TUTORIAL DIALOGUE CONFIGURED: 25 scene events with editable conditions, subtitle IDs and reminders.");
    }

    private static SignalCondition Signal(Component step) => step.GetComponent<SignalCondition>() ?? step.gameObject.AddComponent<SignalCondition>();
    private static void Remove<T>(Component step) where T : Component
    {
        foreach (var component in step.GetComponents<T>()) Object.DestroyImmediate(component);
    }
}
