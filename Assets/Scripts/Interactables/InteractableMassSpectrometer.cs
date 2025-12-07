using System.Collections;
using Cinemachine;
using TMPro;
using UnityEngine;

public class InteractableMassSpectrometer : MonoBehaviour, IInteractable
{
    [Header("Components")]
    [SerializeField]
    private Transform magnetPivot;

    [SerializeField]
    private Transform acceleratorRing;

    [SerializeField]
    private Transform mainLever;

    [SerializeField]
    private GameObject ionBeamObj;

    [SerializeField]
    private TextMeshPro screenText;

    [SerializeField]
    private GameObject overheatSmoke;

    [Header("Head Cam & View Target")]
    [SerializeField]
    private string interactAnimTrigger = "InspectMachine";

    [SerializeField]
    private float animationDuration = 1.0f;

    [SerializeField]
    private Transform cameraViewTarget;

    [SerializeField]
    private Vector3 headOffset = new Vector3(0, 0.1f, 0.15f);

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
    private AudioClip leverSound;

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
    private Transform headBone;
    private Transform cinemachineTarget;
    private Animator playerAnimator;

    private UnityEngine.CharacterController playerPhysicsController;
    private MonoBehaviour playerInputScript;

    private bool isInteracting = false;
    private bool isPoweredOn = false;
    private bool isBroken = false;
    private bool isSolved = false;
    private float currentGrindTimer = 0f;
    private float currentCooldown = 0f;
    private Quaternion leverStartRot;
    private Quaternion leverEndRot;
    private float currentRingAngleValue = 0f;

    private void Start()
    {
        playerPhysicsController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerPhysicsController != null)
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

        if (mainLever != null)
        {
            leverStartRot = mainLever.localRotation;
            leverEndRot = leverStartRot * Quaternion.Euler(45, 0, 0);
        }

        float randomOffset = Random.Range(-40f, 40f);
        currentRingAngleValue = ringTargetAngle + 180f + randomOffset;
        UpdateRingRotation();
        ResetMachineVisuals();
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
        if (mainLever != null)
            mainLever.localRotation = leverStartRot;
        isPoweredOn = false;
        isSolved = false;
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
        if (isBroken)
            return $"Sistem Soğuyor... ({currentCooldown:F0}s)";
        if (isSolved)
            return "Kalibrasyon Tamamlandı";
        if (!isPoweredOn)
            return "[Sol Tık] Güç Kolunu Çek";
        return isInteracting ? "" : "[Sol Tık] Analiz Ekranı";
    }

    public void Interact()
    {
        if (isBroken)
        {
            if (audioSource)
                audioSource.PlayOneShot(overheatSound);
            return;
        }
        if (!isPoweredOn)
        {
            StartCoroutine(PullLeverSequence());
            return;
        }
        if (!isInteracting && !isSolved)
        {
            StartCoroutine(EnterMachineView());
        }
    }

    private IEnumerator EnterMachineView()
    {
        isInteracting = true;

        if (playerPhysicsController)
            playerPhysicsController.enabled = false;
        if (playerInputScript)
            playerInputScript.enabled = false;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        if (cinemachineBrain)
            cinemachineBrain.enabled = false;

        // 1. KAFAYA YAPIŞ
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

        // 2. SABİT NOKTAYA
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

    private IEnumerator ExitMachineView()
    {
        isInteracting = false;
        ResetPenalty();

        TogglePlayerModel(true);

        // --- SMOOTH UNDOCK ---
        if (cinemachineTarget != null)
        {
            mainCamera.SetParent(null); // Parent yok, Lerp var
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            float undockTime = 0.5f;
            float t = 0f;

            while (t < undockTime)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / undockTime);
                // Cinemachine hedef noktasına (CameraRoot) süzül
                mainCamera.position = Vector3.Lerp(startPos, cinemachineTarget.position, s);
                mainCamera.rotation = Quaternion.Slerp(startRot, cinemachineTarget.rotation, s);
                yield return null;
            }
        }

        // SNAP FIX: Karakteri kameranın baktığı yere çevir
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
        if (isBroken)
        {
            currentCooldown -= Time.deltaTime;
            if (screenText != null)
                screenText.text = $"OVERHEATED\nWAIT: {currentCooldown:F1}s";
            if (currentCooldown <= 0)
            {
                isBroken = false;
                ResetMachineVisuals();
            }
            return;
        }

        if (!isInteracting || isSolved)
            return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

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
                ApplyPenaltyLogic();
        }
        else
            ResetPenalty();
    }

    private void TogglePlayerModel(bool show)
    {
        if (!playerPhysicsController)
            return;
        Renderer[] renderers = playerPhysicsController.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = show;
    }

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
        isBroken = true;
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
        yield return StartCoroutine(ExitMachineView());
    }

    private IEnumerator SuccessSequence()
    {
        isSolved = true;
        ResetPenalty();
        if (audioSource)
            audioSource.PlayOneShot(successSound);
        if (screenText)
        {
            screenText.color = Color.cyan;
            screenText.text = "CALIBRATION COMPLETE\nCODE: 84-12-99";
        }
        if (ionBeamObj)
        {
            ionBeamObj.SetActive(true);
            if (loopAudioSource)
            {
                loopAudioSource.clip = beamHumSound;
                loopAudioSource.Play();
            }
        }
        yield return new WaitForSeconds(3.0f);
        StartCoroutine(ExitMachineView());
    }

    private IEnumerator PullLeverSequence()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            mainLever.localRotation = Quaternion.Slerp(leverStartRot, leverEndRot, t);
            yield return null;
        }
        if (audioSource)
            audioSource.PlayOneShot(leverSound);
        isPoweredOn = true;
        if (screenText)
        {
            screenText.color = Color.yellow;
            screenText.text = "SYSTEM READY\nWAITING INPUT";
        }
    }
}
