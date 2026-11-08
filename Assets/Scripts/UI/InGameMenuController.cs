using System.Collections;
using UnityEngine;
using UnityEngine.Playables; // TİMELİNE İÇİN BU KÜTÜPHANEYİ EKLEDİK

public class InGameMenuController : MonoBehaviour
{
    [Header("Referanslar")]
    public Animator playerAnimator;
    public StarterAssets.CharacterController playerController;

    [Header("Cutscene Ayarları")] // TİMELİNE REFERANSIMIZ BURADA
    [Tooltip("Başlangıçta çalışacak Timeline objesini (StartCutScene) buraya sürükle")]
    public PlayableDirector introTimeline;

    [Tooltip("Menü bu timeline saniyesinde donuk bekler, Start'ta buradan akar; öncesindeki SitIDLE gövdeyi masadan geride tutar")]
    public float introStartTime = 5f;

    [Tooltip("Kamera bu saniyede kafadan ayrılır; sonraki Recorded (3) klibi gövdeyi geri çeker (kadraj dışında), kalkışa yumuşak geçilir")]
    public float headReleaseTime = 14.95f;

    [Tooltip("Kafa kamerasının yukarı bakış açısı (derece), timeline saniyesine göre; sigara içerken ve kalkmadan önce düz öne bakar, eller kadrajdan çıkar")]
    public AnimationCurve introLookUp = new AnimationCurve(
        new Keyframe(7.9f, 0f), new Keyframe(8.5f, 25f), new Keyframe(9.7f, 25f), new Keyframe(10.3f, 0f),
        new Keyframe(13.8f, 0f), new Keyframe(14.4f, 25f));

    [Header("Menü Objeleri (Masadaki Yazılar)")]
    public GameObject[] menuObjectsToHide;

    [Header("Kamera Bağlama Ayarları")]
    public Transform cameraTarget;
    public Transform cameraAnchor;
    public float standUpDuration = 2.5f;

    [Header("UI Ayarları")]
    public GameObject[] inGameUIPanels;
    public float uiFadeDuration = 1.5f;

    [Header("Ses Ayarları")]
    public AudioSource menuMusicSource;
    public float musicFadeDuration = 2.0f;
    public AudioSource sfxSource;
    public AudioClip standUpSound;

    [Header("Kapatılacak Kafa Modelleri")]
    public GameObject[] headModelsToHide;

    public AudioSource gameMusicSource;
    public float gameMusicFadeDuration = 3.0f;

    void Start()
    {
        if (GameManager.Instance != null && !GameManager.Instance.isGameStarted)
        {
            if (playerAnimator != null)
            {
                playerAnimator.Play("Sitting");
            }

            foreach (GameObject ui in inGameUIPanels)
            {
                if (ui != null)
                    ui.SetActive(false);
            }

            // Menü = intronun ilk karesi, kamera kafada: Start'a basınca kesmeden akar.
            if (introTimeline != null)
            {
                introTimeline.time = introStartTime;
                introTimeline.Play();
                introTimeline.playableGraph.GetRootPlayable(0).SetSpeed(0);
            }
            if (cameraTarget != null && cameraAnchor != null)
            {
                cameraTarget.SetParent(cameraAnchor);
                cameraTarget.localPosition = Vector3.zero;
                cameraTarget.localRotation = Quaternion.identity;
            }

            if (menuMusicSource != null && !menuMusicSource.isPlaying)
            {
                menuMusicSource.Play();
                foreach (GameObject obj in menuObjectsToHide)
                {
                    if (obj != null)
                        obj.SetActive(true);
                }
            }
        }
    }

    public void PlayStartSequence()
    {
        StartCoroutine(GameStartRoutine());
    }

    private IEnumerator GameStartRoutine()
    {
        // 1. FAREYİ KİLİTLE VE MENÜ ELEMANLARINI GİZLE
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (MegaphoneSystem.Instance != null)
        {
            MegaphoneSystem.Instance.TriggerGameStartAudio();
        }

        foreach (GameObject obj in menuObjectsToHide)
        {
            if (obj != null)
                StartCoroutine(FadeOutAndHide(obj, uiFadeDuration));
        }
        // ---> YENİ: OYUN MÜZİĞİNİ YAVAŞÇA BAŞLAT <---

        if (menuMusicSource != null)
        {
            StartCoroutine(FadeOutMusic(menuMusicSource, musicFadeDuration));
        }
        if (gameMusicSource != null)
        {
            StartCoroutine(FadeInMusic(gameMusicSource, gameMusicFadeDuration));
        }
        // =================================================================
        // 2. TİMELİNE ÇALIŞSIN, KAMERA BAŞTAN SONA KAFADA (FPS) KALSIN
        // =================================================================
        bool wasSkipped = false;
        if (introTimeline != null)
        {
            if (introTimeline.state != PlayState.Playing)
            {
                introTimeline.time = introStartTime;
                introTimeline.Play();
            }
            introTimeline.playableGraph.GetRootPlayable(0).SetSpeed(1); // menüde donuk duran kare akmaya başlar

            // 🚨 SİHİRLİ DOKUNUŞ: Timeline oynadığı süre boyunca her kare (frame) çalışır

            float timelineTimer = (float)introTimeline.time;
            float skipTimer = 0f;
            float skipHoldTime = 1.5f; // Atlamak için E'ye basılı tutulacak süre (saniye)
            var skipHint = IntroSkipHint.Create(); // sağ altta "[E] Geçmek için basılı tut"
            while (timelineTimer < (float)introTimeline.duration)
            {
                timelineTimer += Time.deltaTime;
                skipHint.SetProgress(skipTimer / skipHoldTime);
                if (GameInput.Held("SkipIntro"))
                {
                    skipTimer += Time.deltaTime;
                    if (skipTimer >= skipHoldTime)
                    {
                        if (cameraTarget != null) cameraTarget.SetParent(null); // gövde son kareye atlar, kamera kayarak gider
                        introTimeline.time = introTimeline.duration;
                        introTimeline.Evaluate(); // <--- BU SATIRI MUTLAKA EKLE (Kamerayı anında son kareye ışınlar)
                        wasSkipped = true;
                        break;
                    }
                }
                else
                {
                    skipTimer = 0f; // Tuş bırakılırsa sayacı sıfırla
                }
                if (cameraTarget != null && cameraTarget.parent == cameraAnchor)
                {
                    if (introTimeline.time >= headReleaseTime)
                        cameraTarget.SetParent(null); // dünya pozunda kalır, kalkış geçişi buradan kafaya gider
                    else
                        cameraTarget.localRotation = Quaternion.Euler(-introLookUp.Evaluate((float)introTimeline.time), 0f, 0f);
                }

                yield return null; // Bir sonraki frame'e geç
            }

            skipHint.SetProgress(wasSkipped ? 1 : 0); skipHint.Close();
        }

        // =================================================================
        // 3. TİMELİNE BİTTİ, STANDUP ANİMASYONU BAŞLIYOR
        // =================================================================
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("StandUp");
        }
        if (sfxSource != null && standUpSound != null)
        {
            sfxSource.PlayOneShot(standUpSound);
        }

        // KAMERA YUMUŞAK GEÇİŞİ (IŞINLANMA YERİNE BURAYI KULLANIYORUZ)
        if (cameraTarget != null && cameraAnchor != null && cameraTarget.parent != cameraAnchor)
        {
            // Eğer E'ye basıp atladıysak kamera hızlıca (örn: 0.5 saniyede) yerine gitsin.
            // Atlamadıysak, kalkma animasyonu süresi (standUpDuration) kadar sürede gitsin.
            float transitionTime = wasSkipped ? 0.5f : standUpDuration;

            yield return StartCoroutine(
                SmoothCameraTransition(cameraTarget, cameraAnchor, transitionTime)
            );
        }
        else
        {
            // Kamera zaten kafada; kalkma animasyonunu kafadan izler.
            yield return new WaitForSeconds(wasSkipped ? 0.5f : standUpDuration);
        }
        if (headModelsToHide != null)
        {
            foreach (GameObject head in headModelsToHide)
            {
                if (head != null)
                    head.SetActive(false);
            }
        }

        // =================================================================
        // 4. KONTROLLERİ VER VE OYUN İÇİ UI AÇ
        // =================================================================
        GameManager.Instance.StartGameMode();

        if (playerController != null)
        {
            playerController.ResetHeadBobYPos(0f);

            if (cameraTarget != null)
            {
                float finalYaw = cameraTarget.eulerAngles.y;
                float finalPitch = cameraTarget.eulerAngles.x;

                if (finalPitch > 180f)
                    finalPitch -= 360f;

                playerController.ForceCameraRotation(finalYaw, finalPitch);
            }
        }

        foreach (GameObject ui in inGameUIPanels)
        {
            if (ui != null)
            {
                StartCoroutine(FadeInUI(ui, uiFadeDuration));
            }
        }

        this.enabled = false;
    }

    private IEnumerator FadeInMusic(AudioSource audioSource, float duration)
    {
        float targetVolume = audioSource.volume; // Inspector'da ayarladığın sesi hedef alır
        audioSource.volume = 0f;
        audioSource.Play();

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, elapsedTime / duration);
            yield return null;
        }

        audioSource.volume = targetVolume;
    }

    private IEnumerator FadeOutAndHide(GameObject panel, float duration)
    {
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        panel.SetActive(false);
    }

    private IEnumerator FadeOutMusic(AudioSource audioSource, float duration)
    {
        float startVolume = audioSource.volume;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / duration);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
    }

    private IEnumerator FadeInUI(GameObject panel, float duration)
    {
        panel.SetActive(true);
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    public void InstantSetupForLoad()
    {
        if (introTimeline != null)
            introTimeline.Stop(); // menüde intronun ilk karesinde donuk bekliyordu

        if (cameraTarget != null && cameraAnchor != null)
        {
            cameraTarget.SetParent(cameraAnchor);
            cameraTarget.localPosition = Vector3.zero;
            cameraTarget.localRotation = Quaternion.identity;
        }

        if (playerAnimator != null)
        {
            playerAnimator.Rebind();
            playerAnimator.Update(0f);
        }

        foreach (GameObject obj in menuObjectsToHide)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        foreach (GameObject ui in inGameUIPanels)
        {
            if (ui != null)
            {
                ui.SetActive(true);
                CanvasGroup cg = ui.GetComponent<CanvasGroup>();
                if (cg != null)
                    cg.alpha = 1f;
            }
        }

        if (menuMusicSource != null)
        {
            menuMusicSource.Stop();
        }
        if (gameMusicSource != null && !gameMusicSource.isPlaying)
        {
            gameMusicSource.Play();
        }
        this.enabled = false;
    }

    private IEnumerator SmoothCameraTransition(Transform target, Transform anchor, float duration)
    {
        // 1. İşlem bitene kadar Parent yapmıyoruz! Dünyadaki koordinatları alıyoruz.
        target.SetParent(null);
        Vector3 startPos = target.position;
        Quaternion startRot = target.rotation;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // Yumuşaklık formülü

            // 2. Hedefe (anchor) world space üzerinden gidiyoruz.
            target.position = Vector3.Lerp(startPos, anchor.position, t);

            // 3. Slerp yerine Lerp kullanıyoruz ki 360 derece sapıtmaları KESİNLİKLE olmasın.
            target.rotation = Quaternion.Lerp(startRot, anchor.rotation, t);
            yield return null;
        }

        // 4. Hedefe ulaştığımızda Parent yapıp değerleri pürüzsüzce sıfırlıyoruz.
        target.SetParent(anchor);
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
    }
}
