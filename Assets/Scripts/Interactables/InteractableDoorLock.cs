using UnityEngine;

public class InteractableDoorLock : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [SerializeField] private InteractableDoor targetDoor; // Kontrol edilecek kapı
    [SerializeField] private bool startsLocked = false; // Başlangıçta kilitli mi?
    
    [Header("Audio")]
    [SerializeField] private AudioClip lockSound;
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Visual Feedback (Opsiyonel)")]
    [SerializeField] private Material lockedMaterial; // Kilitli materiali
    [SerializeField] private Material unlockedMaterial; // Açık materiali
    [SerializeField] private MeshRenderer lockRenderer; // Kilit mesh renderer'ı
    [SerializeField] private GameObject lockIndicator; // Kilit göstergesi (örn: kırmızı/yeşil ışık)

    private void Start()
    {
        if (targetDoor == null)
        {
            // Eğer atanmamışsa, parent'ta veya aynı objede kapı ara
            targetDoor = GetComponentInParent<InteractableDoor>();
            
            if (targetDoor == null)
            {
                Debug.LogError("Door Lock'a kapı atanmamış!", this);
                return;
            }
        }
        
        // Başlangıç durumunu ayarla
        targetDoor.SetLocked(startsLocked);
        UpdateVisuals();
    }

    public void Interact()
    {
        ToggleLock();
    }

    public string GetInteractionPrompt()
    {
        if (targetDoor == null) return "";
        
        return targetDoor.IsLocked() ? "[E] Kilidi Aç" : "[E] Kilitle";
    }

    private void ToggleLock()
    {
        if (targetDoor == null) return;
        
        bool newLockState = !targetDoor.IsLocked();
        targetDoor.SetLocked(newLockState);
        
        PlayLockSound(newLockState);
        UpdateVisuals();
    }

    private void PlayLockSound(bool locked)
    {
        if (audioSource != null)
        {
            AudioClip soundToPlay = locked ? lockSound : unlockSound;
            if (soundToPlay != null)
            {
                audioSource.PlayOneShot(soundToPlay);
            }
        }
    }

    private void UpdateVisuals()
    {
        if (targetDoor == null) return;
        
        bool isLocked = targetDoor.IsLocked();
        
        // Materyal değiştir
        if (lockRenderer != null && lockedMaterial != null && unlockedMaterial != null)
        {
            lockRenderer.material = isLocked ? lockedMaterial : unlockedMaterial;
        }
        
        // Gösterge objesi (örn: ışık)
        if (lockIndicator != null)
        {
            // Kırmızı=kilitli, Yeşil=açık gibi bir gösterge için
            // lockIndicator.GetComponent<Light>().color = isLocked ? Color.red : Color.green;
        }
    }

    // Inspector'da kolayca test etmek için
    [ContextMenu("Toggle Lock")]
    private void TestToggle()
    {
        ToggleLock();
    }
}