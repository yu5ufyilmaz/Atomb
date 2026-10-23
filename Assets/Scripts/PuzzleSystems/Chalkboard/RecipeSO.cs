using System.Collections.Generic;
using UnityEngine;

public enum RecipeType
{
    Synthesis,
    Decomposition,
}

[CreateAssetMenu(fileName = "NewRecipe", menuName = "Senzora/Chalkboard/Recipe")]
public class RecipeSO : ScriptableObject
{
    public string recipeName;
    public RecipeType recipeType = RecipeType.Synthesis;

    [Tooltip("Sentez için gerekenler veya Parçalanacak ana obje")]
    public List<FormulaItemSO> inputs = new List<FormulaItemSO>();

    [Tooltip("Sentez sonucu çıkacak ürün veya Parçalanmadan düşecek temel parçalar")]
    public List<FormulaItemSO> outputs = new List<FormulaItemSO>();

    [Header("Grafik ve İpucu Kuralları")]
    [Tooltip("Atanırsa girdi listesine ek olarak bu şemanın topolojisi doğrulanır.")]
    public QuestBlueprintSO blueprint;
    [Tooltip("Eski tariflerde boş bırakılabilir. Kapasite ve bağlı grafik kontrolü her zaman yapılır.")]
    public List<SynthesisRuleData> rules = new List<SynthesisRuleData>();
}
