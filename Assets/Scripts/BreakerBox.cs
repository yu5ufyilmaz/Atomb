using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 

public class BreakerBox : MonoBehaviour, IInteractable
{
    public static BreakerBox Instance;

    [Header("Breaker Ayarları")]
    [Tooltip("PDF'teki 'CHECK INTERVAL' (saniye)")]
    [SerializeField] private float checkInterval = 180f;
    
    [Tooltip("Sisteme kayıtlı TÜM ışıklar yandığında oluşacak temel risk (örn: 0.8 = %80)")]
    [SerializeField] [Range(0.1f, 1f)] private float maxRiskAtFullLoad = 0.8f; 
    
    [Tooltip("Her bir stabil döngünün riske eklediği çarpan (örn: 0.01 = %1)")]
    [SerializeField] private float cycleRiskMultiplier = 0.01f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip breakerTripSound; 
    [SerializeField] private AudioClip breakerResetSound; 

    private int cycleCount = 0; 
    private bool isTripped = false;
    private List<ControllableLight> allLights = new List<ControllableLight>();

    public event System.Action OnBreakerTripped;
    public event System.Action OnBreakerReset;

    public bool IsTripped => isTripped;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        StartCoroutine(CheckBreakerLoop());
    }

    #region Işık Yönetimi
    public void RegisterLight(ControllableLight light)
    {
        if (!allLights.Contains(light))
        {
            allLights.Add(light);
        }
    }

    public void UnregisterLight(ControllableLight light)
    {
        if (allLights.Contains(light))
        {
            allLights.Remove(light);
        }
    }

    private int GetActiveLightCount()
    {
        return allLights.Count(l => l.IsOn);
    }
    #endregion

    // --- DEĞİŞEN BÖLÜM BAŞLANGICI ---

    #region Breaker Döngüsü (PDF Akışı)
    private IEnumerator CheckBreakerLoop()
    {
        while (true)
        {
            if (isTripped)
            {
                // ŞARTEL ATIK: 180 saniye bekleme.
                // Oyuncu şarteli kaldırana kadar (isTripped = false olana kadar)
                // her frame hızlıca kontrol et.
                yield return null; 
            }
            else
            {
                // ŞARTEL AÇIK: 180 saniye bekle.
                yield return new WaitForSeconds(checkInterval);
                
                // 180 saniye sonra, HÂLÂ AÇIKSA (bu bekleme sırasında atik duruma geçmemişse)
                // risk kontrolünü çalıştır.
                if (!isTripped) 
                {
                    RunRiskCheck();
                }
            }
        }
    }

    // PDF Akışını yürüten yeni fonksiyon
    private void RunRiskCheck()
    {
        int activeLights = GetActiveLightCount();
        int totalLights = allLights.Count;
        
        // Eğer hiç ışık yanmıyorsa VEYA sisteme kayıtlı hiç ışık yoksa risk olmaz
        if (activeLights == 0 || totalLights == 0)
        {
            cycleCount = 0;
            return; // Risk yok, fonksiyondan çık
        }

        // 4. "Calculate the probability..."
        float loadPercentage = (float)activeLights / totalLights;
        float baseRisk = loadPercentage * maxRiskAtFullLoad; 
        float cycleRisk = cycleCount * cycleRiskMultiplier;
        float tripChance = Mathf.Clamp01(baseRisk + cycleRisk); 

        Debug.Log($"Breaker Check: {activeLights}/{totalLights} ışık (%{loadPercentage * 100:F0} yük). Temel Risk: {baseRisk * 100:F0}%. Döngü Riski: {cycleRisk * 100:F0}%. Toplam Trip Şansı: {tripChance * 100:F0}%");

        if (Random.value < tripChance)
        {
            // 7. "YES" -> "Trigger global BREAKER TRIP event."
            Debug.LogWarning("BREAKER TRIPPED! System risk reset.");
            isTripped = true;
            cycleCount = 0; 
            PlaySound(breakerTripSound);
            OnBreakerTripped?.Invoke(); 
        }
        else
        {
            // 8. "NO" -> "Increment CycleCount"
            cycleCount++;
            Debug.Log("System is stable. Risk increased for next cycle.");
        }
    }
    #endregion

    // --- DEĞİŞEN BÖLÜM BİTİŞİ ---

    #region IInteractable (Şarteli Kaldırma)
    public void Interact()
    {
        if (!isTripped) return; 

        Debug.Log("Breaker Manually Reset!");
        isTripped = false;
        cycleCount = 0; 
        PlaySound(breakerResetSound);
        OnBreakerReset?.Invoke(); 
    }

    public string GetInteractionPrompt()
    {
        return isTripped ? "[Sol Tık] Şarteli Kaldır" : "Sistem Stabil";
    }
    #endregion

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}