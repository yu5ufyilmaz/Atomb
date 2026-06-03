using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

[DefaultExecutionOrder(-10000)]
public class TimelineRecordingMode : MonoBehaviour
{
    private static readonly string[] DefaultOverrideTypeKeywords =
    {
        "StarterAssetsInputs",
        "StarterAssets.CharacterController",
        "FirstPersonController",
        "ThirdPersonController",
        "PlayerInteraction",
        "PlayerReferences",
        "PlayerSaveHandler",
        "BasicRigidBodyPush",
        "DynomaFlashLight",
        "SaveManager",
        "GameManager",
        "MainMenuManager",
        "GlobalEnemyManager",
        "RoomManager",
        "PressureSystemManager",
        "JumpScareManager",
        "JumpscareManager",
        "GuderianAI",
        "AdamAI",
        "LeesEnemyAI",
        "PauseManager",
        "InGameMenuController",
        "SymbolOverlayManager",
        "Interactable",
        "EndGameButton"
    };

    [Serializable]
    private class RestoredScenePose
    {
        public Transform target;
        public bool restorePosition = true;
        public bool restoreRotation = true;
        public bool restoreScale = true;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;

        public void Capture()
        {
            if (target == null)
                return;

            localPosition = target.localPosition;
            localEulerAngles = target.localEulerAngles;
            localScale = target.localScale;
        }

        public void Apply()
        {
            if (target == null)
                return;

            if (restorePosition)
                target.localPosition = localPosition;

            if (restoreRotation)
                target.localEulerAngles = localEulerAngles;

            if (restoreScale)
                target.localScale = localScale;
        }
    }

    [Header("Timeline")]
    [SerializeField]
    private PlayableDirector director;

    [SerializeField]
    private bool playTimelineOnStart = true;

    [Header("Recording Camera")]
    [SerializeField]
    private Camera recordingCamera;

    [SerializeField]
    private bool makeRecordingCameraMain = true;

    [SerializeField]
    private int recordingCameraDepth = 100;

    [Header("Auto Setup")]
    [SerializeField]
    private bool autoCollectOnAwake = true;

    [SerializeField]
    private bool includeInactiveObjects = true;

    [SerializeField]
    private bool disableOtherSceneCameras = true;

    [SerializeField]
    private bool disableNonTimelineDoorAnimators = true;

    [SerializeField]
    private string[] extraOverrideTypeKeywords = Array.Empty<string>();

    [Header("Keep Scene Pose")]
    [SerializeField]
    private bool restoreScenePoseOnPlay = true;

    [SerializeField]
    private bool autoCollectPoseTargets = true;

    [SerializeField]
    private float poseLockSeconds = 0.75f;

    [SerializeField]
    private RestoredScenePose[] scenePosesToRestore = Array.Empty<RestoredScenePose>();

    [Header("Disable Gameplay Overrides")]
    [SerializeField]
    private Behaviour[] behavioursToDisable;

    [SerializeField]
    private GameObject[] objectsToDisable;

    [SerializeField]
    private GameObject[] objectsToEnable;

    [SerializeField]
    private bool disableCinemachineBrains = true;

    private readonly HashSet<Transform> timelineBoundTransforms = new HashSet<Transform>();
    private float poseLockEndTime;

    private void Awake()
    {
        Time.timeScale = 1f;
        AutoAssignReferences();
        CacheTimelineBindings();

        if (autoCollectOnAwake)
            AutoCollectRecordingLists(false);

        if (restoreScenePoseOnPlay)
            PrepareScenePoses(false);

        foreach (GameObject obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        foreach (GameObject obj in objectsToEnable)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        foreach (Behaviour behaviour in behavioursToDisable)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }

        if (disableCinemachineBrains)
        {
            CinemachineBrain[] brains = FindSceneObjects<CinemachineBrain>();
            foreach (CinemachineBrain brain in brains)
            {
                if (brain != null)
                    brain.enabled = false;
            }
        }

        PrepareRecordingCamera();

        if (restoreScenePoseOnPlay)
        {
            ApplyScenePoses(true);
            poseLockEndTime = Time.unscaledTime + poseLockSeconds;
        }
    }

    [ContextMenu("Auto-Fill Recording Lists")]
    public void AutoFillRecordingLists()
    {
        AutoAssignReferences();
        CacheTimelineBindings();
        AutoCollectRecordingLists(true);
        PrepareScenePoses(true);
    }

    private void Start()
    {
        if (restoreScenePoseOnPlay)
            ApplyScenePoses(true);

        if (!playTimelineOnStart || director == null)
            return;

        director.time = 0d;
        director.Evaluate();
        director.Play();
    }

    private void LateUpdate()
    {
        if (!restoreScenePoseOnPlay || poseLockSeconds <= 0f)
            return;

        if (Time.unscaledTime <= poseLockEndTime)
            ApplyScenePoses(true);
    }

    private void PrepareRecordingCamera()
    {
        if (recordingCamera == null)
            return;

        recordingCamera.gameObject.SetActive(true);
        recordingCamera.enabled = true;
        recordingCamera.depth = recordingCameraDepth;

        if (makeRecordingCameraMain)
            recordingCamera.tag = "MainCamera";
    }

    private void AutoAssignReferences()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        if (recordingCamera != null)
            return;

        Camera[] cameras = FindSceneObjects<Camera>();
        foreach (Camera camera in cameras)
        {
            if (camera == null)
                continue;

            string cameraName = camera.gameObject.name;
            if (cameraName.Contains("DeathCutsceneCamera", StringComparison.OrdinalIgnoreCase)
                || cameraName.Contains("Cutscene", StringComparison.OrdinalIgnoreCase))
            {
                recordingCamera = camera;
                return;
            }
        }

        recordingCamera = Camera.main;
    }

    private void AutoCollectRecordingLists(bool logResult)
    {
        List<Behaviour> behaviours = new List<Behaviour>();
        AddUnique(behaviours, behavioursToDisable);

        Behaviour[] sceneBehaviours = FindSceneObjects<Behaviour>();
        foreach (Behaviour behaviour in sceneBehaviours)
        {
            if (ShouldDisableBehaviour(behaviour))
                AddUnique(behaviours, behaviour);
        }

        behavioursToDisable = behaviours.ToArray();

        List<GameObject> enableObjects = new List<GameObject>();
        AddUnique(enableObjects, objectsToEnable);

        if (director != null)
            AddUnique(enableObjects, director.gameObject);

        if (recordingCamera != null)
            AddUnique(enableObjects, recordingCamera.gameObject);

        objectsToEnable = enableObjects.ToArray();

        List<GameObject> disableObjects = new List<GameObject>();
        AddUnique(disableObjects, objectsToDisable);

        if (disableOtherSceneCameras)
        {
            Camera[] cameras = FindSceneObjects<Camera>();
            foreach (Camera camera in cameras)
            {
                if (camera == null || camera == recordingCamera)
                    continue;

                if (camera.CompareTag("MainCamera")
                    || camera.GetComponent<CinemachineBrain>() != null
                    || camera.gameObject.name.Contains("Player", StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(disableObjects, camera.gameObject);
                }
            }
        }

        objectsToDisable = disableObjects.ToArray();

        if (logResult)
        {
            Debug.Log(
                $"TimelineRecordingMode: {behavioursToDisable.Length} behaviour disable listesine, "
                + $"{objectsToDisable.Length} obje kapatma listesine, "
                + $"{objectsToEnable.Length} obje açma listesine eklendi.",
                this
            );
        }
    }

    private bool ShouldDisableBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || behaviour == this)
            return false;

        if (behaviour is Animator animator)
            return ShouldDisableAnimator(animator);

        if (behaviour is TimelineRecordingMode
            || behaviour is PlayableDirector
            || behaviour is Camera
            || behaviour is AudioListener
            || behaviour is Light
            || behaviour is Canvas
            || behaviour is CanvasGroup)
        {
            return false;
        }

        Type type = behaviour.GetType();
        if (type == null)
            return false;

        string fullName = type.FullName ?? type.Name;
        if (fullName.StartsWith("UnityEngine.", StringComparison.Ordinal)
            || fullName.StartsWith("UnityEditor.", StringComparison.Ordinal))
        {
            return false;
        }

        if (type.Name.Equals("CutsceneTimelineEffects", StringComparison.Ordinal)
            || type.Name.Equals("Volume", StringComparison.Ordinal)
            || type.Name.Equals("CinemachineVirtualCamera", StringComparison.Ordinal)
            || type.Name.Equals("CinemachineVirtualCameraBase", StringComparison.Ordinal))
        {
            return false;
        }

        return MatchesAnyKeyword(fullName, DefaultOverrideTypeKeywords)
            || MatchesAnyKeyword(fullName, extraOverrideTypeKeywords);
    }

    private bool ShouldDisableAnimator(Animator animator)
    {
        if (!disableNonTimelineDoorAnimators || animator == null)
            return false;

        if (IsTimelineBound(animator.transform))
            return false;

        return animator.GetComponentInParent<InteractableDoor>() != null
            || animator.gameObject.name.Contains("Door", StringComparison.OrdinalIgnoreCase)
            || animator.gameObject.name.Contains("Kapi", StringComparison.OrdinalIgnoreCase)
            || animator.gameObject.name.Contains("Kapı", StringComparison.OrdinalIgnoreCase);
    }

    private void PrepareScenePoses(bool logResult)
    {
        if (autoCollectPoseTargets)
            AutoCollectScenePoseTargets();

        foreach (RestoredScenePose pose in scenePosesToRestore)
            pose?.Capture();

        if (logResult)
        {
            Debug.Log(
                $"TimelineRecordingMode: {scenePosesToRestore.Length} transformun sahne pozu yakalandı.",
                this
            );
        }
    }

    private void AutoCollectScenePoseTargets()
    {
        List<RestoredScenePose> poses = new List<RestoredScenePose>();

        if (scenePosesToRestore != null)
        {
            foreach (RestoredScenePose pose in scenePosesToRestore)
            {
                if (pose != null && pose.target != null)
                    AddPoseTarget(poses, pose.target);
            }
        }

        if (recordingCamera != null)
            AddPoseTarget(poses, recordingCamera.transform);

        UnityEngine.CharacterController[] characterControllers =
            FindSceneObjects<UnityEngine.CharacterController>();
        foreach (UnityEngine.CharacterController characterController in characterControllers)
            AddPoseTarget(poses, characterController.transform);

        InteractableDoor[] doors = FindSceneObjects<InteractableDoor>();
        foreach (InteractableDoor door in doors)
        {
            if (door == null)
                continue;

            Transform[] doorTransforms = door.GetComponentsInChildren<Transform>(true);
            foreach (Transform doorTransform in doorTransforms)
                AddPoseTarget(poses, doorTransform);
        }

        scenePosesToRestore = poses.ToArray();
    }

    private void AddPoseTarget(List<RestoredScenePose> poses, Transform target)
    {
        if (target == null)
            return;

        foreach (RestoredScenePose pose in poses)
        {
            if (pose != null && pose.target == target)
                return;
        }

        poses.Add(new RestoredScenePose { target = target });
    }

    private void ApplyScenePoses(bool skipTimelineBound)
    {
        if (scenePosesToRestore == null)
            return;

        foreach (RestoredScenePose pose in scenePosesToRestore)
        {
            if (pose == null || pose.target == null)
                continue;

            if (skipTimelineBound && IsTimelineBound(pose.target))
                continue;

            pose.Apply();
        }
    }

    private void CacheTimelineBindings()
    {
        timelineBoundTransforms.Clear();

        if (director == null || director.playableAsset == null)
            return;

        foreach (PlayableBinding output in director.playableAsset.outputs)
        {
            UnityEngine.Object binding = director.GetGenericBinding(output.sourceObject);
            AddTimelineBinding(binding);
        }
    }

    private void AddTimelineBinding(UnityEngine.Object binding)
    {
        if (binding == null)
            return;

        if (binding is GameObject boundGameObject)
        {
            timelineBoundTransforms.Add(boundGameObject.transform);
            return;
        }

        if (binding is Component boundComponent)
            timelineBoundTransforms.Add(boundComponent.transform);
    }

    private bool IsTimelineBound(Transform target)
    {
        if (target == null || timelineBoundTransforms.Count == 0)
            return false;

        Transform current = target;
        while (current != null)
        {
            if (timelineBoundTransforms.Contains(current))
                return true;

            current = current.parent;
        }

        return false;
    }

    private bool MatchesAnyKeyword(string fullName, string[] keywords)
    {
        if (string.IsNullOrEmpty(fullName) || keywords == null)
            return false;

        foreach (string keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword)
                && fullName.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private T[] FindSceneObjects<T>() where T : UnityEngine.Object
    {
        return FindObjectsByType<T>(
            includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
    }

    private void AddUnique<T>(List<T> list, T item) where T : UnityEngine.Object
    {
        if (item != null && !list.Contains(item))
            list.Add(item);
    }

    private void AddUnique<T>(List<T> list, T[] items) where T : UnityEngine.Object
    {
        if (items == null)
            return;

        foreach (T item in items)
            AddUnique(list, item);
    }
}
