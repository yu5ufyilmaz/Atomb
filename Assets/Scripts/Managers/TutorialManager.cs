using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Playables; // Required for Timeline integration

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    public string objectiveText;
    public string subtitleID;
    public AudioClip voiceClip;

    // CHANGED: Now a List to allow multiple interactables per step
    public List<GameObject> allowedInteractables = new List<GameObject>();

    public Transform focusTarget;
    public float focusDuration = 2.0f;
    public float initialDelay = 0.5f;
}

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Core Systems")]
    public TextMeshProUGUI objectiveUI;
    public AudioSource innerVoiceSource;
    public StarterAssetsInputs playerInputs;

    // NEW: Timeline reference to wait for the intro
    [Header("Intro Settings")]
    public PlayableDirector introTimeline;

    private StarterAssets.CharacterController playerMoveScript;

    [Header("Tutorial Sequence")]
    public List<TutorialStep> tutorialSequence;
    private Queue<TutorialStep> stepQueue = new Queue<TutorialStep>();

    private TutorialStep currentStep;

    [Header("Phase Specific Trackers")]
    public InteractableDoor personnelSpaceDoor;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        playerMoveScript = FindFirstObjectByType<StarterAssets.CharacterController>();

        foreach (var step in tutorialSequence)
        {
            stepQueue.Enqueue(step);
        }

        // Check if there is a timeline to wait for
        if (introTimeline != null)
        {
            introTimeline.stopped += OnIntroFinished;
        }
        else
        {
            Invoke(nameof(AdvanceQueue), 1.0f);
        }
    }

    // Triggered automatically when the intro cutscene ends
    private void OnIntroFinished(PlayableDirector director)
    {
        introTimeline.stopped -= OnIntroFinished; // Unsubscribe to prevent memory leaks
        AdvanceQueue();
    }

    public void AdvanceQueue()
    {
        if (stepQueue.Count == 0)
        {
            Debug.Log("Tutorial Complete!");
            if (PlayerInteraction.Instance != null)
                PlayerInteraction.Instance.isTutorialMode = false;
            return;
        }

        currentStep = stepQueue.Dequeue();
        StartCoroutine(PlayStepCues(currentStep));
    }

    private IEnumerator PlayStepCues(TutorialStep step)
    {
        yield return new WaitForSeconds(step.initialDelay);

        if (objectiveUI != null)
            objectiveUI.text = step.objectiveText;

        if (GlobalSubtitleManager.Instance != null && !string.IsNullOrEmpty(step.subtitleID))
        {
            GlobalSubtitleManager.Instance.Show(step.subtitleID);
        }
        if (innerVoiceSource != null && step.voiceClip != null)
        {
            innerVoiceSource.PlayOneShot(step.voiceClip);
        }

        // CHANGED: Iterate and add all assigned interactables for this phase
        if (PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.allowedTutorialObjects.Clear();
            if (step.allowedInteractables != null && step.allowedInteractables.Count > 0)
            {
                PlayerInteraction.Instance.allowedTutorialObjects.AddRange(
                    step.allowedInteractables
                );
            }
        }

        if (step.focusTarget != null)
        {
            yield return StartCoroutine(FocusCameraOnTarget(step.focusTarget, step.focusDuration));
        }
    }

    private IEnumerator FocusCameraOnTarget(Transform target, float holdDuration)
    {
        if (playerInputs == null || playerMoveScript == null || Camera.main == null)
            yield break;

        playerInputs.cursorInputForLook = false;
        playerInputs.move = Vector2.zero;

        Vector3 startForward = Camera.main.transform.forward;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;

            Vector3 targetDir = (target.position - Camera.main.transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(targetDir);

            Quaternion currentRot = Quaternion.Slerp(
                Quaternion.LookRotation(startForward),
                targetRot,
                Mathf.SmoothStep(0f, 1f, t)
            );

            float yaw = currentRot.eulerAngles.y;
            float pitch = currentRot.eulerAngles.x;

            if (pitch > 180f)
                pitch -= 360f;

            playerMoveScript.ForceCameraRotation(yaw, pitch);
            yield return null;
        }

        yield return new WaitForSeconds(holdDuration);

        playerInputs.cursorInputForLook = true;
    }

    private void OnDestroy()
    {
        // Failsafe cleanup for the event listener
        if (introTimeline != null)
        {
            introTimeline.stopped -= OnIntroFinished;
        }
    }

    private void Update()
    {
        if (currentStep == null)
            return;

        switch (currentStep.stepName)
        {
            case "LockDoor":
                if (personnelSpaceDoor != null && personnelSpaceDoor.IsLocked())
                {
                    AdvanceQueue();
                }
                break;

            case "FindPuzzle":
                // Logic for puzzle piece pickup goes here
                break;
        }
    }
}
