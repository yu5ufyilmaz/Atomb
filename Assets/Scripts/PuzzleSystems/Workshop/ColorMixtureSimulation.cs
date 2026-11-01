using System.Collections.Generic;
using UnityEngine;

public static class ColorMixtureSimulation
{
    public static string ValidateTarget(MixtureResult target)
    {
        if (target == null || !PuzzleSimulation.Finite(target.red) || !PuzzleSimulation.Finite(target.green) || !PuzzleSimulation.Finite(target.blue)
            || !PuzzleSimulation.Finite(target.solvent) || target.red < 0 || target.green < 0 || target.blue < 0 || target.solvent < 0
            || target.Pigment <= 0 || target.Volume > 128)
            return "Hedef, pigment içeren ve miktarı 128 dozu aşmayan bir karışım olmalı.";
        return null;
    }

    // Hat çözümleme ve deney koşulları PuzzleSimulation ile ortaktır; burada yalnız sıvı akışı hesaplanır.
    public static SignalExperiment.Reading Measure(SynthesisGraphData graph, PuzzleDefinition puzzle)
    {
        SignalExperiment.Reading Invalid(string issue) => new SignalExperiment.Reading { problem = issue, kind = PuzzleKind.ColorMixture, directedTrial = true };
        string problem = PuzzleSimulation.BuildDag(graph, PuzzleKind.ColorMixture, out var dag);
        if (problem != null) return Invalid(problem);
        int count = dag.parts.Length;
        var values = new MixtureResult[count];
        for (int i = 0; i < count; i++) values[i] = new MixtureResult();
        values[dag.source].solvent = 1;
        foreach (int i in dag.order)
        {
            if (!dag.reached[i]) continue;
            var value = values[i]; string id = dag.parts[i].id;
            if (id == ColorMixtureLibrary.Red) value.red += 1;
            if (id == ColorMixtureLibrary.Green) value.green += 1;
            if (id == ColorMixtureLibrary.Blue) value.blue += 1;
            if (id == ColorMixtureLibrary.Solvent) value.solvent += 1;
            if (id == ColorMixtureLibrary.Concentrator) value.solvent *= .5f;
            foreach (int next in dag.outgoing[i]) values[next].Add(value, id == ColorMixtureLibrary.Splitter ? .5f : 1);
        }
        if (!dag.reached[dag.meter]) return Invalid("Analizöre sıvı ulaşmıyor.");
        string issue = PuzzleSimulation.CheckRules(dag, puzzle?.rules, PuzzleKind.ColorMixture);
        var result = values[dag.meter]; var target = puzzle?.mixtureTarget;
        bool matches = issue == null && target != null && (result.Ratios - target.Ratios).sqrMagnitude <= puzzle.tolerance * puzzle.tolerance
            && Mathf.Abs(result.Concentration - target.Concentration) <= puzzle.tolerance && Mathf.Abs(result.Volume - target.Volume) <= puzzle.tolerance;
        return new SignalExperiment.Reading { kind = PuzzleKind.ColorMixture, directedTrial = true, mixture = result, targetMatch = matches, targetFeedback = issue };
    }
}
