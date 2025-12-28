using UnityEngine;

public class DynamoFlashlight : MonoBehaviour
{
    [Header("Bileşenler")]
    [Tooltip("Işık kaynağı (Spotlight)")]
    [SerializeField]
    private Light targetLight;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip crankSound; // Şarj etme sesi (Click-zzzt)

    [Header("Enerji Ayarları")]
    [Tooltip("Maksimum enerji kapasitesi")]
    [SerializeField]
    private float maxEnergy = 100f;

    [Tooltip("Başlangıçtaki enerji")]
    [SerializeField]
    private float currentEnergy = 0f;

    [Tooltip("Her sağ tıkta ne kadar enerji dolsun?")]
    [SerializeField]
    private float chargePerClick = 20f;

    [Tooltip("Saniyede ne kadar enerji azalsın? (Yüksek yaparsan çabuk söner)")]
    [SerializeField]
    private float drainRate = 15f;

    [Header("Işık Ayarları")]
    [SerializeField]
    private float maxIntensity = 2.5f; // Işığın en parlak hali

    [SerializeField]
    private float minIntensity = 0f; // Sönük hali

    private void Start()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        // Başlangıçta kapalı başlasın
        UpdateLight();
    }

    private void Update()
    {
        HandleInput();
        HandleDrain();
        UpdateLight();
    }

    private void HandleInput()
    {
        // Sağ Tık (Bas-Çek Mantığı)
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

        // Ses çal (Pitch'i rastgele yapalım ki mekanik hissi versin)
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

        // Eksiye düşmesin
        if (currentEnergy < 0)
            currentEnergy = 0;
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
}
