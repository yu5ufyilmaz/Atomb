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

    private IEnumerator MoveToInteractionPoint()
    {
        isInteracting = true;
        inMachineMode = false;

        // Karakterin kendi kontrolünü kapat
        if (playerLookScript)
            playerLookScript.enabled = false;

        // KRİTİK NOKTA: Hareket scriptini kapatmazsak çatışma çıkar
        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        // Ama CharacterController (fizik/collider) açık kalmalı ki biz hareket ettirebilelim
        if (playerController)
            playerController.enabled = true;

        // --- YÜRÜME DÖNGÜSÜ ---
        if (interactionStandPoint != null)
        {
            int animIDSpeed = Animator.StringToHash("Speed");
            int animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            int animIDGrounded = Animator.StringToHash("Grounded");

            float timer = 0f;
            while (timer < 4.0f) // Max 4 saniye dene
            {
                timer += Time.deltaTime;
                Vector3 playerPos = playerController.transform.position;
                Vector3 targetPos = interactionStandPoint.position;
                playerPos.y = targetPos.y = 0; // Yükseklik farkını yoksay

                float distance = Vector3.Distance(playerPos, targetPos);
                if (distance < 0.1f)
                    break; // Geldik sayılır

                Vector3 dir = (targetPos - playerPos).normalized;
                if (dir != Vector3.zero)
                {
                    Quaternion lookRot = Quaternion.LookRotation(dir);
                    playerController.transform.rotation = Quaternion.Slerp(
                        playerController.transform.rotation,
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

                // Hareketi uygula
                Vector3 motion = dir * speed;
                motion.y = -9.81f;
                playerController.Move(motion * Time.deltaTime);

                yield return null;
            }

            // Durdur
            if (playerAnimator)
            {
                playerAnimator.SetFloat(animIDSpeed, 0f);
                playerAnimator.SetFloat(animIDMotionSpeed, 1f);
            }
        }

        // --- HİZALAMA (SNAP) ---
        // Yürüme bitince tam noktaya kaydır ki yamuk durmasın
        if (interactionStandPoint != null)
        {
            float rotTimer = 0f;
            Quaternion startRot = playerController.transform.rotation;
            Vector3 startPos = playerController.transform.position;

            while (rotTimer < 0.5f)
            {
                rotTimer += Time.deltaTime;
                float t = rotTimer / 0.5f;
                // Yumuşak geçiş
                playerController.transform.position = Vector3.Lerp(
                    startPos,
                    interactionStandPoint.position,
                    t
                );
                playerController.transform.rotation = Quaternion.Slerp(
                    startRot,
                    interactionStandPoint.rotation,
                    t
                );
                yield return null;
            }
        }

        StartCoroutine(EnterMachineView());
    }

    private IEnumerator EnterMachineView()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        // 1. Cinemachine'i Kapat (Kamerayı serbest bırak)
        if (cinemachineBrain)
            cinemachineBrain.enabled = false;

        // 2. Oyuncuyu Dondur
        if (playerController)
            playerController.enabled = false;

        // 3. Animasyonu Oynat (Örn: Eğilme/Bakma)
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        // 4. Kamerayı Yumuşakça Yerine Al
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
            // Tam oturt
            mainCamera.position = fixedCameraTransform.position;
            mainCamera.rotation = fixedCameraTransform.rotation;
        }

        // 5. Modu Aktif Et (LateUpdate kamerayı kilitleyecek)
        inMachineMode = true;

        // UI ve Cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.ShowControls("W/S: Voltaj | A/D: Zaman | F: Kalk");
        PlayerInteraction playerInt = FindObjectOfType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(false);

        // Sesleri Aç
        FadeAudio(true, 1.0f);
        UpdateAudioAndWaveform();
        UpdateKnobVisuals();
    }

    private IEnumerator ExitMachineView()
    {
        if (isExiting)
            yield break;
        isExiting = true;
        inMachineMode = false; // Kamera kilidini kaldır

        FadeAudio(false, 0.5f);

        // 1. Cinemachine Brain'i Aç (Otomatik Blend yapsın)
        // AYAKLARA GİTME SORUNUNU BU ÇÖZER
        if (cinemachineBrain)
        {
            cinemachineBrain.enabled = true;
        }

        // Blend süresi kadar bekle (1 saniye ideal)
        yield return new WaitForSeconds(1.0f);

        // 2. Kontrolleri Geri Ver
        if (playerController != null)
            playerController.enabled = true;
        if (playerLookScript)
            playerLookScript.enabled = true;
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

    private void LateUpdate()
    {
        // KAMERAYI ÇİVİ GİBİ ÇAK
        // Eğer makine modundaysak kamera bir milim bile kıpırdayamaz.
        if (inMachineMode && fixedCameraTransform != null)
        {
            mainCamera.position = fixedCameraTransform.position;
            mainCamera.rotation = fixedCameraTransform.rotation;
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
