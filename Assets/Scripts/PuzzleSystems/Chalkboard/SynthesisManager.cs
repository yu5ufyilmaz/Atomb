using System.Collections.Generic;
using System.Linq; // Listeleri kolay karşılaştırmak için gerekli
using UnityEngine;

public class SynthesisManager : MonoBehaviour
{
    [Tooltip("Sahnede oluşturduğun IsTrigger'lı Sentez Çemberi")]
    public ReactionZone synthesisZone;

    [Tooltip("Oyundaki tüm RecipeSO tariflerini buraya sürükle")]
    public List<RecipeSO> allRecipes;

    // Bu metodu sahnede oluşturacağın bir UI Button'un OnClick eventine bağlayacaksın
    public void AttemptSynthesis()
    {
        if (synthesisZone.currentItems.Count == 0)
        {
            Debug.Log("Çember boş, sentez yapılacak bir şey yok.");
            return;
        }

        // 1. Bölgedeki parçaların ID'lerini bir listeye topla ve A'dan Z'ye sırala
        List<string> currentIDs = new List<string>();
        foreach (var item in synthesisZone.currentItems)
        {
            currentIDs.Add(item.formulaData.itemID);
        }
        currentIDs.Sort();

        bool recipeFound = false;

        // 2. Bütün tarifleri dön ve eşleşen var mı bak
        foreach (var recipe in allRecipes)
        {
            if (recipe.recipeType != RecipeType.Synthesis)
                continue;

            // Tarifin gereksinimlerini topla ve sırala
            List<string> recipeIDs = new List<string>();
            foreach (var input in recipe.inputs)
            {
                recipeIDs.Add(input.itemID);
            }
            recipeIDs.Sort();

            // 3. İçerideki parçalar ile tarif BİREBİR aynı mı? (SequenceEqual sıralı listeleri kıyaslar)
            if (currentIDs.SequenceEqual(recipeIDs))
            {
                Debug.Log($"Sentez Başarılı! Üretilen: {recipe.outputs[0].itemName}");
                recipeFound = true;

                // Eski parçaları sahneden sil
                foreach (var item in synthesisZone.currentItems)
                {
                    Destroy(item.gameObject);
                }
                synthesisZone.currentItems.Clear();

                // Yeni ürünü (Output) çemberin tam ortasına yarat
                foreach (var output in recipe.outputs)
                {
                    if (output.chalkPrefab != null)
                    {
                        Instantiate(
                            output.chalkPrefab,
                            synthesisZone.transform.position,
                            Quaternion.identity
                        );
                    }
                }

                break; // Eşleşme bulundu, diğer tariflere bakmaya gerek yok
            }
        }

        if (!recipeFound)
        {
            Debug.LogWarning("Hata: Bu parçalar birbiriyle uyumsuz. Bir şey üretilemedi!");
        }
    }
}
