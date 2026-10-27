using System;
using System.Collections.Generic;
using UnityEngine;

public enum BondType { Single = 1, Double = 2 }
public enum FormulaNodeState { Open, Blocked, Unstable }

[Serializable]
public class SynthesisPartData
{
    public string itemID;
    public int capacity;
    public int openEnds;
    public int blockedEnds;
}

[Serializable]
public class SynthesisBondData
{
    public int fromIndex;
    public int toIndex;
    public BondType bondType = BondType.Single;
    public string label;
    public bool directed;
}

[Serializable]
public class SynthesisGraphData
{
    public List<SynthesisPartData> parts = new List<SynthesisPartData>();
    public List<SynthesisBondData> bonds = new List<SynthesisBondData>();
}

[Serializable]
public class QuestNodeData
{
    public string itemID;
    public bool isUnknown;
    public Vector2 position;
}

[Serializable]
public class SynthesisQuestData
{
    public PuzzleKind puzzleKind;
    public string questID;
    public string title;
    public string outputItemID;
    public List<QuestNodeData> nodes = new List<QuestNodeData>();
    public List<SynthesisBondData> bonds = new List<SynthesisBondData>();
    public bool completed;
    public List<SignalTrial.Pulse> targetPulses = new List<SignalTrial.Pulse>();
    public MixtureResult mixtureTarget;
}

