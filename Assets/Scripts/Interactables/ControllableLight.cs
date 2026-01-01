using UnityEngine;

public class ControllableLight : MonoBehaviour, IInteractable
{
    [Header("Light Settings")]
    [SerializeField]
    private Light[] targetLight; // Kontrol edilecek ışıklar

    [Tooltip("Oyuncu bu ışığın açık olmasını mı istiyor?")]
    [SerializeField]
    private bool desiredStateIsOn = true;

    [Header("Animation Settings (YENİ)")]
    [Tooltip("Dönmesini istediğin anahtar modeli (Pivot noktası önemli!)")]
    [SerializeField]
    private Transform switchModel; // Hareket edecek parça

    [Tooltip("Anahtar AÇIK konumdayken alacağı rotasyon (Inspector'dan bakarak gir)")]
    [SerializeField]
    private Vector3 onRotation = new Vector3(-15f, 0f, 0f);

    [Tooltip("Anahtar KAPALI konumdayken alacağı rotasyon")]
    [SerializeField]
    private Vector3 offRotation = new Vector3(15f, 0f, 0f);

    [Header("Audio")]
    [SerializeField]
    private AudioClip switchSound;

    [SerializeField]
    private AudioClip errorSound; // Şartel atıkken basma sesi

    [SerializeField]
    private AudioSource audioSource;

    [Header("Visual Feedback (Materyal)")]
    [SerializeField]
    private Material onMaterial;

    [SerializeField]
    private Material offMaterial;

    [SerializeField]
    private MeshRenderer switchRenderer;

    public bool IsOn => desiredStateIsOn;

    private void Start()
    {
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
        if (BreakerBox.Instance != null)
        {
            BreakerBox.Instance.UnregisterLight(this);
            BreakerBox.Instance.OnBreakerTripped -= HandleBreakerTrip;
            BreakerBox.Instance.OnBreakerReset -= HandleBreakerReset;
        }
    }

    private void OnValidate()
    {
        // Editörde açıları denerken anlık görmek için burası çalışır
        UpdateLightVisual();
    }

    public void ToggleLightEditor()
    {
        desiredStateIsOn = !desiredStateIsOn;
        UpdateLightVisual();
    }

    public void Interact()
    {
        // Eğer şartel atmışsa, ışığı açmaya izin verme (Ses çalar ama düğme dönmez)
        // NOT: Eğer şartel atıkken de düğmenin "klik" yapıp dönmesini ama ışığın yanmamasını istiyorsan
        // aşağıdaki if bloğunu kaldırabilirsin. Şu anki haliyle şartel atıksa düğme kilitli gibi davranır.
        if (BreakerBox.Instance != null && BreakerBox.Instance.IsTripped)
        {
            PlaySound(errorSound);
            return;
        }

        // Oyuncunun "isteğini" değiştir
        desiredStateIsOn = !desiredStateIsOn;
        PlaySound(switchSound);

        // Görseli ve ışığı güncelle
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
        UpdateLightVisual();
    }

    private void HandleBreakerReset()
    {
        UpdateLightVisual();
    }

    // Işığın GÖRSEL durumunu ve MODEL ROTASYONUNU günceller
    private void UpdateLightVisual()
    {
        // 1. IŞIK KAYNAĞI KONTROLÜ (Ampul)
        bool canBeOn = (BreakerBox.Instance == null) || !BreakerBox.Instance.IsTripped;
        bool lightActiveState = canBeOn && desiredStateIsOn; // Elektrik var mı VE düğme açık mı?

        if (targetLight != null)
        {
            foreach (Light item in targetLight)
            {
                if (item != null)
                    item.enabled = lightActiveState;
            }
        }

        // 2. MATERYAL KONTROLÜ (Emissive vb.)
        if (switchRenderer != null && onMaterial != null && offMaterial != null)
        {
            switchRenderer.sharedMaterial = lightActiveState ? onMaterial : offMaterial;
        }

        // 3. MODEL ROTASYONU (Anahtarın kendisi) [YENİ KISIM]
        // Not: Anahtarın fiziksel konumu, elektriğin olup olmamasından bağımsızdır.
        // Oyuncu düğmeyi "AÇIK" bıraktıysa, elektrik kesilse bile düğme "AÇIK" pozisyonda durur.
        // Bu yüzden burada 'lightActiveState' yerine direkt 'desiredStateIsOn' kullanıyoruz.
        if (switchModel != null)
        {
            switchModel.localEulerAngles = desiredStateIsOn ? onRotation : offRotation;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }
}
