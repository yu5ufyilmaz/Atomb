using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableChalkboard : MonoBehaviour, IInteractable, IForceExitable
{
    public string promptText = "[Sol Tık] Tahtaya Geç";

    [Header("Player Control")]
    [SerializeField]
    private UnityEngine.CharacterController playerPhysicsController;

    [SerializeField]
    private StarterAssetsInputs playerLookScript;

    [SerializeField]
    private Animator playerAnimator;

    [SerializeField]
    private StarterAssets.CharacterController playerMovementScript;
    public PlayerInteraction playerInteractionScript;

    [Header("KAMERA AYARLARI")]
    public Transform fixedCameraTransform;
    private CinemachineVirtualCamera interactVCam;

    [SerializeField]
    private GameObject _playerFollowCamera;

    [Header("Etkileşim Pozisyonu")]
    public Transform interactionStandPoint;
    public float focusDistance = 0.5f;

    [SerializeField]
    private string interactAnimTrigger = "InteractIdle";

    private bool isUsing = false;
    private bool isExiting = false;
    private bool inMachineMode = false;

    private void Start()
    {
        if (playerPhysicsController == null)
            playerPhysicsController =
                Object.FindFirstObjectByType<UnityEngine.CharacterController>();

        if (playerPhysicsController != null)
        {
            GameObject p = playerPhysicsController.gameObject;
            playerLookScript = p.GetComponent<StarterAssetsInputs>();
            playerAnimator = p.GetComponent<Animator>();
            if (playerMovementScript == null)
                playerMovementScript = p.GetComponent<StarterAssets.CharacterController>();
            if (playerInteractionScript == null)
                playerInteractionScript = Object.FindFirstObjectByType<PlayerInteraction>();
        }

        if (fixedCameraTransform != null)
        {
            interactVCam = fixedCameraTransform.GetComponentInChildren<CinemachineVirtualCamera>();
            if (interactVCam == null)
            {
                GameObject vcamObj = new GameObject("Chalkboard_VCam");
                vcamObj.transform.parent = fixedCameraTransform;
                vcamObj.transform.localPosition = Vector3.zero;
                vcamObj.transform.localRotation = Quaternion.identity;
                interactVCam = vcamObj.AddComponent<CinemachineVirtualCamera>();
                interactVCam.Priority = 0;
                interactVCam.m_Lens.FieldOfView = 60f;
            }
        }
    }

    public void Interact()
    {
        if (isUsing || inMachineMode || isExiting)
            return;
        StartCoroutine(MoveToInteractionPoint());
    }

    private IEnumerator MoveToInteractionPoint()
    {
        isUsing = true;
        inMachineMode = false;

        if (playerMovementScript != null)
            playerMovementScript.SetFrozen(true, lockCameraInput: true, restrictRotation: false);

        if (playerLookScript)
            playerLookScript.enabled = false;
        if (playerPhysicsController != null)
            playerPhysicsController.enabled = false;

        if (interactionStandPoint != null)
        {
            float duration = 0.5f;
            float t = 0f;
            Vector3 startPos = playerPhysicsController.transform.position;
            Quaternion startRot = playerPhysicsController.transform.rotation;

            if (playerAnimator)
            {
                playerAnimator.SetFloat("Speed", 0f);
                playerAnimator.SetFloat("MotionSpeed", 0f);
            }

            while (t < duration)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / duration);
                playerPhysicsController.transform.position = Vector3.Lerp(
                    startPos,
                    interactionStandPoint.position,
                    smoothT
                );
                playerPhysicsController.transform.rotation = Quaternion.Slerp(
                    startRot,
                    interactionStandPoint.rotation,
                    smoothT
                );
                yield return null;
            }

            playerPhysicsController.transform.position = interactionStandPoint.position;
            playerPhysicsController.transform.rotation = interactionStandPoint.rotation;
        }

        StartCoroutine(EnterMachineView());
    }

    private IEnumerator EnterMachineView()
    {
        if (GameManager.Instance)
            GameManager.Instance.activeInteraction = this;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        if (interactVCam)
        {
            interactVCam.Priority = 102;
            interactVCam.transform.localPosition = Vector3.zero;
            interactVCam.transform.localRotation = Quaternion.identity;
            interactVCam.m_Lens.FieldOfView =105f;
        }

        yield return new WaitForSeconds(1.0f);

        inMachineMode = true;

        if (playerMovementScript != null)
            playerMovementScript.SetFrozen(true, lockCameraInput: true, restrictRotation: true);

        if (playerLookScript)
            playerLookScript.enabled = false;

        // KAMERA SABİT, MOUSE İMLECİ AKTİF (Sürükleme işlemleri için)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.ShowMachineUI(
                ControlsUIManager.MachineType.Chalkboard,
                "Sentez Tahtası\n[F] Çık"
            );

        if (DoFManager.Instance != null)
            DoFManager.Instance.SetFocus(focusDistance);

        // Tahta yöneticisini uyandır
        if (ChalkboardManager.Instance != null)
            ChalkboardManager.Instance.SetMachineActive(true);
    }

    private void Update()
    {
        if (!inMachineMode || isExiting)
            return;

        if (playerInteractionScript != null)
            playerInteractionScript.ToggleCrosshair(false); // Sürükleme yapacağımız için crosshair gizli kalsın

        // SADECE F TUŞU İLE ÇIKIŞ (ESC'yi GameManager'a bıraktık)
        if (Input.GetKeyDown(KeyCode.F))
        {
            ForceExit();
        }
    }

    public void ForceExit()
    {
        if (inMachineMode && !isExiting)
            StartCoroutine(ExitMachineView());
    }

    private IEnumerator ExitMachineView()
    {
        isExiting = true;
        inMachineMode = false;

        // Tahta yöneticisini uyut
        if (ChalkboardManager.Instance != null)
            ChalkboardManager.Instance.SetMachineActive(false);

        if (interactVCam)
        {
            interactVCam.Priority = 0;
            if (_playerFollowCamera != null)
                _playerFollowCamera.SetActive(true);
        }

        yield return new WaitForSeconds(1.0f);

        if (playerPhysicsController != null)
            playerPhysicsController.enabled = true;
        if (playerMovementScript != null)
            playerMovementScript.SetFrozen(false, lockCameraInput: false, restrictRotation: false);
        if (playerLookScript)
            playerLookScript.enabled = true;
        if (playerInteractionScript)
            playerInteractionScript.ToggleCrosshair(true);

        if (ControlsUIManager.Instance)
            ControlsUIManager.Instance.HideControls();
        if (DoFManager.Instance != null)
            DoFManager.Instance.ResetFocus();

        if (GameManager.Instance)
        {
            GameManager.Instance.activeInteraction = null;
            GameManager.Instance.UpdateCursorState(); // Cursor lock/hide işlemini GameManager yapsın
        }

        isUsing = false;
        isExiting = false;
        PlayerInteraction.NotifyInteractionExit(gameObject);
    }

    public string GetInteractionPrompt() => promptText;

    public void OnFocus() { }

    public void OnLoseFocus() { }
}
