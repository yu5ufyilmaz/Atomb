using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering; // Post-Processing için gerekli
using UnityEngine.SceneManagement;

// 1. Ayarlarımızı JSON olarak kaydedebilmek için bir veri sınıfı oluşturuyoruz
[System.Serializable]
public class SettingsData
{
    // Ses Ayarları
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;

    // Grafik Ayarları
    public int qualityIndex = 2;
    public bool isFullscreen = true;
    public int resolutionIndex = -1;

    // --- YENİ: Parlaklık (Exposure) ---
    // Değeri 0 varsayılan kabul edeceğiz (Genelde -2 ile +2 arası çalışır)
    public float brightness = 0f;

    // Oynanış ve Kontrol
    public float mouseSensitivity = 1f;
    public int languageIndex = 0;
    public string languageCode = "";
    public bool subtitlesEnabled = true;
}

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    public static SettingsManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var existing = Object.FindFirstObjectByType<SettingsManager>();
        if (existing != null) return existing;
        var go = new GameObject("SettingsManager");
        return go.AddComponent<SettingsManager>();
    }

    public static string CodeFromLegacyIndex(int index)
    {
        switch (index)
        {
            case 0: return "tr";
            case 2: return "de";
            default: return "en";
        }
    }

    public static int LegacyIndexFromCode(string code)
    {
        if (string.Equals(code, "tr", System.StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(code, "de", System.StringComparison.OrdinalIgnoreCase)) return 2;
        return 1;
    }

    [Header("Referanslar")]
    public AudioMixer mainAudioMixer;

    [Tooltip("Sahnede Post-Processing ayarlarını barındıran Global Volume objesi")]
    public Volume globalVolume;

    public SettingsData currentSettings;

    private string saveFilePath;
    private Resolution[] availableResolutions;

    private void Awake()
    {
        // Singleton yapısı (Sahneler arası silinmesin)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            if (Instance.mainAudioMixer == null && mainAudioMixer != null)
                Instance.mainAudioMixer = mainAudioMixer;
            if (Instance.globalVolume == null && globalVolume != null)
                Instance.globalVolume = globalVolume;
            Destroy(gameObject);
            return;
        }

        // Kayıt dosyasının yolu: AppData/LocalLow/SeninSirketin/Senzora/settings.json
        saveFilePath = Path.Combine(Application.persistentDataPath, "SettingsSave.json");

        // Cihazın desteklediği çözünürlükleri al
        availableResolutions = Screen.resolutions;

        LoadSettings();
    }

    private IEnumerator Start()
    {
        ApplyGraphicsSettings();
        // Unity'nin AudioMixer'ı tam yükleyebilmesi için çeyrek saniye bekle
        yield return new WaitForSeconds(0.25f);

        // Oyun ilk başladığında ayarları sisteme uygula
        ApplyAudioSettings();
    }

    private void OnEnable()
    {
        // Script aktif olduğunda sahne yüklenme olayına abone ol
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Script kapandığında abonelikten çık
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Sahne her baştan yüklendiğinde (öldükten sonra vb.) sesleri tekrar uygula
        StartCoroutine(ReapplyAudioRoutine());
    }

    private IEnumerator ReapplyAudioRoutine()
    {
        // AudioMixer'ın yeni sahneye tam oturması için çok kısa bir süre bekle
        yield return new WaitForSeconds(0.1f);
        ApplyAudioSettings();
    }

    // --- KAYIT VE YÜKLEME (JSON) ---
    public void SaveSettings()
    {
        string json = JsonUtility.ToJson(currentSettings, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("Ayarlar Kaydedildi: " + saveFilePath);
    }

    public void LoadSettings()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            currentSettings = JsonUtility.FromJson<SettingsData>(json);
        }
        else
        {
            // OYUNCU OYUNA İLK DEFA GİRİYOR (JSON DOSYASI YOK)
            currentSettings = new SettingsData();

            // Varsayılan olarak tam ekran başlasın
            currentSettings.isFullscreen = true;

            // Listede 1920x1080 çözünürlüğünü ara
            int targetResIndex = availableResolutions.Length - 1; // Bulamazsa en yükseği seçer
            for (int i = 0; i < availableResolutions.Length; i++)
            {
                if (availableResolutions[i].width == 1920 && availableResolutions[i].height == 1080)
                {
                    targetResIndex = i; // 1920x1080 bulundu!
                }
            }

            currentSettings.resolutionIndex = targetResIndex;
            currentSettings.languageCode = CodeFromLegacyIndex(currentSettings.languageIndex);

            SaveSettings();
        }

        NormalizeLanguageSetting();
        GameLocalization.SetLanguage(currentSettings.languageCode);
    }

    private void NormalizeLanguageSetting()
    {
        if (currentSettings == null)
            currentSettings = new SettingsData();
        if (string.IsNullOrWhiteSpace(currentSettings.languageCode))
            currentSettings.languageCode = CodeFromLegacyIndex(currentSettings.languageIndex);
        else
            currentSettings.languageCode = currentSettings.languageCode.Trim().ToLowerInvariant();
        currentSettings.languageIndex = LegacyIndexFromCode(currentSettings.languageCode);
    }

    // --- AYARLARI SİSTEME UYGULAMA METOTLARI ---
    public void ApplyAllSettings()
    {
        ApplyGraphicsSettings();
        ApplyAudioSettings();
    }

    private void ApplyGraphicsSettings()
    {
        SetQuality(currentSettings.qualityIndex);
        SetFullscreen(currentSettings.isFullscreen);

        if (
            currentSettings.resolutionIndex >= 0
            && currentSettings.resolutionIndex < availableResolutions.Length
        )
        {
            SetResolution(currentSettings.resolutionIndex);
        }

        NormalizeLanguageSetting();
        SetLanguage(currentSettings.languageCode);
        SetBrightness(currentSettings.brightness);
    }

    // Sesleri ayırdık ki Start içinde çeyrek saniye bekleyip sorunsuz çalışabilsin
    private void ApplyAudioSettings()
    {
        SetMasterVolume(currentSettings.masterVolume);
        SetMusicVolume(currentSettings.musicVolume);
        SetSFXVolume(currentSettings.sfxVolume);
    }

    // -- SES AYARLARI -- (AudioMixer'daki parametre isimleri Master, Music, SFX olmalı)
    public void SetMasterVolume(float volume)
    {
        currentSettings.masterVolume = volume;
        mainAudioMixer?.SetFloat("Master", Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
    }

    public void SetMusicVolume(float volume)
    {
        currentSettings.musicVolume = volume;
        mainAudioMixer?.SetFloat("Music", Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
    }

    public void SetSFXVolume(float volume)
    {
        currentSettings.sfxVolume = volume;
        mainAudioMixer?.SetFloat("SFX", Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
    }

    // -- GRAFİK AYARLARI --
    public void SetQuality(int qualityIndex)
    {
        currentSettings.qualityIndex = qualityIndex;
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        currentSettings.isFullscreen = isFullscreen;
        Screen.fullScreen = isFullscreen;
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex >= 0 && resolutionIndex < availableResolutions.Length)
        {
            currentSettings.resolutionIndex = resolutionIndex;
            Resolution res = availableResolutions[resolutionIndex];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }
    }

    // -- OYNANIŞ AYARLARI --
    public void SetMouseSensitivity(float sensitivity)
    {
        currentSettings.mouseSensitivity = sensitivity;
        // Not: StarterAssetsInputs içindeki Look hassasiyetini buraya bağlayacağız
    }

    public void SetLanguage(int languageIndex)
    {
        if (currentSettings == null)
            currentSettings = new SettingsData();
        var languages = GameLocalization.Languages;
        if (languages != null && languageIndex >= 0 && languageIndex < languages.Count)
        {
            SetLanguage(languages[languageIndex].code);
            return;
        }
        currentSettings.languageIndex = languageIndex;
        currentSettings.languageCode = CodeFromLegacyIndex(languageIndex);
        GameLocalization.SetLanguage(currentSettings.languageCode);
    }

    public void SetLanguage(string languageCode)
    {
        if (currentSettings == null)
            currentSettings = new SettingsData();
        currentSettings.languageCode = string.IsNullOrWhiteSpace(languageCode)
            ? "en"
            : languageCode.Trim().ToLowerInvariant();
        currentSettings.languageIndex = LegacyIndexFromCode(currentSettings.languageCode);
        GameLocalization.SetLanguage(currentSettings.languageCode);
    }

    public void SetSubtitlesEnabled(bool isEnabled)
    {
        currentSettings.subtitlesEnabled = isEnabled;
    }

    public void SetBrightness(float value)
    {
        currentSettings.brightness = value;

        if (globalVolume != null && globalVolume.profile != null)
        {
            // HDRP ColorAdjustments bileşenini bulmaya çalış
            if (
                globalVolume.profile.TryGet(
                    out UnityEngine.Rendering.HighDefinition.ColorAdjustments colorAdjustments
                )
            )
            {
                // Post Exposure (Pozlama) değerini değiştir
                colorAdjustments.postExposure.value = value;
            }
        }
    }
}
