using System.Collections;
using Cinemachine;
using TMPro;
using UnityEngine;

public class InteractableOscilloscope : MonoBehaviour, IInteractable, IForceExitable
{
    // ... (Eski Değişkenler Aynı) ...
    [Header("Player Control")]
    [SerializeField]
    private UnityEngine.CharacterController playerController;

    [SerializeField]
    private MonoBehaviour playerLookScript;

    [Header("Head Cam & View Target")]
    [SerializeField]
    private string interactAnimTrigger = "InspectScope";

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

    [Header("Audio & Settings")]
    [SerializeField]
    private Transform voltsKnob;

    [SerializeField]
    private Transform timeKnob;

    [SerializeField]
    private float rotationPerStep = 36f;

    // --- YENİ SES AYARLARI ---
    [Header("🔊 PROXIMITY AUDIO (Mesafe/Doğruluk Sesi)")]
    [Tooltip("Net, stabil osiloskop sesi (Doğru ayarda duyulur)")]
    [SerializeField]
    private AudioSource stableAudioSource;

    [Tooltip("Bozuk, cızırtılı ses (Yanlış ayarda duyulur)")]
    [SerializeField]
    private AudioSource staticAudioSource;

    [Tooltip("Pitch değişimi ne kadar etkili olsun?")]
    [SerializeField]
    private float pitchVariation = 0.5f;

    // -------------------------

    [Header("Puzzle Logic")]
    [SerializeField]
    private int maxVoltsSetting = 10;

    [SerializeField]
    private int maxTimeSetting = 10;

    [SerializeField]
    private int correctVoltsSetting = 5;

    [SerializeField]
    private int correctTimeSetting = 5;

    [SerializeField]
    private WaveformGenerator waveformScript;

    private int currentVoltsSetting = 0;
    private int currentTimeSetting = 0;
    private bool isSolved = false;
    private bool isInteracting = false;
    private Quaternion voltsKnobInitialRot;
    private Quaternion timeKnobInitialRot;

    [Header("Password Settings")]
    [SerializeField]
    private TextMeshPro screenText;
    private string assignedPassword = "";

    // SES FADE KONTROLÜ
    private Coroutine audioFadeRoutine;

    void Start()
    {
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();

        if (playerController != null)
        {
            playerLookScript =
                playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>();
            if (playerAnimator)
                headBone = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                headBone = playerController.transform;
            Transform camRoot = playerController.transform.Find("PlayerCameraRoot");
            cinemachineTarget = (camRoot != null) ? camRoot : headBone;
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

        // --- SESLERİ BAŞLAT (AMA KISIK) ---
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

        currentVoltsSetting = 2; // Rastgele başlangıç
        currentTimeSetting = 2;
        UpdateKnobVisuals();
    }

    public void Interact()
    {
        if (isInteracting || isSolved)
            return;
        StartCoroutine(EnterMachineView());
    }

    public string GetInteractionPrompt() =>
        isSolved ? "Sinyal Stabil" : (isInteracting ? "" : "[Sol Tık] Sinyali Düzelt");

    private IEnumerator EnterMachineView()
    {
        isInteracting = true;
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        if (playerController)
            playerController.enabled = false;
        if (playerLookScript)
            playerLookScript.enabled = false;
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
                mainCamera.localPosition = Vector3.Lerp(startPos, headOffset, s);
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

        // YENİ: Makineye girince sesleri başlat (Fade In)
        FadeAudio(true, 1.0f);
        UpdateAudioAndWaveform(); // İlk karışımı yap

        UpdateKnobVisuals();
    }

    private IEnumerator ExitMachineView()
    {
        isInteracting = false;
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;

        // YENİ: Makineden çıkınca sesleri kapat (Fade Out)
        FadeAudio(false, 0.5f);

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

        if (playerController != null)
        {
            Vector3 camForward = mainCamera.forward;
            camForward.y = 0;
            if (camForward != Vector3.zero)
                playerController.transform.rotation = Quaternion.LookRotation(camForward);
        }

        if (cinemachineBrain)
            cinemachineBrain.enabled = true;
        if (playerController)
            playerController.enabled = true;
        if (playerLookScript)
            playerLookScript.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // --- GÜNCELLENMİŞ SES VE GÖRSEL MANTIĞI ---
    private void UpdateAudioAndWaveform()
    {
        // 1. Doğruluk Oranını Hesapla (0.0 = Tamamen Yanlış, 1.0 = Tamamen Doğru)
        float voltsDiff = Mathf.Abs(currentVoltsSetting - correctVoltsSetting);
        float timeDiff = Mathf.Abs(currentTimeSetting - correctTimeSetting);

        float maxError = maxVoltsSetting + maxTimeSetting;
        float currentError = voltsDiff + timeDiff;

        // Doğruluk oranı (1'e ne kadar yakınsa o kadar iyi)
        float accuracy = 1.0f - (currentError / maxError);
        accuracy = Mathf.Clamp01(accuracy);

        // --- SES KARIŞTIRMA (CROSSFADE) ---
        if (audioFadeRoutine == null)
        {
            float stableVol = Mathf.Pow(accuracy, 2);
            float staticVol = 1.0f - accuracy;

            if (stableAudioSource)
                stableAudioSource.volume = stableVol;
            if (staticAudioSource)
                staticAudioSource.volume = staticVol * 0.5f;
        }

        // Pitch ayarı
        float targetPitch = 1.0f + ((0.5f - accuracy) * pitchVariation);
        if (stableAudioSource)
            stableAudioSource.pitch = isSolved ? 1.0f : targetPitch;
        if (staticAudioSource)
            staticAudioSource.pitch = Random.Range(0.8f, 1.2f);

        // --- WAVEFORM GÖRSELİ (YENİLENEN KISIM) ---
        if (waveformScript)
        {
            // Genlik ve Frekans ayarları (Görsel olarak knobların etkisini görelim)
            float vRatio = (float)currentVoltsSetting / maxVoltsSetting;
            float tRatio = (float)currentTimeSetting / maxTimeSetting;

            waveformScript.amplitude = Mathf.Lerp(0.2f, 2.5f, vRatio);
            waveformScript.frequency = Mathf.Lerp(0.5f, 3.0f, tRatio);

            // YENİ: Gürültü (Noise) Kontrolü
            // Accuracy 0 iken (yanlışken) gürültü 0.8f olsun (çok bozuk)
            // Accuracy 1 iken (doğruyken) gürültü 0f olsun (tertemiz)

            // Eğer çözüldüyse direkt 0 yap
            if (isSolved)
            {
                waveformScript.noiseAmount = 0f;
            }
            else
            {
                // Yanlışlık arttıkça bozulma artar
                waveformScript.noiseAmount = Mathf.Lerp(0.8f, 0f, accuracy);
            }
        }
    }

    // --- GİRİŞ / ÇIKIŞ FADE İŞLEMİ ---
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

        // Hedef değerleri UpdateAudioAndWaveform hesaplasın ama biz master volume gibi düşünelim
        // Basitçe: FadeIn ise sesleri aç (Play), FadeOut ise kapat (Stop)

        if (fadeIn)
        {
            if (stableAudioSource && !stableAudioSource.isPlaying)
                stableAudioSource.Play();
            if (staticAudioSource && !staticAudioSource.isPlaying)
                staticAudioSource.Play();

            // İlk değerleri hesapla ki birden patlamasın
            UpdateAudioAndWaveform();
        }

        float initialStableTarget = stableAudioSource ? stableAudioSource.volume : 0;
        float initialStaticTarget = staticAudioSource ? staticAudioSource.volume : 0;

        // Eğer FadeIn ise 0'dan hedefe, FadeOut ise mevcut olandan 0'a
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
            else // Fade Out
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

    private void Update()
    {
        if (!isInteracting)
            return;
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
            UpdateAudioAndWaveform(); // <-- HER TUŞTA SESİ GÜNCELLE
            CheckForSolution();
        }
    }

    private void TogglePlayerModel(bool show)
    {
        if (!playerController)
            return;
        Renderer[] renderers = playerController.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = show;
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

            // Çözülünce sadece temiz ses kalsın
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
        if (isInteracting)
            StartCoroutine(ExitMachineView());
    }

    public void ForceExit()
    {
        if (isInteracting)
            StartCoroutine(ExitMachineView());
    }
}
