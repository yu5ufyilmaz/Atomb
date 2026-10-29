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

    private readonly List<string> languageCodes = new List<string>();
    private bool labelsConfigured;

    private void Awake()
    {
        EnsureLanguageSelector();
    }

    private void OnEnable()
    {
        EnsureLanguageSelector();
        GameLocalization.LanguageChanged -= SyncLanguageDropdown;
        GameLocalization.LanguageChanged += SyncLanguageDropdown;
        SyncLanguageDropdown();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= SyncLanguageDropdown;
    }

    private void Start()
    {
        EnsureLanguageSelector();
        // Menü açıldığında, UI elemanlarını mevcut ayarlara göre doldur
        LoadSettingsToUI();

        // Çözünürlük dropdown'ını cihazın desteklediği ekran boyutlarına göre doldur
        PopulateResolutions();
    }

    public void EnsureLanguageSelector()
    {
        if (languageDropdown == null)
        {
            Transform existing = transform.Find("Language");
            if (existing != null)
                languageDropdown = existing.GetComponentInChildren<TMP_Dropdown>(true);
        }

        if (languageDropdown == null)
        {
            TMP_Dropdown template = resolutionDropdown != null ? resolutionDropdown : qualityDropdown;
            if (template != null && template.transform.parent != null)
            {
                GameObject row = Instantiate(template.transform.parent.gameObject, template.transform.parent.parent);
                row.name = "Language";
                if (row.transform is RectTransform rowRect)
                    rowRect.anchoredPosition = new Vector2(337f, -20f);

                languageDropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
                if (languageDropdown != null)
                    languageDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();

                TMP_Text rowLabel = row.GetComponent<TMP_Text>();
                if (rowLabel == null)
                {
                    foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (text.transform.parent != row.transform) continue;
                        rowLabel = text;
                        break;
                    }
                }
                if (rowLabel != null)
                {
                    rowLabel.text = "LANGUAGE";
                    GameLocalization.Set(rowLabel, "LANGUAGE");
                }
            }
        }

        PopulateLanguages();
    }

    public void ConfigureLocalizedLabels()
    {
        if (labelsConfigured) return;
        var rows = new List<(TMP_Text label, RectTransform row, float controlLeft, float controlRight, float centerY)>();
        TMP_Text reference = null;
        var corners = new Vector3[4];
        for (int i = 0; i < transform.childCount; i++)
        {
            if (!(transform.GetChild(i) is RectTransform row)) continue;
            var control = (Component)row.GetComponentInChildren<Slider>(true)
                ?? (Component)row.GetComponentInChildren<TMP_Dropdown>(true) ?? row.GetComponentInChildren<Toggle>(true);
            if (control == null || control.transform.parent != row) continue;
            TMP_Text label = null;
            foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
                if (text.transform.parent == row) { label = text; break; }
            if (label == null) continue;
            if (label.GetComponent<LocalizedText>()?.sourceArtwork == null && label.font != null
                && (reference == null || Mathf.Abs(label.fontSize - 30f) < Mathf.Abs(reference.fontSize - 30f))) reference = label;
            ((RectTransform)control.transform).GetWorldCorners(corners);
            var min = transform.InverseTransformPoint(corners[0]);
            var max = transform.InverseTransformPoint(corners[2]);
            rows.Add((label, row, min.x, max.x, (min.y + max.y) * .5f));
        }
        if (reference == null) return;
        foreach (var row in rows)
        {
            var binding = row.label.GetComponent<LocalizedText>();
            if (binding == null) { GameLocalization.Set(row.label, row.label.text); binding = row.label.GetComponent<LocalizedText>(); }
            var overrides = new List<LocalizedText.LocaleOverride>(binding.overrides ?? System.Array.Empty<LocalizedText.LocaleOverride>());
            if (overrides.Exists(value => value != null && string.Equals(value.languageCode, "tr", System.StringComparison.OrdinalIgnoreCase))) continue;
            float columnControlLeft = row.controlLeft;
            foreach (var other in rows)
                if (Mathf.Abs(other.controlLeft - row.controlLeft) < 120f) columnControlLeft = Mathf.Min(columnControlLeft, other.controlLeft);
            float labelLeft = columnControlLeft - 220f;
            // Leave room for controls in the preceding column, including dropdown arrows.
            foreach (var member in rows)
                if (Mathf.Abs(member.controlLeft - row.controlLeft) < 120f)
                    foreach (var other in rows)
                        if (other.controlLeft < columnControlLeft - 120f && Mathf.Abs(other.centerY - member.centerY) < 44f)
                            labelLeft = Mathf.Max(labelLeft, other.controlRight + 12f);
            Vector3 position = row.row.InverseTransformPoint(transform.TransformPoint(new Vector3(labelLeft, row.centerY, 0f)));
            overrides.Add(new LocalizedText.LocaleOverride {
                languageCode = "tr", overrideFont = true, font = reference.font,
                overrideMaterial = true, material = reference.fontSharedMaterial,
                overrideFontSize = true, fontSize = 30f, overrideAutoSizing = true, autoSizing = false,
                overrideFontStyle = true, fontStyle = FontStyles.Normal,
                overrideAlignment = true, alignment = TextAlignmentOptions.Left,
                overrideCharacterSpacing = true, characterSpacing = reference.characterSpacing,
                overrideLineSpacing = true, lineSpacing = reference.lineSpacing,
                overrideWrapping = true, wrapping = TextWrappingModes.NoWrap,
                overrideAnchors = true, anchorMin = new Vector2(.5f, .5f), anchorMax = new Vector2(.5f, .5f),
                overridePivot = true, pivot = new Vector2(0f, .5f),
                overridePosition = true, anchoredPosition = (Vector2)position - row.row.rect.center,
                overrideSize = true, sizeDelta = new Vector2(columnControlLeft - labelLeft - 12f, 44f)
            });
            binding.overrides = overrides.ToArray();
            binding.Apply();
        }
        labelsConfigured = true;
    }

    private void PopulateLanguages()
    {
        if (languageDropdown == null)
            return;

        languageCodes.Clear();
        var options = new List<string>();
        var languages = GameLocalization.Languages;
        if (languages != null && languages.Count > 0)
        {
            for (int i = 0; i < languages.Count; i++)
            {
                var lang = languages[i];
                if (lang == null || string.IsNullOrWhiteSpace(lang.code)) continue;
                languageCodes.Add(lang.code);
                options.Add(!string.IsNullOrWhiteSpace(lang.nativeName) ? lang.nativeName : lang.code.ToUpperInvariant());
            }
        }

        if (languageCodes.Count == 0)
        {
            languageCodes.Add("tr"); options.Add("Türkçe");
            languageCodes.Add("en"); options.Add("English");
        }

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(options);
        languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        SyncLanguageDropdown();
    }

    private void SyncLanguageDropdown()
    {
        if (languageDropdown == null || languageCodes.Count == 0)
            return;

        string activeCode = GameLocalization.CurrentCode;
        if (SettingsManager.Instance != null && SettingsManager.Instance.currentSettings != null
            && !string.IsNullOrWhiteSpace(SettingsManager.Instance.currentSettings.languageCode))
        {
            activeCode = SettingsManager.Instance.currentSettings.languageCode;
        }

        int index = languageCodes.FindIndex(c => string.Equals(c, activeCode, System.StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = 0;
        languageDropdown.SetValueWithoutNotify(index);
        languageDropdown.RefreshShownValue();
    }

    private void LoadSettingsToUI()
    {
        EnsureLanguageSelector();
        if (SettingsManager.Instance == null)
            return;

        var data = SettingsManager.Instance.currentSettings;

        if (masterSlider)
            masterSlider.SetValueWithoutNotify(data.masterVolume);
        if (musicSlider)
            musicSlider.SetValueWithoutNotify(data.musicVolume);
        if (sfxSlider)
            sfxSlider.SetValueWithoutNotify(data.sfxVolume);

        if (qualityDropdown)
            qualityDropdown.SetValueWithoutNotify(data.qualityIndex);
        if (fullscreenToggle)
            fullscreenToggle.SetIsOnWithoutNotify(data.isFullscreen);

        if (sensitivitySlider)
            sensitivitySlider.SetValueWithoutNotify(data.mouseSensitivity);
        SyncLanguageDropdown();
        if (subtitleToggle)
            subtitleToggle.SetIsOnWithoutNotify(data.subtitlesEnabled);
        if (brightnessSlider)
            brightnessSlider.SetValueWithoutNotify(data.brightness); // <--- YENİ EKLENDİ
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
                + resolutions[i].refreshRateRatio
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
        var manager = SettingsManager.EnsureInstance();
        if (val >= 0 && val < languageCodes.Count)
            manager.SetLanguage(languageCodes[val]);
        else
            manager.SetLanguage(val);
        Save();
    }

    public void OnSubtitlesToggled(bool val)
    {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.SetSubtitlesEnabled(val);
        Save();
    }

    private void Save()
    {
        // Her ayar değiştiğinde arka planda JSON'a kaydeder
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SaveSettings();
    }
}
