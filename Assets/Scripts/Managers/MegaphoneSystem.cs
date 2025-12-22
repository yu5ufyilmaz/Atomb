using System.Collections;
using TMPro;
using UnityEngine;

public class MegaphoneSystem : MonoBehaviour
{
    public static MegaphoneSystem Instance;

    [Header("Components")]
    public AudioSource speakerSource;

    // SubtitleText artık GlobalSubtitleManager tarafından yönetildiği için buradan silebiliriz
    // Ama Inspector'da referans kopmasın diye tutmak istersen kalabilir, fakat kullanılmayacak.

    [Header("--- TUTORIAL CLIPS ---")]
    public AudioClip introClip;
    public AudioClip nagPickupClip;
    public AudioClip nagInputClip;
    public AudioClip machineUnlockClip;

    [Header("--- GAMEPLAY CLIPS ---")]
    public AudioClip pressureEmergencyClip;
    public AudioClip pressureRoomIntroClip;

    [Header("--- IDLE / NAG CLIPS ---")]
    public AudioClip idleCorridorClip;
    public AudioClip idlePuzzleClip;
    public AudioClip holdingCodeClip;
    public AudioClip lightsWarningClip;

    [Header("--- FINAL CLIPS ---")]
    public AudioClip denyExitClip;
    public AudioClip finalSpeechClip;
    public AudioClip nagFinalClip;

    [Header("Settings")]
    public float idleThreshold = 45f;

    // State
    private bool isTutorialActive = true;
    private bool hasPickedUpNote = false;
    private bool hasEnteredPressureRoom = false;
    private bool isRealGameStarted = false;

    private float lastInteractionTime;
    private bool hasCodeButNotEntered = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        lastInteractionTime = Time.time;
        StartCoroutine(StartTutorialSequence());
    }

    void Update()
    {
        if (isRealGameStarted)
        {
            CheckIdleStatus();
        }
    }

    // ========================================================================
    // 📢 TEMEL ÇALMA VE OLAY YÖNETİMİ
    // ========================================================================

    /// <summary>
    /// RoomManager veya diğer scriptlerden gelen "DialogueEvent" paketini çalar.
    /// </summary>
    public void PlayEvent(DialogueEvent evt)
    {
        if (evt == null)
            return;
        PlayAudio(evt.clip, evt.subtitleID);
    }

    /// <summary>
    /// Hem sesi çalar hem de Global Altyazı sistemine ID gönderir.
    /// </summary>
    private void PlayAudio(AudioClip clip, string subtitleID)
    {
        // 1. SESİ ÇAL
        if (clip != null && speakerSource != null)
        {
            speakerSource.Stop();
            speakerSource.clip = clip;
            speakerSource.Play();
        }

        // 2. ALTYAZIYI GÖSTER (GlobalSubtitleManager Varsa)
        if (GlobalSubtitleManager.Instance != null)
        {
            // Eğer clip varsa süresini al, yoksa varsayılan 3 saniye ver
            float duration = (clip != null) ? clip.length : 3f;
            GlobalSubtitleManager.Instance.Show(subtitleID, duration);
        }
        else
        {
            // Eğer GlobalManager yoksa ama eski usul Text varsa (Yedek)
            Debug.LogWarning(
                "MegaphoneSystem: GlobalSubtitleManager bulunamadı, altyazı gösterilemiyor."
            );
        }
    }

    // ========================================================================
    // 🎓 TUTORIAL AKIŞI
    // ========================================================================

    IEnumerator StartTutorialSequence()
    {
        // Ses dosyası yoksa sadece log basar, çökmez.
        if (introClip == null)
        {
            Debug.LogWarning("⚠️ MegaphoneSystem: Intro Clip atanmamış! Sadece altyazı çalışacak.");
        }

        // "tutorial_intro" ID'sini veritabanında oluşturmayı unutma!
        PlayAudio(introClip, "tutorial_intro");

        float waitTime = (introClip != null) ? introClip.length : 4f;
        yield return new WaitForSeconds(waitTime + 2f);

        if (!hasPickedUpNote)
        {
            PlayAudio(nagPickupClip, "tutorial_nag_pickup");
        }
    }

    public void OnNotePickedUp()
    {
        if (hasPickedUpNote)
            return;

        hasPickedUpNote = true;
        StopAllCoroutines();

        PlayAudio(machineUnlockClip, "tutorial_unlock");
        StartCoroutine(WaitForInputRoutine());
    }

    IEnumerator WaitForInputRoutine()
    {
        yield return new WaitForSeconds(20f);
        if (isTutorialActive)
        {
            PlayAudio(nagInputClip, "tutorial_nag_input");
        }
    }

    public void OnTutorialCompleted()
    {
        if (!isTutorialActive)
            return;

        isTutorialActive = false;
        isRealGameStarted = true;
        StopAllCoroutines();

        if (PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.DisableTutorialMode();
        }

        Debug.Log("Tutorial Bitti. Gerçek Oyun Başladı.");
    }

    // ========================================================================
    // ⚡ OYUN İÇİ OLAYLAR (Basınç, Idle vb.)
    // ========================================================================

    public void CheckPressureEvent(float currentPressure, float threshold)
    {
        if (!isRealGameStarted)
            return;

        if (currentPressure >= threshold)
        {
            // Basınç odasına henüz girilmediyse uyar
            if (!hasEnteredPressureRoom)
            {
                PlayAudio(pressureEmergencyClip, "pressure_emergency");
                // Not: hasEnteredPressureRoom'u burada true yapmıyoruz,
                // oyuncu gerçekten odaya girdiğinde RoomManager yapacak.
            }
        }
    }

    // NOT: OnRoomEnter fonksiyonu SİLİNDİ. Artık RoomManager.cs kendi sesini "PlayEvent" ile çalıyor.

    public void ResetIdleTimer()
    {
        lastInteractionTime = Time.time;
    }

    public void OnCodeFound()
    {
        hasCodeButNotEntered = true;
        ResetIdleTimer();
    }

    public void OnCodeSubmitted()
    {
        hasCodeButNotEntered = false;
        ResetIdleTimer();
    }

    private void CheckIdleStatus()
    {
        if (speakerSource != null && speakerSource.isPlaying)
            return;

        if (Time.time - lastInteractionTime > idleThreshold)
        {
            if (hasCodeButNotEntered)
                PlayAudio(holdingCodeClip, "idle_holding_code");
            else
                PlayAudio(idleCorridorClip, "idle_move_warning");

            ResetIdleTimer();
        }
    }

    public void CheckLightsState(int openLightCount)
    {
        if (openLightCount > 5)
        {
            PlayAudio(lightsWarningClip, "lights_warning");
        }
    }

    public void OnFinalGateTry(bool allCodesEntered)
    {
        if (allCodesEntered)
            PlayAudio(nagFinalClip, "final_gate_open");
        else
            PlayAudio(denyExitClip, "final_gate_locked");
    }
}
