using System.Collections;
using Cinemachine;
using UnityEngine;

public class HidingSpot : MonoBehaviour, IInteractable
{
    [Header("Pozisyon Ayarları")]
    [Tooltip("Saklanınca kameranın duracağı dip nokta")]
    [SerializeField]
    private Transform hideCameraPosition;

    [Tooltip("Peek atınca (kafa uzatınca) kameranın geleceği nokta")]
    [SerializeField]
    private Transform peekCameraPosition;

    [Tooltip("Saklanmaktan çıkınca oyuncunun duracağı yer")]
    [SerializeField]
    private Transform exitPosition;

    [Header("Animasyon Ayarları")]
    [SerializeField]
    private Animator propAnimator;

    [SerializeField]
    private string propOpenTrigger = "Open";

    [SerializeField]
    private string propCloseTrigger = "Close";

    [SerializeField]
    private string propPeekBool = "IsPeeking";

    [Tooltip("Oyuncunun gireceği animasyonun Trigger adı")]
    [SerializeField]
    private string playerEnterAnimTrigger = "HideEnter";

    [SerializeField]
    private float enterAnimDuration = 2.0f;

    [Header("Kamera Ayarları (Head Cam)")]
    [Tooltip("Animasyon sırasında kameranın kafaya göre konumu (Göz hizası ayarı)")]
    [SerializeField]
    private Vector3 headOffset = new Vector3(0, 0.1f, 0.15f);

    [Header("Ses Efektleri")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip hideSound;

    [SerializeField]
    private AudioClip unhideSound;

    [SerializeField]
    private AudioClip peekSound;

    private bool isOccupied = false;
    private bool isPeeking = false;
    private bool inTransition = false;

    // Referanslar
    private UnityEngine.CharacterController playerController;
    private StarterAssets.StarterAssetsInputs playerInput;
    private Animator playerAnimator;
    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;
    private Transform headBone; // Kafa kemiğini burada tutacağız

    public bool IsOccupied => isOccupied;

    private void Start()
    {
        playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerController)
        {
            playerInput = playerController.GetComponent<StarterAssets.StarterAssetsInputs>();
            playerAnimator = playerController.GetComponent<Animator>();

            // KAFA KEMİĞİNİ OTOMATİK BULMA (Humanoid Rig ise çalışır)
            if (playerAnimator != null)
            {
                headBone = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
            }

            // Eğer Humanoid değilse veya bulamazsa manuel atama gerekebilir,
            // ama StarterAssets karakterleri genelde Humanoid'dir.
            if (headBone == null)
            {
                Debug.LogWarning(
                    "Kafa kemiği bulunamadı! Lütfen karakterin Humanoid Rig olduğundan emin olun."
                );
                // Yedek olarak karakterin transformunu alalım ki hata vermesin
                headBone = playerController.transform;
            }
        }

        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }
    }

    public void Interact()
    {
        if (inTransition)
            return;

        if (isOccupied)
            AttemptExit();
        else
            EnterHiding();
    }

    public string GetInteractionPrompt()
    {
        if (inTransition)
            return "";
        return isOccupied ? "[Sol Tık] Çık / [W] Gözetle" : "[Sol Tık] Saklan";
    }

    private void Update()
    {
        if (isOccupied && !inTransition)
        {
            HandlePeeking();
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
            {
                AttemptExit();
            }
        }
    }

    private void HandlePeeking()
    {
        bool holdingPeek = Input.GetKey(KeyCode.W) || Input.GetMouseButton(1);

        if (holdingPeek != isPeeking)
        {
            isPeeking = holdingPeek;
            if (propAnimator)
                propAnimator.SetBool(propPeekBool, isPeeking);
            if (isPeeking && peekSound && audioSource)
                audioSource.PlayOneShot(peekSound);
        }

        if (mainCamera != null && hideCameraPosition != null && peekCameraPosition != null)
        {
            Transform targetPos = isPeeking ? peekCameraPosition : hideCameraPosition;
            mainCamera.position = Vector3.Lerp(
                mainCamera.position,
                targetPos.position,
                Time.deltaTime * 5f
            );
            mainCamera.rotation = Quaternion.Slerp(
                mainCamera.rotation,
                targetPos.rotation,
                Time.deltaTime * 5f
            );
        }
    }

    private void EnterHiding()
    {
        StartCoroutine(EnterSequence());
    }

    private void AttemptExit()
    {
        if (GuderianAI.Instance != null && GuderianAI.Instance.IsCampingPlayer(this))
        {
            StartCoroutine(CaughtSequence());
        }
        else
        {
            StartCoroutine(ExitSequence());
        }
    }

    // --- İŞTE OLAYI ÇÖZEN GİRİŞ KODU ---
    private IEnumerator EnterSequence()
    {
        inTransition = true;
        isOccupied = true;

        // 1. Kontrolleri Kapat
        if (playerInput)
        {
            playerInput.cursorInputForLook = false;
            playerInput.move = Vector2.zero;
            playerInput.enabled = false;
        }
        if (playerController)
            playerController.enabled = false;

        // 2. Ses ve Dolap
        PlaySound(hideSound);
        if (propAnimator)
            propAnimator.SetTrigger(propOpenTrigger);

        // 3. Karakteri Hizala (Dolaba dön)
        playerController.transform.position = exitPosition.position;
        playerController.transform.rotation = transform.rotation;

        // 4. ANİMASYONU BAŞLAT
        if (playerAnimator)
            playerAnimator.SetTrigger(playerEnterAnimTrigger);

        // --- 5. KAMERA HAREKETİ (SMOOTH HEAD LOCK) ---

        // Cinemachine'i kapat (Kontrol bizde)
        if (cinemachineBrain)
            cinemachineBrain.enabled = false;

        if (headBone != null)
        {
            // A) Kamerayı fiziksel olarak kafaya bağla (Parenting)
            // Şu an kamera nerede duruyorsa orada kalsın ama artık kafayla beraber hareket etsin.
            mainCamera.SetParent(headBone);

            // B) Şu anki yerel (Local) pozisyonunu kaydet
            // (Bu pozisyon kafaya göre olan uzaklığıdır)
            Vector3 startLocalPos = mainCamera.localPosition;
            Quaternion startLocalRot = mainCamera.localRotation;

            // C) Hedef: Senin belirlediğin Head Offset (Göz hizası) ve dümdüz rotasyon
            Vector3 targetLocalPos = headOffset;
            Quaternion targetLocalRot = Quaternion.identity;

            float lockDuration = 0.5f; // Kafaya yerleşme süresi (Yarım saniye)
            float t = 0f;

            // D) Kamerayı bulunduğu yerden gözün içine doğru yumuşakça kaydır
            // Karakter animasyonla eğilirken kamera da yavaşça göz hizasına iner.
            while (t < lockDuration)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / lockDuration);

                mainCamera.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, smoothT);
                mainCamera.localRotation = Quaternion.Slerp(startLocalRot, targetLocalRot, smoothT);

                yield return null;
            }

            // Tam yerine oturt (Küsürat kalmasın)
            mainCamera.localPosition = targetLocalPos;
            mainCamera.localRotation = targetLocalRot;
        }

        // 6. Animasyonun Geri Kalanını Bekle
        // lockDuration kadar zaman zaten geçti, onu düşüyoruz.
        // Eğer animasyon 2 saniye ise, 0.5 saniye yerleşti, 1.5 saniye de kafada izleyeceğiz.
        yield return new WaitForSeconds(enterAnimDuration - 0.5f);

        // --- DOLABA YERLEŞME ---

        mainCamera.SetParent(null); // Kamerayı kafadan ayır

        // Kamerayı saklanma noktasına (hideCameraPosition) ışınla
        // (Burada da istersen Lerp yapabilirsin ama karakter gizleneceği için gerek yok)
        if (hideCameraPosition)
        {
            mainCamera.position = hideCameraPosition.position;
            mainCamera.rotation = hideCameraPosition.rotation;
        }

        // Modeli gizle
        TogglePlayerModel(false);
        if (propAnimator)
            propAnimator.SetTrigger(propCloseTrigger);

        inTransition = false;
    }

    private IEnumerator ExitSequence()
    {
        inTransition = true;

        // 1. Kapıyı Aç ve Sesi Çal
        if (propAnimator)
            propAnimator.SetTrigger(propOpenTrigger);
        PlaySound(unhideSound);

        yield return new WaitForSeconds(0.2f); // Kapı açılma payı

        // 2. Modeli Görünür Yap ve Oyuncuyu Işınla
        TogglePlayerModel(true);

        if (exitPosition)
        {
            // Işınlama sırasında CharacterController sorun çıkarmasın diye kapatıp açıyoruz
            if (playerController)
                playerController.enabled = false;

            playerController.transform.position = exitPosition.position;
            playerController.transform.rotation = exitPosition.rotation;

            if (playerController)
                playerController.enabled = true;
        }

        // 3. ANİMASYON DÜZELTME (HATA VEREN KISIM SİLİNDİ)
        // "Play" komutu yerine parametreleri sıfırlıyoruz.
        // StarterAssets'in kendi scripti Update'te animasyonu otomatik düzeltecektir.
        if (playerAnimator)
        {
            // Giriş animasyonu takılı kalmasın diye trigger'ı resetle
            playerAnimator.ResetTrigger(playerEnterAnimTrigger);

            // Hareketsiz durduğunu animatöre bildir
            playerAnimator.SetFloat("Speed", 0f);
            playerAnimator.SetFloat("MotionSpeed", 1f);
        }

        // 4. KAMERA GEÇİŞİ (Yumuşakça Dışarı Çıkış)
        if (cinemachineBrain)
            cinemachineBrain.enabled = false;
        mainCamera.SetParent(null); // Kamerayı serbest bırak

        Vector3 startPos = mainCamera.position;
        Quaternion startRot = mainCamera.rotation;

        // Hedef: Çıkış noktasında karakterin göz hizası (Yaklaşık 1.5m yukarı)
        Vector3 targetPos = exitPosition.position + (Vector3.up * 1.5f);
        Quaternion targetRot = exitPosition.rotation;

        float duration = 0.8f; // Çıkış süresi
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, t / duration);

            mainCamera.position = Vector3.Lerp(startPos, targetPos, smoothT);
            mainCamera.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

            yield return null;
        }

        // 5. Bitiş: Kontrolü Cinemachine'e Devret
        if (cinemachineBrain)
            cinemachineBrain.enabled = true;

        if (playerInput)
        {
            playerInput.enabled = true;
            playerInput.cursorInputForLook = true;
            playerInput.move = Vector2.zero; // Hareket girdisini sıfırla
        }

        isOccupied = false;
        isPeeking = false;
        inTransition = false;
    }

    private IEnumerator CaughtSequence()
    {
        inTransition = true;
        if (propAnimator)
            propAnimator.SetTrigger(propOpenTrigger);
        PlaySound(unhideSound);
        yield return new WaitForSeconds(0.2f);
        GuderianAI.Instance.TriggerLockerJumpscare(exitPosition);
    }

    private void TogglePlayerModel(bool show)
    {
        if (!playerController)
            return;
        Renderer[] renderers = playerController.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = show;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource && clip)
            audioSource.PlayOneShot(clip);
    }
}
