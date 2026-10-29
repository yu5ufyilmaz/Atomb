using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class LocalizationValidator
{
    [Serializable]
    private sealed class TableData
    {
        public LocalizationEntry[] entries;
    }

    private static readonly Regex PlaceholderRegex = new Regex(@"\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}");

    [MenuItem("Tools/Localization/Validate Localization Catalog")]
    public static void ValidateFromMenu()
    {
        int issues = ValidateCatalog(logSuccess: true);
        if (issues > 0)
        {
            Debug.LogError($"[LocalizationValidator] Validation finished with {issues} issue(s).");
        }
    }

    public static int ValidateCatalog(bool logSuccess = false)
    {
        int issues = 0;
        var languages = Resources.LoadAll<LocalizationLanguage>("Localization/Languages");
        if (languages == null || languages.Length == 0)
        {
            Debug.LogError("[LocalizationValidator] No LocalizationLanguage assets found under Resources/Localization/Languages.");
            return 1;
        }

        var keysByLang = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in languages)
        {
            if (lang == null) continue;
            if (string.IsNullOrWhiteSpace(lang.code))
            {
                Debug.LogError($"[LocalizationValidator] Language asset '{lang.name}' has an empty language code.", lang);
                issues++;
                continue;
            }

            if (lang.tables == null || lang.tables.Length == 0)
            {
                Debug.LogError($"[LocalizationValidator] Language '{lang.code}' ({lang.name}) has no assigned JSON tables.", lang);
                issues++;
                continue;
            }

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tableAsset in lang.tables)
            {
                if (tableAsset == null)
                {
                    Debug.LogError($"[LocalizationValidator] Language '{lang.code}' has a null table reference.", lang);
                    issues++;
                    continue;
                }

                TableData table;
                try
                {
                    table = JsonUtility.FromJson<TableData>(tableAsset.text);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LocalizationValidator] Failed to parse table '{tableAsset.name}': {ex.Message}", tableAsset);
                    issues++;
                    continue;
                }

                if (table == null || table.entries == null || table.entries.Length == 0)
                {
                    Debug.LogError($"[LocalizationValidator] Table '{tableAsset.name}' contains no entries.", tableAsset);
                    issues++;
                    continue;
                }

                foreach (var entry in table.entries)
                {
                    if (entry == null) continue;
                    if (!string.IsNullOrEmpty(entry.key) && !seenKeys.Add(entry.key))
                    {
                        Debug.LogError($"[LocalizationValidator] Duplicate key '{entry.key}' in '{tableAsset.name}'.", tableAsset);
                        issues++;
                    }

                    if (string.IsNullOrEmpty(entry.text))
                    {
                        Debug.LogWarning($"[LocalizationValidator] Empty translation text for key '{entry.key}' in '{tableAsset.name}'.", tableAsset);
                        issues++;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(entry.source))
                    {
                        var srcPlaceholders = ExtractPlaceholders(entry.source);
                        var txtPlaceholders = ExtractPlaceholders(entry.text);
                        if (!srcPlaceholders.SetEquals(txtPlaceholders))
                        {
                            Debug.LogError($"[LocalizationValidator] Placeholder mismatch for key '{entry.key}' in '{tableAsset.name}'. Source='{entry.source}', Text='{entry.text}'", tableAsset);
                            issues++;
                        }
                    }
                }
            }

            keysByLang[lang.code] = seenKeys;
        }

        if (keysByLang.TryGetValue("en", out var enKeys) && keysByLang.TryGetValue("tr", out var trKeys))
        {
            foreach (var key in enKeys)
            {
                if (!trKeys.Contains(key))
                {
                    Debug.LogError($"[LocalizationValidator] Key '{key}' exists in 'en' but is missing in 'tr'.");
                    issues++;
                }
            }
            foreach (var key in trKeys)
            {
                if (!enKeys.Contains(key))
                {
                    Debug.LogError($"[LocalizationValidator] Key '{key}' exists in 'tr' but is missing in 'en'.");
                    issues++;
                }
            }
        }

        if (issues == 0 && logSuccess)
        {
            Debug.Log($"[LocalizationValidator] PASS: Validated {languages.Length} language(s) with 0 issues.");
        }

        return issues;
    }

    private static HashSet<string> ExtractPlaceholders(string input)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(input)) return set;
        foreach (Match match in PlaceholderRegex.Matches(input))
        {
            set.Add(match.Groups[1].Value);
        }
        return set;
    }
}
