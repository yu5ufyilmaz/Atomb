using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaveformGenerator : MonoBehaviour
{
    private LineRenderer lineRenderer;

    [Header("Wave Settings")]
    [Tooltip("Dalga üzerindeki nokta sayısı (LineRenderer'daki Size ile aynı olmalı)")]
    [SerializeField]
    private int pointCount = 100;

    [Tooltip("Dalganın ekranın solundan sağına ne kadar genişleyeceği")]
    [SerializeField]
    private float waveWidth = 10f;

    [Tooltip("Dalganın yatayda ne kadar hızlı kayacağı (animasyon)")]
    [SerializeField]
    private float animationSpeed = 1f;

    // --- DEĞİŞKENLER ---
    // Bu değerler DIŞARIDAN (InteractableOscilloscope'tan) kontrol edilecek
    [HideInInspector]
    public float amplitude = 1.0f; // Genlik (Yükseklik)

    [HideInInspector]
    public float frequency = 1.0f; // Sıklık

    // YENİ: Gürültü Miktarı (0 = Pürüzsüz, 0.5+ = Çok Bozuk)
    [HideInInspector]
    public float noiseAmount = 0f;

    private float xOffset = 0f; // Animasyon için kaydırma miktarı

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = pointCount;
    }

    void Update()
    {
        DrawWave();
    }

    void DrawWave()
    {
        // Animasyon için X ekseninde kaydır
        xOffset += Time.deltaTime * animationSpeed;

        for (int i = 0; i < pointCount; i++)
        {
            // X pozisyonunu hesapla (0'dan waveWidth'e kadar)
            float x = (float)i / (pointCount - 1) * waveWidth;

            // Temel Sinüs Dalgası
            float sineValue = Mathf.Sin((x * frequency) + xOffset);

            // YENİ: Gürültü Ekleme (Random Jitter)
            // Her nokta için rastgele ufak bir sapma ekliyoruz.
            // noiseAmount ne kadar büyükse, sapma o kadar çılgın olur.
            float noise = Random.Range(-noiseAmount, noiseAmount);

            // Son Y Değeri: Genlik * (Sinüs + Gürültü)
            // Gürültüyü genliğe eklemiyoruz, sinüsün üstüne bindiriyoruz ki dalga formu bozulsun.
            float y = amplitude * (sineValue + noise);

            // LineRenderer'a noktayı ata
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }
}
