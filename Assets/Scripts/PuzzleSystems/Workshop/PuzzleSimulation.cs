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

    public static SignalExperiment.Reading Measure(SynthesisGraphData graph, PuzzleDefinition puzzle)
    {
        if (puzzle?.kind == PuzzleKind.ColorMixture) return ColorMixtureSimulation.Measure(graph, puzzle);
        SignalExperiment.Reading Invalid(string text) => new SignalExperiment.Reading { directedTrial = true, problem = text };
        if (graph?.parts == null || graph.bonds == null || graph.parts.Count < 2 || graph.parts.Count > MaxParts)
            return Invalid("Kaynak ile ölçer arasında bir deney düzeni kur (en fazla 64 parça).");
        int count = graph.parts.Count, source = -1, meter = -1;
        var incoming = new int[count]; var outgoing = new List<int>[count]; var reverse = new List<int>[count];
        var parts = new SignalPartLibrary.Part[count]; var quantities = new Dictionary<string, int>();
        int sampleCount = 0;
        for (int i = 0; i < count; i++)
        {
            var part = SignalPartLibrary.Find(graph.parts[i]?.itemID);
            if (part == null) return Invalid("Ölçümde tanınmayan bir bileşen var.");
            parts[i] = part; outgoing[i] = new List<int>(); reverse[i] = new List<int>();
            if (part.id == SignalTrial.Source) { if (source >= 0) return Invalid("Tek atım kaynağı kullan."); source = i; }
            if (part.id == SignalTrial.Meter) { if (meter >= 0) return Invalid("Tek ölçer kullan."); meter = i; }
            quantities.TryGetValue(part.id, out int quantity); quantities[part.id] = quantity + 1;
            if (part.sample) sampleCount++;
        }
        if (source < 0 || meter < 0) return Invalid("Atım kaynağı veya ölçer eksik.");
        foreach (var edge in graph.bonds)
        {
            if (edge == null || !edge.directed || edge.bondType != BondType.Single || edge.fromIndex < 0 || edge.toIndex < 0
                || edge.fromIndex >= count || edge.toIndex >= count || edge.fromIndex == edge.toIndex || outgoing[edge.fromIndex].Contains(edge.toIndex))
                return Invalid("Hat okunamadı. Çıkış okunu bir giriş halkasına bağla.");
            outgoing[edge.fromIndex].Add(edge.toIndex); incoming[edge.toIndex]++; reverse[edge.toIndex].Add(edge.fromIndex);
        }
        for (int i = 0; i < count; i++)
            if (incoming[i] > parts[i].inputs || outgoing[i].Count > parts[i].outputs) return Invalid("Bir bileşenin giriş veya çıkış sayısı aşıldı.");

        // Kahn's ordering detects cycles even in an unused/disconnected component.
        var pending = (int[])incoming.Clone(); var queue = new Queue<int>(); var order = new List<int>();
        for (int i = 0; i < count; i++) if (pending[i] == 0) queue.Enqueue(i);
        while (queue.Count > 0)
        { int i = queue.Dequeue(); order.Add(i); foreach (int next in outgoing[i]) if (--pending[next] == 0) queue.Enqueue(next); }
        if (order.Count != count) return Invalid("Hat kendi üzerine dönüyor; döngüyü kaldır.");
        var signals = new List<SignalTrial.Pulse>[count]; var depth = new int[count];
        for (int i = 0; i < count; i++) signals[i] = new List<SignalTrial.Pulse>();
        signals[source].Add(new SignalTrial.Pulse { time = .18f, height = 1, width = .045f });
        foreach (int i in order)
        {
            var part = parts[i]; var changed = new List<SignalTrial.Pulse>();
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
            foreach (int next in outgoing[i])
            {
                signals[next].AddRange(changed); depth[next] = Mathf.Max(depth[next], depth[i] + 1);
                if (signals[next].Count > MaxPulses) return Invalid("Ölçüm iz bütçesi aşıldı.");
            }
        }
        var pulses = Normalize(signals[meter]);
        if (pulses.Count == 0) return Invalid("Ölçere ulaşan iz yok. Açık kalan hatları gözlemle.");
        var reachesMeter = new bool[count]; reachesMeter[meter] = true;
        for (int j = order.Count - 1; j >= 0; j--) foreach (int next in outgoing[order[j]]) reachesMeter[order[j]] |= reachesMeter[next];
        string condition = null;
        for (int i = 0; i < count; i++)
            if (signals[i].Count == 0 || !reachesMeter[i] || (i != meter && outgoing[i].Count != parts[i].outputs))
                condition = "Bütün parçalar kaynaktan beslenip ölçere ulaşmalı; açık veya kullanılmayan uçları kaldır.";
        var rules = puzzle?.rules;
        if (condition == null && rules != null)
        {
            if (count < rules.minParts || count > rules.maxParts) condition = $"Toplam parça koşulu: {rules.minParts}–{rules.maxParts}.";
            else if (sampleCount < rules.minSamples || sampleCount > rules.maxSamples) condition = $"Numune adedi koşulu: {rules.minSamples}–{rules.maxSamples}.";
            else if (depth[meter] > rules.maxDepth) condition = "En fazla bağlantı derinliği aşıldı.";
            else foreach (var part in parts) if (!rules.Allows(part.id)) { condition = "Bu görevde kullanılamaz: " + part.title; break; }
            if (condition == null && rules.parts != null) foreach (var rule in rules.parts)
            {
                if (rule == null) continue;
                quantities.TryGetValue(rule.itemID, out int quantity);
                if (quantity < rule.minimum || quantity > (rule.allowed ? rule.maximum : 0))
                { condition = SignalPartLibrary.Find(rule.itemID)?.title + $": adet koşulu {rule.minimum}–{(rule.allowed ? rule.maximum : 0)}."; break; }
            }
        }
        bool matches = condition == null && puzzle?.target != null;
        var target = Normalize(puzzle?.target);
        matches &= pulses.Count == target.Count;
        if (matches) for (int i = 0; i < pulses.Count; i++)
            if (Mathf.Abs(pulses[i].time - target[i].time) > puzzle.tolerance || Mathf.Abs(pulses[i].height - target[i].height) > puzzle.tolerance
                || Mathf.Abs(pulses[i].width - target[i].width) > puzzle.tolerance) { matches = false; break; }
        return new SignalExperiment.Reading { directedTrial = true, pulses = pulses, targetMatch = matches,
            targetFeedback = condition, main = pulses[0].height, echo = pulses.Count > 1 ? pulses[1].height : 0 };
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
