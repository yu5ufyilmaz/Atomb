using System.Collections;
using UnityEngine;

public class InGameMenuController : MonoBehaviour
{
    [Header("Referanslar")]
    public Animator playerAnimator;
    public StarterAssets.CharacterController playerController;

    [Header("Menü Objeleri (Masadaki Yazılar)")]
    [Tooltip("Oyun başlar başlamaz gizlenecek 3D Text veya objeler")]
    public GameObject[] menuObjectsToHide;

    [Header("Kamera Bağlama Ayarları")]
    public Transform cameraTarget;
    public Transform cameraAnchor;
    public float standUpDuration = 2.5f;

    [Header("UI Ayarları")]
    public GameObject[] inGameUIPanels;
    public float uiFadeDuration = 1.5f;

    // --- YENİ EKLENEN SES AYARLARI ---
    [Header("Ses Ayarları")]
    [Tooltip("Menüdeyken çalacak müziğin AudioSource'u")]
    public AudioSource menuMusicSource;

    [Tooltip("Müziğin kaç saniyede yavaşça kısılarak kapanacağı")]
    public float musicFadeDuration = 2.0f;

    [Tooltip("Ayağa kalkma sesi gibi kısa efektleri çalacak AudioSource")]
    public AudioSource sfxSource;

    [Tooltip("Ayağa kalkarken çalacak ses efekti (Sandelye gıcırtısı vs.)")]
    public AudioClip standUpSound;

    // Kamera kilit sistemi
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

            // InGameMenuController.cs Start() metodu içi
            if (cameraTarget != null)
            {
                lockedPosition = cameraTarget.position;
                isCameraLocked = true;

                // --- YENİ EKLENEN KOD: Kamerayı kemikten ayır ki titremesin ---
                cameraTarget.SetParent(null);
            }

            // --- YENİ: MENÜ MÜZİĞİNİ BAŞLAT ---
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
        // --- 1. OYUNCU KİLİDİNİ AÇMA, SADECE FAREYİ GİZLE ---
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Masadaki yazıları yavaşça silerek kapat
        foreach (GameObject obj in menuObjectsToHide)
        {
            if (obj != null)
            {
                StartCoroutine(FadeOutAndHide(obj, uiFadeDuration));
            }
        }

        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("StandUp");
        }

        if (sfxSource != null && standUpSound != null)
        {
            sfxSource.PlayOneShot(standUpSound);
        }

        if (menuMusicSource != null)
        {
            StartCoroutine(FadeOutMusic(menuMusicSource, musicFadeDuration));
        }

        // --- KAMERA YUMUŞAK GEÇİŞİ ---
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

            // Kamerayı fiziksel olarak Anchor'a (Kafaya) bağla ve İÇ pozisyonunu/rotasyonunu KESİN SIFIRLA
            cameraTarget.SetParent(cameraAnchor);
            cameraTarget.localPosition = Vector3.zero;
            cameraTarget.localRotation = Quaternion.identity; // Kemik nereye bakıyorsa oraya bak
        }
        else
        {
            yield return new WaitForSeconds(standUpDuration);
        }

        // --- 2. GEÇİŞ BİTTİ: OYUNU ŞİMDİ BAŞLAT (Karakterin kilidini aç) ---
        GameManager.Instance.StartGameMode();

        if (playerController != null)
        {
            playerController.ResetHeadBobYPos(0f);

            // --- 3. KAMERA AÇILARINI SENKRONİZE ET (Kontrol verildiği an ışınlanmasın) ---
            if (cameraTarget != null)
            {
                float finalYaw = cameraTarget.eulerAngles.y;
                float finalPitch = cameraTarget.eulerAngles.x;

                // Pitch (X) açısı Unity'de 360 sarmalına girebilir, düzeltiyoruz:
                if (finalPitch > 180f)
                    finalPitch -= 360f;

                // CharacterController'a kameranın şu anki açısını öğretiyoruz
                playerController.ForceCameraRotation(finalYaw, finalPitch);
            }
        }

        if (MegaphoneSystem.Instance != null)
        {
            MegaphoneSystem.Instance.TriggerGameStartAudio();
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

    // --- YENİ: Objeleri yavaşça şeffaflaştırıp sonra tamamen kapatan fonksiyon ---
    private IEnumerator FadeOutAndHide(GameObject panel, float duration)
    {
        // Objenin üzerinde CanvasGroup yoksa otomatik ekle
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        // Zamanla şeffaflığı 0'a (tamamen görünmez) doğru çek
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / duration);
            yield return null;
        }

        // Garanti olsun diye tam sıfırla ve objeyi kapat (performans için)
        canvasGroup.alpha = 0f;
        panel.SetActive(false);
    }

    // --- YENİ: MÜZİK YUMUŞAKÇA KISMA FONKSİYONU ---
    private IEnumerator FadeOutMusic(AudioSource audioSource, float duration)
    {
        float startVolume = audioSource.volume; // O anki ses seviyesini al
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            // Sesi, başlangıç seviyesinden 0'a doğru yavaşça indir
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / duration);
            yield return null;
        }

        // Tamamen kısıldığında müziği durdur ve sesi sıfırla (garanti olsun diye)
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
}
