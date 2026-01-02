using System.Collections;
using UnityEngine;

public class MegaphoneSystem : MonoBehaviour
{
    public static MegaphoneSystem Instance;

    [Header("Components")]
    public AudioSource speakerSource; // Inspector'dan Spatial Blend'i 1 (3D) yapmayı unutma!

    [Header("--- 1. OYUN BAŞLANGICI ---")]
    public AudioClip startGameClip;
    public string startGameSubtitleID = "start_game";
    public float startGameDelay = 3f; // Kaç saniye sonra çalsın?

    [Header("--- 2. İLK NOTEPAD ALIMI ---")]
    public AudioClip notepadPickupClip;
    public string notepadPickupSubtitleID = "first_notepad";

    [Header("--- 3. İLK HATA (YANLIŞ ŞİFRE) ---")]
    public AudioClip firstMistakeClip;
    public string firstMistakeSubtitleID = "first_mistake";

    [Header("--- 4. TUTORIAL BİTİŞİ (ŞİFRE ÇÖZÜLDÜ) ---")]
    public AudioClip tutorialSolvedClip;
    public string tutorialSolvedSubtitleID = "tutorial_solved";

    [Header("--- 5. BASINÇ UYARISI (İLK KEZ) ---")]
    public AudioClip pressureWarningClip;
    public string pressureWarningSubtitleID = "pressure_warning";

    [Header("--- 6. ŞARTEL ATTI (İLK KEZ) ---")]
    public AudioClip breakerTripClip;
    public string breakerTripSubtitleID = "breaker_trip";

    [Header("--- 7. FİNAL ŞİFRE GİRİLDİ ---")]
    public AudioClip finalCodeClip;
    public string finalCodeSubtitleID = "final_code";

    // --- KONTROL BAYRAKLARI (Tek seferlik çalışması için) ---
    private bool hasPlayedStart = false;
    private bool hasPlayedNotepad = false;
    private bool hasPlayedMistake = false;
    private bool hasPlayedPressure = false;
    private bool hasPlayedBreaker = false;
    private bool hasPlayedTutorial = false;

    // Tutorial ve Final zaten doğası gereği tek seferliktir ama yine de kontrol edebiliriz.

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // 1. Koşul: Oyun başladıktan birkaç saniye sonra çal
        StartCoroutine(StartGameRoutine());
    }

    // ========================================================================
    // 📢 TEMEL ÇALMA FONKSİYONLARI
    // ========================================================================

    // Diğer scriptlerden (RoomManager) gelen özel olaylar için
    public void PlayEvent(DialogueEvent evt, Transform soundOrigin = null)
    {
        if (evt == null)
            return;
        PlayAudio(evt.clip, evt.subtitleID, soundOrigin);
    }

    private void PlayAudio(AudioClip clip, string subtitleID, Transform soundOrigin = null)
    {
        if (clip != null && speakerSource != null)
        {
            speakerSource.Stop();
            speakerSource.clip = clip;

            // Eğer bir konum (hoparlör) verildiyse oraya ışınla ve 3D yap
            if (soundOrigin != null)
            {
                speakerSource.transform.position = soundOrigin.position;
                speakerSource.spatialBlend = 1.0f;
            }
            else
            {
                // Konum yoksa (kafa sesi/genel anons) 2D yap
                speakerSource.spatialBlend = 0.0f;
            }

            speakerSource.Play();
        }

        // Altyazıyı Tetikle
        if (GlobalSubtitleManager.Instance != null)
        {
            GlobalSubtitleManager.Instance.Show(subtitleID);
        }
    }

    // ========================================================================
    // 🎯 7 TEMEL KOŞUL FONKSİYONLARI (Diğer scriptler bunları çağıracak)
    // ========================================================================

    // 1. OYUN BAŞLANGICI (Otomatik)
    IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(startGameDelay);
        if (!hasPlayedStart)
        {
            PlayAudio(startGameClip, startGameSubtitleID);
            hasPlayedStart = true;
        }
    }

    // 2. İLK NOTEPAD (InteractableBook.cs çağıracak)
    public void OnNotepadPickedUp()
    {
        if (hasPlayedNotepad)
            return;

        PlayAudio(notepadPickupClip, notepadPickupSubtitleID);
        hasPlayedNotepad = true;
    }

    // 3. İLK HATA (PasswordManager.cs çağıracak)
    public void OnFirstMistake()
    {
        if (hasPlayedMistake)
            return;

        PlayAudio(firstMistakeClip, firstMistakeSubtitleID);
        hasPlayedMistake = true;
    }

    public void OnTutorialSolved()
    {
        // EĞER DAHA ÖNCE ÇALDIYSA İŞLEM YAPMA
        if (hasPlayedTutorial)
            return;

        PlayAudio(tutorialSolvedClip, tutorialSolvedSubtitleID);

        // BİR KERE ÇALDIKTAN SONRA KİLİTLE
        hasPlayedTutorial = true;
    }

    // 5. BASINÇ UYARISI (PressureSystemManager.cs çağıracak)
    public void OnPressureThresholdExceeded()
    {
        if (hasPlayedPressure)
            return;

        PlayAudio(pressureWarningClip, pressureWarningSubtitleID);
        hasPlayedPressure = true;
    }

    // 6. ŞARTEL ATTI (BreakerBox.cs çağıracak)
    public void OnBreakerTripped()
    {
        if (hasPlayedBreaker)
            return;

        PlayAudio(breakerTripClip, breakerTripSubtitleID);
        hasPlayedBreaker = true;
    }

    // 7. FİNAL ŞİFRE (PasswordManager.cs çağıracak)
    public void OnFinalCodeEntered()
    {
        PlayAudio(finalCodeClip, finalCodeSubtitleID);
    }
}
