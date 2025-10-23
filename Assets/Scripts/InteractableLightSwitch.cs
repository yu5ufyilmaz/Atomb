using System.Collections;
using UnityEngine;

public class InteractableLightSwitch : MonoBehaviour, IInteractable
{
    [Header("Light Settings")]
    [SerializeField]
    private Light targetLight; // Kontrol edilecek ışık

    [SerializeField]
    private bool isOn = true; // Başlangıç durumu

    [SerializeField]
    private float minIntensity = 0.5f;

    [SerializeField]
    private float maxIntensity = 1f;

    [SerializeField]
    private float flickerSpeed = 0.1f;

    [SerializeField]
    private float flickerDuration = 1.0f; // Kırpışma efekti toplam süresi

    [SerializeField]
    private float smoothTransitionDuration = 0.5f; // Stabil hale gelme süresi

    [Header("Audio")]
    [SerializeField]
    private AudioClip switchSound;

    [SerializeField]
    private AudioSource audioSource;

    [Header("Visual Feedback (Opsiyonel)")]
    [SerializeField]
    private Material onMaterial; // Düğme açık materiali

    [SerializeField]
    private Material offMaterial; // Düğme kapalı materiali

    [SerializeField]
    private MeshRenderer switchRenderer; // Düğmenin mesh renderer'ı

    private Coroutine flickerCoroutine;

    private void Start()
    {
        // Başlangıç durumunu ayarla
        if (targetLight != null)
        {
            targetLight.enabled = isOn;
            if (isOn)
            {
                targetLight.intensity = maxIntensity;
            }
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
        if (targetLight == null)
            return;

        isOn = !isOn; // Durumu tersine çevir

        PlaySwitchSound();
        UpdateVisuals();

        // Eğer başka bir flicker efekti çalışıyorsa, onu durdur
        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
        }

        if (isOn)
        {
            // Işığı açarken flicker efektini başlat
            flickerCoroutine = StartCoroutine(FlickerOn());
        }
        else
        {
            // Işığı kapat
            targetLight.enabled = false;
            // Işık kapalıyken intensity'yi sıfırlayabilir veya max'ta bırakabiliriz (tekrar açılma durumu için)
            targetLight.intensity = maxIntensity;
        }
    }

    // Işığı kırpışarak açan ve sonra yumuşakça stabil hale getiren Coroutine
    private IEnumerator FlickerOn()
    {
        // Önce ışığı aç
        targetLight.enabled = true;

        float elapsedTime = 0f;

        // --- 1. AŞAMA: Kırpışma Efekti ---
        while (elapsedTime < flickerDuration)
        {
            targetLight.intensity = Random.Range(minIntensity, maxIntensity);
            yield return new WaitForSeconds(flickerSpeed);
            elapsedTime += flickerSpeed;
        }

        // --- 2. AŞAMA: Smooth Geçiş (Yumuşatma) ---

        // Kırpışma bittikten sonra mevcut (son rastgele) intensity değerini al
        float currentIntensity = targetLight.intensity;
        float transitionElapsedTime = 0f;

        // Yumuşak geçiş süresi boyunca her frame'de çalış
        while (transitionElapsedTime < smoothTransitionDuration)
        {
            // Geçiş süresine göre 0 ile 1 arasında bir ilerleme değeri hesapla
            float t = transitionElapsedTime / smoothTransitionDuration;

            // Geçişi daha yumuşak (ease-in/ease-out) yapmak için SmoothStep kullanabiliriz
            t = Mathf.SmoothStep(0.0f, 1.0f, t);

            // Mevcut intensity'den maxIntensity'ye doğru yavaşça artır (Lerp)
            targetLight.intensity = Mathf.Lerp(currentIntensity, maxIntensity, t);

            // Geçen süreyi artır
            transitionElapsedTime += Time.deltaTime;

            // Bir sonraki frame'e kadar bekle
            yield return null;
        }

        // Süre dolduğunda, ışığın yoğunluğunu tam olarak maxIntensity yap (garanti olsun)
        targetLight.intensity = maxIntensity;

        // YENİ EKLENEN SATIR:
        Debug.Log(
            "Flicker Coroutine bitti. Işık intensity şu değere ayarlandı: "
                + targetLight.intensity
                + " (maxIntensity: "
                + maxIntensity
                + ")",
            targetLight.gameObject
        );

        flickerCoroutine = null; // Coroutine bitti
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
