using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Blockout sahnesinin objeleri artık Edit modunda gerçek GameObject'ler olarak sahnede durur
// (Senzora > Sentez Blockout > Sahneyi Düzenlenebilir Objelerle Oluştur menüsüyle üretilir).
// Bu script yalnızca Play'e özgü işleri yapar: test GameManager'ı, kitap parlaması ve oyun modunu başlatma.
// Awake/Start, sahnedeki diğer scriptlerden önce çalışsın diye düşük sıra kullanılır.
[DefaultExecutionOrder(-1000)]
public class SynthesisBlockoutScene : MonoBehaviour
{
    [Header("Blockout İçeriği")]
    public NotebookData notebookData;
    [Tooltip("Test masasında kullanılan gerçek kitap prefabı (yalnızca sahneyi oluşturan menü komutu kullanır).")]
    public InteractableBook bookPrefab;
    [Header("Sahne Referansları (menü komutu otomatik atar)")]
    [Tooltip("Sahnedeki test kitabı. Kitabı sildiyseniz veya değiştirdiyseniz buraya yeniden atayın.")]
    public InteractableBook book;

    private GameManager game;

    private void Awake()
    {
        if (GameManager.Instance != null)
        {
            Debug.LogError("Blockout sahnesini tek başına açın. Başka bir sahnenin GameManager'ı zaten çalışıyor.", this);
            return;
        }
        game = new GameObject("Test GameManager").AddComponent<GameManager>();
    }

    private IEnumerator Start()
    {
        if (game == null) yield break;
        if (book == null)
        {
            Debug.LogError("Blockout sahnesinde Book alanı boş. Menüden 'Sahneyi Düzenlenebilir Objelerle Oluştur' komutunu çalıştırın.", this);
            yield break;
        }

        // Bu kısım diğer scriptlerin Start'ından önce çalışır (kitap kendi Start'ında materyalleri kopyalar).
        GetComponent<ResearchPuzzleTrial>()?.Configure();
        book.ClearPassword();
        foreach (var renderer in book.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.materials)
            {
                if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                if (material.HasProperty("_EmissiveIntensity")) material.SetFloat("_EmissiveIntensity", 0);
                material.DisableKeyword("_EMISSION");
            }

        yield return null; // Etkileşim scriptlerinin Start referansları ve GameManager imleci hazır olsun.
        game.StartGameMode();
    }

}
