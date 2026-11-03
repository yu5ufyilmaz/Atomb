using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Attach to a dedicated camera pivot. Call BeginResistance after the player turns away.</summary>
public sealed class LeesResistanceController : MonoBehaviour
{
    public Camera viewCamera;
    public Transform headPivot;
    public LeesResistanceHud hud;
    public LeesResistanceHands hands;
    public StarterAssets.CharacterController playerMovement;
    public LeesResistanceModel.Tuning tuning = new LeesResistanceModel.Tuning();
    [Tooltip("Eğitimdeki güvenli karşılaşma: daha kısa ve daha zayıf çekiş.")]
    public LeesResistanceModel.Tuning practiceTuning = new LeesResistanceModel.Tuning
    { survivalSeconds = 8, pullDegreesPerSecond = 28, failureAngle = 80, strainRate = .4f, mouseDegreesPerPixel = .045f };
    [Range(0, 1)] public float motionIntensity = .9f;
    [Tooltip("Enable in the workshop to keep the retry prompt after a result.")]
    public bool keepResultVisible;
    [Tooltip("Let the main game's PauseManager own ESC and the pause menu.")]
    public bool useGamePause;
    [Tooltip("Assign the components which normally move the player / rotate this camera.")]
    public Behaviour[] suspendDuringResistance = new Behaviour[0];
    public AudioSource heartbeatAudio;
    [Header("Hissiyat (3 Kasım oyun testi)")]
    [Tooltip("Kavrama anında bir kez çalar (boşsa LEES'in şok sesi atanır).")]
    public AudioClip grabSound;
    [Tooltip("Kavrama anındaki kamera darbesi (derece).")]
    public float grabKickDegrees = 4.5f;
    [Tooltip("Çekiş yön değiştirince başın savrulması (derece).")]
    public float flipJoltDegrees = 3.5f;
    [Tooltip("Sarsıntı çarpanı; eski his 1'di.")]
    public float shakeScale = 3.5f;
    [Tooltip("Tehlike arttıkça vinyet, renk sapması ve renk kaybı.")]
    [Range(0, 1)] public float dangerEffects = 1f;
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
    // Grab hit and pull-flip jolts decay from 1 to 0; the volume is this struggle's own, never the shared profile.
    float grabHit, flipJolt;
    int lastPullDirection;
    LeesResistanceModel.Phase lastPhase;
    Volume strainVolume;

    void Awake()
    {
        Model = new LeesResistanceModel(tuning); if (hud != null) hud.gameObject.SetActive(false); hands?.Hide();
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(.5f); vignette.smoothness.Override(.45f); vignette.color.Override(new Color(.18f, 0, 0));
        profile.Add<ChromaticAberration>().intensity.Override(.75f);
        profile.Add<ColorAdjustments>().saturation.Override(-45f);
        strainVolume = gameObject.AddComponent<Volume>();
        strainVolume.isGlobal = true; strainVolume.priority = 900; strainVolume.weight = 0; strainVolume.sharedProfile = profile;
    }

    void OnDestroy() { if (strainVolume != null) Destroy(strainVolume.sharedProfile); }

    public void BeginResistance() { BeginResistance(false); }

    public void BeginResistance(bool practice)
    {
        if (ownsCamera || !isActiveAndEnabled || headPivot == null || viewCamera == null) return;
        Model = new LeesResistanceModel(practice ? practiceTuning : tuning); Model.Begin(Random.Range(0, int.MaxValue));
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
        grabHit = flipJolt = 0; lastPhase = Model.State; lastPullDirection = 0;
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
        UpdateFeel(); UpdateAudio(); hud?.Present(Model, false, !useGamePause);
    }

    // The grab used to be silent and flat, and pull flips barely moved the view (3 Nov playtest).
    void UpdateFeel()
    {
        bool struggling = Model.State == LeesResistanceModel.Phase.Struggling;
        if (struggling && lastPhase == LeesResistanceModel.Phase.Approaching)
        {
            grabHit = 1;
            if (grabSound != null && heartbeatAudio != null) heartbeatAudio.PlayOneShot(grabSound);
        }
        if (struggling && lastPullDirection != 0 && Model.PullDirection != lastPullDirection) flipJolt = 1;
        lastPhase = Model.State; lastPullDirection = struggling ? Model.PullDirection : 0;
        grabHit = Mathf.MoveTowards(grabHit, 0, Time.deltaTime / .35f);
        flipJolt = Mathf.MoveTowards(flipJolt, 0, Time.deltaTime / .3f);
        float strain = struggling ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(Model.Danger)) : 0;
        if (strainVolume != null)
            strainVolume.weight = Mathf.Clamp01(Mathf.Max(strain, grabHit) * dangerEffects * motionIntensity);
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
        float shake = struggling ? motionIntensity * shakeScale * (.08f + danger * .32f) : 0;
        float angle = Model.HeadAngle;
        if (Model.State == LeesResistanceModel.Phase.Survived) angle *= 1 - Mathf.SmoothStep(0, 1, endingTime / .65f);
        if (Model.State == LeesResistanceModel.Phase.Caught) angle = Mathf.Lerp(angle, Model.PullDirection * Model.Settings.failureAngle, endingTime / .3f);
        float roll = -Model.PullDirection * Model.PullStrength * .14f * motionIntensity;
        // Irregular struggle shake instead of a smooth sine; the grab snaps the head down, a flip yanks it sideways.
        float kick = grabHit * grabHit * grabKickDegrees * motionIntensity;
        float jolt = flipJolt * flipJolt * flipJoltDegrees * motionIntensity * Model.PullDirection;
        var offset = Quaternion.Euler((Mathf.PerlinNoise(t * 7, 0) - .5f) * 2 * shake + kick,
            angle + jolt, roll + (Mathf.PerlinNoise(0, t * 9) - .5f) * 2 * shake - kick * .5f * Model.PullDirection);
        if (playerMovement != null) headPivot.rotation = originalWorldRotation * offset;
        else headPivot.localRotation = originalRotation * offset;
        headPivot.localPosition = originalPosition + Vector3.up * Mathf.Sin(t * 12) * shake * .007f;
        viewCamera.fieldOfView = originalFov - (danger * 6 + grabHit * 5) * motionIntensity;
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
        if (strainVolume != null) strainVolume.weight = 0;
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
