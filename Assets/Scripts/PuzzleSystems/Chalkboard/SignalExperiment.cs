using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Measurement output and Turing submission for the current directed signal experiment.
public sealed class SignalExperiment : MonoBehaviour
{
    public static SignalExperiment Active { get; private set; }
    public InteractableTuringMachine terminal;
    public string CurrentQuestID => trial?.CurrentPuzzle?.id ?? SignalTrial.QuestID;
    public static bool IsMeasurementNote(NotebookBlockData note) => note != null
        && ((note.itemID ?? "").StartsWith("signal-result/") || (note.itemID ?? "").StartsWith("mixture-result/"));
    private ResearchPuzzleTrial trial;
    private NotebookDraggableBlock output;
    private CanvasGroup outputInput;
    private readonly Dictionary<string, Reading> readings = new Dictionary<string, Reading>();
    public string TerminalMessage { get; internal set; } = "Ölçümler'deki ölçüm kağıdını okuyucuya bırak veya kodu elle gir.";
    public Reading LastReading { get; private set; }

    [Serializable]
    public struct Reading
    {
        public PuzzleKind kind;
        public MixtureResult mixture;
        public float main, echo;
        public bool directedTrial, targetMatch;
        public List<SignalTrial.Pulse> pulses;
        public string measurementCode;
        public string sessionID, puzzleFingerprint;
        public string targetFeedback;
        public string problem;
        public bool Measurable => string.IsNullOrEmpty(problem);
        public bool MeetsTarget => Measurable && targetMatch;
        public string Code => !string.IsNullOrEmpty(measurementCode) ? measurementCode : "ECHOECHO_=_" + Mathf.RoundToInt(main * 100 + echo * 100).ToString("D3");
    }

    public void Initialize(ResearchPuzzleTrial research)
    {
        if (gameObject.scene.name != "scn_SynthesisBlockout" && !research.allowGameplayScene) return;
        Active = this;
        trial = research;
        terminal?.ResetMeasurementSession();
        readings.Clear();
        if (output != null)
        {
            LastReading = new Reading { problem = "Önce bir düzen kur; ÖLÇ düğmesine bas." };
            ShowOutput(LastReading);
            return;
        }
        var root = new GameObject("İz Ölçüm Çıktısı", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var rect = (RectTransform)root.transform;
        rect.SetParent(trial.Board.boardTransform, false);
        rect.localPosition = new Vector3(trial.Board.minBounds.x + .40f, trial.Board.minBounds.y + .30f, .045f);
        rect.localRotation = Quaternion.Euler(0, 180, 0);
        rect.localScale = Vector3.one / 300;
        rect.sizeDelta = new Vector2(218, 156);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main; canvas.sortingOrder = 7;
        outputInput = root.GetComponent<CanvasGroup>();
        var paper = new GameObject("Ölçüm Kağıdı", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
        paper.transform.SetParent(rect, false);
        output = paper.GetComponent<NotebookDraggableBlock>();
        ShowOutput(new Reading { problem = "Önce bir düzen kur; ÖLÇ düğmesine bas." });
    }

    public void Measure(SynthesisGraphData graph)
    {
        var reading = PuzzleSimulation.Measure(graph, trial.CurrentPuzzle);
        if (readings.Count >= 999) reading.problem = "Bu oturumda 999 ölçüm alındı. Yeni ölçüm kodu için yeni oyun başlat.";
        if (reading.Measurable)
        {
            reading.measurementCode = "ECHOECHO_=_" + (readings.Count + 1).ToString("D3");
            reading.sessionID = trial.SessionID;
            reading.puzzleFingerprint = trial.CurrentPuzzle.Fingerprint();
        }
        LastReading = reading;
        if (LastReading.Measurable) readings[LastReading.Code] = LastReading;
        ShowOutput(LastReading);
        Debug.Log($"SIGNAL TRIAL MEASUREMENT: main={LastReading.main:F2}, echo={LastReading.echo:F2}, code={(LastReading.Measurable ? LastReading.Code : LastReading.problem)}");
    }

    private void ShowOutput(Reading result)
    {
        // Replace the source card so previously collected output notes remain immutable snapshots.
        var parent = output.transform.parent;
        output.gameObject.SetActive(false);
        Destroy(output.gameObject);
        var card = new GameObject("Ölçüm Kağıdı", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
        card.transform.SetParent(parent, false);
        output = card.GetComponent<NotebookDraggableBlock>();
        output.Initialize(new NotebookBlockData { blockType = NotebookBlockType.RuleClue,
            itemID = result.kind == PuzzleKind.ColorMixture && result.mixture != null ? result.mixture.Encode(false) : SignalTrial.Encode(result.pulses),
            title = result.Measurable ? "ÖLÇÜM / " + result.Code.Replace("_", " ") : "ÖLÇÜM KAĞIDI",
            measurementCode = result.Measurable ? result.Code : null,
            measurementSessionID = result.sessionID, puzzleFingerprint = result.puzzleFingerprint,
            questID = trial?.Puzzles.FirstOrDefault(p => p.Fingerprint() == result.puzzleFingerprint)?.id ?? CurrentQuestID,
            description = result.Measurable ? "Ölçümler'e al; Turing okuyucusuna bırak. Turing: 6." : result.problem }, true);
        ((RectTransform)output.transform).sizeDelta = ((RectTransform)parent).sizeDelta;
        output.enabled = result.Measurable;
    }

    public bool TryGetMeasurementCode(NotebookBlockData note, out string code)
    {
        code = null;
        if (!IsMeasurementNote(note) || note.measurementSessionID != trial.SessionID
            || note.puzzleFingerprint != trial.CurrentPuzzle.Fingerprint()) return false;
        foreach (var entry in readings)
            if (note.measurementCode == entry.Key || (string.IsNullOrEmpty(note.measurementCode)
                && note.title == "ÖLÇÜM / " + entry.Key.Replace("_", " ")))
            { code = entry.Key; return true; }
        return false;
    }

    public bool Submit(string code)
    {
        if (!readings.TryGetValue(code, out var reading))
        { TerminalMessage = "Bu koda ait bir ölçüm yok. Tahtadaki çıktıyı kontrol et."; return false; }
        if (reading.sessionID != trial.SessionID || reading.puzzleFingerprint != trial.CurrentPuzzle.Fingerprint())
        { TerminalMessage = "Bu ölçüm başka bir puzzle veya oturuma ait."; return false; }
        if (!reading.MeetsTarget)
        {
            TerminalMessage = reading.targetFeedback ?? (reading.kind == PuzzleKind.ColorMixture ? "Karışım hedefe uymuyor. Rengi, yoğunluğu ve miktarı karşılaştır." : "Kayıt hedefle örtüşmüyor. Şekli, yüksekliği ve zaman aralığını karşılaştır.");
            return false;
        }
        trial.MarkCompleted(CurrentQuestID);
        if (trial.Board.PinnedQuest != null) trial.Board.PinnedQuest.completed = true;
        TerminalMessage = trial.Puzzles.Count > 1 ? $"DENEY ONAYLANDI / {trial.Completed.Count}/{trial.Puzzles.Count} hedef tamamlandı." : "DENEY ONAYLANDI / Kayıt hedefle örtüşüyor.";
        Debug.Log("SIGNAL TRIAL COMPLETE: " + code);
        return true;
    }

    public List<Reading> CaptureReadings() => new List<Reading>(readings.Values);

    public void RestoreReadings(List<Reading> saved)
    {
        readings.Clear();
        if (saved != null) foreach (var reading in saved)
        {
            if (reading.Measurable && !string.IsNullOrEmpty(reading.measurementCode) && reading.sessionID == trial.SessionID
                && trial.Puzzles.Any(p => p.Fingerprint() == reading.puzzleFingerprint)) readings[reading.Code] = reading;
        }
        LastReading = readings.Count > 0 ? new List<Reading>(readings.Values)[readings.Count - 1]
            : new Reading { problem = "Önce bir düzen kur; ÖLÇ düğmesine bas." };
        if (output != null) ShowOutput(LastReading);
    }

    private void LateUpdate()
    {
        if (outputInput == null || trial == null) return;
        outputInput.alpha = 1;
        outputInput.blocksRaycasts = outputInput.interactable = trial.Board.isMachineActive
            && (GameManager.Instance == null || !GameManager.Instance.isGamePaused);
    }

    private void OnDestroy() { if (Active == this) Active = null; }
}
