using System.Collections;
using Cinemachine;
using UnityEngine;

public class InteractableOscilloscope : MonoBehaviour, IInteractable, IForceExitable
{
    // ... (Değişkenler aynı) ...
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
    private AudioSource audioSource;

    [SerializeField]
    private Transform voltsKnob;

    [SerializeField]
    private Transform timeKnob;

    [SerializeField]
    private float rotationPerStep = 36f;

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

    [SerializeField]
    private float minPitch = 0.5f;

    [SerializeField]
    private float maxPitch = 2.0f;

    [SerializeField]
    private float minVolume = 0.2f;

    [SerializeField]
    private float maxVolume = 1.0f;
    private int currentVoltsSetting = 0;
    private int currentTimeSetting = 0;
    private bool isSolved = false;
    private bool isInteracting = false;
    private Quaternion voltsKnobInitialRot;
    private Quaternion timeKnobInitialRot;

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
        if (audioSource)
        {
            audioSource.Stop();
            audioSource.loop = true;
        }
        currentVoltsSetting = 2;
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

        // --- GM KAYIT ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;
        // ----------------

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
        if (audioSource)
        {
            UpdateAudioAndWaveform();
            audioSource.Play();
        }
        UpdateKnobVisuals();
    }

    private IEnumerator ExitMachineView()
    {
        isInteracting = false;

        // --- GM KAYIT SİL ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        // --------------------

        if (audioSource)
            audioSource.Stop();
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
            UpdateAudioAndWaveform();
            CheckForSolution();
        }
    }

    // ... (Diğer fonksiyonlar aynı kalsın) ...
    private void TogglePlayerModel(bool show)
    {
        if (!playerController)
            return;
        Renderer[] renderers = playerController.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = show;
    }

    private void UpdateAudioAndWaveform()
    {
        float tRatio = (float)currentTimeSetting / maxTimeSetting;
        float targetPitch =
            (currentTimeSetting == correctTimeSetting)
                ? 1.0f
                : Mathf.Lerp(minPitch, maxPitch, tRatio);
        if (audioSource)
            audioSource.pitch = targetPitch;
        float vRatio = (float)currentVoltsSetting / maxVoltsSetting;
        float targetVolume =
            (currentVoltsSetting == correctVoltsSetting)
                ? 1.0f
                : Mathf.Lerp(minVolume, maxVolume, vRatio);
        if (audioSource)
            audioSource.volume = targetVolume;
        if (waveformScript)
        {
            waveformScript.frequency = targetPitch;
            waveformScript.amplitude = Mathf.Lerp(0.5f, 2.5f, vRatio);
        }
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
            StartCoroutine(AutoExit(2.0f));
        }
    }

    private IEnumerator AutoExit(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isInteracting)
            StartCoroutine(ExitMachineView());
    }

    // --- IFORCEEXITABLE ---
    public void ForceExit()
    {
        if (isInteracting)
            StartCoroutine(ExitMachineView());
    }
}
