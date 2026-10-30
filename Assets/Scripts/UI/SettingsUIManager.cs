using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Existing scene references and UnityEvents retain this adapter; all menus share the same view prefab.
public class SettingsUIManager : MonoBehaviour
{
    public Slider masterSlider, musicSlider, sfxSlider, sensitivitySlider, brightnessSlider;
    public TMP_Dropdown resolutionDropdown, qualityDropdown, languageDropdown;
    public Toggle fullscreenToggle, subtitleToggle;
    public SettingsMenuView View { get; private set; }
    void Awake() { EnsureLanguageSelector(); }
    void OnEnable() { EnsureLanguageSelector(); }
    public void EnsureLanguageSelector()
    {
        if (View == null) View = GetComponentInChildren<SettingsMenuView>(true);
        if (View == null)
        {
            var prefab = Resources.Load<GameObject>("Settings/SettingsMenu");
            if (prefab == null) return;
            View = Instantiate(prefab, transform, false).GetComponent<SettingsMenuView>();
        }
        foreach (Transform child in transform) if (child != View.transform) child.gameObject.SetActive(false);
        View.gameObject.SetActive(true);
        View.EnsureBuilt();
    }
    public void ConfigureLocalizedLabels() { EnsureLanguageSelector(); }
    public void OnMasterVolumeChanged(float value) => SettingsManager.EnsureInstance().SetMasterVolume(value);
    public void OnMusicVolumeChanged(float value) => SettingsManager.EnsureInstance().SetMusicVolume(value);
    public void OnSFXVolumeChanged(float value) => SettingsManager.EnsureInstance().SetSFXVolume(value);
    public void OnQualityChanged(int value) => SettingsManager.EnsureInstance().SetQuality(value);
    public void OnFullscreenChanged(bool value) => SettingsManager.EnsureInstance().SetFullscreen(value);
    public void OnResolutionChanged(int value) => SettingsManager.EnsureInstance().SetResolution(value);
    public void OnSensitivityChanged(float value) => SettingsManager.EnsureInstance().SetMouseSensitivity(value);
    public void OnBrightnessChanged(float value) => SettingsManager.EnsureInstance().SetBrightness(value);
    public void OnLanguageChanged(int value) => SettingsManager.EnsureInstance().SetLanguage(value);
    public void OnSubtitlesToggled(bool value) => SettingsManager.EnsureInstance().SetSubtitlesEnabled(value);
}
