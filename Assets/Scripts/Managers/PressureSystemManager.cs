using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition; // HDRP Kütüphanesi

public class PressureSystemManager : MonoBehaviour
{
    public static PressureSystemManager Instance;

    [Header("Pressure Settings")]
    [Range(0, 100)]
    public float currentPressure = 0f;

    [SerializeField]
    private float pressureIncreaseRate = 2.0f;

    [SerializeField]
    private float warningThreshold = 90f;
    public float pressureDecreaseRate = 15f;

    [Header("Volume Settings")]
    [SerializeField]
    private Volume globalVolume;

    // HDRP Efekt Referansları
    private Vignette m_Vignette;
    private ChromaticAberration m_Aberration;
    private LensDistortion m_LensDistortion;
    private ColorAdjustments m_ColorAdjustments; // <-- YENİ: Renk kontrolü için

    [Header("Player References")]
    [SerializeField]
    private StarterAssets.CharacterController playerController;

    [Header("UI & Game Over")]
    [SerializeField]
    private GameObject handheldDeviceUI;

    [SerializeField]
    private TextMeshProUGUI pressureText;

    [SerializeField]
    private GameObject explosionEffect;

    [SerializeField]
    private GameObject warningUI;

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
        if (handheldDeviceUI != null)
            handheldDeviceUI.SetActive(false);
        if (warningUI != null)
            warningUI.SetActive(false);
        if (playerController == null)
            playerController = FindObjectOfType<StarterAssets.CharacterController>();

        if (globalVolume == null)
            globalVolume = FindObjectOfType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out m_Vignette);
            globalVolume.profile.TryGet(out m_Aberration);
            globalVolume.profile.TryGet(out m_LensDistortion);
            globalVolume.profile.TryGet(out m_ColorAdjustments); // <-- YENİ: Referansı alıyoruz
        }
    }

    private void Update()
    {
        if (isGameOver)
            return;

        // Basınç Artışı
        currentPressure += pressureIncreaseRate * Time.deltaTime;

        if (currentPressure >= 100f)
        {
            currentPressure = 100f;
            TriggerGameOver();
        }
        else if (currentPressure < 0f)
            currentPressure = 0f;

        HandlePressureEffects();
        HandleHandheldDevice();

        // --- EKLENEN KISIM: Megafon Acil Durum Kontrolü ---
        if (MegaphoneSystem.Instance != null)
        {
            // Basınç değerini ve uyarı eşiğini (örn: 90) gönderiyoruz.
            // Eğer eşiği geçerse "ACİL DURUM" anonsu çalacak.
            MegaphoneSystem.Instance.CheckPressureEvent(currentPressure, warningThreshold);
        }
        // --------------------------------------------------
    }

    private void HandlePressureEffects()
    {
        // 1. VIGNETTE (Kararma) - %50'de başlar
        if (m_Vignette != null)
        {
            if (currentPressure > 50f)
            {
                float ratio = (currentPressure - 50f) / 50f;
                m_Vignette.intensity.Override(Mathf.Clamp01(ratio) * 0.5f);
            }
            else
                m_Vignette.intensity.Override(0f);
        }

        // 2. BULANTI EFEKTLERİ - %60'ta başlar
        if (currentPressure > 60f)
        {
            float nauseaRatio = (currentPressure - 60f) / 40f;
            float pulse = 1f + (Mathf.Sin(Time.time * 2f) * 0.3f);

            // Lens Distortion (Bükülme)
            if (m_LensDistortion != null)
            {
                float distortionAmount = -0.5f * nauseaRatio * pulse;
                m_LensDistortion.intensity.Override(distortionAmount);
                m_LensDistortion.scale.Override(1f - (nauseaRatio * 0.1f));
            }

            // Chromatic Aberration (Renk Kayması)
            if (m_Aberration != null)
            {
                m_Aberration.intensity.Override(nauseaRatio * 1f);
            }
        }
        else
        {
            // %60'ın altındaysa efektleri sıfırla
            if (m_LensDistortion != null)
            {
                m_LensDistortion.intensity.Override(0f);
                m_LensDistortion.scale.Override(1f);
            }
            if (m_Aberration != null)
                m_Aberration.intensity.Override(0f);
        }

        // 3. RENKLERİN GİDİŞİ (Saturation) - %75'te başlar
        // %75 basınçtan %100'e kadar renkler yavaşça Siyah-Beyaza döner.
        if (m_ColorAdjustments != null)
        {
            if (currentPressure > 75f)
            {
                // %75 ile %100 arasını 0 ile 1 arasına orantıla
                float fadeRatio = (currentPressure - 75f) / 25f;

                // Saturation: 0 (Normal) ile -100 (Siyah Beyaz) arası
                float saturationValue = Mathf.Lerp(0f, -100f, fadeRatio);

                m_ColorAdjustments.saturation.Override(saturationValue);
            }
            else
            {
                m_ColorAdjustments.saturation.Override(0f);
            }
        }

        // Sarhoşluk Parametresi (Karakter Kontrolcüsü İçin)
        if (playerController != null)
        {
            float drunkRatio = (currentPressure > 80f) ? (currentPressure - 80f) / 20f : 0f;
            playerController.drunkIntensity = drunkRatio;
        }

        // UI Uyarı
        if (warningUI != null)
        {
            bool shouldShow = currentPressure > warningThreshold;
            if (warningUI.activeSelf != shouldShow)
                warningUI.SetActive(shouldShow);
        }
    }

    private void HandleHandheldDevice()
    {
        if (Input.GetKey(KeyCode.B))
        {
            if (handheldDeviceUI != null)
                handheldDeviceUI.SetActive(true);
            if (pressureText != null)
            {
                pressureText.text = $"PRESSURE: {currentPressure:F0}%";
                pressureText.color = currentPressure > 90 ? Color.red : Color.green;
            }
        }
        else
        {
            if (handheldDeviceUI != null && handheldDeviceUI.activeSelf)
                handheldDeviceUI.SetActive(false);
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

    public float GetPressure() => currentPressure;

    public float GetWarningThreshold() => warningThreshold;

    public bool IsWarningActive() => currentPressure > warningThreshold;
}
