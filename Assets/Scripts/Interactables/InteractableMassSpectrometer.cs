using System.Collections;
using System.Reflection; // Input değerlerini sıfırlamak için gerekli
using Cinemachine;
using StarterAssets;
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
    public StarterAssets.CharacterController playerMovementScript;

    [Header("🎥 KAMERA AYARLARI")]
    [Tooltip("Etkileşim başladığında kameranın gidip sabitleneceği nokta.")]
    public Transform fixedCameraTransform;

    private CinemachineVirtualCamera _interactionVC;

    [Header("🔧 Model Rotation Settings")]
    public Vector3 magnetRotationAxis = Vector3.forward;
    public int ringRotationAxisIndex = 1;
    public float ringBaseRotation = 0f;

    [SerializeField]
    private float cameraTransitionDuration = 1.0f;

    [Header("Head Cam & View Target")]
    [SerializeField]
    private string interactAnimTrigger = "InspectMachine";

    [Header("Components")]
    [SerializeField]
    private Transform magnetPivot;

    [SerializeField]
    private Transform acceleratorRing;

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

    [Tooltip("Mıknatıs dönerken çalacak ses (YENİ)")]
    [SerializeField]
    private AudioClip magnetMoveSound; // <-- BUNU EKLE

    public bool IsPoweredOn { get; private set; } = false;
    public bool IsBroken { get; private set; } = false;
    public bool IsSolved { get; private set; } = false;

    private bool inMachineMode = false;
    private bool isExiting = false;

    private float currentGrindTimer = 0f;
    private float currentCooldown = 0f;
    private float currentRingAngleValue = 0f;
    private string assignedPassword = "";
    private Vector3 _startMagnetPos;

    private void Start()
    {
        InitializeComponents();
        SetupSafeCamera();

        float randomOffset = Random.Range(-40f, 40f);
        currentRingAngleValue = ringTargetAngle + 180f + randomOffset;
        _startMagnetPos = magnetPivot.localPosition;
        UpdateRingRotation();
        ResetMachineVisuals();
    }

    private void SetupSafeCamera()
    {
        if (fixedCameraTransform == null)
        {
            Debug.LogError("MassSpectrometer: Fixed Camera Transform ATANMAMIŞ!");
            return;
        }

        GameObject vcObj = new GameObject($"VC_{gameObject.name}");
        _interactionVC = vcObj.AddComponent<CinemachineVirtualCamera>();
        vcObj.transform.SetParent(fixedCameraTransform);
        vcObj.transform.localPosition = Vector3.zero;
        vcObj.transform.localRotation = Quaternion.identity;

        _interactionVC.Priority = 0;
        _interactionVC.m_Lens.FieldOfView = 60f;
    }

    private void InitializeComponents()
    {
        if (playerPhysicsController == null)
            playerPhysicsController = FindObjectOfType<UnityEngine.CharacterController>();

        if (playerPhysicsController != null)
        {
            GameObject p = playerPhysicsController.gameObject;
            playerInputScript = p.GetComponent<StarterAssetsInputs>() as MonoBehaviour;
            playerAnimator = p.GetComponent<Animator>();

            // --- DEĞİŞİKLİK BURADA ---
            // Eğer inspector'dan atanmamışsa, kodla bul
            if (playerMovementScript == null)
            {
                playerMovementScript = p.GetComponent<StarterAssets.CharacterController>();
            }
        }
    }

    // --- GÜNCELLENMİŞ VERSİYON ---
    private void TogglePlayerControl(bool enableControl)
    {
        // Artık "as" ile cast etmeye gerek yok, direkt değişkene sahibiz.
        if (playerMovementScript != null)
        {
            if (!enableControl)
            {
                // DONDUR: Hareket etmesin (true), Kamerayı da kilitlesin (true)
                // Çünkü makineye bakarken kafasını çevirmesini istemiyoruz.
                playerMovementScript.SetFrozen(true, lockCameraInput: true);

                // Ekstra önlem: Animator parametrelerini sıfırla
                if (playerAnimator != null)
                {
                    playerAnimator.SetFloat("Speed", 0f);
                    playerAnimator.SetFloat("MotionSpeed", 0f);
                    playerAnimator.SetFloat("VelocityX", 0f);
                    playerAnimator.SetFloat("VelocityZ", 0f);
                }
            }
            else
            {
                // ÇÖZ: Her şeyi serbest bırak
                playerMovementScript.SetFrozen(false, lockCameraInput: false);
            }
        }
        else
        {
            Debug.LogError(
                "HATA: MassSpectrometer playerMovementScript'i bulamadı! Inspector'ı kontrol et."
            );
        }

        // Input scriptini kapatmıyoruz, SetFrozen işi hallediyor.
    }

    public void SetPower(bool state)
    {
        if (IsSolved || IsBroken)
            return;
        IsPoweredOn = state;

        if (IsPoweredOn)
        {
            if (screenText)
            {
                screenText.color = Color.yellow;
                screenText.text = "SYSTEM READY\nWAITING INPUT";
            }
        }
        else
            ResetMachineVisuals();
    }

    public void Interact()
    {
        if (inMachineMode || isExiting || IsSolved)
            return;

        if (IsBroken)
        {
            if (audioSource)
                audioSource.PlayOneShot(overheatSound);
            return;
        }

        if (fixedCameraTransform == null)
            return;

        if (!IsPoweredOn)
        {
            if (screenText != null)
            {
                screenText.color = Color.red;
                screenText.text = "POWER REQUIRED\nCHECK MAIN LEVER";
            }
            return;
        }

        // --- HAREKET KODU YOK! DİREKT MODA GİRİYORUZ ---
        StartCoroutine(EnterMachineView());
    }

    private IEnumerator EnterMachineView()
    {
        // 1. OYUNCUYU KİLİTLE (Hareket etmesin)
        TogglePlayerControl(false);

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        // 2. Sanal Kamerayı Aç (Geçiş Başlasın)
        if (_interactionVC != null)
            _interactionVC.Priority = 100;

        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        // 3. Kamera Geçişi Kadar Bekle
        yield return new WaitForSeconds(cameraTransitionDuration);

        // 4. Makine Modunu Aktif Et
        inMachineMode = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayerInteraction playerInt = FindObjectOfType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(false);

        if (ControlsUIManager.Instance != null)
        {
            ControlsUIManager.Instance.ShowMachineUI(
                ControlsUIManager.MachineType.MassSpectrometer
            );
        }
    }

    private IEnumerator ExitMachineView()
    {
        if (isExiting)
            yield break;
        isExiting = true;
        inMachineMode = false;
        ResetPenalty();

        // --- YENİ: Çıkarken dönme sesi veya grind sesi kalmasın ---
        if (loopAudioSource && loopAudioSource.isPlaying)
            loopAudioSource.Stop();
        // ---------------------------------------------------------

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.HideControls();

        // ... (Kalan kodlar aynı: GameManager, Kamera, TogglePlayerControl vb.) ...

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;

        if (_interactionVC != null)
            _interactionVC.Priority = 0;

        yield return new WaitForSeconds(cameraTransitionDuration);

        TogglePlayerControl(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayerInteraction playerInt = FindObjectOfType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(true);

        isExiting = false;
    }

    private void Update()
    {
        if (IsBroken)
        {
            currentCooldown -= Time.deltaTime;
            if (screenText != null)
                screenText.text = $"OVERHEATED\nWAIT: {currentCooldown:F1}s";
            if (currentCooldown <= 0)
                ResetMachineVisuals();
            return;
        }

        if (!inMachineMode || isExiting)
            return;
        if (Time.timeScale == 0f)
            return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

        if (IsSolved)
            return;

        // --- PUZZLE KODLARI ---
        float mouseX = Input.GetAxis("Mouse X");

        // ==========================================================
        // --- YENİ EKLENEN KISIM: DÖNME SESİ MANTIĞI BAŞLANGICI ---
        // ==========================================================
        bool isMovingMouse = Mathf.Abs(mouseX) > 0.1f;
        bool isGrinding = (currentGrindTimer > 0); // O sırada ceza sesi çalıyor mu?

        // Eğer fare oynuyorsa, ceza yoksa ve ses atanmışsa
        if (isMovingMouse && !isGrinding && magnetMoveSound != null && loopAudioSource != null)
        {
            // Zaten bu ses çalmıyorsa başlat
            if (!loopAudioSource.isPlaying || loopAudioSource.clip != magnetMoveSound)
            {
                loopAudioSource.clip = magnetMoveSound;
                loopAudioSource.loop = true;
                loopAudioSource.pitch = 1.0f;
                loopAudioSource.Play();
            }
        }
        // Fare durduysa ama ses hala çalıyorsa sustur
        else if (!isMovingMouse && !isGrinding && loopAudioSource != null)
        {
            if (loopAudioSource.isPlaying && loopAudioSource.clip == magnetMoveSound)
            {
                loopAudioSource.Stop();
            }
        }
        // ==========================================================
        // --- YENİ EKLENEN KISIM SONU ---
        // ==========================================================

        if (magnetPivot != null)
        {
            magnetPivot.Rotate(Vector3.right * mouseX * magnetRotateSpeed * -1, Space.Self);
        }

        // ... Geri kalan kodlar aynen kalacak (Açı hesaplama, UI, Halka kontrolü vb.) ...
        float rawAngle = magnetPivot.localEulerAngles.x;
        float currentMagAngle = (rawAngle > 180) ? rawAngle - 360 : rawAngle;
        float magDiff = Mathf.Abs(Mathf.DeltaAngle(currentMagAngle, safeZoneAngle));
        bool isMagnetSafe = magDiff < safeZoneTolerance;

        // UI
        float displayRingAngle = currentRingAngleValue % 360;
        if (displayRingAngle < 0)
            displayRingAngle += 360;

        if (screenText != null)
        {
            if (isMagnetSafe)
            {
                screenText.color = Color.green;
                screenText.text = $"MAGNET STABLE\nRING: {displayRingAngle:F0}°";
            }
            else
            {
                screenText.color = Color.red;
                screenText.text = $"MAGNET UNSTABLE ({currentMagAngle:F0}°)\nALIGN RED AXIS";
            }
        }

        // HALKA
        float ringInput = 0f;
        if (Input.GetKey(KeyCode.D))
            ringInput = 1f;
        if (Input.GetKey(KeyCode.A))
            ringInput = -1f;

        float ringDiff = Mathf.Abs(Mathf.DeltaAngle(currentRingAngleValue, ringTargetAngle));

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

    // --- YARDIMCI FONKSİYONLAR ---
    private void ApplyPenaltyLogic()
    {
        currentGrindTimer += Time.deltaTime;

        // Düzeltme: Eğer şu an 'Grind' (sürtme) sesi çalmıyorsa (başka ses varsa veya susmuşsa) Grind çal
        if (loopAudioSource && (!loopAudioSource.isPlaying || loopAudioSource.clip != grindSound))
        {
            loopAudioSource.clip = grindSound;
            loopAudioSource.loop = true;
            loopAudioSource.pitch = 1.0f;
            loopAudioSource.Play();
        }

        float shake = Random.Range(-0.03f, 0.03f);
        magnetPivot.localPosition = new Vector3(
            _startMagnetPos.x + shake,
            _startMagnetPos.y + shake,
            _startMagnetPos.z
        );

        if (currentGrindTimer > maxGrindTime)
            StartCoroutine(TriggerBreakdown());
    }

    private void ResetPenalty()
    {
        currentGrindTimer = 0f;

        // Düzeltme: Sadece Grind sesi çalıyorsa sustur. Dönme sesi çalıyorsa dokunma.
        if (loopAudioSource && loopAudioSource.isPlaying && loopAudioSource.clip == grindSound)
            loopAudioSource.Stop();

        magnetPivot.localPosition = _startMagnetPos;
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

        IsPoweredOn = false;
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
        currentGrindTimer = 0f;
    }

    private void UpdateRingRotation()
    {
        if (acceleratorRing != null)
        {
            float x = (ringRotationAxisIndex == 0) ? currentRingAngleValue : ringBaseRotation;
            float y = (ringRotationAxisIndex == 1) ? currentRingAngleValue : 0;
            float z = (ringRotationAxisIndex == 2) ? currentRingAngleValue : 0;

            if (ringRotationAxisIndex == 1)
                x = ringBaseRotation;

            acceleratorRing.localRotation = Quaternion.Euler(x, y, z);
        }
    }

    public string GetInteractionPrompt()
    {
        if (IsBroken)
            return $"Sistem Soğuyor... ({currentCooldown:F0}s)";
        if (IsSolved)
            return "Kalibrasyon Tamamlandı";
        if (!IsPoweredOn)
            return "Güç Yok - Ana Şalteri Bul";
        return inMachineMode ? "" : "[Sol Tık] Analiz Ekranı";
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
