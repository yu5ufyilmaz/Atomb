using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class DisplaySettings
{
    public int width, height;
    public int mode = (int)FullScreenMode.FullScreenWindow;
    public uint refreshNumerator = 60, refreshDenominator = 1;
    public DisplaySettings Copy() => (DisplaySettings)MemberwiseClone();
    public double Hz => refreshDenominator == 0 || refreshNumerator == 0 ? 60 : (double)refreshNumerator / refreshDenominator;
    public RefreshRate RefreshRate => refreshNumerator == 0 || refreshDenominator == 0 ? new RefreshRate { numerator = 60, denominator = 1 } : new RefreshRate { numerator = refreshNumerator, denominator = refreshDenominator };
    public bool SameAs(DisplaySettings other) => other != null && width == other.width && height == other.height && mode == other.mode && Math.Abs(Hz - other.Hz) < .1;
    public static DisplaySettings Current() => new DisplaySettings { width = Screen.width, height = Screen.height, mode = (int)Screen.fullScreenMode,
        refreshNumerator = Screen.currentResolution.refreshRateRatio.numerator, refreshDenominator = Screen.currentResolution.refreshRateRatio.denominator };
}

[Serializable]
public class SettingsData
{
    public int schemaVersion;
    public float masterVolume = 1, musicVolume = 1, sfxVolume = 1, voiceVolume = 1;
    public int qualityIndex = 1;
    public bool isFullscreen = true;
    public int resolutionIndex = -1;
    public DisplaySettings display;
    public bool vSync = true;
    public int frameRateLimit = 144;
    public float brightness;
    public bool motionBlur = true, filmGrain = true, depthOfField = true;
    public float headBob = 1;
    public float mouseSensitivity = 1, gamepadSensitivity = 1, gamepadDeadzone = .125f;
    public bool invertMouseY, invertGamepadY;
    public int languageIndex;
    public string languageCode = "";
    public bool subtitlesEnabled = true;
    public int subtitleSize = 1;
    public float subtitleBackground = .5f;
    public SettingsData Copy() { var copy = (SettingsData)MemberwiseClone(); copy.display = display?.Copy(); return copy; }
    public static SettingsData Parse(string json, Resolution[] resolutions)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") || !json.TrimEnd().EndsWith("}")) throw new InvalidDataException("Invalid settings JSON");
        var data = new SettingsData(); JsonUtility.FromJsonOverwrite(json, data);
        if (data.display == null)
        {
            data.display = DisplaySettings.Current();
            data.display.mode = (int)(data.isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            if (resolutions != null && data.resolutionIndex >= 0 && data.resolutionIndex < resolutions.Length)
            {
                var res = resolutions[data.resolutionIndex]; data.display.width = res.width; data.display.height = res.height;
                data.display.refreshNumerator = res.refreshRateRatio.numerator; data.display.refreshDenominator = res.refreshRateRatio.denominator;
            }
        }
        data.Normalize(QualitySettings.names.Length); return data;
    }
    static float Bounded(float value, float min, float max, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    public void Normalize(int qualityCount)
    {
        masterVolume = Bounded(masterVolume, 0, 1, 1); musicVolume = Bounded(musicVolume, 0, 1, 1); sfxVolume = Bounded(sfxVolume, 0, 1, 1); voiceVolume = Bounded(voiceVolume, 0, 1, 1);
        brightness = Bounded(brightness, -2, 2, 0); headBob = Bounded(headBob, 0, 1, 1);
        mouseSensitivity = Bounded(mouseSensitivity, .1f, 5, 1); gamepadSensitivity = Bounded(gamepadSensitivity, .1f, 5, 1);
        gamepadDeadzone = Bounded(gamepadDeadzone, .05f, .4f, .125f); subtitleBackground = Bounded(subtitleBackground, 0, 1, .5f);
        qualityIndex = Mathf.Clamp(qualityIndex, 0, Math.Max(0, qualityCount - 1)); subtitleSize = Mathf.Clamp(subtitleSize, 0, 2);
        if (Array.IndexOf(new[] { 30, 60, 90, 120, 144, 165, 240, -1 }, frameRateLimit) < 0) frameRateLimit = 144;
        if (string.IsNullOrWhiteSpace(languageCode)) languageCode = SettingsManager.CodeFromLegacyIndex(languageIndex);
        languageCode = languageCode.Trim().ToLowerInvariant(); languageIndex = SettingsManager.LegacyIndexFromCode(languageCode);
        if (display == null) { display = DisplaySettings.Current(); display.mode = (int)(isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed); }
        display.width = Mathf.Clamp(display.width, 640, 16384); display.height = Mathf.Clamp(display.height, 480, 16384);
        if (display.mode != (int)FullScreenMode.Windowed && display.mode != (int)FullScreenMode.FullScreenWindow && display.mode != (int)FullScreenMode.ExclusiveFullScreen) display.mode = (int)FullScreenMode.FullScreenWindow;
        if (display.refreshNumerator == 0 || display.refreshDenominator == 0 || display.Hz < 20 || display.Hz > 1000) { display.refreshNumerator = 60; display.refreshDenominator = 1; }
        isFullscreen = display.mode != (int)FullScreenMode.Windowed; schemaVersion = 2;
    }
}

public static class SettingsStore
{
    public static void Write(string path, SettingsData data)
    {
        string temp = path + ".tmp";
        try { File.WriteAllText(temp, JsonUtility.ToJson(data, true)); if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
