using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;
    public static SettingsData Current => Instance != null ? Instance.currentSettings : null;
    public AudioMixer mainAudioMixer;
    public AudioMixerGroup voiceGroup, effectsGroup;
    public Volume globalVolume;
    public SettingsData currentSettings;
    public event Action Changed;
    public event Action SaveFailed;
    public string LastSaveError { get; private set; }
    string saveFilePath;
    DisplaySettings pendingDisplay;
    double displayDeadline;
    Volume preferenceVolume;
    ColorAdjustments color;
    MotionBlur blur;
    FilmGrain grain;
    bool savePending;
    float saveDeadline;
    public bool IsTestingDisplay => pendingDisplay != null;
    public float DisplayTestRemaining => IsTestingDisplay ? Mathf.Max(0, (float)(displayDeadline - Time.realtimeSinceStartupAsDouble)) : 0;
    public DisplaySettings EffectiveDisplay => (pendingDisplay ?? currentSettings.display).Copy();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() { EnsureInstance(); }
    public static SettingsManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var existing = FindFirstObjectByType<SettingsManager>();
        return existing != null ? existing : new GameObject("SettingsManager").AddComponent<SettingsManager>();
    }
    public static string CodeFromLegacyIndex(int index) => index == 0 ? "tr" : index == 2 ? "de" : "en";
    public static int LegacyIndexFromCode(string code) => string.Equals(code, "tr", StringComparison.OrdinalIgnoreCase) ? 0 : string.Equals(code, "de", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (mainAudioMixer != null) Instance.mainAudioMixer = mainAudioMixer;
            if (voiceGroup != null) Instance.voiceGroup = voiceGroup;
            if (effectsGroup != null) Instance.effectsGroup = effectsGroup;
            Destroy(gameObject); return;
        }
        Instance = this;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        var style = Resources.Load<SettingsMenuStyle>("Settings/SettingsMenuStyle");
        if (style != null) { mainAudioMixer = style.mixer; voiceGroup = style.voice; effectsGroup = style.effects; }
        saveFilePath = Path.Combine(Application.persistentDataPath, "SettingsSave.json"); LoadSettings();
    }
    IEnumerator Start() { ApplyAllSettings(); yield return null; ApplyAudioSettings(); }
    void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; }
    void OnDestroy()
    {
        if (Instance != this) return;
        if (preferenceVolume != null && preferenceVolume.sharedProfile != null) Destroy(preferenceVolume.sharedProfile);
        Instance = null;
    }
    void SceneLoaded(Scene scene, LoadSceneMode mode) { RevertDisplayTest(); ApplyPreferences(); StartCoroutine(SceneAudio()); }
    IEnumerator SceneAudio() { yield return null; ApplyAudioSettings(); }
    void Update()
    {
        if (IsTestingDisplay && DisplayTestRemaining <= 0) RevertDisplayTest();
        if (savePending && Time.unscaledTime >= saveDeadline) SaveSettings();
    }
    void OnApplicationQuit() { RevertDisplayTest(); if (savePending) SaveSettings(); }
    public void LoadSettings()
    {
        try { currentSettings = File.Exists(saveFilePath) ? SettingsData.Parse(File.ReadAllText(saveFilePath), Screen.resolutions) : new SettingsData(); }
        catch (Exception error)
        {
            Debug.LogWarning("Settings could not be loaded: " + error.Message);
            try { if (File.Exists(saveFilePath)) File.Copy(saveFilePath, saveFilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")); }
            catch (Exception backupError) { Debug.LogWarning(backupError.Message); }
            currentSettings = new SettingsData();
        }
        currentSettings.Normalize(QualitySettings.names.Length); currentSettings.display = SupportedDisplay(currentSettings.display);
        GameLocalization.SetLanguage(currentSettings.languageCode);
        currentSettings.languageCode=GameLocalization.CurrentCode;currentSettings.languageIndex=LegacyIndexFromCode(currentSettings.languageCode);
    }
    public void SaveSettings()
    {
        savePending = false;
        try { SettingsStore.Write(saveFilePath, currentSettings); LastSaveError = null; }
        catch (Exception error) { LastSaveError = error.Message; Debug.LogWarning("Settings could not be saved: " + error.Message); SaveFailed?.Invoke(); }
    }
    public void ChangedAndSave()
    {
        currentSettings.Normalize(QualitySettings.names.Length); ApplyPreferences(); Changed?.Invoke(); savePending = true; saveDeadline = Time.unscaledTime + .35f;
    }
    public void ApplyAllSettings() { QualitySettings.SetQualityLevel(currentSettings.qualityIndex); ApplyDisplay(currentSettings.display); ApplyPreferences(); }
    public void ApplyPreferences()
    {
        QualitySettings.vSyncCount = currentSettings.vSync ? 1 : 0; Application.targetFrameRate = currentSettings.vSync ? -1 : currentSettings.frameRateLimit;
        ApplyVolume(); ApplyAudioSettings(); GameInput.ApplyControlSettings(currentSettings.gamepadDeadzone);
    }
    void ApplyVolume()
    {
        if (preferenceVolume == null)
        {
            var go = new GameObject("User display preferences"); go.transform.SetParent(transform, false);
            preferenceVolume = go.AddComponent<Volume>(); preferenceVolume.isGlobal = true; preferenceVolume.priority = 100000;
            preferenceVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            color = preferenceVolume.sharedProfile.Add<ColorAdjustments>(); blur = preferenceVolume.sharedProfile.Add<MotionBlur>(); grain = preferenceVolume.sharedProfile.Add<FilmGrain>();
        }
        color.postExposure.Override(currentSettings.brightness); blur.active = !currentSettings.motionBlur; blur.intensity.Override(0); grain.active = !currentSettings.filmGrain; grain.intensity.Override(0);
    }
    static float Decibels(float value) => value <= 0 ? -80 : Mathf.Log10(value) * 20;
    public void ApplyAudioSettings()
    {
        if (mainAudioMixer == null) return;
        mainAudioMixer.SetFloat("Master", Decibels(currentSettings.masterVolume)); mainAudioMixer.SetFloat("Music", Decibels(currentSettings.musicVolume));
        mainAudioMixer.SetFloat("SFX", Decibels(currentSettings.sfxVolume)); mainAudioMixer.SetFloat("Voice", Decibels(currentSettings.voiceVolume));
        AudioListener.volume = currentSettings.masterVolume == 0 ? 0 : 1;
    }
    public void RouteVoice(AudioSource source) { if (source != null && voiceGroup != null) source.outputAudioMixerGroup = voiceGroup; }
    public static void PlayEffectAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        if(clip==null)return;
        var go=new GameObject("One shot effect");go.transform.position=position;
        var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.volume=Mathf.Clamp01(volume);source.clip=clip;
        source.outputAudioMixerGroup=EnsureInstance().effectsGroup;source.Play();Destroy(go,clip.length);
    }
    public void SetMasterVolume(float value) { currentSettings.masterVolume = value; ChangedAndSave(); }
    public void SetMusicVolume(float value) { currentSettings.musicVolume = value; ChangedAndSave(); }
    public void SetSFXVolume(float value) { currentSettings.sfxVolume = value; ChangedAndSave(); }
    public void SetQuality(int value) { currentSettings.qualityIndex = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1); QualitySettings.SetQualityLevel(currentSettings.qualityIndex); ChangedAndSave(); }
    public void SetBrightness(float value) { currentSettings.brightness = value; ChangedAndSave(); }
    public void SetMouseSensitivity(float value) { currentSettings.mouseSensitivity = value; ChangedAndSave(); }
    public void SetSubtitlesEnabled(bool value) { currentSettings.subtitlesEnabled = value; ChangedAndSave(); }
    public void SetLanguage(int index) { var list = GameLocalization.Languages; SetLanguage(index >= 0 && index < list.Count ? list[index].code : CodeFromLegacyIndex(index)); }
    public void SetLanguage(string code)
    {
        GameLocalization.SetLanguage(code); currentSettings.languageCode = GameLocalization.CurrentCode; currentSettings.languageIndex = LegacyIndexFromCode(currentSettings.languageCode); ChangedAndSave();
    }
    public void SetFullscreen(bool value) { var next = currentSettings.display.Copy(); next.mode = (int)(value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed); BeginDisplayTest(next); }
    public void SetResolution(int index)
    {
        if (index < 0 || index >= Screen.resolutions.Length) return;
        var next = currentSettings.display.Copy(); var res = Screen.resolutions[index]; next.width = res.width; next.height = res.height;
        next.refreshNumerator = res.refreshRateRatio.numerator; next.refreshDenominator = res.refreshRateRatio.denominator; BeginDisplayTest(next);
    }
    public static DisplaySettings SupportedDisplay(DisplaySettings value)
    {
        var result = value.Copy(); var resolutions = Screen.resolutions;
        if (resolutions.Length > 0)
        {
            var res = resolutions.OrderBy(r => Math.Abs((long)r.width - result.width) + Math.Abs((long)r.height - result.height))
                .ThenBy(r => Math.Abs(r.refreshRateRatio.value - result.Hz)).First();
            result.width = res.width; result.height = res.height; result.refreshNumerator = res.refreshRateRatio.numerator; result.refreshDenominator = res.refreshRateRatio.denominator;
        }
        if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor && result.mode == (int)FullScreenMode.ExclusiveFullScreen) result.mode = (int)FullScreenMode.FullScreenWindow;
        return result;
    }
    static void ApplyDisplay(DisplaySettings value)
    {
        var rate = value.mode == (int)FullScreenMode.FullScreenWindow ? Screen.currentResolution.refreshRateRatio : value.RefreshRate;
        Screen.SetResolution(value.width, value.height, (FullScreenMode)value.mode, rate);
    }
    public void BeginDisplayTest(DisplaySettings value) { pendingDisplay = SupportedDisplay(value); displayDeadline = Time.realtimeSinceStartupAsDouble + 15; ApplyDisplay(pendingDisplay); Changed?.Invoke(); }
    public void ConfirmDisplayTest()
    {
        if (!IsTestingDisplay) return;
        currentSettings.display = pendingDisplay.Copy(); currentSettings.isFullscreen = pendingDisplay.mode != (int)FullScreenMode.Windowed; pendingDisplay = null; Changed?.Invoke(); SaveSettings();
    }
    public void RevertDisplayTest() { if (!IsTestingDisplay) return; pendingDisplay = null; ApplyDisplay(currentSettings.display); Changed?.Invoke(); }
    public void ResetSection(string section)
    {
        var d = currentSettings;
        switch (section)
        {
            case "audio": d.masterVolume = d.musicVolume = d.sfxVolume = d.voiceVolume = 1; break;
            case "video": d.qualityIndex = Math.Min(1, QualitySettings.names.Length - 1); d.vSync = true; d.frameRateLimit = 144; d.brightness = 0; d.motionBlur = d.filmGrain = true; d.headBob = 1; QualitySettings.SetQualityLevel(d.qualityIndex); break;
            case "controls": d.mouseSensitivity = d.gamepadSensitivity = 1; d.invertMouseY = d.invertGamepadY = false; d.gamepadDeadzone = .125f; GameInput.ResetBindings(); break;
            case "language": d.subtitlesEnabled = true; d.subtitleSize = 1; d.subtitleBackground = .5f; SetLanguage("en"); break;
        }
        ChangedAndSave();
    }
}
