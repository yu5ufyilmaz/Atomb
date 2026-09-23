using System.Collections;
using Cinemachine;
using UnityEngine;

// IForceExitable arayüzü eklendi
public class InteractableHidingSpot : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Pozisyon Ayarları")]
    [Tooltip("Saklanınca kameranın duracağı dip nokta (Dolabın içi)")]
    [SerializeField]
    private Transform hideCameraPosition;
    private StarterAssets.CharacterController playerMoveScript;

    [Tooltip("Peek atınca (kafa uzatınca) kameranın geleceği nokta")]
    [SerializeField]
    private Transform peekCameraPosition;

    [Tooltip("Kapı önü noktası. MAVİ OKU (Z) Mutlaka Odaya Bakmalı!")]
    [SerializeField]
    private Transform exitPosition;

    [Tooltip("Dolap içi zemin noktası.")]
    [SerializeField]
    private Transform insidePosition;

    [Header("Zamanlama")]
    [SerializeField]
    private float alignDuration = 0.5f;

    [SerializeField]
    private float cameraDockDuration = 0.6f;

    [SerializeField]
    private float enterAnimDuration = 2.0f;

    [Header("Peek Kamera Kısıtlama Ayarları")]
    [SerializeField]
    private float peekYawLimit = 35f; // Sağa-sola bakış limiti (derece)

    [SerializeField]
    private float peekPitchLimit = 20f; // Yukarı-aşağı bakış limiti (derece)

    [SerializeField]
    private float peekLookSensitivity = 2f; // Fare dönüş hassasiyeti

    private float currentPeekYaw = 0f;
    private float currentPeekPitch = 0f;

    [Header("Animasyon & Ses")]
    [SerializeField]
    private Animator propAnimator;

    [SerializeField]
    private string propOpenTrigger = "Open";

    [SerializeField]
    private string propCloseTrigger = "Close";

    [SerializeField]
    private string propPeekBool = "IsPeeking";

    [SerializeField]
    private string playerAnimTrigger = "HideEnter";

    [SerializeField]
    private Vector3 headOffset = new Vector3(0, 0.1f, 0.15f);

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
    public bool canExit = true;

    private UnityEngine.CharacterController playerController;
    private StarterAssets.StarterAssetsInputs playerInput;
    private Animator playerAnimator;
    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;
    private Transform headBone;
    private Transform originalCameraParent;
    public bool IsOccupied => isOccupied;

    [Header("Guderian Peek Ayarları")]
    [SerializeField]
    private float maxPeekTimeWhileDangerous = 2.0f;
    private float currentPeekTimer = 0f;

    [Tooltip("Kapı açılırken karakterin ne kadar bekleyeceği (Animasyon süresi kadar yap)")]
    [SerializeField]
    private float doorOpenDelay = 1.0f; // <-- BUNU EKLE

    private void Start()
    {
        playerController = Object.FindFirstObjectByType<UnityEngine.CharacterController>();
        if (playerController)
        {
            playerInput = playerController.GetComponent<StarterAssets.StarterAssetsInputs>();
            playerAnimator = playerController.GetComponent<Animator>();
            if (playerAnimator != null)
                headBone = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                headBone = playerController.transform;
        }
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            originalCameraParent = mainCamera.parent; // Eski ebeveyni kaydet
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
        return isOccupied ? "[Sol Tık / W] Gözetle | [F] Çık" : "[Sol Tık] Saklan";
    }

    private void Update()
    {
        if (isOccupied && !inTransition)
        {
            HandlePeeking();

            // Sol tık artık peek yaptığı için çıkış sadece F tuşuna bağlandı
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (canExit)
                    AttemptExit();
            }
        }
    }

    private void HandlePeeking()
    {
        // Sağ tık fenerle çakışmasın diye Sol Tık (0) ve W tuşuna bağlandı
        bool holdingPeek = Input.GetKey(KeyCode.W) || Input.GetMouseButton(0);

        if (holdingPeek != isPeeking)
        {
            isPeeking = holdingPeek;

            if (propAnimator)
                propAnimator.SetBool(propPeekBool, isPeeking);

            if (isPeeking && peekSound && audioSource)
                audioSource.PlayOneShot(peekSound);

            // Gözetlemeyi bıraktığında merkez açıyı sıfırla
            if (!isPeeking)
            {
                currentPeekYaw = 0f;
                currentPeekPitch = 0f;
                currentPeekTimer = 0f;
            }
        }
        if (isPeeking && GuderianAI.Instance != null)
        {
            var gState = GuderianAI.Instance.currentState;

            // Bekleme (Hidden, WaitingBehindDoor, Ambush) dışındaki tüm hareketli durumlar tehlikelidir
            bool isDangerous = (
                gState == GuderianAI.GuderianState.Approaching
                || gState == GuderianAI.GuderianState.Breaching
                || gState == GuderianAI.GuderianState.Entering
                || gState == GuderianAI.GuderianState.Searching
                || gState == GuderianAI.GuderianState.Exiting
            );

            if (isDangerous)
            {
                currentPeekTimer += Time.deltaTime;
                if (currentPeekTimer >= maxPeekTimeWhileDangerous)
                {
                    currentPeekTimer = 0f;
                    isPeeking = false;
                    if (propAnimator)
                        propAnimator.SetBool(propPeekBool, false);
                    StartCoroutine(CaughtSequence());
                    return; // Kamerayı daha fazla oynatma, Jumpscare'e geç
                }
            }
            else
            {
                // Bekleme modundaysa sayacı sıfırla, istediği kadar durabilir
                currentPeekTimer = 0f;
            }
        }
        if (mainCamera != null && hideCameraPosition != null && peekCameraPosition != null)
        {
            Transform targetAnchor = isPeeking ? peekCameraPosition : hideCameraPosition;

            // Pozisyonu hedefe yumuşakça taşı
            mainCamera.position = Vector3.Lerp(
                mainCamera.position,
                targetAnchor.position,
                Time.deltaTime * 6f
            );

            Quaternion targetRotation;

            if (isPeeking)
            {
                // Peek sırasında fare girdilerini alıp sınırla
                float mouseX = Input.GetAxis("Mouse X") * peekLookSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * peekLookSensitivity;

                currentPeekYaw = Mathf.Clamp(currentPeekYaw + mouseX, -peekYawLimit, peekYawLimit);
                currentPeekPitch = Mathf.Clamp(
                    currentPeekPitch - mouseY,
                    -peekPitchLimit,
                    peekPitchLimit
                );

                // Peek noktasının ana rotasyonuna kısıtlanmış açıları ekle
                Quaternion offsetRot = Quaternion.Euler(currentPeekPitch, currentPeekYaw, 0f);
                targetRotation = peekCameraPosition.rotation * offsetRot;
            }
            else
            {
                targetRotation = hideCameraPosition.rotation;
            }

            // Rotasyonu slerp ile yumuşat
            mainCamera.rotation = Quaternion.Slerp(
                mainCamera.rotation,
                targetRotation,
                Time.deltaTime * 10f
            );
        }
    }

    private void EnterHiding()
    {
        StartCoroutine(EnterSequence());
    }

    private void AttemptExit()
    {
        // 1. BUG FIX: Çıkış yaparken kapak açık kalmasın diye Peek durumunu zorla kapatıyoruz.
        isPeeking = false;
        if (propAnimator)
            propAnimator.SetBool(propPeekBool, false);

        // 2. ERKEN ÇIKIŞ ÖLÜM KONTROLÜ
        if (GuderianAI.Instance != null)
        {
            var gState = GuderianAI.Instance.currentState;
            bool isBreachingOrEntering = (
                gState == GuderianAI.GuderianState.Approaching
                || gState == GuderianAI.GuderianState.Breaching
                || gState == GuderianAI.GuderianState.Entering
            );

            // Guderian kamp kurmuşsa VEYA henüz odaya girme/kapı kırma aşamasındaysa anında yakalan!
            if (GuderianAI.Instance.IsCampingPlayer(this) || isBreachingOrEntering)
            {
                StartCoroutine(CaughtSequence());
                return;
            }
        }

        StartCoroutine(ExitSequence());
    }

    // --- GİRİŞ SEKANSI ---
    private IEnumerator EnterSequence()
    {
        inTransition = true;

        // Eğer playerMoveScript null ise burada bulalım ki hata vermesin
        if (playerMoveScript == null && playerController != null)
            playerMoveScript = playerController.GetComponent<StarterAssets.CharacterController>();

        ToggleControls(false); // Dondur

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        // 1. Kapı önüne hizalan
        Quaternion lookInRot = Quaternion.LookRotation(
            insidePosition.position - exitPosition.position
        );
        yield return StartCoroutine(
            MoveAndLockRotation(exitPosition.position, lookInRot, alignDuration)
        );

        isOccupied = true;
        PlaySound(hideSound);

        // A. KAPIYI AÇ
        if (propAnimator)
            propAnimator.SetTrigger(propOpenTrigger);
        if (playerAnimator)
            playerAnimator.SetTrigger(playerAnimTrigger);

        // B. BEKLE (Animasyon süresi kadar)
        yield return new WaitForSeconds(doorOpenDelay);

        // 2. İÇERİ YÜRÜ
        StartCoroutine(MoveAndLockRotation(insidePosition.position, lookInRot, enterAnimDuration));

        // Kamera Kafa Takibi
        if (cinemachineBrain)
            cinemachineBrain.enabled = false;

        if (headBone != null)
        {
            mainCamera.SetParent(headBone);
            Vector3 startLocalPos = mainCamera.localPosition;
            Quaternion startLocalRot = mainCamera.localRotation;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                mainCamera.localPosition = Vector3.Lerp(startLocalPos, headOffset, t / 0.5f);
                mainCamera.localRotation = Quaternion.Slerp(
                    startLocalRot,
                    Quaternion.identity,
                    t / 0.5f
                );
                yield return null;
            }
            mainCamera.localPosition = headOffset;
            mainCamera.localRotation = Quaternion.identity;
        }

        // Docking Bekleme
        float safeWaitDuration = enterAnimDuration - cameraDockDuration - 0.2f;
        if (safeWaitDuration < 0)
            safeWaitDuration = 0;
        yield return new WaitForSeconds(safeWaitDuration);

        // Kamera Yerleşimi
        mainCamera.SetParent(null);
        if (hideCameraPosition)
        {
            Vector3 startDockPos = mainCamera.position;
            Quaternion startDockRot = mainCamera.rotation;
            float t = 0f;
            while (t < cameraDockDuration)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / cameraDockDuration);
                mainCamera.position = Vector3.Lerp(
                    startDockPos,
                    hideCameraPosition.position,
                    smoothT
                );
                mainCamera.rotation = Quaternion.Slerp(
                    startDockRot,
                    hideCameraPosition.rotation,
                    smoothT
                );

                if (t > (cameraDockDuration * 0.3f))
                    TogglePlayerModel(false);
                yield return null;
            }
            mainCamera.position = hideCameraPosition.position;
            mainCamera.rotation = hideCameraPosition.rotation;
        }

        TogglePlayerModel(false);

        // C. KAPIYI KAPAT
        if (propAnimator)
            propAnimator.SetTrigger(propCloseTrigger);

        inTransition = false;
    }

    // --- ÇIKIŞ SEKANSI ---
    private IEnumerator ExitSequence()
    {
        inTransition = true;

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;

        // A. KAPIYI AÇ
        if (propAnimator)
            propAnimator.SetTrigger(propOpenTrigger);
        PlaySound(unhideSound);

        // B. BEKLE (Kapı açılsın diye)
        yield return new WaitForSeconds(doorOpenDelay);

        // 1. POZİSYON VE YÖNÜ AYARLA
        if (insidePosition != null && playerController != null)
        {
            playerController.enabled = false; // Fiziği kapat
            playerController.transform.position = insidePosition.position;
            playerController.transform.rotation = exitPosition.rotation;
            yield return null;
        }

        TogglePlayerModel(true);

        // 2. KAMERAYI KAFAYA AL
        if (headBone != null)
        {
            mainCamera.SetParent(headBone);
            Vector3 startLocalPos = mainCamera.localPosition;
            Quaternion startLocalRot = mainCamera.localRotation;
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / 0.2f);
                mainCamera.localPosition = Vector3.Lerp(startLocalPos, headOffset, smoothT);
                mainCamera.localRotation = Quaternion.Slerp(
                    startLocalRot,
                    Quaternion.identity,
                    smoothT
                );
                yield return null;
            }
            mainCamera.localPosition = headOffset;
            mainCamera.localRotation = Quaternion.identity;
        }

        if (playerAnimator)
        {
            playerAnimator.ResetTrigger(playerAnimTrigger);
            playerAnimator.SetTrigger(playerAnimTrigger);
        }

        // 4. DIŞARI YÜRÜ
        // MoveAndLockRotation metodunda playerPhysics kullanıyorsan orayı da playerController yapmayı unutma!
        StartCoroutine(
            MoveAndLockRotation(exitPosition.position, exitPosition.rotation, enterAnimDuration)
        );

        yield return new WaitForSeconds(enterAnimDuration);

        // 5. BİTİŞ - KAPIYI KAPAT
        if (propAnimator)
            propAnimator.SetTrigger(propCloseTrigger);

        mainCamera.SetParent(originalCameraParent);

        // KAMERA YÖNÜNÜ DÜZELT (Yüzünü görmemen için)
        if (playerMoveScript != null)
        {
            float finalYaw = mainCamera.rotation.eulerAngles.y;
            float finalPitch = mainCamera.rotation.eulerAngles.x;
            playerMoveScript.ForceCameraRotation(finalYaw, finalPitch);
        }

        if (playerController)
            playerController.enabled = true; // Fiziği aç
        if (cinemachineBrain)
            cinemachineBrain.enabled = true;

        ToggleControls(true); // Kontrolü ver

        isOccupied = false;
        isPeeking = false;
        inTransition = false;
        PlayerInteraction.NotifyInteractionExit(gameObject);
    }

    // --- HAREKET ET VE ROTASYONU KİLİTLE ---
    private IEnumerator MoveAndLockRotation(Vector3 targetPos, Quaternion fixedRot, float duration)
    {
        Vector3 startPos = playerController.transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            playerController.transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            playerController.transform.rotation = fixedRot;

            yield return null;
        }
        playerController.transform.position = targetPos;
        playerController.transform.rotation = fixedRot;
    }

    // --- GUDERIAN YAKALANMA SEKANSI ---
    private IEnumerator CaughtSequence()
    {
        inTransition = true;

        // Kamerayı serbest bırak (Açısını sıfırlama!)
        if (mainCamera != null && originalCameraParent != null)
        {
            mainCamera.SetParent(originalCameraParent);
        }

        //TogglePlayerModel(false);

        // Hiç beklemeden anında Guderian'ı tetikle
        if (GuderianAI.Instance != null)
        {
            GuderianAI.Instance.TriggerLockerJumpscare(insidePosition);
        }
        yield return null;
    }

    private void ToggleControls(bool state)
    {
        if (playerInput)
        {
            playerInput.cursorInputForLook = state;
            playerInput.move = Vector2.zero;
            playerInput.enabled = state;
        }
        if (playerController)
            playerController.enabled = state;
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

    private void OnDrawGizmos()
    {
        if (exitPosition != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(exitPosition.position, exitPosition.forward * 1.5f);
            Gizmos.DrawSphere(exitPosition.position + exitPosition.forward * 1.5f, 0.1f);
        }
    }

    public void OnFocus() { } // Şimdilik boş kalsın

    public void OnLoseFocus() { } // Şimdilik boş kalsın

    // --- IForceExitable Arayüzü Uygulaması ---
    // Lees (veya başka sistemler) tarafından çağrılır.
    public void ForceExit()
    {
        // Eğer zaten çıkıyorsak veya boşsa bir şey yapma
        if (inTransition || !isOccupied)
            return;

        // Normal çıkış rutinini başlat.
        // Bu sayede karakter animasyonla çıkar ve görünür olur.
        StartCoroutine(ExitSequence());
    }
}
