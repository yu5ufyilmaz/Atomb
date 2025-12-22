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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);
        ClearSubtitle();
    }

    public void Show(string id, float duration)
    {
        if (database == null)
            return;
        SubtitleEntry entry = database.GetEntry(id);
        if (entry != null)
        {
            StopAllCoroutines();
            StartCoroutine(SubtitleRoutine(entry.GetText(currentLanguage), duration));
        }
    }

    IEnumerator SubtitleRoutine(string text, float duration)
    {
        if (subtitleText != null)
            subtitleText.text = text;
        if (subtitlePanel != null)
            subtitlePanel.SetActive(true);
        yield return new WaitForSeconds(duration);
        ClearSubtitle();
    }

    public void ClearSubtitle()
    {
        if (subtitleText != null)
            subtitleText.text = "";
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
    }
}
