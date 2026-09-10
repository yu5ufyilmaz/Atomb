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

    private Vector3 lockedPosition;
    private bool isCameraLocked = false;

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

            if (cameraTarget != null)
            {
                lockedPosition = cameraTarget.position;
                isCameraLocked = true;
                cameraTarget.SetParent(null);
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

    void LateUpdate()
    {
        // Kamera kilitliyken hedefi sabit tutar
        if (isCameraLocked && cameraTarget != null)
        {
            cameraTarget.position = lockedPosition;
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

        if (menuMusicSource != null)
        {
            StartCoroutine(FadeOutMusic(menuMusicSource, musicFadeDuration));
        }

        // =================================================================
        // 2. TİMELİNE ÇALIŞSIN VE ROOT OBJESİ GİZLİCE KAMERAYI TAKİP ETSİN!
        // =================================================================
        if (introTimeline != null)
        {
            isCameraLocked = false;

            introTimeline.Play();

            // 🚨 SİHİRLİ DOKUNUŞ: Timeline oynadığı süre boyunca her kare (frame) çalışır

            float timelineTimer = 0f;
            float skipTimer = 0f;
            float skipHoldTime = 1.5f; // Atlamak için E'ye basılı tutulacak süre (saniye)
            while (timelineTimer < (float)introTimeline.duration)
            {
                timelineTimer += Time.deltaTime;
                if (Input.GetKey(KeyCode.E))
                {
                    skipTimer += Time.deltaTime;
                    if (skipTimer >= skipHoldTime)
                    {
                        introTimeline.time = introTimeline.duration; // Timeline'ı sona sar
                        break; // Döngüden hemen çık
                    }
                }
                else
                {
                    skipTimer = 0f; // Tuş bırakılırsa sayacı sıfırla
                }
                // Root objesini (0,0,0'da unutulan objeyi) her saniye zorla kameranın içine çekiyoruz
                if (cameraTarget != null && Camera.main != null)
                {
                    cameraTarget.position = Camera.main.transform.position;
                    cameraTarget.rotation = Camera.main.transform.rotation;
                    lockedPosition = cameraTarget.position;
                }

                yield return null; // Bir sonraki frame'e geç
            }

            // Timeline bittiğinde Root objesi tam olarak son kameranın (yüz kamerasının) olduğu yerde kalır!
            isCameraLocked = true;
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

        // KAMERA YUMUŞAK GEÇİŞİ (Ayağa kalkarken)
        // Artık lockedPosition kesinlikle 0,0,0 DEĞİL, yüz kamerasının koordinatı!
        if (cameraTarget != null && cameraAnchor != null)
        {
            Vector3 startPos = lockedPosition;
            Quaternion startRot = cameraTarget.rotation;

            float elapsedTime = 0f;

            while (elapsedTime < standUpDuration)
            {
                elapsedTime += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, elapsedTime / standUpDuration);

                lockedPosition = Vector3.Lerp(startPos, cameraAnchor.position, smoothT);
                cameraTarget.rotation = Quaternion.Lerp(startRot, cameraAnchor.rotation, smoothT);

                yield return null;
            }

            isCameraLocked = false;

            cameraTarget.SetParent(cameraAnchor);
            cameraTarget.localPosition = Vector3.zero;
            cameraTarget.localRotation = Quaternion.identity;
        }
        else
        {
            yield return new WaitForSeconds(standUpDuration);
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
        isCameraLocked = false;

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

        this.enabled = false;
    }
}
