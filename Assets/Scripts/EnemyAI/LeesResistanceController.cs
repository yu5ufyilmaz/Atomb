using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Attach to a dedicated camera pivot. Call BeginResistance after the player turns away.</summary>
public sealed class LeesResistanceController : MonoBehaviour
{
    public Camera viewCamera;
    public Transform headPivot;
    public LeesResistanceHud hud;
    public LeesResistanceHands hands;
    public StarterAssets.CharacterController playerMovement;
    public LeesResistanceModel.Tuning tuning = new LeesResistanceModel.Tuning();
    [Range(0, 1)] public float motionIntensity = .9f;
    [Tooltip("Enable in the workshop to keep the retry prompt after a result.")]
    public bool keepResultVisible;
    [Tooltip("Let the main game's PauseManager own ESC and the pause menu.")]
    public bool useGamePause;
    [Tooltip("Assign the components which normally move the player / rotate this camera.")]
    public Behaviour[] suspendDuringResistance = new Behaviour[0];
    public AudioSource heartbeatAudio;
    public UnityEvent onEscaped = new UnityEvent();
    public UnityEvent onCaught = new UnityEvent();

    public LeesResistanceModel Model { get; private set; }
    public bool IsRunning { get { return ownsCamera; } }
    public bool IsPaused { get { return locallyPaused || GamePaused; } }
    bool GamePaused { get { return useGamePause && (Time.timeScale <= 0 || (GameManager.Instance != null && GameManager.Instance.isGamePaused)); } }
    bool ownsCamera, focused = true, locallyPaused, cameraClaimed, previousExternalCamera;
    bool[] previousEnabled;
    Quaternion originalRotation;
    Quaternion originalWorldRotation;
    Vector3 originalPosition;
    float originalFov, endingTime, audioLevel;
    CursorLockMode previousLock;
    bool previousCursor;

    void Awake() { Model = new LeesResistanceModel(tuning); if (hud != null) hud.gameObject.SetActive(false); hands?.Hide(); }

    public void BeginResistance()
    {
        if (ownsCamera || !isActiveAndEnabled || headPivot == null || viewCamera == null) return;
        Model = new LeesResistanceModel(tuning); Model.Begin(Random.Range(0, int.MaxValue));
        originalRotation = headPivot.localRotation; originalPosition = headPivot.localPosition;
        originalFov = viewCamera.fieldOfView;
        previousExternalCamera = playerMovement != null && playerMovement.ExternalCameraControl;
        cameraClaimed = false;
        previousLock = Cursor.lockState; previousCursor = Cursor.visible;
        previousEnabled = new bool[suspendDuringResistance.Length];
        for (int i = 0; i < previousEnabled.Length; i++)
        {
            var component = suspendDuringResistance[i];
            if (component == null || component == this) continue;
            previousEnabled[i] = component.enabled;
        }
        // Snapshot first, then disable: duplicate Inspector entries must preserve the original state.
        for (int i = 0; i < previousEnabled.Length; i++)
            if (suspendDuringResistance[i] != null && suspendDuringResistance[i] != this
                && suspendDuringResistance[i] != playerMovement
                && !(playerMovement != null && suspendDuringResistance[i] is Cinemachine.CinemachineBrain))
                suspendDuringResistance[i].enabled = false;
        if (hud != null) hud.gameObject.SetActive(true);
        ownsCamera = true; endingTime = 0; audioLevel = 0;
        locallyPaused = false; CaptureCursor(); hands?.Hide();
        PlayLoop(heartbeatAudio);
        if (playerMovement == null) TakeHeadControl();
    }

    void Update()
    {
        if (!ownsCamera) return;
        if (GamePaused) { if (hud != null) hud.gameObject.SetActive(false); return; }
        if (!useGamePause && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPaused(!locallyPaused);
        if (IsPaused)
        {
            if (focused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) SetPaused(false);
            hud?.Present(Model, IsPaused, !useGamePause); return;
        }
        if (Model.State == LeesResistanceModel.Phase.Approaching || Model.State == LeesResistanceModel.Phase.Struggling)
        {
            // Mouse delta is already the displacement for this frame. Do not multiply it by deltaTime.
            float pixels = Mouse.current != null ? Mouse.current.delta.ReadValue().x : 0;
            Model.Step(Time.deltaTime, pixels);
        }
        else
        {
            endingTime += Time.deltaTime;
            if (endingTime >= .65f)
            {
                bool escaped = Model.State == LeesResistanceModel.Phase.Survived;
                Release();
                if (keepResultVisible) hud?.Present(Model, false, !useGamePause);
                if (escaped) onEscaped.Invoke(); else onCaught.Invoke();
                return;
            }
        }
        UpdateAudio(); hud?.Present(Model, false, !useGamePause);
    }

    void LateUpdate()
    {
        if (!ownsCamera || IsPaused) return;
        // Turning away does not take control from the player. Only the actual grip owns the view.
        if (playerMovement != null && Model.State == LeesResistanceModel.Phase.Approaching)
        { hands?.Present(Model); return; }
        if (!cameraClaimed) TakeHeadControl();
        float t = Model.Elapsed;
        float danger = Mathf.Clamp01(Model.Danger);
        bool struggling = Model.State == LeesResistanceModel.Phase.Struggling;
        float shake = struggling ? motionIntensity * (.08f + danger * .32f) : 0;
        float angle = Model.HeadAngle;
        if (Model.State == LeesResistanceModel.Phase.Survived) angle *= 1 - Mathf.SmoothStep(0, 1, endingTime / .65f);
        if (Model.State == LeesResistanceModel.Phase.Caught) angle = Mathf.Lerp(angle, Model.PullDirection * tuning.failureAngle, endingTime / .3f);
        float roll = -Model.PullDirection * Model.PullStrength * .14f * motionIntensity;
        var offset = Quaternion.Euler(Mathf.Sin(t * 21) * shake, angle, roll + Mathf.Sin(t * 27) * shake);
        if (playerMovement != null) headPivot.rotation = originalWorldRotation * offset;
        else headPivot.localRotation = originalRotation * offset;
        headPivot.localPosition = originalPosition + Vector3.up * Mathf.Sin(t * 12) * shake * .007f;
        viewCamera.fieldOfView = originalFov - danger * 6 * motionIntensity;
        hands?.Present(Model, endingTime);
    }

    public void CatchForMovement() { if (ownsCamera && !IsPaused) Model.Catch(); }
    public void SetPaused(bool paused)
    {
        if (useGamePause) return; // Main gameplay has one pause owner: the game's explicit pause menu.
        if (!ownsCamera || locallyPaused == paused) return;
        locallyPaused = paused;
        if (paused)
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            heartbeatAudio?.Pause();
        }
        else
        {
            CaptureCursor(); heartbeatAudio?.UnPause();
        }
    }
    public void CancelResistance()
    {
        Release(); Model?.Reset(); if (hud != null) hud.gameObject.SetActive(false); hands?.Hide();
    }
    void OnApplicationFocus(bool hasFocus) { focused = hasFocus; if (!hasFocus && !useGamePause) SetPaused(true); }
    void OnDisable() { Release(); }
    void Release()
    {
        if (!ownsCamera) return;
        ownsCamera = false; locallyPaused = false;
        if (cameraClaimed)
        {
            if (headPivot != null) { headPivot.localRotation = originalRotation; headPivot.localPosition = originalPosition; }
            if (viewCamera != null) viewCamera.fieldOfView = originalFov;
        }
        if (playerMovement != null) playerMovement.ExternalCameraControl = previousExternalCamera;
        cameraClaimed = false;
        for (int i = 0; i < previousEnabled.Length; i++)
            if (suspendDuringResistance[i] != null && suspendDuringResistance[i] != this)
                suspendDuringResistance[i].enabled = previousEnabled[i];
        heartbeatAudio?.Stop(); hands?.Hide();
        if (hud != null) hud.gameObject.SetActive(false);
        Cursor.lockState = previousLock; Cursor.visible = previousCursor;
    }
    void TakeHeadControl()
    {
        originalRotation = headPivot.localRotation; originalWorldRotation = headPivot.rotation;
        originalPosition = headPivot.localPosition; originalFov = viewCamera.fieldOfView;
        if (playerMovement != null)
        {
            playerMovement.ExternalCameraControl = true;
            var brain = viewCamera.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
        }
        cameraClaimed = true;
    }
    void UpdateAudio()
    {
        audioLevel = Mathf.MoveTowards(audioLevel, Model.State == LeesResistanceModel.Phase.Approaching ? .22f : .35f + Model.Danger * .4f, Time.deltaTime);
        if (heartbeatAudio != null) { heartbeatAudio.volume = audioLevel * .8f; heartbeatAudio.pitch = .85f + Model.Danger * .4f; }
    }
    static void CaptureCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    static void PlayLoop(AudioSource source) { if (source == null) return; source.loop = true; source.volume = 0; source.Play(); }
}
