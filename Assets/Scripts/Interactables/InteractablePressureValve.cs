using System.Collections;
using Cinemachine;
using UnityEngine;

public class InteractablePressureValve : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Components")]
    [SerializeField]
    private Transform valveHandleModel;

    [Header("Head Cam & View Target")]
    [SerializeField]
    private string interactAnimTrigger = "TurnValve";

    [SerializeField]
    private float animationDuration = 1.0f;

    [SerializeField]
    private Transform cameraViewTarget;

    [SerializeField]
    private Vector3 headOffset = new Vector3(0, 0.1f, 0.15f);
    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;
    private Transform headBone;
    private Transform cinemachineTarget;
    private Animator playerAnimator;

    [Header("Valve Feel Settings")]
    [Tooltip("Vananın ağırlığı. 1.0 tüy gibi, 0.1 çok ağır.")]
    [Range(0.01f, 1.0f)]
    [SerializeField]
    private float resistanceMultiplier = 0.15f;

    [Tooltip("Tek seferde dönebileceği maksimum açı")]
    [SerializeField]
    private float maxRotationPerFrame = 2.0f;

    [Tooltip("Sadece Saat Yönünde mi dönsün?")]
    [SerializeField]
    private bool onlyClockwise = true;

    // --- YENİ SES AYARLARI ---
    [Header("🔊 Audio Settings")]
    [Tooltip("Vana dönüş sesi için AudioSource")]
    [SerializeField]
    private AudioSource audioSource;

    [Tooltip("Sürekli çalacak dönme sesi (Loop)")]
    [SerializeField]
    private AudioClip turnLoopSound;

    [Tooltip("Sesin açılma/kapanma hızı")]
    [SerializeField]
    private float fadeSpeed = 5f;

    // Pitch değişimi (Hız hissi için opsiyonel)
    [SerializeField]
    private float minPitch = 0.9f;

    [SerializeField]
    private float maxPitch = 1.1f;

    private bool isTurning = false; // O karede dönüyor mu?

    // -------------------------

    private UnityEngine.CharacterController playerPhysicsController;
    private MonoBehaviour playerInputScript;
    private bool isInteracting = false;
    private Vector2 screenCenter;
    private float lastAngle;

    private void Start()
    {
        screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        playerPhysicsController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerPhysicsController)
        {
            GameObject p = playerPhysicsController.gameObject;
            playerInputScript = p.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            playerAnimator = p.GetComponent<Animator>();
            if (playerAnimator)
                headBone = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                headBone = p.transform;
            Transform camRoot = p.transform.Find("PlayerCameraRoot");
            cinemachineTarget = (camRoot != null) ? camRoot : headBone;
        }
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }

        // Ses Kaynağını Hazırla
        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.clip = turnLoopSound;
        }
    }

    public void Interact()
    {
        if (isInteracting)
            return;
        StartCoroutine(EnterValveMode());
    }

    public string GetInteractionPrompt() => isInteracting ? "" : "[Sol Tık] Basınç Vanası";

    private IEnumerator EnterValveMode()
    {
        isInteracting = true;

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        Vector2 mousePos = Input.mousePosition;
        Vector2 direction = mousePos - screenCenter;
        lastAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (playerPhysicsController)
            playerPhysicsController.enabled = false;
        if (playerInputScript)
            playerInputScript.enabled = false;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);
        if (cinemachineBrain)
            cinemachineBrain.enabled = false;

        if (headBone != null)
        {
            mainCamera.SetParent(headBone);
            float t = 0f;
            Vector3 startPos = mainCamera.localPosition;
            Quaternion startRot = mainCamera.localRotation;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float s = t / 0.5f;
                mainCamera.localPosition = Vector3.Lerp(startPos, new Vector3(0, 0.1f, 0.15f), s);
                mainCamera.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, s);
                yield return null;
            }
        }
        yield return new WaitForSeconds(animationDuration - 0.5f);
        if (cameraViewTarget != null)
        {
            mainCamera.SetParent(null);
            Vector3 startDockPos = mainCamera.position;
            Quaternion startDockRot = mainCamera.rotation;
            float dockTime = 0.8f;
            float t = 0f;
            while (t < dockTime)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / dockTime);
                mainCamera.position = Vector3.Lerp(startDockPos, cameraViewTarget.position, s);
                mainCamera.rotation = Quaternion.Slerp(startDockRot, cameraViewTarget.rotation, s);
                yield return null;
            }
            mainCamera.position = cameraViewTarget.position;
            mainCamera.rotation = cameraViewTarget.rotation;
        }
        TogglePlayerModel(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator ExitValveMode()
    {
        isInteracting = false;
        isTurning = false; // Çıkarken sesi kes

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;

        TogglePlayerModel(true);

        if (cinemachineTarget != null)
        {
            mainCamera.SetParent(null);
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            float undockTime = 0.5f;
            float t = 0f;
            while (t < undockTime)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / undockTime);
                mainCamera.position = Vector3.Lerp(startPos, cinemachineTarget.position, s);
                mainCamera.rotation = Quaternion.Slerp(startRot, cinemachineTarget.rotation, s);
                yield return null;
            }
        }
        if (playerPhysicsController != null)
        {
            Vector3 camForward = mainCamera.forward;
            camForward.y = 0;
            if (camForward != Vector3.zero)
                playerPhysicsController.transform.rotation = Quaternion.LookRotation(camForward);
        }
        if (cinemachineBrain)
            cinemachineBrain.enabled = true;
        if (playerPhysicsController)
            playerPhysicsController.enabled = true;
        if (playerInputScript)
            playerInputScript.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Ses Yönetimi (Her kare çalışmalı ki fade out düzgün olsun)
        HandleAudio();

        if (!isInteracting)
            return;
        if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1))
        {
            StartCoroutine(ExitValveMode());
            return;
        }
        HandleCircularMotion();
    }

    private void TogglePlayerModel(bool show)
    {
        if (!playerPhysicsController)
            return;
        Renderer[] renderers = playerPhysicsController.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = show;
    }

    private void HandleCircularMotion()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 direction = mousePos - screenCenter;
        float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float deltaAngle = Mathf.DeltaAngle(lastAngle, currentAngle);

        if (onlyClockwise && deltaAngle > 0)
            deltaAngle = 0;

        // --- DÖNÜŞ KONTROLÜ ---
        // Eğer deltaAngle 0.01'den büyükse dönüyor demektir.
        if (Mathf.Abs(deltaAngle) > 0.01f)
        {
            isTurning = true;

            float dampenedDelta = deltaAngle * resistanceMultiplier;
            dampenedDelta = Mathf.Clamp(dampenedDelta, -maxRotationPerFrame, maxRotationPerFrame);

            if (valveHandleModel != null)
                valveHandleModel.Rotate(Vector3.forward, -dampenedDelta, Space.Self);

            if (dampenedDelta < 0 && PressureSystemManager.Instance != null)
            {
                float reduction =
                    PressureSystemManager.Instance.pressureDecreaseRate
                    * Time.deltaTime
                    * Mathf.Abs(dampenedDelta);
                PressureSystemManager.Instance.ReducePressure(reduction);
                if (PressureSystemManager.Instance.GetPressure() <= 0f)
                    StartCoroutine(ExitValveMode());
            }
        }
        else
        {
            isTurning = false;
        }

        lastAngle = currentAngle;
    }

    // --- YENİ SES YÖNETİMİ ---
    private void HandleAudio()
    {
        if (audioSource == null)
            return;

        // Hedef Ses Seviyesi: Dönüyorsa 1, duruyorsa 0
        // Eğer etkileşimde değilsek (isInteracting false) kesinlikle 0 olmalı.
        float targetVol = (isInteracting && isTurning) ? 1f : 0f;

        // Sesi yumuşakça hedefe götür (Fade)
        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVol, Time.deltaTime * fadeSpeed);

        // Optimizasyon: Ses çok kısıldıysa durdur, açılacaksa oynat
        if (audioSource.volume > 0.01f)
        {
            if (!audioSource.isPlaying)
                audioSource.Play();

            // Opsiyonel: Rastgele pitch ile mekanik hissi arttır
            audioSource.pitch = Mathf.Lerp(
                minPitch,
                maxPitch,
                Mathf.PingPong(Time.time * 0.5f, 1f)
            );
        }
        else if (audioSource.isPlaying && targetVol == 0f)
        {
            audioSource.Stop();
        }
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    public void ForceExit()
    {
        if (isInteracting)
            StartCoroutine(ExitValveMode());
    }
}
