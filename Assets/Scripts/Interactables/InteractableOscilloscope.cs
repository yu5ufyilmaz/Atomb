using System.Collections;
using Cinemachine;
// StarterAssets namespace'ini ekledim ki scripti tip güvenli bulabilelim
using StarterAssets;
using TMPro;
using UnityEngine;

public class InteractableOscilloscope : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Player Control")]
    [SerializeField]
    private UnityEngine.CharacterController playerController;
    private CinemachineVirtualCamera interactVCam;

    [SerializeField]
    private MonoBehaviour playerLookScript;

    [SerializeField]
    private Animator playerAnimator;

    [Header("⚠️ ÖNEMLİ: Hareket Scripti (Otomatik Bulunur ama Kontrol Et)")]
    [Tooltip("Karakterin yürümesini sağlayan ana script.")]
    public MonoBehaviour playerMovementScript;

    [Header("🎥 KAMERA AYARLARI (MUTLAKA ATANMALI)")]
    [Tooltip("Etkileşim başladığında kameranın gidip sabitleneceği nokta.")]
    public Transform fixedCameraTransform;

    [SerializeField]
    private float cameraTransitionDuration = 1.0f;

    [Header("📍 Etkileşim Pozisyonu")]
    public Transform interactionStandPoint;
    public float autoWalkSpeed = 2.0f;
    public float autoRotateSpeed = 5.0f;

    [SerializeField]
    private string interactAnimTrigger = "InspectScope";

    // ... Diğer Değişkenler ...
    [Header("Audio & Settings")]
    [SerializeField]
    private Transform voltsKnob;

    [SerializeField]
    private Transform timeKnob;

    [SerializeField]
    private float rotationPerStep = 36f;

    [SerializeField]
    private AudioSource stableAudioSource;

    [SerializeField]
    private AudioSource staticAudioSource;

    [SerializeField]
    private float pitchVariation = 0.5f;

    [SerializeField]
    private WaveformGenerator waveformScript;

    [SerializeField]
    private TextMeshPro screenText;

    [Header("Puzzle Logic")]
    [SerializeField]
    private int maxVoltsSetting = 10;

    [SerializeField]
    private int maxTimeSetting = 10;

    [SerializeField]
    private int correctVoltsSetting = 5;

    [SerializeField]
    private int correctTimeSetting = 5;

    private int currentVoltsSetting = 0;
    private int currentTimeSetting = 0;
    private bool isSolved = false;
    private bool isInteracting = false;
    private bool inMachineMode = false;
    private bool isExiting = false;
    private string assignedPassword = "";
    private Quaternion voltsKnobInitialRot;
    private Quaternion timeKnobInitialRot;
    private Coroutine audioFadeRoutine;

    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;

    void Start()
    {
        // 1. Player Controller Bul
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();

        // 2. Diğer Scriptleri Bul (Özellikle Movement Script)
        if (playerController != null)
        {
            playerLookScript =
                playerController.GetComponent<StarterAssetsInputs>() as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>();

            // DÜZELTME BURADA: İsme göre ("ThirdPersonController") aramak yerine
            // direkt senin yüklediğin "StarterAssets.CharacterController" tipini arıyoruz.
            // Bu sayede scripti %100 bulur.
            if (playerMovementScript == null)
            {
                playerMovementScript =
                    playerController.GetComponent<StarterAssets.CharacterController>();
                // Eğer null dönerse diye log basalım
                if (playerMovementScript == null)
                    Debug.LogError(
                        "Osiloskop: Player Movement Script (StarterAssets.CharacterController) BULUNAMADI! Karakter titreyebilir."
                    );
            }
        }

        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }
        if (fixedCameraTransform != null)
        {
            interactVCam = fixedCameraTransform.GetComponentInChildren<CinemachineVirtualCamera>();
            if (interactVCam == null)
            {
                GameObject vcamObj = new GameObject("Oscilloscope_Interact_VCam");
                vcamObj.transform.parent = fixedCameraTransform;
                vcamObj.transform.localPosition = Vector3.zero;
                vcamObj.transform.localRotation = Quaternion.identity;
                interactVCam = vcamObj.AddComponent<CinemachineVirtualCamera>();
                interactVCam.Priority = 0;
            }
        }
        if (voltsKnob)
            voltsKnobInitialRot = voltsKnob.localRotation;
        if (timeKnob)
            timeKnobInitialRot = timeKnob.localRotation;

        // Audio Başlat
        if (stableAudioSource)
        {
            stableAudioSource.loop = true;
            stableAudioSource.volume = 0f;
        }
        if (staticAudioSource)
        {
            staticAudioSource.loop = true;
            staticAudioSource.volume = 0f;
        }

        currentVoltsSetting = 2;
        currentTimeSetting = 2;
        UpdateKnobVisuals();
    }

    public void Interact()
    {
        if (isInteracting || isExiting || isSolved)
            return;

        // Hata ayıklama: Fixed Transform yoksa uyar
        if (fixedCameraTransform == null)
        {
            Debug.LogError("Osiloskop: Fixed Camera Transform ATANMAMIŞ! Kamera geçiş yapamaz.");
            return;
        }

        StartCoroutine(MoveToInteractionPoint());
    }

    public string GetInteractionPrompt() =>
        isSolved ? "Sinyal Stabil" : (isInteracting ? "" : "[Sol Tık] Sinyali Düzelt");

    // --- SİNEMATİK OTURTMA (OSİLOSKOP) ---
    private IEnumerator MoveToInteractionPoint()
    {
        isInteracting = true;
        inMachineMode = false;

        // 1. İNPUTLARI VE KAMERA KONTROLÜNÜ DONDUR
        StarterAssets.CharacterController saController =
            playerMovementScript as StarterAssets.CharacterController;
        if (saController != null)
        {
            saController.SetFrozen(true, lockCameraInput: true, restrictRotation: false);
        }
        if (playerLookScript)
            playerLookScript.enabled = false;

        // 2. SCRİPTİ VE FİZİĞİ KAPAT (Konsol hatalarını ve titremeyi önler)
        if (playerMovementScript)
            playerMovementScript.enabled = false;
        if (playerController)
            playerController.enabled = false;

        // 3. YUMUŞAK GEÇİŞ (0.5 Saniyede Pürüzsüzce Masaya Geçiş)
        if (interactionStandPoint != null)
        {
            float duration = 0.5f;
            float t = 0f;
            Vector3 startPos = playerController.transform.position;
            Quaternion startRot = playerController.transform.rotation;

            if (playerAnimator)
            {
                playerAnimator.SetFloat("Speed", 0f);
                playerAnimator.SetFloat("MotionSpeed", 0f);
            }

            while (t < duration)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / duration);

                playerController.transform.position = Vector3.Lerp(
                    startPos,
                    interactionStandPoint.position,
                    smoothT
                );
                playerController.transform.rotation = Quaternion.Slerp(
                    startRot,
                    interactionStandPoint.rotation,
                    smoothT
                );
                yield return null;
            }

            playerController.transform.position = interactionStandPoint.position;
            playerController.transform.rotation = interactionStandPoint.rotation;
        }

        StartCoroutine(EnterMachineView());
    }

    private IEnumerator EnterMachineView()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        // VCAM AKTİF ET (Manuel Lerp ve Brain kapatmaya gerek kalmadı, sistem süzülecek)
        if (interactVCam)
            interactVCam.Priority = 100;

        yield return new WaitForSeconds(1.5f); // Kameranın süzülmesini bekle

        inMachineMode = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (ControlsUIManager.Instance != null)
        {
            ControlsUIManager.Instance.ShowMachineUI(ControlsUIManager.MachineType.Oscilloscope);
        }

        PlayerInteraction playerInt = FindObjectOfType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(false);

        FadeAudio(true, 1.0f);
        UpdateAudioAndWaveform();
        UpdateKnobVisuals();
    }

    private IEnumerator ExitMachineView()
    {
        if (isExiting)
            yield break;
        isExiting = true;
        inMachineMode = false;

        FadeAudio(false, 0.5f);

        // VCAM PASİF ET (Kamera yumuşakça karakterin ensesine geri dönecek)
        if (interactVCam)
            interactVCam.Priority = 0;

        yield return new WaitForSeconds(1.5f);

        // 1. FİZİĞİ VE SCRİPTİ GERİ AÇ
        if (playerController)
            playerController.enabled = true;
        if (playerMovementScript)
            playerMovementScript.enabled = true;

        // 2. HAREKET KİLİDİNİ ÇÖZ
        StarterAssets.CharacterController saController =
            playerMovementScript as StarterAssets.CharacterController;
        if (saController != null)
        {
            saController.SetFrozen(false, lockCameraInput: false, restrictRotation: false);
        }
        if (playerLookScript)
            playerLookScript.enabled = true;

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
        if (!inMachineMode || isExiting)
            return;

        // ÇIKIŞ
        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

        if (isSolved)
        {
            if (waveformScript)
                waveformScript.frequency = 1.0f;
            return;
        }

        // KONTROLLER
        bool changed = false;
        if (Input.GetKeyDown(KeyCode.W))
        {
            currentVoltsSetting = Mathf.Min(currentVoltsSetting + 1, maxVoltsSetting);
            changed = true;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            currentVoltsSetting = Mathf.Max(currentVoltsSetting - 1, 0);
            changed = true;
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            currentTimeSetting = Mathf.Min(currentTimeSetting + 1, maxTimeSetting);
            changed = true;
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            currentTimeSetting = Mathf.Max(currentTimeSetting - 1, 0);
            changed = true;
        }

        if (changed)
        {
            UpdateKnobVisuals();
            UpdateAudioAndWaveform();
            CheckForSolution();
        }
    }


    // --- YARDIMCI FONKSİYONLAR ---
    private void UpdateAudioAndWaveform()
    {
        float voltsDiff = Mathf.Abs(currentVoltsSetting - correctVoltsSetting);
        float timeDiff = Mathf.Abs(currentTimeSetting - correctTimeSetting);
        float maxError = maxVoltsSetting + maxTimeSetting;
        float currentError = voltsDiff + timeDiff;
        float accuracy = 1.0f - (currentError / maxError);
        accuracy = Mathf.Clamp01(accuracy);

        if (audioFadeRoutine == null)
        {
            float stableVol = Mathf.Pow(accuracy, 2);
            float staticVol = 1.0f - accuracy;
            if (stableAudioSource)
                stableAudioSource.volume = stableVol;
            if (staticAudioSource)
                staticAudioSource.volume = staticVol * 0.5f;
        }

        float targetPitch = 1.0f + ((0.5f - accuracy) * pitchVariation);
        if (stableAudioSource)
            stableAudioSource.pitch = isSolved ? 1.0f : targetPitch;
        if (staticAudioSource)
            staticAudioSource.pitch = Random.Range(0.8f, 1.2f);

        if (waveformScript)
        {
            float vRatio = (float)currentVoltsSetting / maxVoltsSetting;
            float tRatio = (float)currentTimeSetting / maxTimeSetting;
            waveformScript.amplitude = Mathf.Lerp(0.2f, 2.5f, vRatio);
            waveformScript.frequency = Mathf.Lerp(0.5f, 3.0f, tRatio);
            if (isSolved)
                waveformScript.noiseAmount = 0f;
            else
                waveformScript.noiseAmount = Mathf.Lerp(0.8f, 0f, accuracy);
        }
    }

    private void FadeAudio(bool fadeIn, float duration)
    {
        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);
        audioFadeRoutine = StartCoroutine(FadeAudioRoutine(fadeIn, duration));
    }

    private IEnumerator FadeAudioRoutine(bool fadeIn, float duration)
    {
        float t = 0f;
        float startStable = stableAudioSource ? stableAudioSource.volume : 0;
        float startStatic = staticAudioSource ? staticAudioSource.volume : 0;
        float initialStableTarget = stableAudioSource ? stableAudioSource.volume : 0;
        float initialStaticTarget = staticAudioSource ? staticAudioSource.volume : 0;

        if (fadeIn)
        {
            if (stableAudioSource && !stableAudioSource.isPlaying)
                stableAudioSource.Play();
            if (staticAudioSource && !staticAudioSource.isPlaying)
                staticAudioSource.Play();
            UpdateAudioAndWaveform();
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = t / duration;
            if (fadeIn)
            {
                if (stableAudioSource)
                    stableAudioSource.volume = Mathf.Lerp(0, initialStableTarget, ratio);
                if (staticAudioSource)
                    staticAudioSource.volume = Mathf.Lerp(0, initialStaticTarget, ratio);
            }
            else
            {
                if (stableAudioSource)
                    stableAudioSource.volume = Mathf.Lerp(startStable, 0, ratio);
                if (staticAudioSource)
                    staticAudioSource.volume = Mathf.Lerp(startStatic, 0, ratio);
            }
            yield return null;
        }
        if (!fadeIn)
        {
            if (stableAudioSource)
                stableAudioSource.Stop();
            if (staticAudioSource)
                staticAudioSource.Stop();
        }
        audioFadeRoutine = null;
    }

    private void UpdateKnobVisuals()
    {
        if (voltsKnob)
            voltsKnob.localRotation =
                voltsKnobInitialRot * Quaternion.Euler(0, currentVoltsSetting * rotationPerStep, 0);
        if (timeKnob)
            timeKnob.localRotation =
                timeKnobInitialRot * Quaternion.Euler(0, currentTimeSetting * rotationPerStep, 0);
    }

    private void CheckForSolution()
    {
        if (currentVoltsSetting == correctVoltsSetting && currentTimeSetting == correctTimeSetting)
        {
            isSolved = true;
            if (screenText != null)
            {
                screenText.text = $"STABLE\nKEY: {assignedPassword}";
                screenText.color = Color.green;
            }
            if (PasswordManager.Instance != null)
                PasswordManager.Instance.DiscoverClue(assignedPassword);
            if (staticAudioSource)
                staticAudioSource.volume = 0;
            if (stableAudioSource)
            {
                stableAudioSource.volume = 1f;
                stableAudioSource.pitch = 1f;
            }
            StartCoroutine(AutoExit(3.0f));
        }
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    public void AssignPassword(string pw)
    {
        assignedPassword = pw;
        if (screenText != null)
            screenText.text = "NO SIGNAL";
    }

    private IEnumerator AutoExit(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (inMachineMode)
            StartCoroutine(ExitMachineView());
    }

    public void ForceExit()
    {
        if (inMachineMode)
            StartCoroutine(ExitMachineView());
    }
}
