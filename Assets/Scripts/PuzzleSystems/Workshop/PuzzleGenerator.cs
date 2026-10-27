using System;
using System.Collections.Generic;
using UnityEngine;

public static class PuzzleGenerator
{
    public const int Attempts = 512;

    public static bool TryGenerate(PuzzleDefinition settings, int seed, out PuzzleDefinition result, out string error)
    {
        result = null;
        error = PuzzleSimulation.ValidateRules(settings?.rules, settings?.kind ?? PuzzleKind.Signal);
        if (error != null) return false;
        string sourceID = PuzzleParts.Source(settings.kind), meterID = PuzzleParts.Meter(settings.kind), splitID = PuzzleParts.Splitter(settings.kind);
        bool mixture = settings.kind == PuzzleKind.ColorMixture;
        var random = new System.Random(seed);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var puzzle = settings.Clone();
            puzzle.seed = seed; puzzle.solution = new SynthesisGraphData(); puzzle.positions = new List<Vector2>();
            int total = random.Next(puzzle.rules.minParts, puzzle.rules.maxParts + 1);
            var splitRule = puzzle.rules.Find(splitID);
            bool split = splitRule != null && splitRule.allowed && splitRule.maximum >= 1 && total >= (mixture ? 6 : 5)
                && (splitRule.minimum > 0 || random.Next(2) == 0);
            if (mixture && !puzzle.rules.Allows(ColorMixtureLibrary.Mixer)) split = false;
            if (splitRule != null && splitRule.minimum > (split ? 1 : 0)) continue;
            if (mixture && puzzle.rules.Find(ColorMixtureLibrary.Mixer)?.minimum > (split ? 1 : 0)) continue;
            int middleCount = total - 2 - (split ? (mixture ? 2 : 1) : 0);
            var bag = new List<string>();
            foreach (var rule in puzzle.rules.parts)
                if (rule.allowed && rule.itemID != sourceID && rule.itemID != meterID && rule.itemID != splitID && rule.itemID != ColorMixtureLibrary.Mixer)
                    for (int i = 0; i < rule.minimum; i++) bag.Add(rule.itemID);
            if (bag.Count > middleCount) continue;
            int desiredSamples = random.Next(puzzle.rules.minSamples, puzzle.rules.maxSamples + 1);
            while (bag.Count < middleCount)
            {
                int samples = bag.FindAll(id => PuzzleParts.Find(id).sample).Count;
                var choices = new List<string>();
                foreach (var part in PuzzleParts.ForKind(settings.kind))
                {
                    var rule = puzzle.rules.Find(part.id);
                    if (part.inputs == 0 || part.outputs == 0 || part.id == splitID || part.id == ColorMixtureLibrary.Mixer || rule == null || !rule.allowed
                        || bag.FindAll(id => id == part.id).Count >= rule.maximum) continue;
                    if (part.sample && samples >= desiredSamples) continue;
                    if (!part.sample && middleCount - bag.Count <= desiredSamples - samples) continue;
                    choices.Add(part.id);
                }
                if (choices.Count == 0) break;
                bag.Add(choices[random.Next(choices.Count)]);
            }
            if (bag.Count != middleCount) continue;
            for (int i = bag.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }
            int Add(string id, float x, float y)
            {
                int index = puzzle.solution.parts.Count;
                puzzle.solution.parts.Add(new SynthesisPartData { itemID = id, capacity = SignalTrial.Inputs(id) + SignalTrial.Outputs(id) });
                puzzle.positions.Add(new Vector2(x, y)); return index;
            }
            void Edge(int a, int b) => puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = a, toIndex = b, directed = true });
            int source = Add(sourceID, 35, 140), last = source;
            if (!split)
            {
                foreach (string id in bag) { int next = Add(id, 35 + puzzle.solution.parts.Count * 185, 140); Edge(last, next); last = next; }
                int meter = Add(meterID, 35 + puzzle.solution.parts.Count * 185, 140); Edge(last, meter);
            }
            else
            {
                int prefix = random.Next(bag.Count - 1);
                for (int i = 0; i < prefix; i++) { int next = Add(bag[i], 35 + (i + 1) * 185, 140); Edge(last, next); last = next; }
                int fork = Add(splitID, 35 + (prefix + 1) * 185, 140); Edge(last, fork);
                int leftCount = random.Next(1, bag.Count - prefix), cursor = prefix;
                var ends = new int[2];
                for (int branch = 0; branch < 2; branch++)
                {
                    last = fork; int length = branch == 0 ? leftCount : bag.Count - cursor;
                    for (int j = 0; j < length; j++)
                    {
                        int next = Add(bag[cursor++], 35 + (prefix + 2 + j) * 185, branch == 0 ? 35 : 245);
                        Edge(last, next); last = next;
                    }
                    ends[branch] = last;
                }
                float x = 35 + (prefix + 2 + Mathf.Max(leftCount, bag.Count - prefix - leftCount)) * 185;
                if (mixture)
                {
                    int mixer = Add(ColorMixtureLibrary.Mixer, x, 140); Edge(ends[0], mixer); Edge(ends[1], mixer);
                    int meter = Add(meterID, x + 185, 140); Edge(mixer, meter);
                }
                else { int meter = Add(meterID, x, 140); Edge(ends[0], meter); Edge(ends[1], meter); }
            }
            var reading = PuzzleSimulation.Measure(puzzle.solution, puzzle);
            if (!reading.Measurable || !string.IsNullOrEmpty(reading.targetFeedback)) continue;
            if (mixture) puzzle.mixtureTarget = reading.mixture;
            else puzzle.target = reading.pulses;
            if (PuzzleSimulation.Validate(puzzle) != null) continue;
            result = puzzle; error = null; return true;
        }
        error = "Bu ayarlarla 512 denemede okunabilir bir hedef bulunamadı. Parça/numune sınırlarını genişlet veya yasakları azalt. Son taslak korundu.";
        return false;
    }
}
