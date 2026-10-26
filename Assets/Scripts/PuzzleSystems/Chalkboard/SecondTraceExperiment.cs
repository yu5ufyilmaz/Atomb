using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Throwaway, scene-scoped experiment. Disable this component to recover the A/B research trials.
public sealed class SecondTraceExperiment : MonoBehaviour
{
    public const string QuestID = "second-trace";
    public const string Source = "trace-source", Sample = "trace-sample", Detector = "trace-detector";
    public const string Reference = "trace-reference", Absorber = "trace-absorber";
    public static SecondTraceExperiment Active { get; private set; }
    public InteractableTuringMachine terminal;
    [Tooltip("Yeni parça görevleriyle kör oynanış testi. Önceki deney içeriği korunur.")]
    public bool newTest;
    public string CurrentQuestID => newTest ? SignalTrial.QuestID : QuestID;
    public static bool IsMeasurementNote(NotebookBlockData note) => note != null
        && ((note.itemID ?? "").StartsWith("signal-result/") || (note.itemID ?? "").StartsWith("trace-result/"));
    private ResearchPuzzleTrial trial;
    private NotebookDraggableBlock output;
    private CanvasGroup outputInput;
    private readonly Dictionary<string, Reading> readings = new Dictionary<string, Reading>();
    public string TerminalMessage { get; internal set; } = "Ölçümler'deki ölçüm kağıdını okuyucuya bırak veya kodu elle gir.";
    public Reading LastReading { get; private set; }

    public struct Reading
    {
        public float main, echo;
        public bool directedTrial, targetMatch;
        public List<SignalTrial.Pulse> pulses;
        public string measurementCode;
        public string targetFeedback;
        public string problem;
        public bool Measurable => string.IsNullOrEmpty(problem);
        public bool MeetsTarget => Measurable && (directedTrial ? targetMatch : main >= .9f && main <= 1.1f && echo <= .1f);
        public string Code => !string.IsNullOrEmpty(measurementCode) ? measurementCode : "ECHOECHO_=_" + Mathf.RoundToInt(main * 100 + echo * 100).ToString("D3");
    }

    public static ResearchTrialContent CreateContent(FormulaItemSO template)
    {
        var content = new ResearchTrialContent();
        content.AddPart(template, Source, "Uyarıcı", 4, "Tek uyarım gönderir. UYA");
        content.AddPart(template, Sample, "Numune", 4, "Tepkisi korunacak örnek. NUM");
        content.AddPart(template, Detector, "Algılayıcı", 4, "Gelen izleri kaydeder. ALG");
        content.AddPart(template, Reference, "Referans", 4, "İkinci ölçüm yolu; deneyde devrede kalmalı. REF");
        content.AddPart(template, Absorber, "Sönümleyici", 4, "İçinden geçen izi zayıflatır. SÖN");
        var quest = content.AddQuest(QuestID, "İkinci İz", 5,
            "İlk izi koru; gecikmiş izi gider. Referans kolu devrede kalmalı.");
        for (int i = 0; i < 4; i++) content.Books[i] = new List<BookWorkspacePage>();
        content.Books[0].Add(Page(0, "EK-07 / Araştırma sorusu",
            new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = QuestID, title = "İkinci İz",
                description = "Tek uyarım, iki iz. İlkini koru; ikinciyi gider." }));
        content.Books[0].Add(Page(1, "EK-07 / Ölçüm notu", Note("baseline", "EK-07 / KURULAN DÜZEN",
            "Tek atımda iki iz kaydedildi. Referans kolu açık kalmalı.")));
        content.Books[1].Add(Page(0, "Parça Atlası / Ölçüm", Part(content.Parts[0]), Part(content.Parts[1]), Part(content.Parts[2])));
        content.Books[1].Add(Page(1, "Parça Atlası / Yan kollar", Part(content.Parts[3]), Part(content.Parts[4])));
        content.Books[2].Add(Page(0, "Bağ İlkeleri / Zayıflama", Note("absorber", "İZİN ZAYIFLAMASI",
            "Sönümleyici, içinden geçen her izi zayıflatır.")));
        content.Books[2].Add(Page(1, "Bağ İlkeleri / Ölçüm", Note("threshold", "ALGILAMA EŞİĞİ",
            "Çok zayıf izler kayıtta görünmez. Bu deneyde tek çizgi kullan."),
            Note("", "ÖLÇÜMÜ TAŞIMAK", "Tahtada ÖLÇ ile çıktı al. Çıktıyı deftere alıp Turing'de kodunu dene.")));
        content.Books[3].Add(Page(0, "Deney Kayıtları / Gecikme", Note("echo", "D-06 / GECİKMİŞ İZ",
            "İki yol, tek uyarımdan iki ayrı zamanda iz üretti.")));
        content.Books[3].Add(Page(1, "Deney Kayıtları / Zayıflama", Note("mainloss", "D-09 / ANA HAT KAYBI",
            "Bu yerleşimde ilk iz de zayıfladı. Başarılı bir kayıt değil.")));
        return content;
    }

    private static NotebookBlockData Part(FormulaItemSO part) => new NotebookBlockData {
        blockType = NotebookBlockType.PartClue, itemID = part.itemID, title = part.itemName, description = part.description };
    private static NotebookBlockData Note(string diagram, string title, string description) => new NotebookBlockData {
        blockType = NotebookBlockType.RuleClue, itemID = "trace-note/" + diagram, title = title, description = description };
    private static BookWorkspacePage Page(int index, string title, params NotebookBlockData[] blocks) =>
        new BookWorkspacePage { pageIndex = index, title = title, blocks = new List<NotebookBlockData>(blocks) };

    public void Initialize(ResearchPuzzleTrial research)
    {
        if (gameObject.scene.name != "scn_SynthesisBlockout" && !research.allowGameplayScene) return;
        Active = this;
        trial = research;
        var root = new GameObject("İz Ölçüm Çıktısı", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var rect = (RectTransform)root.transform;
        rect.SetParent(trial.Board.boardTransform, false);
        rect.localPosition = newTest
            ? new Vector3(trial.Board.minBounds.x + .40f, trial.Board.minBounds.y + .30f, .045f)
            : new Vector3(-.48f, .78f, .045f);
        rect.localRotation = Quaternion.Euler(0, 180, 0);
        rect.localScale = Vector3.one / 300;
        rect.sizeDelta = newTest ? new Vector2(218, 156) : new Vector2(510, 165);
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
        var reading = newTest ? SignalTrial.Simulate(graph) : Simulate(graph);
        if (newTest && reading.Measurable)
            reading.measurementCode = "ECHOECHO_=_" + (readings.Count + 1).ToString("D3");
        LastReading = reading;
        if (LastReading.Measurable) readings[LastReading.Code] = LastReading;
        ShowOutput(LastReading);
        Debug.Log($"SECOND TRACE MEASUREMENT: main={LastReading.main:F2}, echo={LastReading.echo:F2}, code={(LastReading.Measurable ? LastReading.Code : LastReading.problem)}");
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
            itemID = newTest ? SignalTrial.Encode(result.pulses) : $"trace-result/{Mathf.RoundToInt(result.main * 100)}/{Mathf.RoundToInt(result.echo * 100)}",
            title = result.Measurable ? "ÖLÇÜM / " + result.Code.Replace("_", " ") : "ÖLÇÜM KAĞIDI",
            measurementCode = result.Measurable ? result.Code : null,
            description = result.Measurable ? "Ölçümler'e al; Turing okuyucusuna bırak. Turing: 6." : result.problem }, true);
        ((RectTransform)output.transform).sizeDelta = ((RectTransform)parent).sizeDelta;
        output.enabled = result.Measurable;
    }

    public bool TryGetMeasurementCode(NotebookBlockData note, out string code)
    {
        code = null;
        if (!IsMeasurementNote(note)) return false;
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
        if (!reading.MeetsTarget)
        {
            TerminalMessage = newTest ? reading.targetFeedback ?? "Kayıt hedefle örtüşmüyor. Şekli, yüksekliği ve zaman aralığını karşılaştır."
                : reading.main < .9f ? "İlk tepki de zayıflamış. Düzeni yeniden incele."
                : reading.echo > .1f ? "Gecikmiş iz hâlâ var. Düzeni yeniden incele." : "İlk tepki değişmiş. Düzeni yeniden incele.";
            return false;
        }
        trial.MarkCompleted(CurrentQuestID);
        if (trial.Board.PinnedQuest != null) trial.Board.PinnedQuest.completed = true;
        TerminalMessage = newTest ? "DENEY ONAYLANDI / Kayıt hedefle örtüşüyor." : "DENEY ONAYLANDI / İlk iz korundu; gecikmiş iz kayboldu.";
        Debug.Log("SECOND TRACE COMPLETE: " + code);
        return true;
    }

    // Five-part fictional model, not a physics solver: every simple path contributes one arrival.
    public static Reading Simulate(SynthesisGraphData graph)
    {
        Reading Invalid(string why) => new Reading { problem = why };
        if (graph == null || graph.parts == null || graph.parts.Count < 4 || graph.parts.Count > 5)
            return Invalid("Ölçüm düzeni eksik. Uyarım, numune, algılayıcı ve referans kaydı gerekli.");
        var ids = new Dictionary<string, int>();
        for (int i = 0; i < graph.parts.Count; i++)
        {
            string id = graph.parts[i]?.itemID;
            if (id == null || ids.ContainsKey(id) || (id != Source && id != Sample && id != Detector && id != Reference && id != Absorber))
                return Invalid("Bu bileşenlerle karşılaştırılabilir bir ölçüm alınamadı.");
            ids.Add(id, i);
        }
        foreach (string id in new[] { Source, Sample, Detector, Reference })
            if (!ids.ContainsKey(id)) return Invalid("Ölçüm düzeni eksik; referans da devrede kalmalı.");
        int count = graph.parts.Count;
        var links = new bool[count, count];
        if (graph.bonds == null) return Invalid("Ölçüm alınamadı; hatlar açık.");
        foreach (var bond in graph.bonds)
        {
            if (bond == null || bond.fromIndex < 0 || bond.fromIndex >= count || bond.toIndex < 0 || bond.toIndex >= count
                || bond.fromIndex == bond.toIndex || bond.bondType != BondType.Single || links[bond.fromIndex, bond.toIndex])
                return Invalid("Ölçüm okunamadı. Bu kayıt tek çizgili hatlarla karşılaştırılır.");
            links[bond.fromIndex, bond.toIndex] = links[bond.toIndex, bond.fromIndex] = true;
        }
        int source = ids[Source], sample = ids[Sample], detector = ids[Detector], reference = ids[Reference];
        int absorber = ids.TryGetValue(Absorber, out int found) ? found : -1;
        float main = 0, echo = 0;
        bool hasMain = false, hasReference = false, bypass = false;
        var seen = new bool[count];
        void Visit(int node, bool sampled, bool reflected, bool damped)
        {
            sampled |= node == sample; reflected |= node == reference; damped |= node == absorber;
            if (node == detector)
            {
                if (!sampled) { bypass = true; return; }
                float gain = damped ? .2f : 1;
                if (reflected) { echo += .5f * gain; hasReference = true; }
                else { main += gain; hasMain = true; }
                return;
            }
            seen[node] = true;
            for (int next = 0; next < count; next++) if (links[node, next] && !seen[next]) Visit(next, sampled, reflected, damped);
            seen[node] = false;
        }
        Visit(source, false, false, false);
        if (bypass) return Invalid("Numuneden geçmeyen uyarım ölçümü örtüyor.");
        if (!hasMain || !hasReference) return Invalid("İlk ölçüm veya referans kolundan kayıt alınamadı.");
        return new Reading { main = main < .12f ? 0 : main, echo = echo < .12f ? 0 : echo };
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
