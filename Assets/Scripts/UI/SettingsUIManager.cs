using System.Collections.Generic;
using TMPro; // TextMeshPro için
using UnityEngine;
using UnityEngine.UI;

public class SettingsUIManager : MonoBehaviour
{
    [Header("Ses Ayarları UI")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Grafik Ayarları UI")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown qualityDropdown;
    public Toggle fullscreenToggle;

    [Header("Oynanış Ayarları UI")]
    public Slider sensitivitySlider;
    public TMP_Dropdown languageDropdown;
    public Toggle subtitleToggle;

    [Tooltip("Parlaklık ayarı için Slider (-2 ile 2 arası olmalı)")]
    public Slider brightnessSlider; // <--- YENİ EKLENDİ

    private void Start()
    {
        // Menü açıldığında, UI elemanlarını mevcut ayarlara göre doldur
        LoadSettingsToUI();

        // Çözünürlük dropdown'ını cihazın desteklediği ekran boyutlarına göre doldur
        PopulateResolutions();
    }

    private void LoadSettingsToUI()
    {
        if (SettingsManager.Instance == null)
            return;

        var data = SettingsManager.Instance.currentSettings;

        if (masterSlider)
            masterSlider.value = data.masterVolume;
        if (musicSlider)
            musicSlider.value = data.musicVolume;
        if (sfxSlider)
            sfxSlider.value = data.sfxVolume;

        if (qualityDropdown)
            qualityDropdown.value = data.qualityIndex;
        if (fullscreenToggle)
            fullscreenToggle.isOn = data.isFullscreen;

        if (sensitivitySlider)
            sensitivitySlider.value = data.mouseSensitivity;
        if (languageDropdown)
            languageDropdown.value = data.languageIndex;
        if (subtitleToggle)
            subtitleToggle.isOn = data.subtitlesEnabled;
        if (brightnessSlider)
            brightnessSlider.value = data.brightness; // <--- YENİ EKLENDİ
    }

    private void PopulateResolutions()
    {
        if (resolutionDropdown == null)
            return;

        Resolution[] resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option =
                resolutions[i].width
                + " x "
                + resolutions[i].height
                + " @ "
                + resolutions[i].refreshRate
                + "Hz";
            options.Add(option);

            if (
                resolutions[i].width == Screen.currentResolution.width
                && resolutions[i].height == Screen.currentResolution.height
            )
            {
                currentResIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);

        // Daha önce kaydedilmiş bir çözünürlük varsa onu seç, yoksa mevcut olanı seç
        int savedResIndex = SettingsManager.Instance.currentSettings.resolutionIndex;
        if (savedResIndex >= 0 && savedResIndex < resolutions.Length)
        {
            resolutionDropdown.value = savedResIndex;
        }
        else
        {
            resolutionDropdown.value = currentResIndex;
        }
        resolutionDropdown.RefreshShownValue();
    }

    // --- UI EVENT DİNLEYİCİLERİ ---
    // Bu metotları Unity Editor'de Slider/Toggle/Dropdown'ların "OnValueChanged" eventlerine bağlayacaksın.

    public void OnMasterVolumeChanged(float val)
    {
        SettingsManager.Instance.SetMasterVolume(val);
        Save();
    }

    public void OnBrightnessChanged(float val)
    {
        SettingsManager.Instance.SetBrightness(val);
        Save();
    }

    public void OnMusicVolumeChanged(float val)
    {
        SettingsManager.Instance.SetMusicVolume(val);
        Save();
    }

    public void OnSFXVolumeChanged(float val)
    {
        SettingsManager.Instance.SetSFXVolume(val);
        Save();
    }

    public void OnQualityChanged(int val)
    {
        SettingsManager.Instance.SetQuality(val);
        Save();
    }

    public void OnFullscreenChanged(bool val)
    {
        SettingsManager.Instance.SetFullscreen(val);
        Save();
    }

    public void OnResolutionChanged(int val)
    {
        SettingsManager.Instance.SetResolution(val);
        Save();
    }

    public void OnSensitivityChanged(float val)
    {
        SettingsManager.Instance.SetMouseSensitivity(val);
        Save();
    }

    public void OnLanguageChanged(int val)
    {
        SettingsManager.Instance.SetLanguage(val);
        Save();
    }

    public void OnSubtitlesToggled(bool val)
    {
        SettingsManager.Instance.SetSubtitlesEnabled(val);
        Save();
    }

    private void Save()
    {
        // Her ayar değiştiğinde arka planda JSON'a kaydeder
        SettingsManager.Instance.SaveSettings();
    }
}
