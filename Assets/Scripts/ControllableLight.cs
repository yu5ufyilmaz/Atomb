using UnityEngine;

public class ControllableLight : MonoBehaviour, IInteractable
{
    [Header("Light Settings")]
    [SerializeField] private Light targetLight; // Kontrol edilecek ışık
    [Tooltip("Oyuncu bu ışığın açık olmasını mı istiyor?")]
    [SerializeField] private bool desiredStateIsOn = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip switchSound;
    [SerializeField] private AudioClip errorSound; // Şartel atıkken basma sesi
    [SerializeField] private AudioSource audioSource; //
    
    [Header("Visual Feedback (Opsiyonel)")]
    [SerializeField] private Material onMaterial; //
    [SerializeField] private Material offMaterial; //
    [SerializeField] private MeshRenderer switchRenderer; //
    
    public bool IsOn => desiredStateIsOn; 

    private void Start()
    {
        if (targetLight == null)
        {
            Debug.LogError("ControllableLight'a ışık atanmamış!", this);
            return;
        }

        // Merkezi BreakerBox'a kendini kaydet
        if (BreakerBox.Instance != null)
        {
            BreakerBox.Instance.RegisterLight(this);
            BreakerBox.Instance.OnBreakerTripped += HandleBreakerTrip;
            BreakerBox.Instance.OnBreakerReset += HandleBreakerReset;
        }

        // Başlangıç durumunu ayarla
        UpdateLightVisual();
    }

    private void OnDestroy()
    {
        // Hafıza sızıntısını önlemek için kaydı sil
        if (BreakerBox.Instance != null)
        {
            BreakerBox.Instance.UnregisterLight(this);
            BreakerBox.Instance.OnBreakerTripped -= HandleBreakerTrip;
            BreakerBox.Instance.OnBreakerReset -= HandleBreakerReset;
        }
    }

    public void Interact()
    {
        // Eğer şartel atmışsa, ışığı açmaya izin verme
        if (BreakerBox.Instance != null && BreakerBox.Instance.IsTripped)
        {
            PlaySound(errorSound);
            return;
        }

        // Oyuncunun "isteğini" değiştir
        desiredStateIsOn = !desiredStateIsOn;
        PlaySound(switchSound);
        
        // Görseli güncelle
        UpdateLightVisual();
    }

    public string GetInteractionPrompt()
    {
        if (BreakerBox.Instance != null && BreakerBox.Instance.IsTripped)
        {
            return "Şartel Atık";
        }
        
        return desiredStateIsOn ? "[Sol Tık] Işığı Kapat" : "[Sol Tık] Işığı Aç";
    }
    
    private void HandleBreakerTrip()
    {
        Debug.Log(gameObject.name + " -> Breaker attı, ışık kapandı.");
        UpdateLightVisual();
    }

    // BreakerBox'tan "Şartel Kaldırıldı" olayı geldiğinde çalışır
    private void HandleBreakerReset()
    {
        Debug.Log(gameObject.name + " -> Breaker kaldırıldı, durum kontrol ediliyor.");
        UpdateLightVisual();
    }

    // Işığın GÖRSEL durumunu günceller
    private void UpdateLightVisual()
    {
        // Işığın yanabilmesi için şartelin atmamış olması GEREKİR
        bool canBeOn = (BreakerBox.Instance == null) || !BreakerBox.Instance.IsTripped;
        
        // Nihai durum: Şartel atmamışsa VE oyuncu açık olmasını istiyorsa
        bool finalState = canBeOn && desiredStateIsOn;

        if (targetLight != null)
        {
            targetLight.enabled = finalState;
        }

        // Düğme materyalini GÜNCEL duruma göre ayarla
        if (switchRenderer != null && onMaterial != null && offMaterial != null)
        {
            switchRenderer.material = finalState ? onMaterial : offMaterial;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}