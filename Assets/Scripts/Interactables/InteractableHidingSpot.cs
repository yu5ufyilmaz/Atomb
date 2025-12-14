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

    private UnityEngine.CharacterController playerController;
    private StarterAssets.StarterAssetsInputs playerInput;
    private Animator playerAnimator;
    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;
    private Transform headBone;

    public bool IsOccupied => isOccupied;

    private void Start()
    {
        playerController = FindObjectOfType<UnityEngine.CharacterController>();
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
            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0))
                AttemptExit();
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
                Time.deltaTime * 6f
            );
            mainCamera.rotation = Quaternion.Slerp(
                mainCamera.rotation,
                targetPos.rotation,
                Time.deltaTime * 6f
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
            StartCoroutine(CaughtSequence());
        else
            StartCoroutine(ExitSequence());
    }

    // --- GİRİŞ SEKANSI ---
    private IEnumerator EnterSequence()
    {
        inTransition = true;
        ToggleControls(false);

        // --- GÜNCELLEME: GameManager'a Kayıt Ol ---
        // Lees bizi burada bulsun diye kendimizi kaydediyoruz.
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;
        // ------------------------------------------

        // 1. Kapı önüne git, İçeriye (InsidePos) dön
        Quaternion lookInRot = Quaternion.LookRotation(
            insidePosition.position - exitPosition.position
        );
        yield return StartCoroutine(
            MoveAndLockRotation(exitPosition.position, lookInRot, alignDuration)
        );

        isOccupied = true;
        PlaySound(hideSound);
        if (propAnimator)
            propAnimator.SetTrigger(propOpenTrigger);
        if (playerAnimator)
            playerAnimator.SetTrigger(playerAnimTrigger);

        // 2. İçeri Yürü (Rotasyon: İçeriye kilitli)
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
        if (propAnimator)
            propAnimator.SetTrigger(propCloseTrigger);
        inTransition = false;
    }

    // --- ÇIKIŞ SEKANSI (KİLİTLİ ROTASYON) ---
    private IEnumerator ExitSequence()
    {
        inTransition = true;

        // --- GÜNCELLEME: GameManager'dan Kaydı Sil ---
        // Artık çıkıyoruz, Lees bizi cihazda aramasın.
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        // --------------------------------------------

        if (propAnimator)
            propAnimator.SetTrigger(propOpenTrigger);
        PlaySound(unhideSound);
        yield return new WaitForSeconds(0.1f);

        // 1. POZİSYON VE YÖNÜ AYARLA (GÖRÜNMEZKEN)
        if (insidePosition != null && playerController != null)
        {
            playerController.enabled = false;

            // Konum: Dolap içi
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

        // 3. ANİMASYON
        if (playerAnimator)
        {
            playerAnimator.ResetTrigger(playerAnimTrigger);
            playerAnimator.SetTrigger(playerAnimTrigger);
        }

        // 4. DIŞARI YÜRÜ (ROTASYON KİLİTLİ)
        StartCoroutine(
            MoveAndLockRotation(exitPosition.position, exitPosition.rotation, enterAnimDuration)
        );

        yield return new WaitForSeconds(enterAnimDuration);

        // 5. BİTİŞ
        mainCamera.SetParent(null);
        if (playerController)
            playerController.enabled = true;
        if (cinemachineBrain)
            cinemachineBrain.enabled = true;
        ToggleControls(true);
        isOccupied = false;
        isPeeking = false;
        inTransition = false;

        if (propAnimator)
            propAnimator.SetTrigger(propCloseTrigger);
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

        // Bu senaryoda kapıyı Guderian açacağı için biz animasyon tetiklemiyoruz.
        yield return new WaitForSeconds(0.1f);

        if (GuderianAI.Instance != null)
        {
            GuderianAI.Instance.TriggerLockerJumpscare(exitPosition);
        }
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
