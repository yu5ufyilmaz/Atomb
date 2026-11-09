using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaveformGenerator : MonoBehaviour
{
    private LineRenderer lineRenderer;

    [Header("Wave Settings")]
    [SerializeField] private int pointCount = 100;
    [SerializeField] private float waveWidth = 10f;
    [SerializeField] private float animationSpeed = 2f; // Biraz hızlandırdım

    [Header("CRT Feel")]
    [Tooltip("Ekran bombesi")]
    [SerializeField] private float curveDepth = 0.5f; 
    [Tooltip("Çizginin uçları ekrana yapışık mı yoksa havada mı?")]
    [SerializeField] private bool lockEdges = true;
    [Tooltip("Analog titreşim miktarı")]
    [SerializeField] private float microJitter = 0.02f;

    // Dışarıdan kontrol edilenler
    [HideInInspector] public float amplitude = 1.0f;
    [HideInInspector] public float frequency = 1.0f;
    [HideInInspector] public float noiseAmount = 0f;

    private float xOffset = 0f;
    private float disturbedUntil;
    public bool IsDisturbed => Time.time < disturbedUntil;
    // Lees anomalisi: iz birkaç saniye delirir, süre bitince kendi değerleriyle çizmeye döner. 0 hemen bitirir.
    public void Disturb(float seconds) => disturbedUntil = seconds > 0 ? Time.time + seconds : 0;
    // Renk geçişi (Gradient) için
    private Gradient baseGradient;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = pointCount;
        lineRenderer.useWorldSpace = false;
        
        // Başlangıç gradientini yedekle
        baseGradient = lineRenderer.colorGradient;
    }

    void Update()
    {
        if (!lineRenderer.isVisible) return; // başka odadayken 100 noktayı her kare yeniden hesaplama
        DrawWave();
        SimulatePhosphorFade();
    }

    void DrawWave()
    {
        xOffset += Time.deltaTime * animationSpeed;
        float startX = -waveWidth / 2f;
        bool disturbed = IsDisturbed;
        float noise = disturbed ? 1.4f : noiseAmount;
        float amp = disturbed ? Mathf.Max(amplitude, 1f) * Random.Range(1.2f, 2.4f) : amplitude;

        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            float x = startX + (t * waveWidth);

            // Sinyal Hesabı
            float sinePhase = (t * waveWidth * frequency) + xOffset;
            float sineValue = Mathf.Sin(sinePhase);
            
            // Gürültü + Mikro Titreşim (Analog hissi için sürekli titrer)
            float totalNoise = Random.Range(-noise, noise) + Random.Range(-microJitter, microJitter);
            float y = amp * (sineValue + totalNoise);

            // Kavis Hesabı (Bombeli Ekran)
            // Kenarlarda 0, ortada 1 olan bir eğri (Sinüs yayı)
            float bulge = Mathf.Sin(t * Mathf.PI);
            float z = -bulge * curveDepth;

            // Kenarları maskeleme (Ekranın dışına taşmasın diye kenarlarda Y'yi sıfırlıyoruz)
            if (lockEdges)
            {
                // Kenarlara yaklaştıkça dalgayı sönümler (Vignette etkisi gibi)
                float edgeMask = 1f - Mathf.Pow(2f * (t - 0.5f), 4f); // Kenarlarda sertçe düşer
                y *= Mathf.Clamp01(edgeMask);
            }

            lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }

    // Ekrandaki o hafif yanıp sönme (Flicker) efekti
    void SimulatePhosphorFade()
    {
        if (Random.value > 0.9f) // Arada bir pırpır etsin
        {
            float dimFactor = Random.Range(0.8f, 1.0f);
            lineRenderer.widthMultiplier = 0.05f * dimFactor; // Kalınlıkla oyna
        }
        else
        {
             lineRenderer.widthMultiplier = 0.05f; // Standart kalınlık (bunu inspector'dan da alabilirsin)
        }
    }
}