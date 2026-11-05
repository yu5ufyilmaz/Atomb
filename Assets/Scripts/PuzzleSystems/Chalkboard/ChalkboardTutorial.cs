using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Bridges the existing scene sequence to real archive, board and Turing actions.
public sealed partial class ChalkboardTutorial : MonoBehaviour
{
    public const string QuestID = "tutorial-first-trace";
    // Numune N-12 kütüphanedeki bu kitapta; sahne kurulumu ve kontroller kitabı bu adla bulur.
    public const string SampleBookTitle = "Kısa ve Uzun Tepkiler";
    public ResearchPuzzleTrial research;
    public InteractableBook lessonBook;
    [Tooltip("Numune N-12'nin konduğu kütüphane kitabı (SampleBookTitle).")]
    public InteractableBook sampleBook;
    public EventLogicController sampleStep;
    public SignalCondition sampleCollected;
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
                explanation = "Önce HEDEF ve BAĞLANTI SIRASI notlarını deftere sürükle. Yan sayfadaki iki parçayı da al. Tab defteri açar/kapatır.",
                blocks = new List<NotebookBlockData> {
                    new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = QuestID,
                        title = puzzle.title, description = puzzle.description, itemID = puzzle.TargetVisual },
                    new NotebookBlockData { blockType = NotebookBlockType.RuleClue, questID = QuestID, itemID = "tutorial/ports",
                        title = "BAĞLANTI SIRASI", description = "Atım Kaynağı → Numune N-12 → Ölçer. Ok çıkış, halka giriştir. Ölçerin bir girişini kullanman yeterli. Tahtada hedefi ve bu ipucunu panoya tak; BELGELERİ TOPLA ile çalışma alanını aç." }
                } },
            // Kaynak ve ölçer kartları PrepareLesson'da masa kitabının kendi sayfalarından buraya taşınır.
            new BookWorkspacePage { pageIndex = 1, title = "KAYNAK VE ÖLÇERİ TOPLA",
                explanation = "İki parçayı deftere sürükle. Numune N-12 bu kitapta değil; F ile kitabı bırak, kütüphanede ara.",
                blocks = new List<NotebookBlockData>() }
        };
        return pages;
    }

    public static List<BookWorkspacePage> CreateSamplePages(NotebookBlockData sample) => new List<BookWorkspacePage> {
        new BookWorkspacePage { pageIndex = 0, title = "KISA TEPKİ NUMUNESİ",
            explanation = "Numune N-12 kartını deftere sürükle. Tahtadaki alıştırma bu numuneyle kurulur.",
            blocks = new List<NotebookBlockData> { sample } },
        new BookWorkspacePage { pageIndex = 1, title = "KISA TEPKİ NUMUNESİ",
            explanation = "Numune defterindeyse F ile kitabı bırak; kara tahtaya geç.", blocks = new List<NotebookBlockData>() }
    };

    // Kart oturumun kitabındaysa oradan alınır (aynı kart iki kez görünmez), yoksa katalogdan yazılır.
    private static NotebookBlockData TakeCard(InteractableBook book, string id)
    {
        foreach (var page in book.synthesisPages)
        {
            int index = page?.blocks?.FindIndex(n => n != null && n.blockType == NotebookBlockType.PartClue && n.itemID == id) ?? -1;
            if (index < 0) continue;
            var card = page.blocks[index]; page.blocks.RemoveAt(index);
            card.bookRegion = default; // ders sayfası kartları sırayla dizer
            return card;
        }
        var part = PuzzleParts.Find(id);
        return new NotebookBlockData { blockType = NotebookBlockType.PartClue, itemID = id, title = part.title, description = part.description };
    }

    private static List<NotebookBlockData> NotebookNotes(NotebookUI notebook)
        => notebook.workspacePages.Where(p => p?.blocks != null).SelectMany(p => p.blocks).Where(n => n != null).ToList();

    public static bool HasCollectedCards(NotebookUI notebook)
    {
        if (notebook == null || !notebook.EnsureCollectedQuests().Exists(n => n.questID == QuestID)) return false;
        var notes = NotebookNotes(notebook);
        return notes.Exists(n => n.blockType == NotebookBlockType.RuleClue && n.questID == QuestID && n.itemID == "tutorial/ports")
            && new[] { SignalTrial.Source, SignalTrial.Meter }
                .All(id => notes.Exists(n => n.blockType == NotebookBlockType.PartClue && n.itemID == id));
    }

    public static bool HasSampleCard(NotebookUI notebook)
        => notebook != null && NotebookNotes(notebook).Exists(n => n.blockType == NotebookBlockType.PartClue && n.itemID == SignalTrial.Narrow);

    private void OnEnable()
    {
        if (research == null) return;
        research.SessionStarted += PrepareLesson;
        experiment = research.GetComponent<SignalExperiment>();
        experiment.MeasurementAccepted += OnMeasurementAccepted;
        finalStep.OnEventTriggered += Complete;
        closeDoorStep = GetComponentsInChildren<EventLogicController>(true).First(e => e.name.EndsWith("_014"));
        closeDoorStep.OnEventTriggered += BeginDoorLesson;
        if (research.Content != null) PrepareLesson();
    }

    private void OnDisable()
    {
        if (research != null) research.SessionStarted -= PrepareLesson;
        if (experiment != null) experiment.MeasurementAccepted -= OnMeasurementAccepted;
        if (finalStep != null) finalStep.OnEventTriggered -= Complete;
        if (lessonBook != null) lessonBook.ExitLocked = false;
        NotebookWorkspaceController.Instance?.ClearGuide();
        if (closeDoorStep != null) closeDoorStep.OnEventTriggered -= BeginDoorLesson;
        EndDoorLesson();
    }

    private void PrepareLesson()
    {
        if (research?.Content == null || preparedSessionID == research.SessionID) return;
        preparedSessionID = research.SessionID;
        panelWasClosed = false;
        sawBreakerTrip = valveUsed = false;
        var puzzle = CreatePuzzle();
        lessonFingerprint = puzzle.Fingerprint();
        // AddTutorial gibi: ders oturumda zaten varsa (kayıttan dönüş) sayfalar ikinci kez eklenmez.
        if (!research.Puzzles.Any(p => p.id == QuestID))
        {
            var pages = CreatePages(puzzle);
            pages[1].blocks.Add(TakeCard(lessonBook, SignalTrial.Source));
            pages[1].blocks.Add(TakeCard(lessonBook, SignalTrial.Meter));
            // Arşivin kendi N-12 kartı yerinde kalır (kaynakçası onu gösterir); dersin kartı ayrı bir kopyadır.
            var sample = TakeCard(lessonBook, SignalTrial.Narrow);
            sample.instanceID = QuestID + "/part/" + SignalTrial.Narrow; // defter hedef sayfası bu kopyanın kitabını gösterir
            research.AddTutorial(puzzle, lessonBook, pages);
            research.InsertPages(sampleBook, CreateSamplePages(sample));
        }
        // Kurulum bütün kitapların ışığını söndürür; ders adımı bu kitabı yanıp söndürebilsin.
        sampleBook.emissionIntensity = 10;
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
        if (collectedStep.HasTriggered && !sampleStep.HasTriggered && (HasSampleCard(NotebookUI.Instance) || HasCorrectReading))
        {
            sampleBook.OnLoseFocus(); sampleBook.emissionIntensity = 0; // malzemeler kurulumdan beri sönük
            sampleCollected.ReceiveSignal();
        }
        if (!collectedStep.HasTriggered)
        {
            bool collected = HasCollectedCards(NotebookUI.Instance) || HasCorrectReading;
            if (lessonBook.ExitLocked != !collected) BenchTrace.Log($"ChalkboardTutorial ExitLocked -> {!collected}"); // fence:
            lessonBook.ExitLocked = !collected;
            if (collected && BenchTrace.Skip("-skipsignal")) collected = false; // bisect:
            if (collected) { BenchTrace.Log("ChalkboardTutorial cardsCollected.ReceiveSignal"); cardsCollected.ReceiveSignal(); } // fence:
        }
        // Rehber defteri ve kitabı tarar; 0,1 sn'de bir yeter, hayalet animasyonu kendi karesinde akar.
        if (Time.unscaledTime >= nextGuidePoll) { nextGuidePoll = Time.unscaledTime + .1f; UpdateGuide(); }
        if (boardStep.HasTriggered && !doorStep.HasTriggered && officeDoor.IsLocked())
        {
            TutorialManager.Instance?.RecordLockCheckpoint(officeDoor);
            doorLocked.ReceiveSignal();
        }
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
        if (!sampleStep.HasTriggered || boardStep.HasTriggered) return;
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
