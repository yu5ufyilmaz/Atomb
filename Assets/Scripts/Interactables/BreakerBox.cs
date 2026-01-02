using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BreakerBox : MonoBehaviour, IInteractable
{
    public static BreakerBox Instance;

    [Header("Breaker Ayarları")]
    [SerializeField]
    private float checkInterval = 180f;

    [Tooltip("Sisteme kayıtlı TÜM ışıklar yandığında oluşacak temel risk (örn: 0.8 = %80)")]
    [SerializeField]
    [Range(0.1f, 1f)]
    private float maxRiskAtFullLoad = 0.8f;

    [Tooltip("Her bir stabil döngünün riske eklediği çarpan (örn: 0.01 = %1)")]
    [SerializeField]
    private float cycleRiskMultiplier = 0.01f;

    [Header("Animasyon Ayarları")] // <--- YENİ EKLENDİ
    [Tooltip("Hareket edecek kol objesi (Mesh)")]
    [SerializeField]
    private Transform handleObject;

    [Tooltip("Kolun AÇIK (Yukarı) halindeki rotasyonu")]
    [SerializeField]
    private Vector3 handleUpRotation = new Vector3(-45, 0, 0);

    [Tooltip("Kolun KAPALI (Aşağı/Attığında) halindeki rotasyonu")]
    [SerializeField]
    private Vector3 handleDownRotation = new Vector3(45, 0, 0);

    [Tooltip("Kolun hareket hızı")]
    [SerializeField]
    private float animationSpeed = 5.0f;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip breakerTripSound;

    [SerializeField]
    private AudioClip breakerResetSound;

    private int cycleCount = 0;
    private bool isTripped = false;
    private List<ControllableLight> allLights = new List<ControllableLight>();
    private Coroutine currentAnimCoroutine; // <--- YENİ EKLENDİ

    public event System.Action OnBreakerTripped;
    public event System.Action OnBreakerReset;

    public bool IsTripped => isTripped;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Başlangıçta kolun konumunu ayarla
        if (handleObject != null)
        {
            handleObject.localEulerAngles = isTripped ? handleDownRotation : handleUpRotation;
        }

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
        // IsOn: Açık mı?
        // ContributesToRisk: Risk grubunda mı?
        return allLights.Count(l => l.IsOn && l.ContributesToRisk);
    }
    #endregion

    #region Breaker Döngüsü (PDF Akışı)
    private IEnumerator CheckBreakerLoop()
    {
        while (true)
        {
            if (isTripped)
            {
                // ŞARTEL ATIK: Oyuncu düzeltene kadar bekle
                yield return null;
            }
            else
            {
                // ŞARTEL AÇIK: 180 saniye bekle.
                yield return new WaitForSeconds(checkInterval);

                if (!isTripped)
                {
                    RunRiskCheck();
                }
            }
        }
    }

    private void RunRiskCheck()
    {
        // Sadece risk oluşturanları hesaba kat (Koridor ışıkları hariç)
        int activeRiskLights = GetActiveLightCount();
        int totalRiskLights = allLights.Count(l => l.ContributesToRisk); // Toplam riskli cihaz sayısı

        // Eğer hiç riskli cihaz yoksa (Sadece koridor ışıkları varsa) risk 0'dır.
        if (activeRiskLights == 0 || totalRiskLights == 0)
        {
            cycleCount = 0;
            return;
        }

        // Yük oranını hesapla (Örn: 3 riskli ışıktan 2'si açık = %66 yük)
        float loadPercentage = (float)activeRiskLights / totalRiskLights;

        float baseRisk = loadPercentage * maxRiskAtFullLoad;
        float cycleRisk = cycleCount * cycleRiskMultiplier;
        float tripChance = Mathf.Clamp01(baseRisk + cycleRisk);

        Debug.Log(
            $"Breaker Check: {activeRiskLights}/{totalRiskLights} riskli ışık açık. Trip Şansı: %{tripChance * 100:F0}"
        );

        if (Random.value < tripChance)
        {
            // ŞARTEL ATTI!
            // Buradaki kod değişmiyor, çünkü 'isTripped = true' olunca
            // tüm ışıklar (koridor dahil) zaten kendini kapatıyor.
            Debug.LogWarning("BREAKER TRIPPED!");
            isTripped = true;
            cycleCount = 0;

            PlaySound(breakerTripSound);
            StartHandleAnimation(handleDownRotation);

            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.OnBreakerTripped();

            OnBreakerTripped?.Invoke();
        }
        else
        {
            cycleCount++;
        }
    }
    #endregion

    #region IInteractable (Şarteli Kaldırma)
    public void Interact()
    {
        if (!isTripped)
            return;

        // --- OYUNCU TARAFINDAN ŞARTEL KALDIRILIYOR ---
        Debug.Log("Breaker Manually Reset!");
        isTripped = false;
        cycleCount = 0;

        PlaySound(breakerResetSound);
        StartHandleAnimation(handleUpRotation); // <--- ANİMASYON EKLENDİ (YUKARI)

        OnBreakerReset?.Invoke();
    }

    public string GetInteractionPrompt()
    {
        return isTripped ? "[Sol Tık] Şarteli Kaldır" : "Sistem Stabil";
    }
    #endregion

    #region Animasyon İşlemleri (YENİ BÖLÜM)
    private void StartHandleAnimation(Vector3 targetRotation)
    {
        if (handleObject == null)
            return;

        if (currentAnimCoroutine != null)
            StopCoroutine(currentAnimCoroutine);
        currentAnimCoroutine = StartCoroutine(AnimateHandleRoutine(targetRotation));
    }

    private IEnumerator AnimateHandleRoutine(Vector3 targetEuler)
    {
        Quaternion startRot = handleObject.localRotation;
        Quaternion endRot = Quaternion.Euler(targetEuler);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * animationSpeed;
            handleObject.localRotation = Quaternion.Lerp(startRot, endRot, t);
            yield return null;
        }
        handleObject.localRotation = endRot;
    }
    #endregion

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    #region GETTERS
    public float GetCurrentRiskPercentage()
    {
        if (allLights.Count == 0)
            return 0f;

        int activeLights = GetActiveLightCount();
        float loadPercentage = (float)activeLights / allLights.Count;
        float baseRisk = loadPercentage * maxRiskAtFullLoad;
        float cycleRisk = cycleCount * cycleRiskMultiplier;

        return Mathf.Clamp01(baseRisk + cycleRisk);
    }

    public int GetActiveLightCountPublic() => GetActiveLightCount();

    public int GetTotalLightCount() => allLights.Count;

    public int GetCycleCount() => cycleCount;
    #endregion
}
