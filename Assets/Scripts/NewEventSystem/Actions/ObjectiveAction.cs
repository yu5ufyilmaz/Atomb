using TMPro;
using UnityEngine;

public class ObjectiveAction : ActionBase
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

    protected override void PerformAction()
    {
        // 1. Ekrandaki Görev Yazısını Güncelle
        if (objectiveTextUI != null)
        {
            GameLocalization.Set(objectiveTextUI, newObjectiveText);
        }

        // 2. Altyazı Göster
        if (GlobalSubtitleManager.Instance != null && (!string.IsNullOrEmpty(subtitleID)
            || (voiceClip != null && innerVoiceSource != null)))
        {
            GlobalSubtitleManager.Instance.Show(subtitleID, voiceSource: voiceClip != null ? innerVoiceSource : null);
        }

        // 3. Karakterin İç Sesini Çal
        if (voiceClip != null && innerVoiceSource != null)
        {
            innerVoiceSource.PlayOneShot(voiceClip);
        }
    }
}
