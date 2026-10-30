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
    private RectTransform measureButton;
    public NotebookDraggableBlock OutputCard => output;
    public Vector3 MeasureButton => measureButton.position;
    private CanvasGroup outputInput;
    private TextMeshProUGUI deviceFeedback;
    private readonly Dictionary<string, Reading> readings = new Dictionary<string, Reading>();
    public string TerminalMessage { get; internal set; } = "Ölçümler'deki ölçüm kağıdını okuyucuya bırak veya kodu elle gir.";
    public Reading LastReading { get; private set; }
    public event Action<string> MeasurementAccepted;

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
        trial.Board.SynthesisFeedback -= ShowFeedback;
        trial.Board.SynthesisFeedback += ShowFeedback;
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
        rect.localPosition = new Vector3(trial.Board.minBounds.x + .375f, trial.Board.minBounds.y + .42f, .045f);
        rect.localRotation = Quaternion.Euler(0, 180, 0);
        rect.localScale = Vector3.one / 300;
        rect.sizeDelta = new Vector2(218, 246);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main; canvas.sortingOrder = 7;
        outputInput = root.GetComponent<CanvasGroup>();
        PhysicalBoardFixture.Attach(rect, "Ölçüm Cihazı", Vector2.zero, rect.sizeDelta, new Color(.14f, .18f, .16f));
        var measure = new GameObject("Deneyi Ölç", typeof(RectTransform), typeof(Image), typeof(Button));
        var measureRect = measureButton = (RectTransform)measure.transform;
        measureRect.SetParent(rect, false); measureRect.sizeDelta = new Vector2(206, 36); measureRect.anchoredPosition = new Vector2(0, 100);
        measure.GetComponent<Image>().color = new Color(.65f, .51f, .27f);
        measure.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (trial.Board.isMachineActive && (GameManager.Instance == null || !GameManager.Instance.isGamePaused))
                trial.Board.AttemptSynthesis();
        });
        DeviceLabel(measureRect, "DENEYİ ÖLÇ", Vector2.zero, new Vector2(200, 32), 15);
        deviceFeedback = DeviceLabel(rect, "Ölçüm kağıdını deftere al.", new Vector2(0, 68), new Vector2(206, 24), 10);
        deviceFeedback.color = new Color(.94f, .90f, .75f);
        var outputSlot = new GameObject("Ölçüm Kağıdı Yuvası", typeof(RectTransform)).GetComponent<RectTransform>();
        outputSlot.SetParent(rect, false); outputSlot.sizeDelta = new Vector2(218, 156); outputSlot.anchoredPosition = new Vector2(0, -28);
        var paper = new GameObject("Ölçüm Kağıdı", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
        paper.transform.SetParent(outputSlot, false);
        output = paper.GetComponent<NotebookDraggableBlock>();
        ShowOutput(new Reading { problem = "Önce bir düzen kur; ÖLÇ düğmesine bas." });
    }

    public void Measure(SynthesisGraphData graph)
    {
        var activePuzzle = trial.CurrentPuzzle;
        var reading = PuzzleSimulation.Measure(graph, activePuzzle);
        if (!reading.MeetsTarget && trial != null && trial.Puzzles != null)
        {
            foreach (var candidatePuzzle in trial.Puzzles)
            {
                if (candidatePuzzle == null || candidatePuzzle == activePuzzle) continue;
                var candidate = PuzzleSimulation.Measure(graph, candidatePuzzle);
                if (candidate.MeetsTarget)
                {
                    activePuzzle = candidatePuzzle;
                    trial.SelectPuzzle(candidatePuzzle.id);
                    reading = candidate;
                    break;
                }
            }
        }
        if (readings.Count >= 999) reading.problem = "Bu oturumda 999 ölçüm alındı. Yeni ölçüm kodu için yeni oyun başlat.";
        if (reading.Measurable)
        {
            reading.measurementCode = "ECHOECHO_=_" + (readings.Count + 1).ToString("D3");
            reading.sessionID = trial.SessionID;
            reading.puzzleFingerprint = activePuzzle.Fingerprint();
        }
        LastReading = reading;
        if (LastReading.Measurable) readings[LastReading.Code] = LastReading;
        ShowOutput(LastReading);
        ShowFeedback(LastReading.Measurable ? "Ölçüm hazır. Kağıdı deftere al." : LastReading.problem, LastReading.Measurable);
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
        if (!IsMeasurementNote(note) || note.measurementSessionID != trial.SessionID) return false;
        if (note.puzzleFingerprint != trial.CurrentPuzzle.Fingerprint())
        {
            var matched = trial.Puzzles?.FirstOrDefault(p => p != null && p.Fingerprint() == note.puzzleFingerprint);
            if (matched != null) trial.SelectPuzzle(matched.id);
            else return false;
        }
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
        if (reading.sessionID != trial.SessionID)
        { TerminalMessage = "Bu ölçüm başka bir puzzle veya oturuma ait."; return false; }
        if (reading.puzzleFingerprint != trial.CurrentPuzzle.Fingerprint())
        {
            var matched = trial.Puzzles?.FirstOrDefault(p => p != null && p.Fingerprint() == reading.puzzleFingerprint);
            if (matched != null) trial.SelectPuzzle(matched.id);
            else { TerminalMessage = "Bu ölçüm başka bir puzzle veya oturuma ait."; return false; }
        }
        if (!reading.MeetsTarget)
        {
            TerminalMessage = reading.targetFeedback ?? (reading.kind == PuzzleKind.ColorMixture ? "Karışım hedefe uymuyor. Rengi, yoğunluğu ve miktarı karşılaştır." : "Kayıt hedefle örtüşmüyor. Şekli, yüksekliği ve zaman aralığını karşılaştır.");
            return false;
        }
        trial.MarkCompleted(CurrentQuestID);
        if (trial.Board.PinnedQuest != null) trial.Board.PinnedQuest.completed = true;
        TerminalMessage = trial.Puzzles.Count > 1 ? $"DENEY ONAYLANDI / {trial.Completed.Count}/{trial.Puzzles.Count} hedef tamamlandı." : "DENEY ONAYLANDI / Kayıt hedefle örtüşüyor.";
        Debug.Log("SIGNAL TRIAL COMPLETE: " + code);
        MeasurementAccepted?.Invoke(CurrentQuestID);
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

    private static TextMeshProUGUI DeviceLabel(Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var obj = new GameObject("Metin", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize; label.enableAutoSizing = true; label.fontSizeMin = 8; label.fontSizeMax = fontSize;
        label.color = new Color(.12f, .15f, .13f); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position;
        GameLocalization.Set(label, value);
        return label;
    }

    private void ShowFeedback(string message, bool success)
    {
        GameLocalization.Set(deviceFeedback, message);
    }

    private void OnDestroy()
    {
        if (trial != null && trial.Board != null) trial.Board.SynthesisFeedback -= ShowFeedback;
        if (outputInput != null) Destroy(outputInput.gameObject);
        if (Active == this) Active = null;
    }
}
