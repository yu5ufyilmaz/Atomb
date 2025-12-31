using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;

public class InteractablePressureValve : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Player Control")]
    [SerializeField]
    private UnityEngine.CharacterController playerPhysicsController;

    [SerializeField]
    private MonoBehaviour playerInputScript;

    [SerializeField]
    private Animator playerAnimator;

    [Header("⚠️ ÖNEMLİ: Hareket Scripti")]
    public MonoBehaviour playerMovementScript;

    [Header("🎥 KAMERA (YENİ SİSTEM)")]
    [Tooltip("Kameranın gidip duracağı nokta (Boş bir GameObject oluşturup buraya sürükle)")]
    public Transform fixedCameraTransform;
    private CinemachineVirtualCamera interactVCam;

    [Header("Animasyon")]
    [SerializeField]
    private string interactAnimTrigger = "TurnValve";

    [Header("📍 Etkileşim Pozisyonu")]
    public Transform interactionStandPoint;
    public float autoWalkSpeed = 2.0f;
    public float autoRotateSpeed = 5.0f;

    [Header("Components")]
    [SerializeField]
    private Transform valveHandleModel;

    // --- YENİ EKLENEN KISIM: İbre (Gauge) Ayarları ---
    [Header("📊 GÖSTERGE (GAUGE) AYARLARI")]
    [Tooltip("Dönecek olan ibre objesi (Needle)")]
    public Transform gaugeNeedle;

    [Tooltip("Basınç %0 iken ibrenin açısı (Örn: -135)")]
    public float gaugeMinAngle = -135f;

    [Tooltip("Basınç %100 iken ibrenin açısı (Örn: 135)")]
    public float gaugeMaxAngle = 135f;

    [Tooltip("İbrenin döneceği eksen (X, Y veya Z'yi 1 yap)")]
    public Vector3 gaugeRotationAxis = new Vector3(0, 0, 1); // Varsayılan Z

    // --------------------------------------------------

    [Header("Valve Settings")]
    [Range(0.01f, 1.0f)]
    [SerializeField]
    private float resistanceMultiplier = 0.15f;

    [SerializeField]
    private float maxRotationPerFrame = 2.0f;

    [SerializeField]
    private bool onlyClockwise = true;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip turnLoopSound;

    [SerializeField]
    private float fadeSpeed = 5f;

    private bool inValveMode = false;
    private bool isExiting = false;
    private bool isTurning = false;
    private Vector2 screenCenter;
    private float lastAngle;

    private void Start()
    {
        screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);

        // 1. Scriptleri Bul
        if (playerPhysicsController == null)
            playerPhysicsController = FindObjectOfType<UnityEngine.CharacterController>();

        if (playerPhysicsController != null)
        {
            GameObject p = playerPhysicsController.gameObject;
            playerInputScript = p.GetComponent<StarterAssetsInputs>() as MonoBehaviour;
            playerAnimator = p.GetComponent<Animator>();
            if (playerMovementScript == null)
                playerMovementScript = p.GetComponent<StarterAssets.CharacterController>();
            if (playerMovementScript == null)
                playerMovementScript = p.GetComponent("ThirdPersonController") as MonoBehaviour;
        }

        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.clip = turnLoopSound;
        }

        // 2. VCAM Oluştur
        if (fixedCameraTransform != null)
        {
            interactVCam = fixedCameraTransform.GetComponentInChildren<CinemachineVirtualCamera>();
            if (interactVCam == null)
            {
                GameObject vcamObj = new GameObject("Valve_VCam");
                vcamObj.transform.parent = fixedCameraTransform;
                vcamObj.transform.localPosition = Vector3.zero;
                vcamObj.transform.localRotation = Quaternion.identity;
                interactVCam = vcamObj.AddComponent<CinemachineVirtualCamera>();
                interactVCam.Priority = 0;
            }
        }
        else
            Debug.LogError("PressureValve: FIXED CAMERA TRANSFORM EKSİK!");
    }

    public void Interact()
    {
        if (inValveMode || isExiting)
            return;
        StartCoroutine(MoveToInteractionPoint());
    }

    private IEnumerator MoveToInteractionPoint()
    {
        // Yürüme
        if (playerInputScript)
            playerInputScript.enabled = false;
        if (playerMovementScript)
            playerMovementScript.enabled = false;
        if (playerPhysicsController)
            playerPhysicsController.enabled = true;

        if (interactionStandPoint != null)
        {
            float timer = 0f;
            while (timer < 4.0f)
            {
                timer += Time.deltaTime;
                Vector3 targetPos = interactionStandPoint.position;
                Vector3 playerPos = playerPhysicsController.transform.position;
                playerPos.y = targetPos.y;

                if (Vector3.Distance(playerPos, targetPos) < 0.15f)
                    break;

                Vector3 dir = (targetPos - playerPos).normalized;
                if (dir != Vector3.zero)
                    playerPhysicsController.transform.rotation = Quaternion.Slerp(
                        playerPhysicsController.transform.rotation,
                        Quaternion.LookRotation(dir),
                        Time.deltaTime * autoRotateSpeed
                    );

                Vector3 move = dir * autoWalkSpeed;
                move.y = -9.81f;
                playerPhysicsController.Move(move * Time.deltaTime);

                if (playerAnimator)
                {
                    playerAnimator.SetFloat("Speed", autoWalkSpeed);
                    playerAnimator.SetBool("Grounded", true);
                }
                yield return null;
            }
        }

        // Snap
        if (interactionStandPoint != null)
        {
            float t = 0f;
            Quaternion startRot = playerPhysicsController.transform.rotation;
            Vector3 startPos = playerPhysicsController.transform.position;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                playerPhysicsController.transform.position = Vector3.Lerp(
                    startPos,
                    interactionStandPoint.position,
                    t / 0.5f
                );
                playerPhysicsController.transform.rotation = Quaternion.Slerp(
                    startRot,
                    interactionStandPoint.rotation,
                    t / 0.5f
                );
                if (playerAnimator)
                    playerAnimator.SetFloat("Speed", 0);
                yield return null;
            }
        }

        StartCoroutine(EnterValveMode());
    }

    private IEnumerator EnterValveMode()
    {
        if (GameManager.Instance)
            GameManager.Instance.activeInteraction = this;

        // Mouse pozisyonu hazırla
        Vector2 mousePos = Input.mousePosition;
        Vector2 dir = mousePos - screenCenter;
        lastAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Kontrolleri kapat
        if (playerPhysicsController)
            playerPhysicsController.enabled = false;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        // GEÇİŞ (Blend)
        if (interactVCam)
            interactVCam.Priority = 100;
        yield return new WaitForSeconds(1.5f);

        inValveMode = true;
        Cursor.lockState = CursorLockMode.None; // Mouse lazım
        Cursor.visible = true;

        if (ControlsUIManager.Instance != null)
        {
            // Artık string yollamak yerine Enum yolluyoruz.
            // Bu sayede "massSpectrometerPanel" açılacak.
            ControlsUIManager.Instance.ShowMachineUI(ControlsUIManager.MachineType.PressureValve);
        }
    }

    private IEnumerator ExitValveMode()
    {
        if (isExiting)
            yield break;
        isExiting = true;
        inValveMode = false;
        isTurning = false;

        // ÇIKIŞ (Blend)
        if (interactVCam)
            interactVCam.Priority = 0;
        yield return new WaitForSeconds(1.5f);

        // Kontrolleri aç
        if (playerPhysicsController)
            playerPhysicsController.enabled = true;
        if (playerInputScript)
            playerInputScript.enabled = true;
        if (playerMovementScript)
            playerMovementScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (ControlsUIManager.Instance)
            ControlsUIManager.Instance.HideControls();
        if (GameManager.Instance)
            GameManager.Instance.activeInteraction = null;

        isExiting = false;
    }

    private void Update()
    {
        // 1. Ses ve Gösterge her zaman güncellensin (Oyuncu etkileşimde olmasa bile ibre dönsün)
        HandleAudio();
        UpdateGauge(); // <--- YENİ EKLENEN METOD ÇAĞRISI

        // 2. Etkileşim kontrolü
        if (!inValveMode || isExiting)
            return;

        if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1))
        {
            StartCoroutine(ExitValveMode());
            return;
        }

        HandleCircularMotion();
    }

    // --- YENİ EKLENEN FONKSİYON: Gösterge Güncelleme ---
    private void UpdateGauge()
    {
        // İbre veya Manager yoksa işlem yapma
        if (gaugeNeedle == null || PressureSystemManager.Instance == null)
            return;

        // Anlık basıncı al
        float currentP = PressureSystemManager.Instance.currentPressure;

        // 0-1 arasına normalize et (Basınç 100 üzerinden varsayıldı)
        float normalizedP = Mathf.Clamp01(currentP / 100f);

        // Hedef açıyı hesapla (Min'den Max'a doğru)
        float targetAngle = Mathf.Lerp(gaugeMinAngle, gaugeMaxAngle, normalizedP);

        // Quaternion hesapla (Belirlenen eksende)
        Quaternion targetRot = Quaternion.Euler(gaugeRotationAxis * targetAngle);

        // Yumuşakça döndür
        gaugeNeedle.localRotation = Quaternion.Slerp(
            gaugeNeedle.localRotation,
            targetRot,
            Time.deltaTime * 5f
        );
    }

    // ----------------------------------------------------

    private void HandleCircularMotion()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 dir = mousePos - screenCenter;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(lastAngle, angle);

        if (onlyClockwise && delta > 0)
            delta = 0;

        if (Mathf.Abs(delta) > 0.01f)
        {
            isTurning = true;
            float rot = Mathf.Clamp(
                delta * resistanceMultiplier,
                -maxRotationPerFrame,
                maxRotationPerFrame
            );
            if (valveHandleModel)
                valveHandleModel.Rotate(Vector3.forward, -rot, Space.Self);

            if (rot < 0 && PressureSystemManager.Instance)
            {
                PressureSystemManager.Instance.ReducePressure(
                    PressureSystemManager.Instance.pressureDecreaseRate
                        * Time.deltaTime
                        * Mathf.Abs(rot)
                );
                if (PressureSystemManager.Instance.GetPressure() <= 0)
                    StartCoroutine(ExitValveMode());
            }
        }
        else
            isTurning = false;
        lastAngle = angle;
    }

    private void HandleAudio()
    {
        if (!audioSource)
            return;
        float target = (inValveMode && isTurning) ? 1f : 0f;
        audioSource.volume = Mathf.Lerp(audioSource.volume, target, Time.deltaTime * fadeSpeed);
        if (audioSource.volume > 0.01f && !audioSource.isPlaying)
            audioSource.Play();
        else if (audioSource.volume <= 0.01f && audioSource.isPlaying)
            audioSource.Stop();
    }

    public void ForceExit()
    {
        if (inValveMode)
            StartCoroutine(ExitValveMode());
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    public string GetInteractionPrompt() => inValveMode ? "" : "[Sol Tık] Basınç Vanası";
}
