using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class GuderianDoorDeathCutscene : MonoBehaviour
{
    [Header("Guderian")]
    [SerializeField]
    private GuderianAI guderianAI;

    [SerializeField]
    private Transform guderianRoot;

    [SerializeField]
    private GameObject guderianModel;

    [SerializeField]
    private Animator guderianAnimator;

    [SerializeField]
    private NavMeshAgent guderianAgent;

    [SerializeField]
    private Transform guderianPosePoint;

    [SerializeField]
    private string guderianDeathTrigger = "Death";

    [SerializeField]
    private AnimationClip guderianDeathClip;

    [Header("Player")]
    [SerializeField]
    private Transform playerPosePoint;

    [SerializeField]
    private string playerAnimationTrigger;

    [Header("Camera")]
    [SerializeField]
    private Camera cutsceneCamera;

    [SerializeField]
    private Transform cameraPosePoint;

    [SerializeField]
    private Transform cameraLookTarget;

    [SerializeField]
    private float cameraFieldOfView = 45f;

    [SerializeField]
    private bool keepCameraLockedToPose = true;

    [Header("Timing")]
    [SerializeField]
    private float fallbackDuration = 3f;

    [SerializeField]
    private float extraDelayBeforeDeathScreen = 0.25f;

    [Header("Options")]
    [SerializeField]
    private bool triggerOnlyOnce = true;

    [SerializeField]
    private bool disableOtherEnemyLogic = true;

    private bool hasTriggered;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnlyOnce)
            return;

        if (!other.CompareTag("Player"))
            return;

        StartCoroutine(PlayCutscene(other.gameObject));
    }

    private IEnumerator PlayCutscene(GameObject player)
    {
        hasTriggered = true;

        FreezePlayer(player);
        PrepareGuderian();
        PrepareCamera();
        PlayAnimations(player);

        float timer = 0f;
        float duration = GetCutsceneDuration();

        while (timer < duration)
        {
            timer += Time.deltaTime;

            if (keepCameraLockedToPose)
                PrepareCamera();

            yield return null;
        }

        if (DeathUIManager.Instance != null)
            DeathUIManager.Instance.ShowDeathScreen();
    }

    private void FreezePlayer(GameObject player)
    {
        StarterAssetsInputs input = player.GetComponent<StarterAssetsInputs>();
        if (input != null)
        {
            input.move = Vector2.zero;
            input.look = Vector2.zero;
            input.jump = false;
            input.sprint = false;
            input.cursorInputForLook = false;
            input.enabled = false;
        }

        StarterAssets.CharacterController starterController =
            player.GetComponent<StarterAssets.CharacterController>();
        if (starterController != null)
            starterController.enabled = false;

        UnityEngine.CharacterController unityController =
            player.GetComponent<UnityEngine.CharacterController>();
        if (unityController != null)
            unityController.enabled = false;

#if ENABLE_INPUT_SYSTEM
        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null)
            playerInput.enabled = false;
#endif

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (playerPosePoint != null)
            player.transform.SetPositionAndRotation(playerPosePoint.position, playerPosePoint.rotation);
    }

    private void PrepareGuderian()
    {
        if (disableOtherEnemyLogic && GlobalEnemyManager.Instance != null)
            GlobalEnemyManager.Instance.stopAllEnemies = true;

        if (guderianAI != null)
        {
            guderianAI.StopAllCoroutines();
            if (disableOtherEnemyLogic)
                guderianAI.enabled = false;
        }

        if (guderianAgent != null)
            guderianAgent.enabled = false;

        if (guderianRoot == null && guderianAnimator != null)
            guderianRoot = guderianAnimator.transform;

        if (guderianPosePoint != null && guderianRoot != null)
        {
            guderianRoot.SetPositionAndRotation(
                guderianPosePoint.position,
                guderianPosePoint.rotation
            );
        }

        if (guderianModel != null)
            guderianModel.SetActive(true);
    }

    private void PrepareCamera()
    {
        if (cutsceneCamera == null)
            cutsceneCamera = Camera.main;

        if (cutsceneCamera == null)
            return;

        CinemachineBrain brain = cutsceneCamera.GetComponent<CinemachineBrain>();
        if (brain != null)
            brain.enabled = false;

        if (cameraPosePoint != null)
            cutsceneCamera.transform.SetPositionAndRotation(
                cameraPosePoint.position,
                cameraPosePoint.rotation
            );

        Transform lookTarget = cameraLookTarget != null ? cameraLookTarget : guderianRoot;
        if (lookTarget != null)
            cutsceneCamera.transform.LookAt(lookTarget.position);

        cutsceneCamera.fieldOfView = cameraFieldOfView;
    }

    private void PlayAnimations(GameObject player)
    {
        Animator playerAnimator = player.GetComponent<Animator>();
        if (playerAnimator != null && !string.IsNullOrWhiteSpace(playerAnimationTrigger))
            playerAnimator.SetTrigger(playerAnimationTrigger);

        if (guderianAnimator == null)
            return;

        guderianAnimator.SetFloat("Speed", 0f);

        if (!string.IsNullOrWhiteSpace(guderianDeathTrigger))
            guderianAnimator.SetTrigger(guderianDeathTrigger);
    }

    private float GetCutsceneDuration()
    {
        float duration = Mathf.Max(0.1f, fallbackDuration);

        if (guderianDeathClip != null)
            duration = Mathf.Max(duration, guderianDeathClip.length);

        return duration + Mathf.Max(0f, extraDelayBeforeDeathScreen);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (guderianPosePoint != null)
            Gizmos.DrawWireSphere(guderianPosePoint.position, 0.25f);

        Gizmos.color = Color.cyan;
        if (cameraPosePoint != null)
            Gizmos.DrawWireSphere(cameraPosePoint.position, 0.18f);
    }
}
