using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SynthesisBoardSaveData
{
    public List<SynthesisBoardPartSaveData> parts = new List<SynthesisBoardPartSaveData>();
    public List<SynthesisBoardBondSaveData> bonds = new List<SynthesisBoardBondSaveData>();
    public List<NotebookStrokeData> strokes = new List<NotebookStrokeData>();
    public string pinnedQuestID;
    public List<NotebookBlockData> information = new List<NotebookBlockData>();
    public List<SynthesisBoardDocumentSaveData> documents = new List<SynthesisBoardDocumentSaveData>();
    public bool documentShadeOpen;
}

[Serializable]
public class SynthesisBoardDocumentSaveData
{
    public string id;
    public Vector2 position;
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
