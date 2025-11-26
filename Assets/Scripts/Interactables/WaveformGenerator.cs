using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaveformGenerator : MonoBehaviour
{
    private LineRenderer lineRenderer;

    [Header("Wave Settings")]
    [Tooltip("Dalga üzerindeki nokta sayısı (LineRenderer'daki Size ile aynı olmalı)")]
    [SerializeField] private int pointCount = 100;
    
    [Tooltip("Dalganın ekranın solundan sağına ne kadar genişleyeceği")]
    [SerializeField] private float waveWidth = 10f;
    
    [Tooltip("Dalganın yatayda ne kadar hızlı kayacağı (animasyon)")]
    [SerializeField] private float animationSpeed = 1f;

    // Bu değerler DIŞARIDAN (InteractableOscilloscope'tan) kontrol edilecek
    [HideInInspector] public float amplitude = 1.0f; // Genlik (Volts/Div)
    [HideInInspector] public float frequency = 1.0f; // Sıklık (Time/Div)

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

            // Y pozisyonunu Sinüs fonksiyonu ile hesapla
            // amplitude = dalganın yüksekliği (Genlik)
            // frequency = dalganın sıklığı (Frekans)
            float y = amplitude * Mathf.Sin((x * frequency) + xOffset);

            // LineRenderer'a noktayı ata
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }
}