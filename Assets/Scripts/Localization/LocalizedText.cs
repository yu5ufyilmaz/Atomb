using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Localization/Localized Text")]
public sealed class LocalizedText : MonoBehaviour
{
    [Serializable]
    public sealed class LocaleOverride
    {
        public string languageCode;
        public bool overrideText;
        [TextArea] public string text;
        public bool overrideFont;
        public TMP_FontAsset font;
        public bool overrideMaterial;
        public Material material;
        public bool overrideFontSize;
        public float fontSize = 36f;
        public bool overrideFontStyle;
        public FontStyles fontStyle = FontStyles.Normal;
        public bool overrideAutoSizing;
        public bool autoSizing;
        public bool overrideFontSizeMin;
        public float fontSizeMin = 18f;
        public bool overrideFontSizeMax;
        public float fontSizeMax = 72f;
        public bool overrideAlignment;
        public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
        public bool overrideCharacterSpacing;
        public float characterSpacing;
        public bool overrideLineSpacing;
        public float lineSpacing;
        public bool overrideWrapping;
        public TextWrappingModes wrapping = TextWrappingModes.Normal;
        public bool overrideAnchors;
        public Vector2 anchorMin;
        public Vector2 anchorMax = Vector2.one;
        public bool overridePivot;
        public Vector2 pivot = new Vector2(.5f, .5f);
        public bool overridePosition;
        public Vector2 anchoredPosition;
        public bool overrideSize;
        public Vector2 sizeDelta;
    }

    public TMP_Text target;
    public string key;
    [TextArea] public string source;
    public LocaleOverride[] overrides = Array.Empty<LocaleOverride>();
    [Tooltip("Optional original title artwork, shown when the resolved text matches its source.")]
    public Image sourceArtwork;
    [Tooltip("Keep translated animated menu labels at the source text's left edge and fit them on one line.")]
    public bool preserveSourceLeftEdge;

    private bool sourceCaptured, baselineCaptured, overrideApplied;
    private LocaleOverride appliedOverride;
    private bool languageFontApplied, languageDirectionApplied;
    private bool menuLayoutApplied;
    private Vector4 originalMargin;
    private TextWrappingModes originalWrapping;
    private object[] formatArguments;
    private TMP_FontAsset originalFont;
    private FontStyles originalFontStyle;
    private Material originalMaterial;
    private float originalFontSize, originalMin, originalMax, originalCharacterSpacing, originalLineSpacing;
    private bool originalAutoSizing, originalRightToLeft;
    private TextAlignmentOptions originalAlignment;
    private Vector2 originalAnchorMin, originalAnchorMax, originalPivot, originalSize;
    private Vector3 originalPosition;

    private void OnEnable()
    {
        GameLocalization.Initialize();
        GameLocalization.LanguageChanged += Apply;
        GameInput.BindingsChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= Apply;
        GameInput.BindingsChanged -= Apply;
    }

    public void SetSource(string canonicalSource)
    {
        source = canonicalSource ?? string.Empty;
        sourceCaptured = true;
        formatArguments = null;
        Apply();
    }

    public void SetFormat(string sourceTemplate, params object[] arguments)
    {
        source = sourceTemplate ?? string.Empty;
        sourceCaptured = true;
        formatArguments = arguments == null ? Array.Empty<object>() : (object[])arguments.Clone();
        Apply();
    }

    public void RefreshBaseline()
    {
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null) return;
        CaptureBaseline();
    }

    public void Apply()
    {
        GameLocalization.Initialize();
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null) return;
        if (!sourceCaptured)
        {
            if (string.IsNullOrEmpty(source)) source = target.text;
            sourceCaptured = true;
        }

        if (menuLayoutApplied)
        {
            target.margin = originalMargin;
            target.alignment = originalAlignment;
            target.enableAutoSizing = originalAutoSizing;
            target.fontSize = originalFontSize;
            target.fontSizeMin = originalMin;
            target.fontSizeMax = originalMax;
            target.textWrappingMode = originalWrapping;
            menuLayoutApplied = false;
        }

        if (overrideApplied && baselineCaptured)
        {
            RestoreBaseline();
            overrideApplied = false;
        }

        var language = GameLocalization.CurrentLanguage;
        bool hasLanguageStyle = language != null && (language.defaultFont != null || language.rightToLeft);
        LocaleOverride activeOverride = null;
        if (overrides != null)
        {
            foreach (var value in overrides)
            {
                if (value == null || !string.Equals(value.languageCode, GameLocalization.CurrentCode, StringComparison.OrdinalIgnoreCase)) continue;
                activeOverride = value;
                break;
            }
        }

        if (hasLanguageStyle || activeOverride != null)
        {
            CaptureBaseline();
            languageFontApplied = language != null && language.defaultFont != null;
            languageDirectionApplied = hasLanguageStyle;
            if (hasLanguageStyle)
            {
                if (languageFontApplied) target.font = language.defaultFont;
                target.isRightToLeftText = language.rightToLeft;
            }
            appliedOverride = activeOverride;
            overrideApplied = true;
        }

        string translated = ResolveText();
        bool alignTranslation = preserveSourceLeftEdge && !string.Equals(translated, source, StringComparison.Ordinal);
        if (preserveSourceLeftEdge && !baselineCaptured) CaptureBaseline();
        float sourceWidth = alignTranslation ? target.GetPreferredValues(source, Mathf.Infinity, Mathf.Infinity).x : 0f;
        if (preserveSourceLeftEdge) target.SetText(translated);
        else target.text = translated;
        if (activeOverride != null) ApplyOverride(activeOverride);
        if (alignTranslation)
        {
            var margin = originalMargin;
            float available = target.rectTransform.rect.width - margin.x - margin.z;
            if (target.horizontalAlignment == HorizontalAlignmentOptions.Center) margin.x += Mathf.Max(0f, (available - sourceWidth) * .5f);
            else if (target.horizontalAlignment == HorizontalAlignmentOptions.Right) margin.x += Mathf.Max(0f, available - sourceWidth);
            target.margin = margin;
            target.horizontalAlignment = HorizontalAlignmentOptions.Left;
            target.enableAutoSizing = true;
            target.fontSizeMin = originalFontSize * .6f;
            target.fontSizeMax = originalFontSize;
            target.textWrappingMode = TextWrappingModes.NoWrap;
            menuLayoutApplied = true;
        }
        if (sourceArtwork != null)
        {
            bool showArtwork = string.Equals(target.text, source, StringComparison.Ordinal);
            sourceArtwork.enabled = showArtwork;
            target.enabled = !showArtwork;
        }
    }

    private string ResolveText()
    {
        if (string.IsNullOrEmpty(key))
            return formatArguments == null ? GameLocalization.Translate(source) : GameLocalization.Format(source, formatArguments);

        string translated = GameLocalization.Get(key, source);
        if (formatArguments == null) return translated;
        var arguments = new object[formatArguments.Length];
        for (int i = 0; i < arguments.Length; i++)
            arguments[i] = formatArguments[i] is string text ? GameLocalization.Translate(text) : formatArguments[i];
        try { return string.Format(translated, arguments); }
        catch (FormatException)
        {
            Debug.LogWarning("Invalid localized format for key: " + key, this);
            return translated;
        }
    }

    private void CaptureBaseline()
    {
        originalFont = target.font;
        originalMaterial = target.fontSharedMaterial;
        originalFontSize = target.fontSize;
        originalFontStyle = target.fontStyle;
        originalAutoSizing = target.enableAutoSizing;
        originalMin = target.fontSizeMin;
        originalMax = target.fontSizeMax;
        originalAlignment = target.alignment;
        originalCharacterSpacing = target.characterSpacing;
        originalLineSpacing = target.lineSpacing;
        originalRightToLeft = target.isRightToLeftText;
        originalMargin = target.margin;
        originalWrapping = target.textWrappingMode;
        var rect = target.rectTransform;
        originalAnchorMin = rect.anchorMin;
        originalAnchorMax = rect.anchorMax;
        originalPivot = rect.pivot;
        originalPosition = rect.anchoredPosition3D;
        originalSize = rect.sizeDelta;
        baselineCaptured = true;
    }

    private void RestoreBaseline()
    {
        // Restore only properties localization changed; runtime skins/layouts own the others.
        var value = appliedOverride;
        bool restoreFont = languageFontApplied || (value != null && value.overrideFont);
        if (restoreFont) target.font = originalFont;
        if (restoreFont || (value != null && value.overrideMaterial)) target.fontSharedMaterial = originalMaterial;
        if (languageDirectionApplied) target.isRightToLeftText = originalRightToLeft;
        if (value == null) return;
        if (value.overrideFontSize || value.overrideAutoSizing || value.overrideFontSizeMin || value.overrideFontSizeMax)
            target.fontSize = originalFontSize;
        if (value.overrideAutoSizing) target.enableAutoSizing = originalAutoSizing;
        if (value.overrideFontStyle) target.fontStyle = originalFontStyle;
        if (value.overrideFontSizeMin) target.fontSizeMin = originalMin;
        if (value.overrideFontSizeMax) target.fontSizeMax = originalMax;
        if (value.overrideAlignment) target.alignment = originalAlignment;
        if (value.overrideCharacterSpacing) target.characterSpacing = originalCharacterSpacing;
        if (value.overrideLineSpacing) target.lineSpacing = originalLineSpacing;
        if (value.overrideWrapping) target.textWrappingMode = originalWrapping;
        var rect = target.rectTransform;
        if (value.overrideAnchors) { rect.anchorMin = originalAnchorMin; rect.anchorMax = originalAnchorMax; }
        if (value.overridePivot) rect.pivot = originalPivot;
        if (value.overridePosition) rect.anchoredPosition3D = originalPosition;
        if (value.overrideSize) rect.sizeDelta = originalSize;
    }

    private void ApplyOverride(LocaleOverride value)
    {
        if (value.overrideText) target.text = value.text ?? string.Empty;
        if (value.overrideFont) target.font = value.font;
        if (value.overrideMaterial) target.fontSharedMaterial = value.material;
        if (value.overrideFontSize) target.fontSize = value.fontSize;
        if (value.overrideFontStyle) target.fontStyle = value.fontStyle;
        if (value.overrideAutoSizing) target.enableAutoSizing = value.autoSizing;
        if (value.overrideFontSizeMin) target.fontSizeMin = value.fontSizeMin;
        if (value.overrideFontSizeMax) target.fontSizeMax = value.fontSizeMax;
        if (value.overrideAlignment) target.alignment = value.alignment;
        if (value.overrideCharacterSpacing) target.characterSpacing = value.characterSpacing;
        if (value.overrideLineSpacing) target.lineSpacing = value.lineSpacing;
        if (value.overrideWrapping) target.textWrappingMode = value.wrapping;
        var rect = target.rectTransform;
        if (value.overrideAnchors) { rect.anchorMin = value.anchorMin; rect.anchorMax = value.anchorMax; }
        if (value.overridePivot) rect.pivot = value.pivot;
        if (value.overridePosition) rect.anchoredPosition = value.anchoredPosition;
        if (value.overrideSize) rect.sizeDelta = value.sizeDelta;
    }
}
