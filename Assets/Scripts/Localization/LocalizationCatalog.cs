using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

[Serializable]
public sealed class LocalizationEntry
{
    public string key;
    public string source;
    public string text;
}

// Kept independent of Unity so imported tables and legacy saved prose share one resolver.
public sealed class LocalizationCatalog
{
    private sealed class Pattern
    {
        public Regex regex;
        public string translation;
        public int argumentCount;
        public int specificity;
    }

    private static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}");
    private static readonly Regex RichTag = new Regex(@"(</?[a-zA-Z][a-zA-Z0-9-]*(?:=[^<>]*|\s[^<>]*)?>)");
    private static readonly string[] Separators = { "\r\n", "\n", " • ", " · ", " → ", " / ", ", ", ": ", "• ", " (" };
    private readonly Dictionary<string, string> keys = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<Pattern> patterns = new List<Pattern>();
    private readonly CultureInfo culture;

    public LocalizationCatalog(IEnumerable<LocalizationEntry> entries, CultureInfo culture)
    {
        this.culture = culture ?? CultureInfo.InvariantCulture;
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.text)) continue;
            if (!string.IsNullOrEmpty(entry.key) && !keys.ContainsKey(entry.key)) keys.Add(entry.key, entry.text);
            if (string.IsNullOrEmpty(entry.source) || sources.ContainsKey(entry.source)) continue;
            sources.Add(entry.source, entry.text);
            AddPattern(entry.source, entry.text);
        }
        patterns.Sort((a, b) => b.specificity.CompareTo(a.specificity));
    }

    public string Get(string key, string fallback)
    {
        string value;
        return !string.IsNullOrEmpty(key) && keys.TryGetValue(key, out value)
            ? value : Translate(fallback ?? key ?? string.Empty);
    }

    public string Translate(string source) { return Translate(source, 0); }

    public string Format(string canonicalFormat, params object[] arguments)
    {
        var translated = Translate(canonicalFormat);
        if (arguments == null) return translated;
        var values = new object[arguments.Length];
        for (int i = 0; i < values.Length; i++)
            values[i] = arguments[i] is string ? Translate((string)arguments[i]) : arguments[i];
        try { return string.Format(culture, translated, values); }
        catch (FormatException) { return canonicalFormat ?? string.Empty; }
    }

    private string Translate(string source, int depth)
    {
        if (string.IsNullOrEmpty(source) || depth > 10) return source ?? string.Empty;
        string translated;
        if (sources.TryGetValue(source, out translated)) return translated;
        string trimmed = source.Trim();
        if (trimmed.Length != source.Length && sources.TryGetValue(trimmed, out translated))
            return source.Substring(0, source.IndexOf(trimmed, StringComparison.Ordinal)) + translated + source.Substring(source.IndexOf(trimmed, StringComparison.Ordinal) + trimmed.Length);

        // Whole templates only: never replace fragments inside identifiers or measurement codes.
        foreach (var pattern in patterns)
        {
            var match = pattern.regex.Match(source);
            if (!match.Success) continue;
            var arguments = new object[pattern.argumentCount];
            for (int i = 0; i < arguments.Length; i++) arguments[i] = Translate(match.Groups["p" + i].Value, depth + 1);
            try { return string.Format(culture, pattern.translation, arguments); }
            catch (FormatException) { return source; }
        }

        if (RichTag.IsMatch(source))
        {
            var parts = RichTag.Split(source);
            for (int i = 0; i < parts.Length; i += 2) parts[i] = Translate(parts[i], depth + 1);
            return string.Concat(parts);
        }
        foreach (string separator in Separators)
        {
            if (source.IndexOf(separator, StringComparison.Ordinal) < 0) continue;
            var parts = source.Split(new[] { separator }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++) parts[i] = Translate(parts[i], depth + 1);
            return string.Join(separator, parts);
        }
        return source;
    }

    private void AddPattern(string source, string translation)
    {
        var matches = Placeholder.Matches(source);
        if (matches.Count == 0) return;
        string literals = Placeholder.Replace(source, string.Empty);
        bool hasLetters = false;
        foreach (char character in literals) if (char.IsLetter(character)) { hasLetters = true; break; }
        if (!hasLetters) return;
        var expression = new StringBuilder("\\A");
        var seen = new HashSet<int>();
        int position = 0, count = 0;
        foreach (Match match in matches)
        {
            expression.Append(Regex.Escape(source.Substring(position, match.Index - position)));
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (index > 32) return;
            expression.Append(seen.Add(index) ? "(?<p" + index + ">[\\s\\S]*?)" : "\\k<p" + index + ">");
            count = Math.Max(count, index + 1);
            position = match.Index + match.Length;
        }
        expression.Append(Regex.Escape(source.Substring(position))).Append("\\z");
        patterns.Add(new Pattern { regex = new Regex(expression.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)), translation = translation, argumentCount = count, specificity = literals.Length });
    }
}
