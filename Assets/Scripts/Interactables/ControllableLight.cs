using UnityEngine;

public class ControllableLight : MonoBehaviour, IInteractable
{
    [Header("Light Settings")]
    [SerializeField]
    private Light[] targetLight; // Kontrol edilecek ışıklar

    [Tooltip("Işık başlangıçta açık mı? (Koridor için TRUE yap)")]
    [SerializeField]
    private bool desiredStateIsOn = true;

    [Header("Breaker & Interaction Settings")]
    [Tooltip("Bu ışık düğmeyle açılıp kapansın mı? (Koridor için FALSE, Oda için TRUE)")]
    [SerializeField]
    private bool canPlayerInteract = true;

    [Tooltip("Bu ışık açıkken şartel atma riskini artırsın mı? (Koridor için genelde FALSE)")]
    [SerializeField]
    private bool contributesToRisk = true;

    [Header("Animation Settings")]
    [Tooltip("Dönmesini istediğin anahtar modeli (Koridor ışığında boş bırakabilirsin)")]
    [SerializeField]
    private Transform switchModel;

    [SerializeField]
    private Vector3 onRotation = new Vector3(-15f, 0f, 0f);

    [SerializeField]
    private Vector3 offRotation = new Vector3(15f, 0f, 0f);

    [Header("Audio")]
    [SerializeField]
    private AudioClip switchSound;

    [SerializeField]
    private AudioClip errorSound;

    [SerializeField]
    private AudioSource audioSource;

    [Header("Visual Feedback")]
    [SerializeField]
    private Material onMaterial;

    [SerializeField]
    private Material offMaterial;

    [SerializeField]
    private MeshRenderer switchRenderer;

    // BreakerBox'ın okuyacağı değerler
    public bool IsOn => desiredStateIsOn;
    public bool ContributesToRisk => contributesToRisk;

    private void Start()
    {
        // Kendini sisteme kaydet
        if (BreakerBox.Instance != null)
        {
            BreakerBox.Instance.RegisterLight(this);
            BreakerBox.Instance.OnBreakerTripped += HandleBreakerTrip;
            BreakerBox.Instance.OnBreakerReset += HandleBreakerReset;
        }

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

    // --- EKSİK OLAN VE HATAYA SEBEP OLAN FONKSİYON ---
    public void ToggleLightEditor()
    {
        desiredStateIsOn = !desiredStateIsOn;
        UpdateLightVisual();
    }

    // --------------------------------------------------

    public void Interact()
    {
        // EĞER OYUNCU ETKİLEŞİMİ KAPALIYSA (Koridor Işığıysa) HİÇBİR ŞEY YAPMA
        if (!canPlayerInteract)
            return;

        if (BreakerBox.Instance != null && BreakerBox.Instance.IsTripped)
        {
            PlaySound(errorSound);
            return;
        }

        desiredStateIsOn = !desiredStateIsOn;
        PlaySound(switchSound);
        UpdateLightVisual();
    }

    public string GetInteractionPrompt()
    {
        // EĞER ETKİLEŞİM KAPALIYSA YAZI ÇIKMASIN
        if (!canPlayerInteract)
            return "";

        if (BreakerBox.Instance != null && BreakerBox.Instance.IsTripped)
        {
            return "Şartel Atık";
        }

        return desiredStateIsOn ? "[Sol Tık] Işığı Kapat" : "[Sol Tık] Işığı Aç";
    }

    private void HandleBreakerTrip()
    {
        // Şartel atınca sadece görseli güncelle (Elektrik kesildi diyecek)
        UpdateLightVisual();
    }

    private void HandleBreakerReset()
    {
        UpdateLightVisual();
    }

    private void UpdateLightVisual()
    {
        // 1. ELEKTRİK KONTROLÜ
        // BreakerBox yoksa veya şartel atmamışsa elektrik vardır.
        bool hasPower = (BreakerBox.Instance == null) || !BreakerBox.Instance.IsTripped;

        // Işık yanıyor mu? = (Elektrik Var MI?) VE (Düğme Açık Mı?)
        bool isLightActive = hasPower && desiredStateIsOn;

        if (targetLight != null)
        {
            foreach (Light item in targetLight)
            {
                if (item != null)
                    item.enabled = isLightActive;
            }
        }

        // 2. MATERYAL (Eğer anahtar modeli varsa)
        if (switchRenderer != null && onMaterial != null && offMaterial != null)
        {
            switchRenderer.sharedMaterial = isLightActive ? onMaterial : offMaterial;
        }

        // 3. ANAHTAR ROTASYONU
        // Sadece anahtar modeli varsa döndür
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
