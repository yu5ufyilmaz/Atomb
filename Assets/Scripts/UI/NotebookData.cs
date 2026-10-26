using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NotebookDatabase", menuName = "Senzora/Notebook/Database")]
public class NotebookData : ScriptableObject
{
    [Header("Çalışma Alanı Görünümü")]
    [Tooltip("Boş bırakılırsa kağıt blokları çalışma anında oluşturulur.")]
    public NotebookDraggableBlock workspaceBlockPrefab;

    [Tooltip("Kitaplarda ve kayıt geri yüklemesinde kullanılan kurgusal sentez modülleri.")]
    public List<FormulaItemSO> synthesisCatalog = new List<FormulaItemSO>();
    [Tooltip("Hile sayfasında gösterilen tarif arşivi; bu liste tahtada yeni tarif açmaz.")]
    public List<RecipeSO> synthesisRecipes = new List<RecipeSO>();

    [Tooltip("Eskizlerin kağıda bırakıldığındaki en fazla açı sapması.")]
    [Range(0f, 15f)] public float sketchRotation = 5f;

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
            : "...";
    }
}
