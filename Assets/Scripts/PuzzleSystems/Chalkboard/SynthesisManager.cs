using System.Collections.Generic;
using UnityEngine;

// Eski sahne/UI Button bağlantıları da aynı grafik ve görev doğrulamasını kullanır.
public class SynthesisManager : MonoBehaviour
{
    [Tooltip("Sahnede oluşturduğun IsTrigger'lı sentez çemberi")]
    public ReactionZone synthesisZone;
    [Tooltip("Oyundaki tüm RecipeSO tarifleri")]
    public List<RecipeSO> allRecipes = new List<RecipeSO>();

    public void AttemptSynthesis()
    {
        ChalkboardManager manager = ChalkboardManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("Sentez için sahnede ChalkboardManager bulunmalı.");
            return;
        }
        manager.AttemptSynthesis(synthesisZone != null ? synthesisZone : manager.synthesisZone,
            allRecipes != null && allRecipes.Count > 0 ? allRecipes : manager.allRecipes);
    }
}
