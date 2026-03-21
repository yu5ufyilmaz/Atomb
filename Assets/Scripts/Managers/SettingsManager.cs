using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering; // Post-Processing için gerekli

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
    public bool subtitlesEnabled = true;
}

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

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
        // Unity'nin AudioMixer'ı tam yükleyebilmesi için çeyrek saniye bekle
        yield return new WaitForSeconds(0.25f);

        // Oyun ilk başladığında ayarları sisteme uygula
        ApplyAllSettings();
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
            // Dosya yoksa varsayılan ayarları oluştur ve kaydet
            currentSettings = new SettingsData();

            // Varsayılan çözünürlüğü en yüksek olarak ayarla
            currentSettings.resolutionIndex = availableResolutions.Length - 1;

            SaveSettings();
        }
    }

    // --- AYARLARI SİSTEME UYGULAMA METOTLARI ---
    public void ApplyAllSettings()
    {
        SetMasterVolume(currentSettings.masterVolume);
        SetMusicVolume(currentSettings.musicVolume);
        SetSFXVolume(currentSettings.sfxVolume);

        SetQuality(currentSettings.qualityIndex);
        SetFullscreen(currentSettings.isFullscreen);
        if (
            currentSettings.resolutionIndex >= 0
            && currentSettings.resolutionIndex < availableResolutions.Length
        )
        {
            SetResolution(currentSettings.resolutionIndex);
        }

        SetLanguage(currentSettings.languageIndex);

        // YENİ: Parlaklığı uygula
        SetBrightness(currentSettings.brightness);
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
        currentSettings.languageIndex = languageIndex;
        // GameLanguage enum sırasına göre dili ayarlar (0=TR, 1=EN, 2=DE)
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
