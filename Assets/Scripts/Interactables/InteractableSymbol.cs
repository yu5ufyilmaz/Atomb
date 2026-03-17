using UnityEngine;

// Senin yazdığın IInteractable arayüzünü (interface) kullanıyoruz
public class InteractableSymbol : MonoBehaviour, IInteractable
{
    [Header("Sembol Kimliği")]
    [Tooltip("Bu sembol hangi ID'ye sahip? (0, 1, 2, 3)")]
    public int symbolID;

    [Header("Etkileşim Ayarları")]
    public string promptText = "Sembolü ve İpucunu Al";
    public AudioClip pickupSound;

    [Header("Sembol Hikayesi / İpucu")]
    [TextArea(3, 10)]
    public string symbolLore =
        "Bu sembol kadim bir ritüeli temsil ediyor...\nŞifresi şu kitapta olabilir...";

    // Eğer HDRP Outline kullanıyorsan buraya public HDRPOutlineController outlineController; ekleyebilirsin.

    public void Interact()
    {
        if (PuzzleInventoryManager.Instance != null)
        {
            // 1. Sembolü Envantere Ekle (Zaten yazmıştık)
            PuzzleInventoryManager.Instance.PickupSymbol(symbolID);

            // 2. Ses Çal (Opsiyonel)
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            // Yeni Yazdığımız Sayfa Ekleme Sistemi!
            if (NotebookUI.Instance != null)
            {
                NotebookUI.Instance.AddLorePage(symbolLore);
            }

            Debug.Log($"[Oyun Dünyası] Oyuncu {symbolID} ID'li sembolü dünyadan topladı!");

            // 3. Obseyi dünyadan yok et (Çünkü artık cebimizde)
            Destroy(gameObject);
        }
    }

    public string GetInteractionPrompt()
    {
        return promptText;
    }

    public void OnFocus()
    {
        // Oyuncu objeye bakarken çalışır (Parlama / Outline açma)
        // outlineController.EnableOutline();
    }

    public void OnLoseFocus()
    {
        // Oyuncu objeye bakmayı bırakınca çalışır (Parlama / Outline kapatma)
        // outlineController.DisableOutline();
    }
}
