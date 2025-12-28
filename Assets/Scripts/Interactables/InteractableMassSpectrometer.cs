using System.Collections;
using Cinemachine;
using StarterAssets; // Karakter kontrolcüsü için gerekli
using TMPro;
using UnityEngine;

public class InteractableMassSpectrometer : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Player Control")]
    [Tooltip("UnityEngine.CharacterController bileşeni")]
    [SerializeField]
    private UnityEngine.CharacterController playerPhysicsController;

    [SerializeField]
    private MonoBehaviour playerInputScript; // StarterAssetsInputs

    [SerializeField]
    private Animator playerAnimator;

    [Header("⚠️ ÖNEMLİ: Hareket Scripti")]
    [Tooltip("Karakterin yürüme mantığını yöneten script (Otomatik bulunur)")]
    public MonoBehaviour playerMovementScript;

    [Header("🎥 KAMERA AYARLARI (MUTLAKA ATANMALI)")]
    [Tooltip("Etkileşim başladığında kameranın gidip sabitleneceği nokta.")]
    public Transform fixedCameraTransform;

    [SerializeField]
    private float cameraTransitionDuration = 1.0f;

    [Header("Head Cam & View Target")]
    [SerializeField]
    private string interactAnimTrigger = "InspectMachine";

    [SerializeField]
    private Transform cameraViewTarget; // Referans

    [Header("📍 Etkileşim Pozisyonu")]
    public Transform interactionStandPoint;
    public float autoWalkSpeed = 2.0f;
    public float autoRotateSpeed = 5.0f;

    [Header("Components")]
    [SerializeField]
    private Transform magnetPivot;

    [SerializeField]
    private Transform acceleratorRing;

    // NOT: mainLever referansı kaldırıldı çünkü artık harici bir script (RemotePowerLever) kullanılıyor.

    [SerializeField]
    private GameObject ionBeamObj;

    [SerializeField]
    private TextMeshPro screenText;

    [SerializeField]
    private GameObject overheatSmoke;

    [Header("Puzzle Settings")]
    [SerializeField]
    private float safeZoneAngle = 0f;

    [SerializeField]
    private float safeZoneTolerance = 12f;

    [SerializeField]
    private float ringTargetAngle = 90f;

    [SerializeField]
    private float ringTolerance = 5f;

    [SerializeField]
    private float maxGrindTime = 1.5f;

    [SerializeField]
    private float cooldownDuration = 10f;

    [Header("Controls & Audio")]
    [SerializeField]
    private float magnetRotateSpeed = 2f;

    [SerializeField]
    private float ringRotateSpeed = 50f;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioSource loopAudioSource;

    // leverSound kaldırıldı (artık kol scriptinde çalacak)

    [SerializeField]
    private AudioClip grindSound;

    [SerializeField]
    private AudioClip breakSound;

    [SerializeField]
    private AudioClip successSound;

    [SerializeField]
    private AudioClip overheatSound;

    [SerializeField]
    private AudioClip beamHumSound;

    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;

    // Durum Değişkenleri (Property olarak tanımlandı ki Editor scripti okuyabilsin)
    public bool IsPoweredOn { get; private set; } = false;
    public bool IsBroken { get; private set; } = false;
    public bool IsSolved { get; private set; } = false;

    private bool isInteracting = false;
    private bool inMachineMode = false;
    private bool isExiting = false;

    private float currentGrindTimer = 0f;
    private float currentCooldown = 0f;
    private float currentRingAngleValue = 0f;
    private string assignedPassword = "";

    private void Start()
    {
        InitializeComponents();

        // Puzzle başlangıç ayarları
        float randomOffset = Random.Range(-40f, 40f);
        currentRingAngleValue = ringTargetAngle + 180f + randomOffset;
        UpdateRingRotation();
        ResetMachineVisuals();
    }

    private void InitializeComponents()
    {
        // 1. Controller'ı Bul
        if (playerPhysicsController == null)
            playerPhysicsController = FindObjectOfType<UnityEngine.CharacterController>();

        // 2. Diğer Scriptleri Bul
        if (playerPhysicsController != null)
        {
            GameObject p = playerPhysicsController.gameObject;
            playerInputScript = p.GetComponent<StarterAssetsInputs>() as MonoBehaviour;
            playerAnimator = p.GetComponent<Animator>();

            // StarterAssets.CharacterController tipinde ara
            if (playerMovementScript == null)
            {
                playerMovementScript = p.GetComponent<StarterAssets.CharacterController>();
                if (playerMovementScript == null)
                    Debug.LogError("MassSpectrometer: Player Movement Script BULUNAMADI!");
            }
        }

        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }
    }

    // --- HARİCİ KONTROL (RemotePowerLever veya Editor Tarafından Çağrılır) ---
    public void SetPower(bool state)
    {
        if (IsSolved || IsBroken)
            return; // Zaten çözülmüşse veya bozuksa müdahale etme

        IsPoweredOn = state;

        if (IsPoweredOn)
        {
            if (screenText)
            {
                screenText.color = Color.yellow;
                screenText.text = "SYSTEM READY\nWAITING INPUT";
            }
            // İstersen burada makineden "Power On" sesi gelebilir
        }
        else
        {
            ResetMachineVisuals();
        }
    }

    public void Interact()
    {
        if (isInteracting || isExiting || IsSolved)
            return;

        if (IsBroken)
        {
            if (audioSource)
                audioSource.PlayOneShot(overheatSound);
            return;
        }

        // Hata Kontrolü
        if (fixedCameraTransform == null)
        {
            Debug.LogError("MassSpectrometer: Fixed Camera Transform ATANMAMIŞ!");
            return;
        }

        // --- GÜÇ KONTROLÜ ---
        if (!IsPoweredOn)
        {
            // Güç yoksa sadece uyarı ver ve işlemi iptal et
            if (screenText != null)
            {
                screenText.color = Color.red;
                screenText.text = "POWER REQUIRED\nCHECK MAIN LEVER";
            }
            return;
        }

        StartCoroutine(MoveToInteractionPoint());
    }

    // --- 1. ADIM: YÜRÜME VE HİZALAMA ---
    private IEnumerator MoveToInteractionPoint()
    {
        isInteracting = true;
        inMachineMode = false;

        // Input'u ve Hareket Scriptini Kapat
        if (playerInputScript)
            playerInputScript.enabled = false;
        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        // CharacterController açık kalsın
        if (playerPhysicsController)
            playerPhysicsController.enabled = true;

        if (interactionStandPoint != null)
        {
            float timer = 0f;
            float timeOut = 4.0f;
            int animIDSpeed = Animator.StringToHash("Speed");
            int animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            int animIDGrounded = Animator.StringToHash("Grounded");

            while (timer < timeOut)
            {
                timer += Time.deltaTime;
                Vector3 playerPos = playerPhysicsController.transform.position;
                Vector3 targetPos = interactionStandPoint.position;
                playerPos.y = targetPos.y = 0;

                float distance = Vector3.Distance(playerPos, targetPos);
                if (distance < 0.1f)
                    break;

                Vector3 dir = (targetPos - playerPos).normalized;
                if (dir != Vector3.zero)
                {
                    Quaternion lookRot = Quaternion.LookRotation(dir);
                    playerPhysicsController.transform.rotation = Quaternion.Slerp(
                        playerPhysicsController.transform.rotation,
                        lookRot,
                        Time.deltaTime * autoRotateSpeed
                    );
                }

                float speed = (distance < 0.5f) ? 0.5f : autoWalkSpeed;
                if (playerAnimator)
                {
                    playerAnimator.SetBool(animIDGrounded, true);
                    playerAnimator.SetFloat(animIDSpeed, speed);
                    playerAnimator.SetFloat(animIDMotionSpeed, 1f);
                }

                Vector3 motion = dir * speed;
                motion.y = -9.81f;
                playerPhysicsController.Move(motion * Time.deltaTime);
                yield return null;
            }

            // Durma Animasyonu
            if (playerAnimator)
            {
                playerAnimator.SetFloat(animIDSpeed, 0f);
                playerAnimator.SetFloat(animIDMotionSpeed, 1f);
            }
        }

        // Snap (Tam Oturtma)
        if (interactionStandPoint != null)
        {
            float rotTimer = 0f;
            Quaternion startRot = playerPhysicsController.transform.rotation;
            Vector3 startPos = playerPhysicsController.transform.position;

            while (rotTimer < 0.5f)
            {
                rotTimer += Time.deltaTime;
                float t = rotTimer / 0.5f;
                playerPhysicsController.transform.position = Vector3.Lerp(
                    startPos,
                    interactionStandPoint.position,
                    t
                );
                playerPhysicsController.transform.rotation = Quaternion.Slerp(
                    startRot,
                    interactionStandPoint.rotation,
                    t
                );
                yield return null;
            }
        }

        StartCoroutine(EnterMachineView());
    }

    // --- 2. ADIM: KAMERA GEÇİŞİ VE BAŞLAMA ---
    private IEnumerator EnterMachineView()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        if (cinemachineBrain)
            cinemachineBrain.enabled = false;
        if (playerPhysicsController)
            playerPhysicsController.enabled = false;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        if (fixedCameraTransform != null)
        {
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            float t = 0f;
            while (t < cameraTransitionDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / cameraTransitionDuration);
                mainCamera.position = Vector3.Lerp(startPos, fixedCameraTransform.position, s);
                mainCamera.rotation = Quaternion.Slerp(startRot, fixedCameraTransform.rotation, s);
                yield return null;
            }
            mainCamera.position = fixedCameraTransform.position;
            mainCamera.rotation = fixedCameraTransform.rotation;
        }

        inMachineMode = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayerInteraction playerInt = FindObjectOfType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(false);

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.ShowControls(
                "A/D: Halkayı Çevir (Mıknatıs Yeşilken) | F: Kalk"
            );
    }

    // --- 3. ADIM: ÇIKIŞ ---
    private IEnumerator ExitMachineView()
    {
        if (isExiting)
            yield break;
        isExiting = true;
        inMachineMode = false;
        ResetPenalty();

        if (cinemachineBrain)
            cinemachineBrain.enabled = true;

        yield return new WaitForSeconds(1.0f);

        if (playerPhysicsController != null)
            playerPhysicsController.enabled = true;
        if (playerInputScript)
            playerInputScript.enabled = true;
        if (playerMovementScript != null)
            playerMovementScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayerInteraction playerInt = FindObjectOfType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(true);

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.HideControls();

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;

        isInteracting = false;
        isExiting = false;
    }

    private void Update()
    {
        // Kırılma / Soğuma Mantığı
        if (IsBroken)
        {
            currentCooldown -= Time.deltaTime;
            if (screenText != null)
                screenText.text = $"OVERHEATED\nWAIT: {currentCooldown:F1}s";
            if (currentCooldown <= 0)
            {
                // Bozulma bitti ama güç kapalı olarak başlasın ki tekrar kolu çekmek gereksin mi?
                // Genelde makine soğuyunca tekrar hazır olur ama gücü kesmek daha mantıklı.
                // Burada otomatik reset atıyoruz:
                ResetMachineVisuals();
            }
            return;
        }

        if (!inMachineMode || isExiting)
            return;

        // ÇIKIŞ TUŞU
        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

        // --- PUZZLE MANTIĞI ---
        float mouseX = Input.GetAxis("Mouse X");
        if (magnetPivot != null)
            magnetPivot.Rotate(Vector3.forward * mouseX * magnetRotateSpeed * -1);

        float currentMagAngle = magnetPivot.localEulerAngles.z;
        float magDiff = Mathf.Abs(Mathf.DeltaAngle(currentMagAngle, safeZoneAngle));
        bool isMagnetSafe = magDiff < safeZoneTolerance;

        float ringDiff = Mathf.Abs(Mathf.DeltaAngle(currentRingAngleValue, ringTargetAngle));
        float displayRingAngle = currentRingAngleValue % 360;
        if (displayRingAngle < 0)
            displayRingAngle += 360;

        if (screenText != null)
        {
            if (isMagnetSafe)
            {
                screenText.color = Color.green;
                screenText.text =
                    $"MAGNET STABLE\nRING: {displayRingAngle:F0}° / TARGET: {ringTargetAngle}°";
            }
            else
            {
                screenText.color = Color.red;
                float displayMagAngle =
                    currentMagAngle > 180 ? currentMagAngle - 360 : currentMagAngle;
                screenText.text = $"MAGNET UNSTABLE ({displayMagAngle:F0}°)\nALIGN MAGNET (0°)";
            }
        }

        float ringInput = 0f;
        if (Input.GetKey(KeyCode.D))
            ringInput = 1f;
        if (Input.GetKey(KeyCode.A))
            ringInput = -1f;

        if (ringInput != 0)
        {
            if (isMagnetSafe)
            {
                currentRingAngleValue += ringInput * ringRotateSpeed * Time.deltaTime;
                UpdateRingRotation();
                ResetPenalty();
                if (ringDiff < ringTolerance)
                    StartCoroutine(SuccessSequence());
            }
            else
            {
                ApplyPenaltyLogic();
            }
        }
        else
        {
            ResetPenalty();
        }
    }

    private void LateUpdate()
    {
        if (inMachineMode && fixedCameraTransform != null)
        {
            mainCamera.position = fixedCameraTransform.position;
            mainCamera.rotation = fixedCameraTransform.rotation;
        }
    }

    // --- YARDIMCI FONKSİYONLAR ---
    private void ApplyPenaltyLogic()
    {
        currentGrindTimer += Time.deltaTime;
        if (loopAudioSource && !loopAudioSource.isPlaying)
        {
            loopAudioSource.clip = grindSound;
            loopAudioSource.Play();
        }
        float shake = Random.Range(-0.03f, 0.03f);
        magnetPivot.localPosition = new Vector3(shake, shake, 0);

        if (currentGrindTimer > maxGrindTime)
            StartCoroutine(TriggerBreakdown());
    }

    private void ResetPenalty()
    {
        currentGrindTimer = 0f;
        if (loopAudioSource && loopAudioSource.isPlaying)
            loopAudioSource.Stop();
        if (magnetPivot)
            magnetPivot.localPosition = Vector3.zero;
    }

    private IEnumerator TriggerBreakdown()
    {
        IsBroken = true;
        currentCooldown = cooldownDuration;
        if (audioSource)
            audioSource.PlayOneShot(breakSound);
        if (overheatSmoke)
            overheatSmoke.SetActive(true);

        if (screenText)
        {
            screenText.color = Color.red;
            screenText.text = "SYSTEM FAILURE\nCRITICAL ERROR";
        }

        IsPoweredOn = false; // Güç kesildi
        yield return StartCoroutine(ExitMachineView());
    }

    private IEnumerator SuccessSequence()
    {
        IsSolved = true;
        ResetPenalty();
        if (audioSource)
            audioSource.PlayOneShot(successSound);

        if (screenText)
        {
            screenText.color = Color.cyan;
            screenText.text = $"CALIBRATION COMPLETE\nCODE: {assignedPassword}";
        }

        if (PasswordManager.Instance != null)
            PasswordManager.Instance.DiscoverClue(assignedPassword);

        if (ionBeamObj)
        {
            ionBeamObj.SetActive(true);
            if (loopAudioSource)
            {
                loopAudioSource.clip = beamHumSound;
                loopAudioSource.Play();
            }
        }

        yield return new WaitForSeconds(4.0f);
        StartCoroutine(ExitMachineView());
    }

    private void ResetMachineVisuals()
    {
        if (screenText != null)
        {
            screenText.text = "SYSTEM OFF";
            screenText.color = Color.white;
        }
        if (ionBeamObj != null)
            ionBeamObj.SetActive(false);
        if (overheatSmoke != null)
            overheatSmoke.SetActive(false);

        IsPoweredOn = false;
        IsBroken = false;
        // IsSolved = false; // Çözüldüyse sıfırlama, kalıcı olsun.

        currentGrindTimer = 0f;
        if (magnetPivot != null)
            magnetPivot.localPosition = Vector3.zero;
    }

    private void UpdateRingRotation()
    {
        if (acceleratorRing != null)
            acceleratorRing.localRotation = Quaternion.Euler(90, currentRingAngleValue, 0);
    }

    public string GetInteractionPrompt()
    {
        if (IsBroken)
            return $"Sistem Soğuyor... ({currentCooldown:F0}s)";
        if (IsSolved)
            return "Kalibrasyon Tamamlandı";
        if (!IsPoweredOn)
            return "Güç Yok - Ana Şalteri Bul";
        return isInteracting ? "" : "[Sol Tık] Analiz Ekranı";
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    public void AssignPassword(string pw)
    {
        assignedPassword = pw;
    }

    public void ForceExit()
    {
        if (inMachineMode)
            StartCoroutine(ExitMachineView());
    }
}
