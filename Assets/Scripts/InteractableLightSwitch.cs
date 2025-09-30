using UnityEngine;

public class InteractableLightSwitch : MonoBehaviour, IInteractable
{
    [Header("Light Settings")]
    [SerializeField] private Light targetLight; // Kontrol edilecek ışık
    [SerializeField] private bool isOn = true; // Başlangıç durumu
    
    [Header("Audio")]
    [SerializeField] private AudioClip switchSound;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Visual Feedback (Opsiyonel)")]
    [SerializeField] private Material onMaterial; // Düğme açık materiali
    [SerializeField] private Material offMaterial; // Düğme kapalı materiali
    [SerializeField] private MeshRenderer switchRenderer; // Düğmenin mesh renderer'ı
    
    private void Start()
    {
        // Başlangıç durumunu ayarla
        if (targetLight != null)
        {
            targetLight.enabled = isOn;
            UpdateVisuals();
        }
        else
        {
            Debug.LogError("Light Switch'e ışık atanmamış!", this);
        }
    }

    public void Interact()
    {
        ToggleLight();
    }

    public string GetInteractionPrompt()
    {
        return isOn ? "[E] Işığı Kapat" : "[E] Işığı Aç";
    }

    private void ToggleLight()
    {
        if (targetLight == null) return;
        
        isOn = !isOn;
        targetLight.enabled = isOn;
        
        PlaySwitchSound();
        UpdateVisuals();
    }

    private void PlaySwitchSound()
    {
        if (switchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(switchSound);
        }
    }

    private void UpdateVisuals()
    {
        if (switchRenderer != null && onMaterial != null && offMaterial != null)
        {
            switchRenderer.material = isOn ? onMaterial : offMaterial;
        }
    }
}