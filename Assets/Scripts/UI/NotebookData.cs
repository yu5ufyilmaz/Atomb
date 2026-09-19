using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NotebookDatabase", menuName = "Senzora/Notebook/Database")]
public class NotebookData : ScriptableObject
{
    [Header("  Tutorials (Bağımsız SO'lar)")]
    public List<TutorialDataSO> tutorialPages = new List<TutorialDataSO>();

    [Header("  Symbols (Bağımsız SO'lar)")]
    public List<SymbolDataSO> symbolDataList = new List<SymbolDataSO>();

    // Index yerine ID'ye göre doğru sembolü bulma mantığı
    public string GetSymbolDescription(int id)
    {
        SymbolDataSO foundSymbol = symbolDataList.Find(s => s.symbolID == id);
        return foundSymbol != null
            ? foundSymbol.description
            : "Unknown signal detected. Calculations failed.";
    }
}
