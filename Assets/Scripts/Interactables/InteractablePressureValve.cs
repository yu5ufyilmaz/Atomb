using System.Collections;
using Cinemachine;
using UnityEngine;

public class InteractablePressureValve : MonoBehaviour, IInteractable, IForceExitable
{
    // ... (Değişkenler aynı) ...
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

        // --- GM KAYIT ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;
        // ----------------

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

        // --- GM KAYIT SİL ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        // --------------------

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

    // ... (Update ve diğerleri aynı) ...
    private void Update()
    {
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
        if (Mathf.Abs(deltaAngle) > 0.01f)
        {
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
        lastAngle = currentAngle;
    }

    // --- IFORCEEXITABLE ---
    public void ForceExit()
    {
        if (isInteracting)
            StartCoroutine(ExitValveMode());
    }
}
