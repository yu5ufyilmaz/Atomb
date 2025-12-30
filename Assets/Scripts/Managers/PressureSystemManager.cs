using System.Collections;
using StarterAssets; // Player Controller için
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition; // HDRP için
using UnityEngine.UI; // Image işlemleri için şart

public class PressureSystemManager : MonoBehaviour
{
    public static PressureSystemManager Instance;

    [Header("⚠️ Basınç Ayarları")]
    [Range(0, 100)]
    public float currentPressure = 0f;

    [Tooltip("Saniyede artış miktarı")]
    [SerializeField]
    private float pressureIncreaseRate = 2.0f;

    [Tooltip("Vana çevirirken düşüş hızı")]
    public float pressureDecreaseRate = 15f;

    [Tooltip("Uyarının başlayacağı sınır (Örn: 90)")]
    [SerializeField]
    private float warningThreshold = 90f;

    [Header("📊 UI Ayarları (HUD)")]
    [Tooltip("Ekranda sürekli duracak olan Bar Image'i (Filled)")]
    [SerializeField]
    private Image pressureBarImage;

    [Tooltip("Varsa basınç yazısı (Örn: %45)")]
    [SerializeField]
    private TextMeshProUGUI pressureText;

    [Tooltip("Bar bu değeri geçerse renk değiştirir")]
    [SerializeField]
    private float colorChangeThreshold = 70f;

    [SerializeField]
    private Color safeColor = Color.green;

    [SerializeField]
    private Color dangerColor = Color.red;

    [Header("🎬 Efektler & Post-Process")]
    [SerializeField]
    private Volume globalVolume;

    private Vignette m_Vignette;
    private ChromaticAberration m_Aberration;
    private LensDistortion m_LensDistortion;
    private ColorAdjustments m_ColorAdjustments;

    [Header("💀 Game Over")]
    [SerializeField]
    private GameObject explosionEffect;

    [SerializeField]
    private GameObject warningUI; // Ekranda yanıp sönen kırmızı ışık vb.

    private StarterAssets.CharacterController playerController;
    private bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Player'ı bul
        playerController = FindObjectOfType<StarterAssets.CharacterController>();

        // HDRP Volume Ayarlarını Çek
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out m_Vignette);
            globalVolume.profile.TryGet(out m_Aberration);
            globalVolume.profile.TryGet(out m_LensDistortion);
            globalVolume.profile.TryGet(out m_ColorAdjustments);
        }

        if (warningUI != null)
            warningUI.SetActive(false);
    }

    private void Update()
    {
        if (isGameOver)
            return;

        // --- BASINÇ MANTIĞI ---
        // Basıncı artır
        currentPressure += pressureIncreaseRate * Time.deltaTime;

        // Sınırla (0 - 100 arası)
        if (currentPressure >= 100f)
        {
            currentPressure = 100f;
            TriggerGameOver();
        }
        else if (currentPressure < 0f)
        {
            currentPressure = 0f;
        }

        // --- GÜNCELLEMELER ---
        UpdateHUD(); // Barı ve yazıyı güncelle
        HandlePostProcessing(); // Ekran efektlerini güncelle
        CheckMegaphone(); // Megafon sistemini kontrol et
    }

    // --- YENİ UI FONKSİYONU ---
    private void UpdateHUD()
    {
        // 1. Bar Doluluğu
        if (pressureBarImage != null)
        {
            pressureBarImage.fillAmount = currentPressure / 100f;

            // 2. Renk Değişimi
            if (currentPressure > colorChangeThreshold)
                pressureBarImage.color = dangerColor;
            else
                pressureBarImage.color = safeColor;
        }

        // 3. Yazı Güncellemesi (Varsa)
        if (pressureText != null)
        {
            pressureText.text = $"{currentPressure:F0}%";
            pressureText.color = (currentPressure > warningThreshold) ? dangerColor : safeColor;
        }

        // 4. Ekstra Uyarı UI'ı (Yanıp sönen ikon vb.)
        if (warningUI != null)
        {
            bool isCritical = currentPressure > warningThreshold;
            if (warningUI.activeSelf != isCritical)
                warningUI.SetActive(isCritical);
        }
    }

    // --- GÖRSEL EFEKTLER ---
    private void HandlePostProcessing()
    {
        // Vignette (Kararma) - %50'den sonra
        if (m_Vignette != null)
        {
            float ratio = (currentPressure > 50f) ? (currentPressure - 50f) / 50f : 0f;
            m_Vignette.intensity.Override(ratio * 0.5f);
        }

        // Bulantı & Bükülme - %60'tan sonra
        if (currentPressure > 60f)
        {
            float ratio = (currentPressure - 60f) / 40f;
            float pulse = 1f + (Mathf.Sin(Time.time * 2f) * 0.3f);

            if (m_LensDistortion != null)
            {
                m_LensDistortion.intensity.Override(-0.5f * ratio * pulse);
                m_LensDistortion.scale.Override(1f - (ratio * 0.1f));
            }
            if (m_Aberration != null)
            {
                m_Aberration.intensity.Override(ratio * 1f);
            }
        }
        else
        {
            if (m_LensDistortion != null)
                m_LensDistortion.intensity.Override(0f);
            if (m_Aberration != null)
                m_Aberration.intensity.Override(0f);
        }

        // Siyah Beyazlaşma - %75'ten sonra
        if (m_ColorAdjustments != null)
        {
            float satVal =
                (currentPressure > 75f) ? Mathf.Lerp(0f, -100f, (currentPressure - 75f) / 25f) : 0f;
            m_ColorAdjustments.saturation.Override(satVal);
        }

        // Karakter Sarhoşluk Hareketi
        if (playerController != null)
        {
            playerController.drunkIntensity =
                (currentPressure > 80f) ? (currentPressure - 80f) / 20f : 0f;
        }
    }

    private void CheckMegaphone()
    {
        if (MegaphoneSystem.Instance != null)
        {
            MegaphoneSystem.Instance.CheckPressureEvent(currentPressure, warningThreshold);
        }
    }

    public void ReducePressure(float amount)
    {
        if (!isGameOver)
            currentPressure = Mathf.Max(0, currentPressure - amount);
    }

    private void TriggerGameOver()
    {
        if (isGameOver)
            return;
        isGameOver = true;

        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        StartCoroutine(WaitAndShowDeathUI());
    }

    private IEnumerator WaitAndShowDeathUI()
    {
        yield return new WaitForSeconds(3.0f);
        if (DeathUIManager.Instance != null)
            DeathUIManager.Instance.ShowDeathScreen();
    }

    // Diğer scriptler için getterlar
    public float GetPressure() => currentPressure;

    public float GetWarningThreshold() => warningThreshold;

    public bool IsWarningActive() => currentPressure > warningThreshold;
}
