using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PuzzleSimulation
{
    public const int MaxParts = 64, MaxPulses = 128;
    public const float MaxTime = .98f, MaxHeight = 2.1f;

    public static string ValidateRules(PuzzleRules rules, PuzzleKind kind = PuzzleKind.Signal)
    {
        if (rules == null || rules.parts == null) return "Parça kuralları eksik.";
        if (rules.minParts < 3 || rules.maxParts < rules.minParts || rules.maxParts > MaxParts)
            return "Toplam parça aralığı 3–64 arasında ve alt sınır üst sınırdan küçük olmalı.";
        if (rules.minSamples < 1 || rules.maxSamples < rules.minSamples || rules.maxSamples > rules.maxParts - 2)
            return "En az bir numune seç; numune aralığı parça bütçesine sığmalı.";
        if (rules.maxDepth < 2 || rules.maxDepth > MaxParts || (kind == PuzzleKind.Signal && (rules.traceCount < 1 || rules.traceCount > 2)))
            return "Derinlik 2–64, görünür iz sayısı 1 veya 2 olmalı.";
        int required = 0, sampleMax = 0; var ids = new HashSet<string>();
        foreach (var rule in rules.parts)
        {
            var part = rule != null ? PuzzleParts.Find(rule.itemID) : null;
            if (part == null || !ids.Add(rule.itemID)) return "Bilinmeyen veya yinelenen parça kuralı var.";
            if (!PuzzleParts.ForKind(kind).Contains(part)) return "Parça seçilen deney türüne ait değil: " + part.title;
            if (rule.minimum < 0 || rule.maximum < rule.minimum || rule.maximum > MaxParts || (!rule.allowed && rule.minimum > 0))
                return part.title + ": adet sınırları veya zorunlu/yasak seçimi çelişiyor.";
            required += rule.minimum;
            if (rule.allowed && part.sample) sampleMax += rule.maximum;
        }
        if (!rules.Allows(PuzzleParts.Source(kind)) || !rules.Allows(PuzzleParts.Meter(kind))) return "Kaynak ve ölçer izinli olmalı.";
        foreach (string id in new[] { PuzzleParts.Source(kind), PuzzleParts.Meter(kind) })
            if (rules.Find(id).minimum > 1) return "Tek kaynak ve tek ölçer kullanılabilir.";
        if (required > rules.maxParts || sampleMax < rules.minSamples) return "Zorunlu parçalar veya numuneler bütçeye sığmıyor.";
        return null;
    }

    public static string Validate(PuzzleDefinition puzzle)
    {
        if (puzzle == null || puzzle.version != PuzzleDefinition.CurrentVersion) return "Puzzle veri sürümü desteklenmiyor.";
        if (string.IsNullOrWhiteSpace(puzzle.id) || string.IsNullOrWhiteSpace(puzzle.title)) return "Puzzle adı ve kimliği gerekli.";
        if (!Enum.IsDefined(typeof(PuzzleKind), puzzle.kind)) return "Puzzle türü desteklenmiyor.";
        string issue = ValidateRules(puzzle.rules, puzzle.kind);
        if (issue != null) return issue;
        if (!Finite(puzzle.tolerance) || puzzle.tolerance <= 0 || puzzle.tolerance > .01f) return "Karşılaştırma toleransı 0–0,01 arasında olmalı.";
        issue = puzzle.kind == PuzzleKind.ColorMixture ? ColorMixtureSimulation.ValidateTarget(puzzle.mixtureTarget) : ValidateTarget(puzzle.target, puzzle.rules.traceCount);
        if (issue != null) return issue;
        var reading = Measure(puzzle.solution, puzzle);
        if (!reading.Measurable) return reading.problem;
        return reading.MeetsTarget ? null : reading.targetFeedback ?? "Örnek çözüm hedef kaydı üretmiyor. Hedefi güncelle veya şemayı düzelt.";
    }

    public static string ValidateTarget(List<SignalTrial.Pulse> target, int count)
    {
        if (target == null || target.Count < 1 || target.Count > 2 || target.Count != count) return "Hedefte istenen sayıda (1–2) görünür iz olmalı.";
        foreach (var p in target)
            if (!Finite(p.time) || !Finite(p.height) || !Finite(p.width) || p.time < .02f || p.width < .005f
                || p.width > .15f || p.time + p.width > MaxTime || p.height < .03f || p.height > MaxHeight)
                return "Hedef izleri grafiğe sığmalı: zaman + genişlik ≤ 0,98; yükseklik 0,03–2,1; genişlik 0,005–0,15.";
        if (target.Count == 2 && Mathf.Abs(target[0].time - target[1].time) <= Mathf.Max(target[0].width, target[1].width) * 2)
            return "Hedef izleri örtüşüyor. Daha açık bir zaman aralığı seç veya eşzamanlı izleri birleştir.";
        return null;
    }

    public static List<SignalTrial.Pulse> Normalize(List<SignalTrial.Pulse> input)
    {
        var result = input == null ? new List<SignalTrial.Pulse>() : new List<SignalTrial.Pulse>(input);
        result.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.width.CompareTo(b.width));
        for (int i = result.Count - 1; i > 0; i--)
            if (Mathf.Abs(result[i].time - result[i - 1].time) < .0001f && Mathf.Abs(result[i].width - result[i - 1].width) < .0001f)
            { var p = result[i - 1]; p.height += result[i].height; result[i - 1] = p; result.RemoveAt(i); }
        return result;
    }

    // Her iki deney türünün ortak hat çözümlemesi: parçalar, kenarlar, sıra, kaynaktan erişim, ölçere erişim ve derinlik.
    internal sealed class Dag
    {
        public SignalPartLibrary.Part[] parts;
        public int[] incoming, depth;
        public List<int>[] outgoing;
        public List<int> order;
        public bool[] reached, reachesMeter;
        public int source = -1, meter = -1, samples;
        public readonly Dictionary<string, int> quantities = new Dictionary<string, int>();
    }

    // Dönen metin ölçüm kartında gösterilen hata; null ise hat geçerli ve dag dolu.
    internal static string BuildDag(SynthesisGraphData graph, PuzzleKind kind, out Dag dag)
    {
        bool mixture = kind == PuzzleKind.ColorMixture; dag = null;
        if (graph?.parts == null || graph.bonds == null || graph.parts.Count < 2 || graph.parts.Count > MaxParts)
            return mixture ? "Kaynak ile analizör arasında bir karışım düzeneği kur." : "Kaynak ile ölçer arasında bir deney düzeni kur (en fazla 64 parça).";
        int count = graph.parts.Count;
        var d = new Dag { parts = new SignalPartLibrary.Part[count], incoming = new int[count], depth = new int[count], outgoing = new List<int>[count],
            reached = new bool[count], reachesMeter = new bool[count], order = new List<int>(count) };
        var library = PuzzleParts.ForKind(kind); string sourceID = PuzzleParts.Source(kind), meterID = PuzzleParts.Meter(kind);
        for (int i = 0; i < count; i++)
        {
            var part = PuzzleParts.Find(graph.parts[i]?.itemID);
            if (part == null || !library.Contains(part)) return mixture ? "Bu parça renk ve karışım deneyinde kullanılamaz." : "Ölçümde tanınmayan bir bileşen var.";
            d.parts[i] = part; d.outgoing[i] = new List<int>();
            if (part.id == sourceID) { if (d.source >= 0) return mixture ? "Tek taşıyıcı kaynağı kullan." : "Tek atım kaynağı kullan."; d.source = i; }
            if (part.id == meterID) { if (d.meter >= 0) return mixture ? "Tek analizör kullan." : "Tek ölçer kullan."; d.meter = i; }
            d.quantities.TryGetValue(part.id, out int quantity); d.quantities[part.id] = quantity + 1;
            if (part.sample) d.samples++;
        }
        if (d.source < 0 || d.meter < 0) return mixture ? "Taşıyıcı kaynağı veya analizör eksik." : "Atım kaynağı veya ölçer eksik.";
        foreach (var edge in graph.bonds)
        {
            if (edge == null || !edge.directed || edge.bondType != BondType.Single || edge.fromIndex < 0 || edge.toIndex < 0
                || edge.fromIndex >= count || edge.toIndex >= count || edge.fromIndex == edge.toIndex || d.outgoing[edge.fromIndex].Contains(edge.toIndex))
                return mixture ? "Çıkış okunu başka bir parçanın girişine bağla." : "Hat okunamadı. Çıkış okunu bir giriş halkasına bağla.";
            d.outgoing[edge.fromIndex].Add(edge.toIndex); d.incoming[edge.toIndex]++;
        }
        for (int i = 0; i < count; i++)
            if (d.incoming[i] > d.parts[i].inputs || d.outgoing[i].Count > d.parts[i].outputs)
                return mixture ? "Giriş veya çıkış kapasitesi aşıldı." : "Bir bileşenin giriş veya çıkış sayısı aşıldı.";
        // Kahn sıralaması kullanılmayan bir bileşendeki döngüyü de yakalar.
        var pending = (int[])d.incoming.Clone(); var queue = new Queue<int>();
        for (int i = 0; i < count; i++) if (pending[i] == 0) queue.Enqueue(i);
        while (queue.Count > 0)
        { int i = queue.Dequeue(); d.order.Add(i); foreach (int next in d.outgoing[i]) if (--pending[next] == 0) queue.Enqueue(next); }
        if (d.order.Count != count) return mixture ? "Sıvı hattında döngü var." : "Hat kendi üzerine dönüyor; döngüyü kaldır.";
        d.reached[d.source] = true;
        foreach (int i in d.order)
        {
            if (!d.reached[i]) continue;
            foreach (int next in d.outgoing[i]) { d.reached[next] = true; d.depth[next] = Mathf.Max(d.depth[next], d.depth[i] + 1); }
        }
        d.reachesMeter[d.meter] = true;
        for (int j = d.order.Count - 1; j >= 0; j--) foreach (int next in d.outgoing[d.order[j]]) d.reachesMeter[d.order[j]] |= d.reachesMeter[next];
        dag = d; return null;
    }

    // Deney koşulları: tüm parçalar çalışmalı; adet, numune, derinlik, izin ve parça başına adet sınırları.
    internal static string CheckRules(Dag dag, PuzzleRules rules, PuzzleKind kind)
    {
        bool mixture = kind == PuzzleKind.ColorMixture; int count = dag.parts.Length;
        for (int i = 0; i < count; i++)
            if (!dag.reached[i] || !dag.reachesMeter[i] || dag.outgoing[i].Count != dag.parts[i].outputs || (mixture && dag.incoming[i] != dag.parts[i].inputs))
                return mixture ? "Bütün parçalar çalışmalı. Dağıtıcının iki çıkışını ve karıştırıcının iki girişini de bağla."
                    : "Bütün parçalar kaynaktan beslenip ölçere ulaşmalı; açık veya kullanılmayan uçları kaldır.";
        if (rules == null) return null;
        if (count < rules.minParts || count > rules.maxParts)
            return mixture ? $"Parça adedi {rules.minParts}–{rules.maxParts} olmalı." : $"Toplam parça koşulu: {rules.minParts}–{rules.maxParts}.";
        if (dag.samples < rules.minSamples || dag.samples > rules.maxSamples)
            return mixture ? $"Pigment adedi {rules.minSamples}–{rules.maxSamples} olmalı." : $"Numune adedi koşulu: {rules.minSamples}–{rules.maxSamples}.";
        if (dag.depth[dag.meter] > rules.maxDepth) return mixture ? "En fazla hat derinliği aşıldı." : "En fazla bağlantı derinliği aşıldı.";
        foreach (var part in dag.parts) if (!rules.Allows(part.id)) return (mixture ? "Yasak parça: " : "Bu görevde kullanılamaz: ") + part.title;
        if (rules.parts != null) foreach (var rule in rules.parts)
        {
            if (rule == null) continue;
            dag.quantities.TryGetValue(rule.itemID, out int quantity);
            if (quantity < rule.minimum || quantity > (rule.allowed ? rule.maximum : 0))
                return PuzzleParts.Find(rule.itemID)?.title + $": adet koşulu {rule.minimum}–{(rule.allowed ? rule.maximum : 0)}.";
        }
        return null;
    }

    public static SignalExperiment.Reading Measure(SynthesisGraphData graph, PuzzleDefinition puzzle)
    {
        if (puzzle?.kind == PuzzleKind.ColorMixture) return ColorMixtureSimulation.Measure(graph, puzzle);
        SignalExperiment.Reading Invalid(string text) => new SignalExperiment.Reading { directedTrial = true, problem = text };
        string problem = BuildDag(graph, PuzzleKind.Signal, out var dag);
        if (problem != null) return Invalid(problem);
        int count = dag.parts.Length;
        var signals = new List<SignalTrial.Pulse>[count];
        for (int i = 0; i < count; i++) signals[i] = new List<SignalTrial.Pulse>();
        signals[dag.source].Add(new SignalTrial.Pulse { time = .18f, height = 1, width = .045f });
        foreach (int i in dag.order)
        {
            var part = dag.parts[i]; var changed = new List<SignalTrial.Pulse>();
            foreach (var incomingPulse in signals[i])
                for (int r = 0; r < part.repeats; r++)
                {
                    var pulse = incomingPulse;
                    if (part.sample) pulse.sample = part.id;
                    if (part.width > 0) pulse.width = part.width;
                    pulse.time += part.delay + r * part.repeatInterval; pulse.height *= part.heightFactor;
                    changed.Add(pulse);
                    if (changed.Count > MaxPulses) return Invalid("Çok fazla iz oluştu. Çift tepki sayısını azalt.");
                }
            signals[i] = changed;
            foreach (int next in dag.outgoing[i])
            {
                signals[next].AddRange(changed);
                if (signals[next].Count > MaxPulses) return Invalid("Ölçüm iz bütçesi aşıldı.");
            }
        }
        var pulses = Normalize(signals[dag.meter]);
        if (pulses.Count == 0) return Invalid("Ölçere ulaşan iz yok. Açık kalan hatları gözlemle.");
        // Sinyalde "beslenen parça" iz taşıyan parçadır; tekrar sayısı sıfır olan bir parça iz üretmez.
        for (int i = 0; i < count; i++) dag.reached[i] = signals[i].Count > 0;
        string condition = CheckRules(dag, puzzle?.rules, PuzzleKind.Signal);
        bool matches = condition == null && puzzle?.target != null;
        var target = Normalize(puzzle?.target);
        matches &= pulses.Count == target.Count;
        if (matches) for (int i = 0; i < pulses.Count; i++)
            if (Mathf.Abs(pulses[i].time - target[i].time) > puzzle.tolerance || Mathf.Abs(pulses[i].height - target[i].height) > puzzle.tolerance
                || Mathf.Abs(pulses[i].width - target[i].width) > puzzle.tolerance) { matches = false; break; }
        return new SignalExperiment.Reading { directedTrial = true, pulses = pulses, targetMatch = matches,
            targetFeedback = condition, main = pulses[0].height, echo = pulses.Count > 1 ? pulses[1].height : 0 };
    }

    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
