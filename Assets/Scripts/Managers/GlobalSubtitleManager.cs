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

    // YENİ MANTIK: Sıralı Oynatma (Sequential)
    IEnumerator PlaySequenceRoutine(SubtitleEntry entry)
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(true);

        foreach (var seg in entry.segments)
        {
            // 1. Yazıyı Göster
            if (subtitleText != null)
                subtitleText.text = seg.GetText(currentLanguage);

            // 2. Süresi kadar bekle (Otomatik zamanlama)
            yield return new WaitForSeconds(seg.duration);
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
        if (subtitleText != null)
            subtitleText.text = "";
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
    }
}
