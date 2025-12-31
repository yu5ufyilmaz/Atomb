using UnityEngine;

public class InteractableDoorLock : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [SerializeField]
    public InteractableDoor targetDoor;

    [SerializeField]
    private bool startsLocked = false;

    // NOT: Sesleri artık InteractableDoor.cs içindeki değişkenlerden alıyor.
    // Buraya ses eklemene gerek yok.

    [Header("Visual Feedback (Opsiyonel)")]
    [SerializeField]
    private Material lockedMaterial;

    [SerializeField]
    private Material unlockedMaterial;

    [SerializeField]
    private MeshRenderer lockRenderer;

    private void Start()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<InteractableDoor>();

        if (targetDoor != null)
        {
            // Başlangıçta kilit durumunu ayarla (Ses çalmadan)
            // SetLocked fonksiyonu ses çalar, o yüzden manuel yapıyoruz:
            // targetDoor.SetLocked(startsLocked); <- YERİNE
            // Manuel ayar yapıp görseli güncelliyoruz.

            // Not: InteractableDoor içindeki isLocked değişkenine doğrudan erişemiyorsak
            // mecburen SetLocked kullanacağız ama başlangıçta (Start) ses çalmasını istemeyiz.
            // Şimdilik InteractableDoor'daki isLocked'i public yapmadıysak SetLocked kullanıyoruz.
            // Ancak Start'ta ses çıkarsa rahatsız edici olabilir.

            // ÇÖZÜM: SetLocked'ı çağırıp sesi InteractableDoor'da Start'tan sonra çalacak şekilde ayarlayabiliriz
            // ya da basitçe oyun başlar başlamaz kilit sesi duymak sorun değilse böyle kalsın.
            targetDoor.SetLocked(startsLocked);
        }

        UpdateVisuals();
    }

    public void Interact()
    {
        ToggleLock();
    }

    public string GetInteractionPrompt()
    {
        if (targetDoor == null)
            return "";
        return targetDoor.IsLocked() ? "[E] Kilidi Aç" : "[E] Kilitle";
    }

    private void ToggleLock()
    {
        if (targetDoor == null)
            return;

        // Mevcut durumun tersini uygula
        bool newState = !targetDoor.IsLocked();

        // Bu fonksiyon artık InteractableDoor içindeki Lock/Unlock sesini de tetikler
        targetDoor.SetLocked(newState);

        UpdateVisuals();
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    private void UpdateVisuals()
    {
        if (targetDoor == null || lockRenderer == null)
            return;

        if (lockedMaterial != null && unlockedMaterial != null)
        {
            lockRenderer.material = targetDoor.IsLocked() ? lockedMaterial : unlockedMaterial;
        }
    }
}
