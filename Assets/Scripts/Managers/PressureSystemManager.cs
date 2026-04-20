using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

public class PressureSystemManager : MonoBehaviour, ISaveable
{
    public static PressureSystemManager Instance;

    [Header("⚠️ Basınç Ayarları")]
    [Range(0, 100)]
    public float currentPressure = 0f;

    [SerializeField]
    private float pressureIncreaseRate = 2.0f;
    public float pressureDecreaseRate = 15f;

    [SerializeField]
    private float warningThreshold = 90f;

    [Header("📊 UI Ayarları (HUD)")]
    [SerializeField]
    private Image pressureBarImage;

    [SerializeField]
    private TextMeshProUGUI pressureText;

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

    // --- YENİ EKLENEN KİLİT ---
    [HideInInspector]
    public bool overridePostProcessing = false; // Jumpscare sırasında True yapacağız

    // --------------------------

    [Header("💀 Game Over")]
    [SerializeField]
    private GameObject explosionEffect;

    [SerializeField]
    private GameObject warningUI;

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
        playerController = FindObjectOfType<StarterAssets.CharacterController>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out m_Vignette);
            globalVolume.profile.TryGet(out m_Aberration);
            globalVolume.profile.TryGet(out m_LensDistortion);
            globalVolume.profile.TryGet(out m_ColorAdjustments);
        }

        if (warningUI != null)
            warningUI.SetActive(false);
        HandlePostProcessing();
    }

    private void Update()
    {
        if (isGameOver)
            return;

        // YENİ EKLENEN KONTROL: Oyun henüz başlamadıysa basınç artışını DONDUR
        if (GameManager.Instance != null && !GameManager.Instance.isGameStarted)
            return;

        // Basınç Mantığı
        currentPressure += pressureIncreaseRate * Time.deltaTime;

        if (currentPressure >= 100f)
        {
            currentPressure = 100f;
            TriggerGameOver();
        }
        else if (currentPressure < 0f)
        {
            currentPressure = 0f;
        }

        UpdateHUD();
        HandlePostProcessing();
        CheckMegaphone();
    }

    private void UpdateHUD()
    {
        if (pressureBarImage != null)
        {
            pressureBarImage.fillAmount = currentPressure / 100f;
            pressureBarImage.color =
                (currentPressure > colorChangeThreshold) ? dangerColor : safeColor;
        }

        if (pressureText != null)
        {
            pressureText.text = $"{currentPressure:F0}%";
            pressureText.color = (currentPressure > warningThreshold) ? dangerColor : safeColor;
        }

        if (warningUI != null)
        {
            bool isCritical = currentPressure > warningThreshold;
            if (warningUI.activeSelf != isCritical)
                warningUI.SetActive(isCritical);
        }
    }

    private void HandlePostProcessing()
    {
        // --- DÜZELTME BURADA ---
        // Eğer Jumpscare Manager kontrolü devraldıysa, burası çalışmasın!
        if (overridePostProcessing)
            return;
        // -----------------------

        if (m_Vignette != null)
        {
            float ratio = (currentPressure > 50f) ? (currentPressure - 50f) / 50f : 0f;
            m_Vignette.intensity.Override(ratio * 0.5f);
        }

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
                m_Aberration.intensity.Override(ratio * 1f);
        }
        else
        {
            if (m_LensDistortion != null)
                m_LensDistortion.intensity.Override(0f);
            if (m_Aberration != null)
                m_Aberration.intensity.Override(0f);
        }

        if (m_ColorAdjustments != null)
        {
            float satVal =
                (currentPressure > 75f) ? Mathf.Lerp(0f, -100f, (currentPressure - 75f) / 25f) : 0f;
            m_ColorAdjustments.saturation.Override(satVal);
        }

        if (playerController != null)
        {
            playerController.drunkIntensity =
                (currentPressure > 80f) ? (currentPressure - 80f) / 20f : 0f;
        }
    }

    private void CheckMegaphone()
    {
        if (currentPressure >= warningThreshold)
        {
            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.OnPressureThresholdExceeded();
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

    // Harici Erişimler
    public float GetPressure() => currentPressure;

    public float GetWarningThreshold() => warningThreshold;

    public bool IsWarningActive() => currentPressure > warningThreshold;

    // --- YENİ FONKSİYON ---
    // Jumpscare Manager bunu çağırıp PressureSystem'i susturacak
    public void StopEffectsForJumpscare()
    {
        overridePostProcessing = true;
    }

    public void LoadData(GameData data)
    {
        // Kaydedilmiş basınç değerini çek
        this.currentPressure = data.savedPressure;

        // Değer değiştiği için UI (HUD) ve Post-Processing efektlerini anında güncelle
        UpdateHUD();
        HandlePostProcessing();

        Debug.Log($"[SaveSystem] Basınç Sistemi yüklendi. Mevcut Basınç: %{currentPressure:F1}");
    }

    public void SaveData(ref GameData data)
    {
        // Şu anki basınç değerini kaydet
        data.savedPressure = this.currentPressure;
    }
}
