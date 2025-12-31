using UnityEngine;
using UnityEngine.UI; // UI işlemleri için bu kütüphane şart

public class DynomaFlashLight : MonoBehaviour
{
    [Header("Bileşenler")]
    [Tooltip("Işık kaynağı (Spotlight)")]
    [SerializeField]
    private Light targetLight;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip crankSound; // Şarj etme sesi (Click-zzzt)

    [Header("UI Ayarları")]
    [Tooltip("Canvas üzerindeki Energy Bar Image'i buraya sürükle")]
    [SerializeField]
    private Image energyBar; // <-- YENİ EKLENEN: Bar referansı

    [Header("Enerji Ayarları")]
    [Tooltip("Maksimum enerji kapasitesi")]
    [SerializeField]
    private float maxEnergy = 100f;

    [Tooltip("Başlangıçtaki enerji")]
    [SerializeField]
    private float currentEnergy = 0f;

    [Tooltip("Her sağ tıkta ne kadar enerji dolsun?")]
    [SerializeField]
    private float chargePerClick = 15f; // Biraz düşürdüm, tıklama hissi artsın diye

    [Tooltip("Saniyede ne kadar enerji azalsın?")]
    [SerializeField]
    private float drainRate = 10f;

    [Header("Işık Ayarları")]
    [SerializeField]
    private float maxIntensity = 2.5f; // Işığın en parlak hali

    [SerializeField]
    private float minIntensity = 0f; // Sönük hali

    private void Start()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        // Başlangıç durumunu ayarla
        UpdateLight();
        UpdateUI();
    }

    private void Update()
    {
        HandleInput();
        HandleDrain();
        UpdateLight();
        UpdateUI(); // Her karede barı güncelle
    }

    private void HandleInput()
    {
        // Sağ Tık Basılınca Şarj Et
        if (Input.GetMouseButtonDown(1))
        {
            ChargeFlashlight();
        }
    }

    private void ChargeFlashlight()
    {
        // Enerji ekle
        currentEnergy += chargePerClick;

        // Maksimumu geçmesin
        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);

        // Ses çal (Pitch rastgeleliği mekanik his verir)
        if (audioSource && crankSound)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(crankSound);
        }
    }

    private void HandleDrain()
    {
        // Enerji varsa zamanla azalt
        if (currentEnergy > 0)
        {
            currentEnergy -= drainRate * Time.deltaTime;
        }
        else
        {
            currentEnergy = 0;
        }
    }

    private void UpdateLight()
    {
        if (targetLight == null)
            return;

        // Enerji yüzdesini hesapla (0.0 ile 1.0 arası)
        float percentage = currentEnergy / maxEnergy;

        // Işık şiddetini enerjiye göre ayarla
        // Enerji azaldıkça ışık kısılacak
        targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, percentage);

        // Hiç enerji yoksa ışığı tamamen kapat (Performans için)
        targetLight.enabled = (currentEnergy > 0);
    }

    // --- YENİ EKLENEN KISIM: UI GÜNCELLEME ---
    private void UpdateUI()
    {
        if (energyBar != null)
        {
            // Barın doluluk oranını (fillAmount) mevcut enerjiye eşitle
            // currentEnergy 50 ise, maxEnergy 100 ise -> 0.5 (Yarım dolu) olur.
            energyBar.fillAmount = currentEnergy / maxEnergy;
        }
    }
}
