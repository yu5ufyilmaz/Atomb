using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameLocalization
{
    [Serializable] private sealed class Table { public LocalizationEntry[] entries; }
    private static readonly List<LocalizationLanguage> languages = new List<LocalizationLanguage>();
    private static LocalizationCatalog catalog;
    private static string currentCode = "en";
    private static bool initialized;
    public static event Action LanguageChanged;
    public static int Revision { get; private set; }
    public static string CurrentCode { get { Initialize(); return currentCode; } }
    public static IReadOnlyList<LocalizationLanguage> Languages { get { Initialize(); return languages; } }
    public static LocalizationLanguage CurrentLanguage { get { Initialize(); return Find(currentCode); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        initialized = false;
        catalog = null;
        languages.Clear();
        currentCode = "en";
        Revision = 0;
        LanguageChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        LoadLanguages();
        currentCode = LoadSavedLanguageCode();
        RebuildCatalog();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static string LoadSavedLanguageCode()
    {
        try
        {
            if (SettingsManager.Instance != null && SettingsManager.Instance.currentSettings != null)
            {
                var settings = SettingsManager.Instance.currentSettings;
                string code = !string.IsNullOrWhiteSpace(settings.languageCode)
                    ? settings.languageCode
                    : SettingsManager.CodeFromLegacyIndex(settings.languageIndex);
                return ResolveCode(code);
            }
            string path = System.IO.Path.Combine(Application.persistentDataPath, "SettingsSave.json");
            if (System.IO.File.Exists(path))
            {
                var saved = JsonUtility.FromJson<SettingsData>(System.IO.File.ReadAllText(path));
                if (saved != null)
                {
                    string code = !string.IsNullOrWhiteSpace(saved.languageCode)
                        ? saved.languageCode
                        : SettingsManager.CodeFromLegacyIndex(saved.languageIndex);
                    return ResolveCode(code);
                }
            }
        }
        catch (Exception) { }
        return ResolveCode(currentCode);
    }

    public static void ReloadLanguages()
    {
        Initialize();
        LoadLanguages();
        currentCode = ResolveCode(currentCode);
        RebuildCatalog();
        Revision++;
        LanguageChanged?.Invoke();
    }

    public static void SetLanguage(string code)
    {
        Initialize();
        string resolved = ResolveCode(code);
        if (string.Equals(resolved, currentCode, StringComparison.OrdinalIgnoreCase)) return;
        currentCode = resolved;
        RebuildCatalog();
        Revision++;
        LanguageChanged?.Invoke();
    }

    public static string Get(string key, string fallback = null) { Initialize(); return GameInput.ResolveHints(catalog.Get(key, fallback)); }
    public static string Translate(string source) { Initialize(); return GameInput.ResolveHints(catalog.Translate(source)); }
    public static string Format(string canonicalFormat, params object[] arguments) { Initialize(); return GameInput.ResolveHints(catalog.Format(canonicalFormat, arguments)); }

    public static void Set(TMP_Text target, string canonical)
    {
        if (target == null) return;
        var binding = target.GetComponent<LocalizedText>();
        if (binding == null) binding = target.gameObject.AddComponent<LocalizedText>();
        binding.target = target;
        var parent = target.transform.parent;
        if (parent != null && parent.GetComponent<Button>() != null
            && (target.name == "TextThin" || target.name == "TextBold")
            && parent.Find("TextThin") != null && parent.Find("TextBold") != null)
            binding.preserveSourceLeftEdge = true;
        binding.SetSource(canonical);
    }

    public static void SetFormat(TMP_Text target, string canonicalFormat, params object[] arguments)
    {
        if (target == null) return;
        var binding = target.GetComponent<LocalizedText>();
        if (binding == null) binding = target.gameObject.AddComponent<LocalizedText>();
        binding.target = target;
        binding.SetFormat(canonicalFormat, arguments);
    }

    public static void BindScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var menu in root.GetComponentsInChildren<MainMenuManager>(true))
                menu.EnsureSettingsAccess();
            foreach (var settingsUI in root.GetComponentsInChildren<SettingsUIManager>(true))
                settingsUI.EnsureLanguageSelector();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponentInParent<SettingsMenuView>(true) != null) continue;
                if (text.GetComponent<LocalizedText>() != null || text.GetComponentInParent<TMP_Dropdown>(true) != null || text.GetComponentInParent<TMP_InputField>(true) != null) continue;
                var subtitles = text.GetComponentInParent<GlobalSubtitleManager>(true);
                if (subtitles != null && subtitles.subtitleText == text) continue;
                if (GlobalSubtitleManager.Instance != null && GlobalSubtitleManager.Instance.subtitleText == text) continue;
                Set(text, text.text);
            }
            LocalizeSettingsArtwork(root);
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { BindScene(scene); }

    private static void LoadLanguages()
    {
        languages.Clear();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in Resources.LoadAll<LocalizationLanguage>("Localization/Languages"))
            if (language != null && !string.IsNullOrWhiteSpace(language.code) && codes.Add(language.code)) languages.Add(language);
        languages.Sort((a, b) => { int order = a.sortOrder.CompareTo(b.sortOrder); return order == 0 ? string.CompareOrdinal(a.code, b.code) : order; });
    }

    private static LocalizationLanguage Find(string code)
    {
        return languages.Find(language => string.Equals(language.code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveCode(string code)
    {
        if (!string.IsNullOrEmpty(code))
        {
            var language = Find(code);
            if (language != null) return language.code;
            int separator = code.IndexOf('-');
            if (separator > 0 && (language = Find(code.Substring(0, separator))) != null) return language.code;
        }
        return Find("en") != null ? "en" : languages.Count > 0 ? languages[0].code : "en";
    }

    private static void RebuildCatalog()
    {
        var entries = new List<LocalizationEntry>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddLanguageEntries(Find(currentCode), visited, entries);
        AddLanguageEntries(Find("en"), visited, entries);
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(currentCode); }
        catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
        catalog = new LocalizationCatalog(entries, culture);
    }

    private static void AddLanguageEntries(LocalizationLanguage language, HashSet<string> visited, List<LocalizationEntry> entries)
    {
        if (language == null || !visited.Add(language.code)) return;
        if (language.tables != null)
            foreach (var asset in language.tables)
            {
                if (asset == null) continue;
                try
                {
                    var table = JsonUtility.FromJson<Table>(asset.text);
                    if (table != null && table.entries != null) entries.AddRange(table.entries);
                }
                catch (Exception exception) { Debug.LogError("Invalid localization table " + asset.name + ": " + exception.Message, asset); }
            }
        if (!string.IsNullOrEmpty(language.fallbackCode)) AddLanguageEntries(Find(language.fallbackCode), visited, entries);
    }

    private static void LocalizeSettingsArtwork(GameObject root)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponentInParent<SettingsMenuView>(true) != null) continue;
            if (!image.enabled || image.sprite == null || image.transform.Find("LocalizedTitle") != null) continue;
            string source;
            switch (image.sprite.name)
            {
                case "Sound": source = "Sound"; break;
                case "Music": source = "Music"; break;
                case "Quality": source = "Quality"; break;
                case "FullScreen": source = "Full Screen"; break;
                case "GamePaused": source = "Game Paused"; break;
                default: continue;
            }
            // Some rows reuse a sprite but already have their own title (Resolution, Subtitle, Tutorial).
            bool hasTitle = false;
            foreach (var text in image.GetComponentsInChildren<TMP_Text>(true))
                if (text.transform.parent == image.transform) { hasTitle = true; break; }
            if (hasTitle) continue;

            TMP_Text reference = null;
            var parent = image.transform.parent;
            if (parent != null)
                foreach (var text in parent.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name != "LocalizedTitle" && text.font != null) { reference = text; break; }
            var labelObject = new GameObject("LocalizedTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(image.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (reference != null)
            {
                label.font = reference.font;
                label.fontSharedMaterial = reference.fontSharedMaterial;
                label.fontStyle = reference.fontStyle;
            }
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.fontSize = Mathf.Max(14f, image.rectTransform.rect.height);
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = label.fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = image.color;
            label.raycastTarget = false;
            Set(label, source);
            var binding = label.GetComponent<LocalizedText>();
            binding.sourceArtwork = image;
            binding.Apply();
        }
        foreach (var settings in root.GetComponentsInChildren<SettingsUIManager>(true)) settings.ConfigureLocalizedLabels();
    }
}
