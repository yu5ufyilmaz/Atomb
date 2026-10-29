using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    private void OnEnable() { GameLocalization.LanguageChanged += RefreshActiveSegment; }
    private void OnDisable() { GameLocalization.LanguageChanged -= RefreshActiveSegment; }

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
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        HideSubtitle();
    }

    public void Show(string id, bool required = false, AudioSource voiceSource = null)
    {
        if (voiceSource != null && !speechSources.Contains(voiceSource)) speechSources.Add(voiceSource);
        if (database == null || string.IsNullOrEmpty(id))
            return;

        // --- YENİ EKLENEN KISIM: Altyazı kapalıysa işlemi direkt iptal et ---
        if (
            !required && SettingsManager.Instance != null
            && !SettingsManager.Instance.currentSettings.subtitlesEnabled
        )
        {
            return;
        }
        // -------------------------------------------------------------------

        SubtitleEntry entry = database.GetEntry(id);
        if (entry != null && entry.segments.Count > 0)
        {
            HideSubtitle();
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
            RefreshActiveSegment();

            // 2. Süresi kadar bekle (Otomatik zamanlama)
            float remaining = seg.duration;
            while (remaining > 0)
            {
                if (GameManager.Instance == null || !GameManager.Instance.isGamePaused) remaining -= Time.deltaTime;
                yield return null;
            }
        }

        // 3. Hepsi bitince kapat
        HideSubtitle();
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
