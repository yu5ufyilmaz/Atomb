using TMPro;
using UnityEngine;

public class ObjectiveAction : MonoBehaviour, IAction
{
    [Header("Görev Arayüzü (UI)")]
    public TextMeshProUGUI objectiveTextUI;
    [TextArea]
    public string newObjectiveText;

    [Header("Ses ve Altyazı (İsteğe Bağlı)")]
    public string subtitleID;
    public AudioClip voiceClip;
    [Tooltip("İç sesin çalacağı AudioSource (Karakterin üzerindeki vb.)")]
    public AudioSource innerVoiceSource;

    public void Execute()
    {
        // 1. Ekrandaki Görev Yazısını Güncelle
        if (objectiveTextUI != null)
        {
            objectiveTextUI.text = newObjectiveText;
        }

        // 2. Altyazı Göster
        if (!string.IsNullOrEmpty(subtitleID) && GlobalSubtitleManager.Instance != null)
        {
            GlobalSubtitleManager.Instance.Show(subtitleID);
        }

        // 3. Karakterin İç Sesini Çal
        if (voiceClip != null && innerVoiceSource != null)
        {
            innerVoiceSource.PlayOneShot(voiceClip);
        }
    }
}