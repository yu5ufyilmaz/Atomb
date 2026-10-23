using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewQuestBlueprint", menuName = "Senzora/Chalkboard/Quest Blueprint")]
public class QuestBlueprintSO : ScriptableObject
{
    [Header("Hedef Şema")]
    public string questID;
    public string title;
    public FormulaItemSO output;
    [Tooltip("Bilinmeyen düğümlerin gerçek kimliği oyuncuya gösterilmez. İpucu kuralları bu yuvaları çözdürür.")]
    public List<QuestNodeData> nodes = new List<QuestNodeData>();
    public List<SynthesisBondData> bonds = new List<SynthesisBondData>();
    public List<SynthesisRuleData> rules = new List<SynthesisRuleData>();

    [Header("Toplanabilir Başlangıç Notları")]
    public List<FormulaItemSO> clueParts = new List<FormulaItemSO>();
    [TextArea(2, 5)] public List<string> clueNotes = new List<string>();

    public SynthesisQuestData CreateQuest()
    {
        var data = new SynthesisQuestData
        {
            questID = string.IsNullOrEmpty(questID) ? name : questID,
            title = title,
            outputItemID = output != null ? output.itemID : "",
            nodes = nodes,
            bonds = bonds,
            rules = rules
        };
        return JsonUtility.FromJson<SynthesisQuestData>(JsonUtility.ToJson(data));
    }
}
