using System;
using System.Collections.Generic;
using UnityEngine;

// Small directed experiment model, isolated by runtime-only pulse-* part IDs.
public static class SignalTrial
{
    public const string QuestID = "pulse-double-record";
    public const string Source = "pulse-source", Narrow = "pulse-n12", Wide = "pulse-n34";
    public const string Splitter = "pulse-splitter", Delay = "pulse-delay", Attenuator = "pulse-attenuator", Meter = "pulse-meter";
    public static bool IsPart(string id) => id == Source || id == Narrow || id == Wide || id == Splitter || id == Delay || id == Attenuator || id == Meter;
    public static int Inputs(string id) => id == Source ? 0 : id == Meter ? 2 : 1;
    public static int Outputs(string id) => id == Meter ? 0 : id == Splitter ? 2 : 1;
    public static string Symbol(string id) => id == Source ? "ATIM" : id == Narrow ? "N-12" : id == Wide ? "N-34"
        : id == Splitter ? "DAĞ" : id == Delay ? "GEC" : id == Attenuator ? "SÖN" : "ÖLÇ";

    [Serializable]
    public struct Pulse
    {
        public float time, height, width;
        public string sample;
    }

    public static ResearchTrialContent CreateContent(FormulaItemSO template)
    {
        var content = new ResearchTrialContent();
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

    public static SecondTraceExperiment.Reading Simulate(SynthesisGraphData graph)
    {
        SecondTraceExperiment.Reading Invalid(string text) => new SecondTraceExperiment.Reading { directedTrial = true, problem = text };
        if (graph == null || graph.parts == null || graph.parts.Count < 2 || graph.bonds == null)
            return Invalid("Kaynak ile ölçer arasında bir deney düzeni kur.");
        int count = graph.parts.Count, source = -1, meter = -1;
        var incoming = new int[count]; var outgoing = new List<int>[count];
        for (int i = 0; i < count; i++)
        {
            string id = graph.parts[i]?.itemID;
            if (!IsPart(id)) return Invalid("Ölçümde tanınmayan bir bileşen var.");
            if (id == Source) { if (source >= 0) return Invalid("Bir ölçüm düzeneğinde tek atım kaynağı kullan."); source = i; }
            if (id == Meter) { if (meter >= 0) return Invalid("Bir ölçüm düzeneğinde tek ölçer kullan."); meter = i; }
            outgoing[i] = new List<int>();
        }
        if (source < 0 || meter < 0) return Invalid("Atımın kaynağı veya ölçerin kaydı eksik.");
        foreach (var edge in graph.bonds)
        {
            if (edge == null || !edge.directed || edge.bondType != BondType.Single || edge.fromIndex < 0 || edge.toIndex < 0
                || edge.fromIndex >= count || edge.toIndex >= count || edge.fromIndex == edge.toIndex || outgoing[edge.fromIndex].Contains(edge.toIndex))
                return Invalid("Hat okunamadı. Çıkış okunu bir giriş halkasına bağla.");
            outgoing[edge.fromIndex].Add(edge.toIndex); incoming[edge.toIndex]++;
        }
        for (int i = 0; i < count; i++)
            if (incoming[i] > Inputs(graph.parts[i].itemID) || outgoing[i].Count > Outputs(graph.parts[i].itemID))
                return Invalid("Bir bileşenin giriş veya çıkış sayısı aşıldı.");
        var pulses = new List<Pulse>(); var path = new bool[count]; string issue = null;
        var samples = new HashSet<int>();
        void Visit(int index, Pulse pulse)
        {
            if (path[index]) { issue = "Hat kendi üzerine dönüyor; karşılaştırılabilir kayıt alınamadı."; return; }
            string id = graph.parts[index].itemID;
            if (id == Narrow || id == Wide)
            {
                samples.Add(index);
                pulse.sample = id; pulse.width = id == Narrow ? .025f : .075f;
            }
            if (id == Delay) pulse.time += .32f;
            if (id == Attenuator) pulse.height *= .5f;
            if (index == meter) { pulses.Add(pulse); return; }
            path[index] = true;
            foreach (int next in outgoing[index]) Visit(next, pulse);
            path[index] = false;
        }
        Visit(source, new Pulse { time = .18f, height = 1, width = .045f });
        if (issue != null) return Invalid(issue);
        if (pulses.Count == 0) return Invalid("Ölçere ulaşan iz yok. Açık kalan hatları gözlemle.");
        pulses.Sort((a, b) => a.time.CompareTo(b.time));
        // Quantity is this quest's condition; repeated processing parts remain valid experiments.
        bool matches = samples.Count == 1 && pulses.Count == 2 && pulses[0].sample == Narrow && pulses[1].sample == Narrow
            && Mathf.Abs(pulses[0].height - .5f) < .01f && Mathf.Abs(pulses[1].height - 1) < .01f
            && Mathf.Abs(pulses[0].time - .18f) < .01f && Mathf.Abs(pulses[1].time - .50f) < .01f;
        return new SecondTraceExperiment.Reading { directedTrial = true, targetMatch = matches, pulses = pulses,
            targetFeedback = samples.Count != 1 ? "Bu görevin koşulu: düzende 1 numune kullan. Ölçüm alındı, fakat numune adedi koşula uymuyor." : null,
            main = pulses[0].height, echo = pulses.Count > 1 ? pulses[1].height : 0 };
    }

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
