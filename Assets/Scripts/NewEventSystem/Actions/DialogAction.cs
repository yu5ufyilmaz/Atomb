using UnityEngine;

public class DialogAction : ActionBase
{
    [Header("Altyazı Ayarları")]
    [Tooltip("MainSubtitles ScriptableObject'i içindeki ID (örn: chemistry_lab_entrance)")]
    public string subtitleID;

    [Header("Ses Ayarları (Opsiyonel)")]
    [Tooltip("Diyalogla eşzamanlı çalacak ses dosyası")]
    public AudioClip dialogClip;

    [Tooltip("Sesin çıkacağı kaynak. Boş bırakırsan ses çalmaz, sadece altyazı akar.")]
    public AudioSource audioSource;

    protected override void PerformAction()
    {
        // 1. Altyazıyı Tetikle (Mevcut GlobalSubtitleManager üzerinden)
        if (!string.IsNullOrEmpty(subtitleID))
        {
            if (GlobalSubtitleManager.Instance != null)
            {
                GlobalSubtitleManager.Instance.Show(subtitleID);
            }
            else
            {
                Debug.LogWarning("[DialogAction] GlobalSubtitleManager sahnede bulunamadı!");
            }
        }

        // 2. İlgili Sesi Çal
        if (dialogClip != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = dialogClip;
            audioSource.Play();
        }
    }
}
