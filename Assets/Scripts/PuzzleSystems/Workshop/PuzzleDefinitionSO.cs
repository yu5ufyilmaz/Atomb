using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum PuzzleKind { Signal, ColorMixture }

[Serializable]
public sealed class PuzzlePartRule
{
    public string itemID;
    public bool allowed = true;
    public int minimum;
    public int maximum = 12;
    [TextArea(2, 4)] public string explanation;
}

[Serializable]
public sealed class PuzzleRules
{
    public int minParts = 3, maxParts = 10;
    public int minSamples = 1, maxSamples = 2;
    public int maxDepth = 10;
    public int traceCount = 2;
    public List<PuzzlePartRule> parts = new List<PuzzlePartRule>();

    public PuzzlePartRule Find(string id) => parts?.Find(p => p != null && p.itemID == id);
    public bool Allows(string id) => Find(id) is PuzzlePartRule rule && rule.allowed && rule.maximum > 0;

    public static PuzzleRules Defaults()
        => ForKind(PuzzleKind.Signal);

    public static PuzzleRules ForKind(PuzzleKind kind)
    {
        var rules = new PuzzleRules();
        if (kind == PuzzleKind.ColorMixture) { rules.maxSamples = 3; rules.maxParts = 9; }
        foreach (var part in PuzzleParts.ForKind(kind))
            rules.parts.Add(new PuzzlePartRule { itemID = part.id, maximum = part.inputs == 0 || part.outputs == 0 ? 1 : 12 });
        return rules;
    }
}

[Serializable]
public sealed class PuzzleDefinition
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public PuzzleKind kind;
    public string id = Guid.NewGuid().ToString("N");
    public string title = "Yeni Puzzle";
    [TextArea(2, 4)] public string description = "Kitaplardaki bilgileri kullanarak hedef kaydı üret.";
    public PuzzleRules rules = PuzzleRules.Defaults();
    public SynthesisGraphData solution = new SynthesisGraphData();
    public List<Vector2> positions = new List<Vector2>();
    public List<SignalTrial.Pulse> target = new List<SignalTrial.Pulse>();
    public MixtureResult mixtureTarget;
    public float tolerance = .005f;
    public List<BookWorkspacePage> notes = new List<BookWorkspacePage>();
    public bool targetInFirstBook = true;
    public int seed;

    public string TargetVisual => kind == PuzzleKind.ColorMixture ? mixtureTarget?.Encode(true)
        : SignalTrial.Encode(target).Replace("signal-result", "signal-target");

    public PuzzleDefinition Clone() => JsonUtility.FromJson<PuzzleDefinition>(JsonUtility.ToJson(this));
    public string Fingerprint()
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(this)))).Replace("-", "");
    }

    public static PuzzleDefinition Legacy()
    {
        var puzzle = new PuzzleDefinition { id = SignalTrial.QuestID, title = "Çift Kayıt",
            description = "Bu deneyde tek numune kullan. Kısa tepkisini iki farklı anda kaydet; izler hedefle örtüşmeli." };
        puzzle.rules.minSamples = puzzle.rules.maxSamples = 1;
        puzzle.rules.maxParts = 32; puzzle.rules.maxDepth = 32;
        foreach (var rule in puzzle.rules.parts)
        {
            rule.allowed = rule.itemID != "pulse-n08" && rule.itemID != "pulse-n21" && rule.itemID != "pulse-n46";
            if (rule.itemID != SignalTrial.Source && rule.itemID != SignalTrial.Meter) rule.maximum = 32;
        }
        foreach (string id in new[] { SignalTrial.Source, SignalTrial.Narrow, SignalTrial.Splitter,
            SignalTrial.Attenuator, SignalTrial.Delay, SignalTrial.Meter })
            puzzle.solution.parts.Add(new SynthesisPartData { itemID = id });
        void Edge(int a, int b) => puzzle.solution.bonds.Add(new SynthesisBondData { fromIndex = a, toIndex = b, directed = true });
        Edge(0, 1); Edge(1, 2); Edge(2, 3); Edge(3, 5); Edge(2, 4); Edge(4, 5);
        puzzle.target.Add(new SignalTrial.Pulse { time = .18f, height = .5f, width = .025f });
        puzzle.target.Add(new SignalTrial.Pulse { time = .50f, height = 1, width = .025f });
        puzzle.positions = new List<Vector2> { new Vector2(35, 120), new Vector2(220, 120), new Vector2(405, 120),
            new Vector2(590, 35), new Vector2(590, 235), new Vector2(790, 120) };
        return puzzle;
    }
}

[CreateAssetMenu(fileName = "Puzzle", menuName = "Senzora/Puzzle Atölyesi/Puzzle")]
public sealed class PuzzleDefinitionSO : ScriptableObject
{
    public PuzzleDefinition definition = new PuzzleDefinition();
    public bool includeInPool = true;
}
