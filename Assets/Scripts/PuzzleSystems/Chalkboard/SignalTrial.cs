using System;
using System.Collections.Generic;
using UnityEngine;

// Small directed experiment model, isolated by runtime-only pulse-* part IDs.
public static class SignalTrial
{
    public const string QuestID = "pulse-double-record";
    public const string Source = "pulse-source", Narrow = "pulse-n12", Wide = "pulse-n34";
    public const string Splitter = "pulse-splitter", Delay = "pulse-delay", Attenuator = "pulse-attenuator", Meter = "pulse-meter";
    public static bool IsPart(string id) => PuzzleParts.Find(id) != null;
    public static int Inputs(string id) => PuzzleParts.Find(id)?.inputs ?? 0;
    public static int Outputs(string id) => PuzzleParts.Find(id)?.outputs ?? 0;
    public static string Symbol(string id) => PuzzleParts.Find(id)?.symbol ?? "?";

    [Serializable]
    public struct Pulse
    {
        public float time, height, width;
        public string sample;
    }

    public static SignalTrialContent CreateContent(FormulaItemSO template)
    {
        var content = new SignalTrialContent();
        Add(Source, "Atım Kaynağı", "BAŞLANGIÇ: Tek uyarım yollar. Giriş yok; bir çıkış.");
        Add(Narrow, "Numune N-12", "Uyarılınca kısa bir tepki verir. Bir giriş, bir çıkış.");
        Add(Meter, "Çift Kanallı Ölçer", "SONUÇ: İki girişe gelen izleri aynı kayda çizer. Çıkış yok.");
        Add(Splitter, "Dağıtıcı", "Gelen tepkiyi iki çıkışa da iletir. Bir giriş, iki çıkış.");
        Add(Delay, "Geciktirici", "İzi bir zaman aralığı geç iletir. Yüksekliği değişmez.");
        Add(Attenuator, "Sönümleyici", "İzin yüksekliğini yarıya indirir. Zamanı değişmez.");
        Add(Wide, "Numune N-34", "Uyarılınca uzun bir tepki verir. Bir giriş, bir çıkış.");
        void Add(string id, string title, string text) => content.AddPart(template, id, title, Inputs(id) + Outputs(id), text);
        var quest = content.AddQuest(QuestID, "Çift Kayıt", content.Parts.Count,
            "Bu deneyde tek numune kullan. Kısa tepkisini iki farklı anda kaydet; izler hedefle örtüşmeli.");
        for (int i = 0; i < 4; i++) content.Books[i] = new List<BookWorkspacePage>();
        content.Books[0].Add(Page(0, new NotebookBlockData { blockType = NotebookBlockType.Quest, questID = QuestID,
            title = "Çift Kayıt", description = "DENEY KOŞULU: 1 numune. Diğer işlem parçaları tekrarlanabilir. Hedefteki kaydı üret." }));
        content.Books[0].Add(Page(1, Note("target", "İSTENEN KAYIT", "İzlerin yüksekliği, genişliği ve aralığı önemlidir."),
            Note("", "DENEYİN KOŞULU", "Bu görevde 1 numune kullan. Her parçayı kullanmak zorunda değilsin; işlem parçalarını gerektiği kadar tekrarlayabilirsin.")));
        content.Books[1].Add(Page(0, Part(content.Parts[0]), Part(content.Parts[1]), Part(content.Parts[2])));
        content.Books[1].Add(Page(1, Part(content.Parts[3]), Part(content.Parts[4]), Part(content.Parts[5])));
        content.Books[1].Add(Page(2, Part(content.Parts[6])));
        content.Books[2].Add(Page(0, Note("ports", "HATLARIN YÖNÜ", "Halka giriş, ok çıkıştır. Bir uçtan tek hat çıkar. Başlangıç: kaynak; bitiş: ölçer."),
            Note("split", "AYNI UYARIM / İKİ KOL", "Dağıtıcı, girişine gelen tepkiyi iki kola da iletir.")));
        content.Books[2].Add(Page(1, Note("attenuate", "YÜKSEKLİK", "Sönümleyiciden geçen iz yarı yüksekliğe iner."),
            Note("delay", "ZAMAN", "Geciktiriciden geçen iz bir aralık sağa kayar. Yüksekliği korunur.")));
        content.Books[3].Add(Page(0, Note("samples", "NUMUNE GÖZLEMLERİ", "Aynı uyarım: N-12 kısa tepki, N-34 uzun tepki."),
            Note("coincident", "EŞZAMANLI VARIŞ", "İki iz aynı anda gelirse tek, daha yüksek bir iz olarak üst üste biner.")));
        content.Books[3].Add(Page(1, Note("beforeafter", "İŞLEMDEN ÖNCE / SONRA", "Değişim, parçanın kendi etkisidir. Etki, içinden geçen her ize uygulanır."),
            Note("", "KAYDI TAŞIMAK", "Tahtada DENEYİ ÖLÇ. Oluşan çıktı deftere alınabilir; kodu Turing'de sınanır.")));
        return content;
    }

    private static NotebookBlockData Part(FormulaItemSO part) => new NotebookBlockData { blockType = NotebookBlockType.PartClue,
        itemID = part.itemID, title = part.itemName, description = part.description };
    private static NotebookBlockData Note(string kind, string title, string description) => new NotebookBlockData {
        blockType = NotebookBlockType.RuleClue, itemID = "signal-note/" + kind, title = title, description = description };
    private static BookWorkspacePage Page(int index, params NotebookBlockData[] notes) => new BookWorkspacePage {
        pageIndex = index, blocks = new List<NotebookBlockData>(notes) };

    public static SignalExperiment.Reading Simulate(SynthesisGraphData graph)
        => PuzzleSimulation.Measure(graph, PuzzleDefinition.Legacy());

    public static string Encode(List<Pulse> pulses)
    {
        string result = "signal-result";
        if (pulses != null) foreach (var p in pulses)
            result += $"/{Mathf.RoundToInt(p.time * 1000)},{Mathf.RoundToInt(p.height * 1000)},{Mathf.RoundToInt(p.width * 1000)}";
        return result;
    }

    public static List<Pulse> Decode(string data)
    {
        var result = new List<Pulse>(); var fields = (data ?? "").Split('/');
        for (int i = 1; i < fields.Length; i++)
        {
            var values = fields[i].Split(',');
            if (values.Length == 3 && int.TryParse(values[0], out int t) && int.TryParse(values[1], out int h) && int.TryParse(values[2], out int w))
                result.Add(new Pulse { time = t / 1000f, height = h / 1000f, width = w / 1000f });
        }
        return result;
    }
}
