using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalSubtitleManager : MonoBehaviour
{
    public static GlobalSubtitleManager Instance;
    public SubtitleData database;
    public TextMeshProUGUI subtitleText;
    public GameObject subtitlePanel;
    public GameLanguage currentLanguage = GameLanguage.Turkish;

    private Coroutine currentSubtitleRoutine;
    private SubtitleEntry activeEntry;
    private int activeSegmentIndex = -1;
    private readonly List<AudioSource> speechSources = new List<AudioSource>();
    private SettingsManager presentationSettings;
    private float authoredFontSize;
    private Image presentationBackground;
    private bool activeRequired;
    public void EnsurePresentationPanel()
    {
        if (subtitlePanel != null || subtitleText == null) return;
        var rect = subtitleText.rectTransform;
        var panel = new GameObject("SubtitlePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(rect.parent, false); panel.transform.SetSiblingIndex(rect.GetSiblingIndex());
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(.1f,0); panelRect.anchorMax = new Vector2(.9f,0); panelRect.pivot = new Vector2(.5f,0);
        panelRect.anchoredPosition = new Vector2(0,48); panelRect.sizeDelta = new Vector2(0,180);
        rect.SetParent(panel.transform,false); rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        subtitlePanel=panel; panel.GetComponent<Image>().raycastTarget=false;panel.GetComponent<Image>().color=new Color(0,0,0,.5f);panel.SetActive(false);
    }
    public string CurrentID { get; private set; }
    public bool IsPlaying => currentSubtitleRoutine != null;
    public bool IsVoicePlaying
    {
        get
        {
            speechSources.RemoveAll(source => source == null);
            return speechSources.Exists(source => source.isPlaying);
        }
    }
    public bool IsOtherVoicePlaying(AudioSource owner)
    {
        speechSources.RemoveAll(source => source == null);
        return speechSources.Exists(source => source != owner && source.isPlaying);
    }
    public bool IsSpeechPlaying => IsPlaying || IsVoicePlaying;

    private void OnEnable() { GameLocalization.LanguageChanged += RefreshActiveSegment; GameInput.BindingsChanged += RefreshActiveSegment; BindPresentation(); }
    private void Start() { BindPresentation(); ApplyPresentation(); }
    private void BindPresentation()
    {
        if (presentationSettings != null) presentationSettings.Changed -= ApplyPresentation;
        presentationSettings = SettingsManager.Instance;
        if (presentationSettings != null) presentationSettings.Changed += ApplyPresentation;
    }
    private void OnDisable() { GameLocalization.LanguageChanged -= RefreshActiveSegment; GameInput.BindingsChanged -= RefreshActiveSegment; if (presentationSettings != null) presentationSettings.Changed -= ApplyPresentation; }
    public void ApplyPresentation()
    {
        var data = SettingsManager.Current;
        if (subtitleText == null || data == null) return;
        EnsurePresentationPanel();
        if (authoredFontSize <= 0) authoredFontSize = subtitleText.fontSize;
        subtitleText.enableAutoSizing = false;
        subtitleText.fontSize = authoredFontSize * (data.subtitleSize == 0 ? .85f : data.subtitleSize == 2 ? 1.25f : 1);
        float height = Mathf.Max(160, subtitleText.fontSize * 4);
        if (subtitlePanel != null && subtitlePanel.transform is RectTransform panelRect) panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        if(subtitleText.transform.parent!=subtitlePanel?.transform) subtitleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        if (presentationBackground == null && subtitlePanel != null)
        {
            presentationBackground = subtitlePanel.GetComponent<Image>();
            if (presentationBackground == null)
            {
                var go = new GameObject("Subtitle background", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(subtitlePanel.transform, false); go.transform.SetAsFirstSibling();
                presentationBackground = go.GetComponent<Image>(); var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            presentationBackground.raycastTarget = false;
        }
        if (presentationBackground != null) presentationBackground.color = new Color(0, 0, 0, data.subtitleBackground);
        if (subtitlePanel != null && activeEntry != null) subtitlePanel.SetActive(activeRequired || data.subtitlesEnabled);
    }

    private void RefreshActiveSegment()
    {
        string code = GameLocalization.CurrentCode;
        currentLanguage = code == "tr" ? GameLanguage.Turkish : code == "de" ? GameLanguage.German : GameLanguage.English;
        if (subtitleText == null || activeEntry == null || activeSegmentIndex < 0 || activeSegmentIndex >= activeEntry.segments.Count) return;
        var segment = activeEntry.segments[activeSegmentIndex];
        string fallback = segment.GetText(currentLanguage);
        if (string.IsNullOrWhiteSpace(fallback)) fallback = !string.IsNullOrWhiteSpace(segment.textEN) ? segment.textEN : segment.textTR;
        fallback = fallback != null ? fallback.Trim() : "";
        subtitleText.text = code == "de" && !string.IsNullOrWhiteSpace(segment.textDE) ? segment.textDE.Trim()
            : GameLocalization.Get("subtitle." + activeEntry.id + "." + activeSegmentIndex, fallback);
        subtitleText.text = GameInput.ResolveHints(subtitleText.text);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Instance.HideSubtitle();
            Instance.subtitleText = subtitleText; Instance.subtitlePanel = subtitlePanel;
            if(database!=null)Instance.database=database;
            Instance.authoredFontSize=0;Instance.presentationBackground=null;
            Instance.EnsurePresentationPanel();Instance.HideSubtitle();Instance.BindPresentation();Instance.ApplyPresentation();
            Destroy(gameObject);
            return;
        }

        EnsurePresentationPanel(); HideSubtitle();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Show(string id, bool required = false, AudioSource voiceSource = null)
    {
        if (voiceSource != null) SettingsManager.Instance?.RouteVoice(voiceSource);
        if (voiceSource != null && !speechSources.Contains(voiceSource)) speechSources.Add(voiceSource);
        if (database == null || string.IsNullOrEmpty(id))
            return;

        SubtitleEntry entry = database.GetEntry(id);
        if (entry != null && entry.segments.Count > 0)
        {
            HideSubtitle();
            activeRequired = required;
            CurrentID = id;
            currentSubtitleRoutine = StartCoroutine(PlaySequenceRoutine(entry));
        }
    }

    // YENİ MANTIK: Sıralı Oynatma (Sequential)
    IEnumerator PlaySequenceRoutine(SubtitleEntry entry)
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(true);

        activeEntry = entry;
        for (int i = 0; i < entry.segments.Count; i++)
        {
            var seg = entry.segments[i];
            // 1. Yazıyı Göster
            activeSegmentIndex = i;
            ApplyPresentation();
            RefreshActiveSegment();

            // 2. Süresi kadar bekle (Otomatik zamanlama)
            float remaining = seg.duration;
            while (remaining > 0)
            {
                if (GameManager.Instance == null || !GameManager.Instance.isGamePaused) remaining -= Time.deltaTime;
                KeepAboveNotebook();
                yield return null;
            }
        }

        // 3. Hepsi bitince kapat
        HideSubtitle();
    }

    // Defter ayrı kanvasta (sıra 70) çizilir; altyazı yalnızca defter açıkken öne çıkar, pause ve ölüm ekranının üstüne binmez.
    private void KeepAboveNotebook()
    {
        if (subtitlePanel == null || !subtitlePanel.activeInHierarchy) return;
        if (!subtitlePanel.TryGetComponent(out Canvas canvas)) canvas = subtitlePanel.AddComponent<Canvas>();
        var notebook = NotebookWorkspaceController.Instance;
        canvas.overrideSorting = notebook != null && notebook.IsActive && !notebook.IsPaused;
        canvas.sortingOrder = 71;
    }

    // Toplam süreyi artık Duration'ları toplayarak buluyoruz
    public float GetTotalDuration(SubtitleEntry entry)
    {
        float total = 0f;
        foreach (var seg in entry.segments)
            total += seg.duration;

        return total + 0.5f; // Emniyet payı
    }

    public void HideSubtitle()
    {
        if (currentSubtitleRoutine != null) StopCoroutine(currentSubtitleRoutine);
        currentSubtitleRoutine = null;
        activeEntry = null;
        activeSegmentIndex = -1;
        CurrentID = null;
        if (subtitleText != null)
            subtitleText.text = "";
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
    }
}
