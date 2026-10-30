using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Bridges the existing scene sequence to real archive, board and Turing actions.
public sealed class ChalkboardTutorial : MonoBehaviour
{
    public const string QuestID = "tutorial-first-trace";
    public ResearchPuzzleTrial research;
    public InteractableBook lessonBook;
    public EventLogicController firstStep, collectedStep, boardStep, terminalReadyStep, finalStep;
    public SignalCondition cardsCollected, boardPracticed, terminalAccepted;
    public InteractableDoor officeDoor;
    public EventLogicController doorStep, valveRoomStep, valveStep;
    public SignalCondition doorLocked, valvePracticed, breakerRestored;
    private bool panelWasClosed;
    private bool sawBreakerTrip;
    private bool valveUsed;
    private string lessonFingerprint;
    private string preparedSessionID;
    private SignalExperiment experiment;

    public static PuzzleDefinition CreatePuzzle()
    {
        var puzzle = new PuzzleDefinition { id = QuestID, title = "İlk İz / Tahta Alıştırması",
            description = "Atım Kaynağı → Numune N-12 → Ölçer. Üç parçayı bu sırayla bağla ve tek kısa iz üret." };
        puzzle.rules.minParts = puzzle.rules.maxParts = 3;
        puzzle.rules.minSamples = puzzle.rules.maxSamples = 1;
        puzzle.rules.traceCount = 1;
        foreach (var rule in puzzle.rules.parts)
        {
            rule.allowed = rule.itemID == SignalTrial.Source || rule.itemID == SignalTrial.Narrow || rule.itemID == SignalTrial.Meter;
            rule.minimum = rule.allowed ? 1 : 0;
            rule.maximum = rule.allowed ? 1 : 0;
        }
        foreach (string id in new[] { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Meter })
            puzzle.solution.parts.Add(new SynthesisPartData { itemID = id });
        puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = 0, toIndex = 1, directed = true });
        puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = 1, toIndex = 2, directed = true });
        puzzle.target.Add(new SignalTrial.Pulse { time = .18f, height = 1, width = .025f });
        return puzzle;
    }

    public static List<BookWorkspacePage> CreatePages(PuzzleDefinition puzzle)
    {
        var pages = new List<BookWorkspacePage> {
            new BookWorkspacePage { pageIndex = 0, title = "TAHTA ALIŞTIRMASI",
                explanation = "Önce HEDEF ve BAĞLANTI SIRASI notlarını deftere sürükle. Yan sayfadaki üç parçayı da al. Tab defteri açar/kapatır.",
                blocks = new List<NotebookBlockData> {
                    new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = QuestID,
                        title = puzzle.title, description = puzzle.description, itemID = puzzle.TargetVisual },
                    new NotebookBlockData { blockType = NotebookBlockType.RuleClue, questID = QuestID, itemID = "tutorial/ports",
                        title = "BAĞLANTI SIRASI", description = "Atım Kaynağı → Numune N-12 → Ölçer. Ok çıkış, halka giriştir. Ölçerin bir girişini kullanman yeterli. Tahtada hedefi ve bu ipucunu panoya tak; BELGELERİ TOPLA ile çalışma alanını aç." }
                } },
            new BookWorkspacePage { pageIndex = 1, title = "ÜÇ PARÇAYI DA TOPLA",
                explanation = "Her parçayı deftere sürükle. F ile kitabı bırak; kara tahtaya geç. Defterdeki asıllar korunur.",
                blocks = new List<NotebookBlockData>() }
        };
        foreach (string id in new[] { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Meter })
        {
            var part = PuzzleParts.Find(id);
            pages[1].blocks.Add(new NotebookBlockData { blockType = NotebookBlockType.PartClue,
                itemID = id, title = part.title, description = part.description });
        }
        return pages;
    }

    public static bool HasCollectedCards(NotebookUI notebook)
    {
        if (notebook == null || !notebook.EnsureCollectedQuests().Exists(n => n.questID == QuestID)) return false;
        var notes = notebook.workspacePages.Where(p => p?.blocks != null).SelectMany(p => p.blocks).Where(n => n != null).ToList();
        return notes.Exists(n => n.blockType == NotebookBlockType.RuleClue && n.questID == QuestID && n.itemID == "tutorial/ports")
            && new[] { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Meter }
                .All(id => notes.Exists(n => n.blockType == NotebookBlockType.PartClue && n.itemID == id));
    }

    private void OnEnable()
    {
        if (research == null) return;
        research.SessionStarted += PrepareLesson;
        experiment = research.GetComponent<SignalExperiment>();
        experiment.MeasurementAccepted += OnMeasurementAccepted;
        finalStep.OnEventTriggered += Complete;
        if (research.Content != null) PrepareLesson();
    }

    private void OnDisable()
    {
        if (research != null) research.SessionStarted -= PrepareLesson;
        if (experiment != null) experiment.MeasurementAccepted -= OnMeasurementAccepted;
        if (finalStep != null) finalStep.OnEventTriggered -= Complete;
    }

    private void PrepareLesson()
    {
        if (research?.Content == null || preparedSessionID == research.SessionID) return;
        preparedSessionID = research.SessionID;
        panelWasClosed = false;
        sawBreakerTrip = valveUsed = false;
        var puzzle = CreatePuzzle();
        lessonFingerprint = puzzle.Fingerprint();
        research.AddTutorial(puzzle, lessonBook, CreatePages(puzzle));
        foreach (var dialogue in GetComponentsInChildren<DialogAction>(true))
            if (dialogue.followConditions) dialogue.ResetDialogue();
    }

    internal bool HasPinnedCards => research.Board.PinnedQuest?.questID == QuestID
        && research.Board.pinnedInformation.Exists(n => n.questID == QuestID && n.itemID == "tutorial/ports");

    internal bool HasCorrectReading => experiment.LastReading.MeetsTarget
        && experiment.LastReading.sessionID == research.SessionID
        && experiment.LastReading.puzzleFingerprint == lessonFingerprint;

    internal bool HasStoredReading => NotebookUI.Instance != null && NotebookUI.Instance.workspacePages
        .Where(p => p?.blocks != null).SelectMany(p => p.blocks).Any(n => n != null && SignalExperiment.IsMeasurementNote(n)
            && n.questID == QuestID && n.measurementSessionID == research.SessionID && n.measurementCode == experiment.LastReading.Code);

    private void Update()
    {
        if (research?.Content == null || GameManager.Instance == null || !GameManager.Instance.isGameStarted || GameManager.Instance.isGamePaused) return;
        // The player may already be inside the opening trigger when New Game ends.
        firstStep.RefreshConditions();
        if (!firstStep.HasTriggered) return;
        if (!collectedStep.HasTriggered && (HasCollectedCards(NotebookUI.Instance) || HasCorrectReading)) cardsCollected.ReceiveSignal();
        if (boardStep.HasTriggered && !doorStep.HasTriggered && officeDoor.IsLocked()) doorLocked.ReceiveSignal();
        if (valveRoomStep.HasTriggered && !valveStep.HasTriggered)
        {
            if (GameManager.Instance.activeInteraction is InteractablePressureValve) valveUsed = true;
            if (valveUsed && PressureSystemManager.Instance.GetPressure() <= 20 && GameManager.Instance.activeInteraction == null)
                valvePracticed.ReceiveSignal();
        }
        if (valveStep.HasTriggered && BreakerBox.Instance != null)
        {
            if (BreakerBox.Instance.IsTripped) sawBreakerTrip = true;
            else if (sawBreakerTrip) breakerRestored.ReceiveSignal();
        }
        if (!collectedStep.HasTriggered || boardStep.HasTriggered) return;
        // Hedef artık panoyu açmaz; yalnız hedefi takmak "panoyu topla" adımını atlatmamalı.
        if (!research.Board.EnsureDocuments().IsDocumentPanelOpen && HasPinnedCards)
            panelWasClosed = true;
        if (HasCorrectReading && GameManager.Instance.activeInteraction == null)
        {
            EnsureStoredReading();
            if (HasStoredReading) boardPracticed.ReceiveSignal();
        }
    }

    private void EnsureStoredReading()
    {
        if (HasStoredReading || !HasCorrectReading || NotebookUI.Instance == null) return;
        var page = NotebookUI.Instance.EnsureWorkspacePage();
        page.blocks.Add(new NotebookBlockData
        {
            blockType = NotebookBlockType.RuleClue,
            itemID = SignalTrial.Encode(experiment.LastReading.pulses),
            title = "ÖLÇÜM / " + experiment.LastReading.Code.Replace("_", " "),
            measurementCode = experiment.LastReading.Code,
            measurementSessionID = research.SessionID,
            puzzleFingerprint = lessonFingerprint,
            questID = QuestID,
            description = "Ölçümler'e al; Turing okuyucusuna bırak. Turing: 6."
        });
    }

    private void OnMeasurementAccepted(string questID)
    {
        if (questID == QuestID && terminalReadyStep.HasTriggered) terminalAccepted.ReceiveSignal();
    }

    private void Complete() { TutorialManager.Instance?.CompleteTutorial(); }
    internal bool PanelWasClosed => panelWasClosed;
    internal bool SawBreakerTrip => sawBreakerTrip;

}
