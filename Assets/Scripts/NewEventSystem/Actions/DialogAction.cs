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

    [Header("Koşula Bağlı Hatırlatmalar (İsteğe Bağlı)")]
    [Tooltip("Bu eventin koşulları sürdükçe hatırlat. Giriş repliği bu oturumda yalnızca bir kez söylenir.")]
    public bool followConditions;
    [Tooltip("İsteğe bağlı sahip bileşen kapatılırsa bu konuşma da bekler.")]
    public Behaviour contextOwner;
    [Tooltip("İki farklı hatırlatma altyazı ID'si. Metin ve isteğe bağlı kayıt altyazı veritabanından alınır.")]
    public string[] reminderSubtitleIDs = System.Array.Empty<string>();
    [Min(1)] public float reminderDelay = 10;
    public bool requiredWithoutVoice = true;

    private TutorialThoughtClock clock = new TutorialThoughtClock();
    private EventLogicController controller;
    private ICondition[] conditions;
    private bool started, audioPaused;
    private string ownedSubtitle;
    private AudioClip ownedClip;

    private void Start()
    {
        controller = GetComponent<EventLogicController>();
        conditions = GetComponents<ICondition>();
        foreach (var condition in conditions) condition.OnConditionChanged += PostponeReminder;
    }

    private void PostponeReminder() { clock.Postpone(reminderDelay); }

    protected override void PerformAction()
    {
        if (followConditions) { started = true; return; }
        var database = GlobalSubtitleManager.Instance != null ? GlobalSubtitleManager.Instance.database : null;
        var clip = dialogClip != null ? dialogClip : database != null ? database.GetEntry(subtitleID)?.voiceClip : null;
        if (clip != null) EnsureAudioSource();
        // 1. Altyazıyı Tetikle (Mevcut GlobalSubtitleManager üzerinden)
        if (!string.IsNullOrEmpty(subtitleID) || clip != null)
        {
            if (GlobalSubtitleManager.Instance != null)
            {
                GlobalSubtitleManager.Instance.Show(subtitleID, voiceSource: clip != null ? audioSource : null);
            }
            else
            {
                Debug.LogWarning("[DialogAction] GlobalSubtitleManager sahnede bulunamadı!");
            }
        }

        // 2. İlgili Sesi Çal
        if (clip != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (!followConditions || !started) return;
        if (controller == null || !controller.ConditionsAreMet || (contextOwner != null && !contextOwner.isActiveAndEnabled))
        {
            StopOwnedSpeech();
            PostponeReminder();
            return;
        }
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused)
        {
            if (OwnsAudio && audioSource.isPlaying) { audioSource.Pause(); audioPaused = true; }
            return;
        }
        if (audioPaused) { if (OwnsAudio) audioSource.UnPause(); audioPaused = false; }
        var subtitles = GlobalSubtitleManager.Instance;
        if (subtitles == null) return;
        if (ownedSubtitle != null && (subtitles.IsOtherVoicePlaying(OwnsAudio ? audioSource : null)
            || (subtitles.IsPlaying && subtitles.CurrentID != ownedSubtitle))) StopOwnedSpeech();
        bool blocked = subtitles.IsSpeechPlaying || (OwnsAudio && audioSource.isPlaying);
        int line = clock.Advance(subtitleID, 0, Time.deltaTime, reminderDelay, blocked);
        if (line < 0) return;
        string id = line == 0 ? subtitleID : reminderSubtitleIDs != null && reminderSubtitleIDs.Length > 0
            ? reminderSubtitleIDs[(line - 1) % reminderSubtitleIDs.Length] : null;
        if (string.IsNullOrEmpty(id)) return;
        var entry = subtitles.database != null ? subtitles.database.GetEntry(id) : null;
        if (entry == null) { Debug.LogWarning("[DialogAction] Altyazı bulunamadı: " + id, this); return; }
        ownedSubtitle = id;
        ownedClip = line == 0 && dialogClip != null ? dialogClip : entry.voiceClip;
        if (ownedClip != null)
        {
            EnsureAudioSource();
            audioSource.clip = ownedClip;
            audioSource.Play();
        }
        subtitles.Show(id, required: requiredWithoutVoice && ownedClip == null, voiceSource: ownedClip != null ? audioSource : null);
    }

    private bool OwnsAudio => ownedClip != null && audioSource != null && audioSource.clip == ownedClip;

    private void EnsureAudioSource()
    {
        if (audioSource != null) { SettingsManager.Instance?.RouteVoice(audioSource); return; }
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0;
        SettingsManager.Instance?.RouteVoice(audioSource);
    }

    private void StopOwnedSpeech()
    {
        var subtitles = GlobalSubtitleManager.Instance;
        if (subtitles != null && ownedSubtitle != null && subtitles.CurrentID == ownedSubtitle) subtitles.HideSubtitle();
        if (OwnsAudio) audioSource.Stop();
        ownedSubtitle = null;
        ownedClip = null;
        audioPaused = false;
    }

    public void ResetDialogue()
    {
        StopOwnedSpeech();
        clock = new TutorialThoughtClock();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        StopOwnedSpeech();
        PostponeReminder();
    }

    private void OnDestroy()
    {
        if (conditions != null) foreach (var condition in conditions) condition.OnConditionChanged -= PostponeReminder;
    }
}
