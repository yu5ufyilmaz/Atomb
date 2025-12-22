using System.Collections;
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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);

        HideSubtitle();
    }

    // Artık sadece ID veriyoruz, süre vermiyoruz. Süreler veritabanında kayıtlı.
    public void Show(string id)
    {
        if (database == null)
            return;

        SubtitleEntry entry = database.GetEntry(id);
        if (entry != null)
        {
            if (currentSubtitleRoutine != null)
                StopCoroutine(currentSubtitleRoutine);

            currentSubtitleRoutine = StartCoroutine(PlaySequenceRoutine(entry));
        }
    }

    IEnumerator PlaySequenceRoutine(SubtitleEntry entry)
    {
        // Paneli aç
        if (subtitlePanel != null)
            subtitlePanel.SetActive(true);
        if (subtitleText != null)
            subtitleText.text = "";

        float timer = 0f;
        float sequenceLength = GetTotalDuration(entry);

        // Segmentlerin "Start Time"ına göre sıralandığından emin olalım (opsiyonel ama güvenli)
        entry.segments.Sort((a, b) => a.startTime.CompareTo(b.startTime));

        while (timer < sequenceLength)
        {
            timer += Time.deltaTime;

            // O anki saniyede hangi segment aktif olmalı?
            SubtitleSegment currentSegment = null;

            foreach (var seg in entry.segments)
            {
                // Eğer zaman, segmentin başlangıcı ile bitişi arasındaysa
                if (timer >= seg.startTime && timer < (seg.startTime + seg.duration))
                {
                    currentSegment = seg;
                    break;
                }
            }

            if (currentSegment != null && subtitleText != null)
            {
                subtitleText.text = currentSegment.GetText(currentLanguage);
            }
            else if (subtitleText != null)
            {
                // Segmentler arası boşluktaysak temizle
                subtitleText.text = "";
            }

            yield return null;
        }

        HideSubtitle();
    }

    // Diyalogun toplam süresini bulur (En son biten segmentin bitiş zamanı)
    float GetTotalDuration(SubtitleEntry entry)
    {
        float maxTime = 0f;
        foreach (var seg in entry.segments)
        {
            float endTime = seg.startTime + seg.duration;
            if (endTime > maxTime)
                maxTime = endTime;
        }
        // Emniyet payı ekle (ses bitiminden hemen sonra kapanmasın diye yarım saniye)
        return maxTime + 0.5f;
    }

    public void HideSubtitle()
    {
        if (subtitleText != null)
            subtitleText.text = "";
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
    }
}
