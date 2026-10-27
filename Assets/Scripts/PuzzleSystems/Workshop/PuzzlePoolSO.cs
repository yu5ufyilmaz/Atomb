using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PuzzlePool", menuName = "Senzora/Puzzle Atölyesi/Puzzle Havuzu")]
public sealed class PuzzlePoolSO : ScriptableObject
{
    public List<PuzzleDefinitionSO> puzzles = new List<PuzzleDefinitionSO>();

    public List<PuzzleDefinitionSO> Playable(out string error)
    {
        error = null; var result = new List<PuzzleDefinitionSO>(); var ids = new HashSet<string>();
        if (puzzles != null) foreach (var asset in puzzles)
        {
            if (asset == null || !asset.includeInPool) continue;
            string issue = PuzzleSimulation.Validate(asset.definition);
            if (issue != null) { error = asset.name + ": " + issue; return result; }
            if (!ids.Add(asset.definition.id)) { error = "Havuzda yinelenen puzzle kimliği: " + asset.definition.id; return result; }
            result.Add(asset);
        }
        if (result.Count == 0) error = "Havuzda etkin ve doğrulanmış puzzle yok.";
        return result;
    }

    public PuzzleDefinition Choose(int seed)
    {
        var candidates = Playable(out string error);
        if (error != null) throw new InvalidOperationException(error);
        return candidates[new System.Random(seed).Next(candidates.Count)].definition.Clone();
    }
}
