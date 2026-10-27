using System;
using System.Collections.Generic;
using UnityEngine;

public static class ColorMixtureSimulation
{
    public static string ValidateTarget(MixtureResult target)
    {
        if (target == null || !Finite(target.red) || !Finite(target.green) || !Finite(target.blue) || !Finite(target.solvent)
            || target.red < 0 || target.green < 0 || target.blue < 0 || target.solvent < 0 || target.Pigment <= 0 || target.Volume > 128)
            return "Hedef, pigment içeren ve miktarı 128 dozu aşmayan bir karışım olmalı.";
        return null;
    }

    public static SignalExperiment.Reading Measure(SynthesisGraphData graph, PuzzleDefinition puzzle)
    {
        SignalExperiment.Reading Invalid(string issue) => new SignalExperiment.Reading { problem = issue, kind = PuzzleKind.ColorMixture, directedTrial = true };
        if (graph?.parts == null || graph.bonds == null || graph.parts.Count < 2 || graph.parts.Count > PuzzleSimulation.MaxParts)
            return Invalid("Kaynak ile analizör arasında bir karışım düzeneği kur.");
        int count = graph.parts.Count, source = -1, meter = -1, samples = 0;
        var parts = new SignalPartLibrary.Part[count]; var incoming = new int[count]; var edges = new List<int>[count];
        var quantities = new Dictionary<string, int>();
        for (int i = 0; i < count; i++)
        {
            var part = ColorMixtureLibrary.Find(graph.parts[i]?.itemID);
            if (part == null) return Invalid("Bu parça renk ve karışım deneyinde kullanılamaz.");
            parts[i] = part; edges[i] = new List<int>();
            if (part.id == ColorMixtureLibrary.Source) { if (source >= 0) return Invalid("Tek taşıyıcı kaynağı kullan."); source = i; }
            if (part.id == ColorMixtureLibrary.Meter) { if (meter >= 0) return Invalid("Tek analizör kullan."); meter = i; }
            if (part.sample) samples++;
            quantities.TryGetValue(part.id, out int q); quantities[part.id] = q + 1;
        }
        if (source < 0 || meter < 0) return Invalid("Taşıyıcı kaynağı veya analizör eksik.");
        foreach (var edge in graph.bonds)
        {
            if (edge == null || !edge.directed || edge.bondType != BondType.Single || edge.fromIndex < 0 || edge.toIndex < 0
                || edge.fromIndex >= count || edge.toIndex >= count || edge.fromIndex == edge.toIndex || edges[edge.fromIndex].Contains(edge.toIndex))
                return Invalid("Çıkış okunu başka bir parçanın girişine bağla.");
            edges[edge.fromIndex].Add(edge.toIndex); incoming[edge.toIndex]++;
        }
        for (int i = 0; i < count; i++) if (incoming[i] > parts[i].inputs || edges[i].Count > parts[i].outputs)
            return Invalid("Giriş veya çıkış kapasitesi aşıldı.");
        var remaining = (int[])incoming.Clone(); var queue = new Queue<int>(); var order = new List<int>();
        for (int i = 0; i < count; i++) if (remaining[i] == 0) queue.Enqueue(i);
        while (queue.Count > 0) { int i = queue.Dequeue(); order.Add(i); foreach (int next in edges[i]) if (--remaining[next] == 0) queue.Enqueue(next); }
        if (order.Count != count) return Invalid("Sıvı hattında döngü var.");
        var values = new MixtureResult[count]; var reached = new bool[count]; var depth = new int[count];
        for (int i = 0; i < count; i++) values[i] = new MixtureResult();
        values[source].solvent = 1; reached[source] = true;
        foreach (int i in order)
        {
            if (!reached[i]) continue;
            var value = values[i]; string id = parts[i].id;
            if (id == ColorMixtureLibrary.Red) value.red += 1;
            if (id == ColorMixtureLibrary.Green) value.green += 1;
            if (id == ColorMixtureLibrary.Blue) value.blue += 1;
            if (id == ColorMixtureLibrary.Solvent) value.solvent += 1;
            if (id == ColorMixtureLibrary.Concentrator) value.solvent *= .5f;
            foreach (int next in edges[i])
            { values[next].Add(value, id == ColorMixtureLibrary.Splitter ? .5f : 1); reached[next] = true; depth[next] = Mathf.Max(depth[next], depth[i] + 1); }
        }
        if (!reached[meter]) return Invalid("Analizöre sıvı ulaşmıyor.");
        var reachesMeter = new bool[count]; reachesMeter[meter] = true;
        for (int j = order.Count - 1; j >= 0; j--) foreach (int next in edges[order[j]]) reachesMeter[order[j]] |= reachesMeter[next];
        string issue = null;
        for (int i = 0; i < count; i++)
            if (!reached[i] || !reachesMeter[i] || incoming[i] != parts[i].inputs || edges[i].Count != parts[i].outputs)
                issue = "Bütün parçalar çalışmalı. Dağıtıcının iki çıkışını ve karıştırıcının iki girişini de bağla.";
        var rules = puzzle.rules;
        if (issue == null && (count < rules.minParts || count > rules.maxParts)) issue = $"Parça adedi {rules.minParts}–{rules.maxParts} olmalı.";
        if (issue == null && (samples < rules.minSamples || samples > rules.maxSamples)) issue = $"Pigment adedi {rules.minSamples}–{rules.maxSamples} olmalı.";
        if (issue == null && depth[meter] > rules.maxDepth) issue = "En fazla hat derinliği aşıldı.";
        if (issue == null) foreach (var part in parts) if (!rules.Allows(part.id)) { issue = "Yasak parça: " + part.title; break; }
        if (issue == null) foreach (var rule in rules.parts)
        {
            quantities.TryGetValue(rule.itemID, out int quantity);
            if (quantity < rule.minimum || quantity > (rule.allowed ? rule.maximum : 0))
            { issue = PuzzleParts.Find(rule.itemID).title + $": adet koşulu {rule.minimum}–{(rule.allowed ? rule.maximum : 0)}."; break; }
        }
        var result = values[meter]; var target = puzzle.mixtureTarget;
        bool matches = issue == null && target != null && (result.Ratios - target.Ratios).sqrMagnitude <= puzzle.tolerance * puzzle.tolerance
            && Mathf.Abs(result.Concentration - target.Concentration) <= puzzle.tolerance && Mathf.Abs(result.Volume - target.Volume) <= puzzle.tolerance;
        return new SignalExperiment.Reading { kind = PuzzleKind.ColorMixture, directedTrial = true, mixture = result, targetMatch = matches, targetFeedback = issue };
    }
    private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
}
