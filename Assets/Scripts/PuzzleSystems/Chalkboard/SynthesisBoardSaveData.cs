using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SynthesisBoardSaveData
{
    public List<SynthesisBoardPartSaveData> parts = new List<SynthesisBoardPartSaveData>();
    public List<SynthesisBoardBondSaveData> bonds = new List<SynthesisBoardBondSaveData>();
    public string pinnedQuestID;
}

[Serializable]
public class SynthesisBoardPartSaveData
{
    public string itemID;
    public Vector3 localPosition;
    public Quaternion localRotation;
    public bool synthesized;
    public List<FormulaNodeState> nodeStates = new List<FormulaNodeState>();
}

[Serializable]
public class SynthesisBoardBondSaveData
{
    public int fromPart;
    public int fromNode;
    public int toPart;
    public int toNode;
    public BondType bondType;
}
