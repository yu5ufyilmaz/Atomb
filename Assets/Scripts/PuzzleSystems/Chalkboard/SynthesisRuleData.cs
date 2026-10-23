using System;
using System.Collections.Generic;
using UnityEngine;

public enum BondType { Single = 1, Double = 2 }
public enum FormulaNodeState { Open, Blocked, Unstable }

public enum SynthesisRuleType
{
    MinDoubleBonds,
    MaxSingleBonds,
    ForbidDirectBond,
    RequireBridge,
    MinPartCount,
    MaxPartCount,
    ExactPartCount,
    ForbiddenPart,
    RequireNoBlockedEnds,
    MaxOpenEnds
}

[Serializable]
public class SynthesisRuleData
{
    public SynthesisRuleType ruleType;
    public string itemID;
    public string otherItemID;
    public string bridgeItemID;
    [Min(0)] public int amount;

    public string GetDescription()
    {
        switch (ruleType)
        {
            case SynthesisRuleType.MinDoubleBonds: return "En az " + amount + " çift bağ bulunmalı.";
            case SynthesisRuleType.MaxSingleBonds: return "Tek bağ sayısı " + amount + " değerini geçemez.";
            case SynthesisRuleType.ForbidDirectBond: return "[" + Label(itemID) + "] ile [" + Label(otherItemID) + "] doğrudan bağlanamaz.";
            case SynthesisRuleType.RequireBridge: return "[" + Label(itemID) + "] ile [" + Label(otherItemID) + "] arasında [" + Label(bridgeItemID) + "] köprüsü bulunmalı; doğrudan bağ yasaktır.";
            case SynthesisRuleType.MinPartCount: return "En az " + amount + " adet [" + Label(itemID) + "] bulunmalı.";
            case SynthesisRuleType.MaxPartCount: return "En fazla " + amount + " adet [" + Label(itemID) + "] bulunabilir.";
            case SynthesisRuleType.ExactPartCount: return "Tam " + amount + " adet [" + Label(itemID) + "] bulunmalı.";
            case SynthesisRuleType.ForbiddenPart: return "[" + Label(itemID) + "] bu yapıda kullanılamaz.";
            case SynthesisRuleType.RequireNoBlockedEnds: return "Tüm (x) kapalı/kararsız uçları silgiyle temizleyin.";
            case SynthesisRuleType.MaxOpenEnds: return "En fazla " + amount + " bağlantısız açık uç kalabilir.";
            default: return "Geçersiz sentez kuralı.";
        }
    }

    private static string Label(string id)
    {
        var generator = ProceduralSynthesisGenerator.Instance;
        var item = generator != null ? generator.ResolveFormula(id) : null;
        return item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName : id;
    }
}

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
    public string questID;
    public string title;
    public string outputItemID;
    public List<QuestNodeData> nodes = new List<QuestNodeData>();
    public List<SynthesisBondData> bonds = new List<SynthesisBondData>();
    public List<SynthesisRuleData> rules = new List<SynthesisRuleData>();
    public bool completed;
}

[Serializable]
public class SynthesisRunData
{
    public int seed;
    public int currentStage;
    public List<SynthesisQuestData> quests = new List<SynthesisQuestData>();
}
