using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public enum JumpscareStyle
{
    Direct,
    SmartDisplacement,
    ForcedBehind,
}

public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance;

    [Header("Referanslar")]
    [SerializeField]
    private StarterAssetsInputs playerInput;

    [SerializeField]
    private StarterAssets.CharacterController playerController;

    [SerializeField]
    private Animator playerAnimator;

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Transform headBone;

    [Header("Volume (Post-Process)")]
    [SerializeField]
    private Volume globalVolume;

    // HDRP Efektleri
    private Vignette m_Vignette;
    private ChromaticAberration m_Aberration;
    private LensDistortion m_LensDistortion;
    private FilmGrain m_FilmGrain;
    private ColorAdjustments m_ColorAdjustments;

    [Header("Varsayılan Ayarlar (Yedek)")]
    [SerializeField]
    private JumpscareProfile defaultProfile; // Eğer düşmanda ayar yoksa bunu kullanır

    [Header("Dönüş Ayarları")]
    [SerializeField]
    private float slowTurnSpeed = 3.5f;

    [SerializeField]
    private float fastTurnSpeed = 15.0f;

    [SerializeField]
    private LayerMask obstacleLayers;

    [SerializeField]
    private Vector3 eyeOffset = new Vector3(0, 0.1f, 0.15f);

    // Animasyon ID'leri
    private int _animIDPanicRight;
    private int _animIDPanicLeft;
    private int _animIDPanicBack;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        _animIDPanicRight = Animator.StringToHash("PanicTurnRight");
        _animIDPanicLeft = Animator.StringToHash("PanicTurnLeft");
        _animIDPanicBack = Animator.StringToHash("PanicTurnBack");
    }

    private void Start()
    {
        // Oyuncu Referanslarını Bul
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            if (!playerInput)
                playerInput = player.GetComponent<StarterAssetsInputs>();
            if (!playerController)
                playerController = player.GetComponent<StarterAssets.CharacterController>();
            if (!playerAnimator)
                playerAnimator = player.GetComponent<Animator>();
            if (headBone == null)
            {
                Transform head = RecursiveFindChild(player.transform, "Head");
                if (head == null)
                    head = RecursiveFindChild(player.transform, "mixamorig:Head");
                if (head != null)
                    headBone = head;
            }
        }
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (globalVolume == null)
            globalVolume = FindObjectOfType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out m_Vignette);
            globalVolume.profile.TryGet(out m_Aberration);
            globalVolume.profile.TryGet(out m_LensDistortion);
            globalVolume.profile.TryGet(out m_FilmGrain);
            globalVolume.profile.TryGet(out m_ColorAdjustments);
        }
    }

    // ARTIK BURASI SENİN İSTEDİĞİN GİBİ PROFİL ALIYOR
    public void StartJumpscare(
        Transform enemy,
        JumpscareProfile profile,
        bool playTurnAnim = true,
        JumpscareStyle style = JumpscareStyle.Direct
    )
    {
        // Eğer profil gönderilmediyse varsayılanı kullan
        JumpscareProfile activeProfile = profile != null ? profile : defaultProfile;

        // Eğer varsayılan da yoksa geçici bir tane oluştur (Hata vermemesi için)
        if (activeProfile == null)
        {
            activeProfile = new JumpscareProfile();
            activeProfile.duration = 2.5f;
            activeProfile.enemyEyeHeightOffset = 1.3f;
            activeProfile.shakeIntensity = 0.5f;
            activeProfile.shakeFrequency = 20f;
            activeProfile.cameraLocalOffset = eyeOffset;
            activeProfile.cameraRotationDelay = 0.35f;
        }

        StartCoroutine(JumpscareRoutine(enemy, activeProfile, playTurnAnim, style));
    }

    private IEnumerator JumpscareRoutine(
        Transform enemy,
        JumpscareProfile settings,
        bool playTurnAnim,
        JumpscareStyle style
    )
    {
        // --- BU SATIRI EN BAŞA EKLE ---
        // Pressure System'e "Efektlere dokunma, bende" diyoruz.
        if (PressureSystemManager.Instance != null)
        {
            PressureSystemManager.Instance.StopEffectsForJumpscare();
        }
        // 1. KONTROLLERİ KAPAT
        if (playerInput)
        {
            playerInput.cursorInputForLook = false;
            playerInput.move = Vector2.zero;
            playerInput.enabled = false;
        }
        if (playerController)
            playerController.enabled = false;
        if (mainCamera != null)
        {
            var brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain)
                brain.enabled = false;
        }

        bool hasCutscene = settings.cutsceneDirector != null;
        bool cutsceneControlsEverything = hasCutscene && settings.cutsceneTakesFullControl;

        if (headBone != null && mainCamera != null && !cutsceneControlsEverything)
        {
            mainCamera.transform.SetParent(headBone);
            mainCamera.transform.localPosition = settings.cameraLocalOffset;
        }

        if (hasCutscene)
            PlayCutscene(settings.cutsceneDirector);

        bool hasCameraAnimatorTrigger =
            !string.IsNullOrWhiteSpace(settings.cameraAnimatorTrigger) &&
            mainCamera != null &&
            !cutsceneControlsEverything;

        if (hasCameraAnimatorTrigger)
            TriggerCameraAnimator(settings.cameraAnimatorTrigger);

        bool hasCameraAnimation =
            settings.cameraAnimationClip != null && mainCamera != null && !cutsceneControlsEverything;
        bool cameraAnimationControlsTransform =
            (hasCameraAnimation || hasCameraAnimatorTrigger) &&
            settings.cameraAnimationOverridesLookAt;
        PlayableGraph cameraAnimationGraph = default;

        if (hasCameraAnimation)
            cameraAnimationGraph = PlayCameraAnimation(settings.cameraAnimationClip);

        // 2. POZİSYON VE DÖNÜŞ HIZI
        Vector3 targetPos = enemy.position;
        float currentTurnSpeed = fastTurnSpeed;
        float startFOV = mainCamera != null ? mainCamera.fieldOfView : 60f;

        switch (style)
        {
            case JumpscareStyle.Direct:
                currentTurnSpeed = fastTurnSpeed;
                break;
            case JumpscareStyle.SmartDisplacement:
                targetPos = GetSmartJumpscarePosition(playerController.transform, 1.2f);
                targetPos.y = playerController.transform.position.y;
                enemy.position = targetPos;
                enemy.LookAt(playerController.transform.position);
                currentTurnSpeed = slowTurnSpeed;
                break;
            case JumpscareStyle.ForcedBehind:
                Vector3 backDir = -playerController.transform.forward;
                targetPos = playerController.transform.position + (backDir * 1.2f);
                targetPos.y = playerController.transform.position.y;
                enemy.position = targetPos;
                enemy.LookAt(playerController.transform.position);
                currentTurnSpeed = slowTurnSpeed;
                break;
        }

        if (settings.cameraTurnSpeedOverride > 0f)
            currentTurnSpeed = settings.cameraTurnSpeedOverride;

        // 3. ANİMASYON
        if (playTurnAnim && playerAnimator != null)
        {
            Vector3 dirToTarget = (enemy.position - playerController.transform.position).normalized;
            float angle = Vector3.SignedAngle(
                playerController.transform.forward,
                dirToTarget,
                Vector3.up
            );
            if (Mathf.Abs(angle) > 135f)
                playerAnimator.SetTrigger(_animIDPanicBack);
            else if (angle > 0)
                playerAnimator.SetTrigger(_animIDPanicRight);
            else
                playerAnimator.SetTrigger(_animIDPanicLeft);
        }

        // 4. DÖNGÜ (AYARLAR ARTIK SETTINGS'DEN GELİYOR)
        float timer = 0f;
        float totalDuration = GetJumpscareDuration(settings);

        while (timer < totalDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / totalDuration;

            if (mainCamera != null && !cameraAnimationControlsTransform && !cutsceneControlsEverything)
            {
                // Profildeki EyeHeightOffset'i kullanıyoruz
                Vector3 enemyHeadPos = enemy.position + (Vector3.up * settings.enemyEyeHeightOffset);
                Quaternion targetRot = Quaternion.LookRotation(
                    enemyHeadPos - mainCamera.transform.position
                );

                if (timer > settings.cameraRotationDelay)
                {
                    mainCamera.transform.rotation = Quaternion.Slerp(
                        mainCamera.transform.rotation,
                        targetRot,
                        Time.deltaTime * currentTurnSpeed
                    );
                }

                // Profildeki Shake ayarlarını kullanıyoruz
                float shake =
                    (Mathf.PerlinNoise(Time.time * settings.shakeFrequency, 0f) - 0.5f)
                    * settings.shakeIntensity;

                // Profildeki Tilt (Eğilme) açısını kullanıyoruz
                float currentTilt = Mathf.Lerp(0, settings.tiltAngle, progress);

                mainCamera.transform.Rotate(new Vector3(shake, shake * 0.5f, currentTilt));
            }

            if (mainCamera != null && !cutsceneControlsEverything)
                mainCamera.fieldOfView = Mathf.Lerp(startFOV, settings.targetFOV, progress);

            if (!hasCutscene || settings.applyProfileEffectsDuringCutscene)
                ApplyJumpscareEffects(progress); // Efektleri uygula
            yield return null;
        }

        if (cameraAnimationGraph.IsValid())
            cameraAnimationGraph.Destroy();

        // --- SON DOKUNUŞ: EFEKTLERİ KİLİTLE ---
        // Döngü bittiğinde efektleri %100'e (maksimum bozukluğa) sabitliyoruz.
        // Böylece Death UI açılıp zaman durduğunda ekran bozuk kalır.
        if (!hasCutscene || settings.applyProfileEffectsDuringCutscene)
            ApplyJumpscareEffects(1.0f);
        // --------------------------------------

        if (DeathUIManager.Instance != null)
            DeathUIManager.Instance.ShowDeathScreen();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private float GetJumpscareDuration(JumpscareProfile settings)
    {
        float duration = Mathf.Max(0.01f, settings.duration);

        if (settings.cameraAnimationClip != null)
            duration = Mathf.Max(duration, settings.cameraAnimationClip.length);

        if (settings.cameraAnimatorClipToWaitFor != null)
            duration = Mathf.Max(duration, settings.cameraAnimatorClipToWaitFor.length);

        if (settings.enemyAnimationClipToWaitFor != null)
            duration = Mathf.Max(duration, settings.enemyAnimationClipToWaitFor.length);

        if (settings.cutsceneDirector != null)
            duration = Mathf.Max(duration, GetPlayableDirectorDuration(settings.cutsceneDirector));

        return duration + Mathf.Max(0f, settings.deathScreenExtraDelay);
    }

    private void PlayCutscene(PlayableDirector director)
    {
        director.time = 0d;
        director.Play();
    }

    private float GetPlayableDirectorDuration(PlayableDirector director)
    {
        double duration = director.duration;

        if ((double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d) &&
            director.playableAsset != null)
        {
            duration = director.playableAsset.duration;
        }

        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
            return 0f;

        return (float)duration;
    }

    private void TriggerCameraAnimator(string triggerName)
    {
        Animator cameraAnimator = mainCamera.GetComponent<Animator>();
        if (cameraAnimator == null)
        {
            Debug.LogWarning("Jumpscare camera trigger verildi ama Main Camera üzerinde Animator yok.");
            return;
        }

        cameraAnimator.enabled = true;
        cameraAnimator.ResetTrigger(triggerName);
        cameraAnimator.SetTrigger(triggerName);
    }

    private PlayableGraph PlayCameraAnimation(AnimationClip clip)
    {
        PlayableGraph graph = PlayableGraph.Create("Jumpscare Camera Animation");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        Animator cameraAnimator = mainCamera.GetComponent<Animator>();
        if (cameraAnimator == null)
            cameraAnimator = mainCamera.gameObject.AddComponent<Animator>();

        cameraAnimator.enabled = true;

        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            graph,
            "Camera Animation",
            cameraAnimator
        );
        output.SetSourcePlayable(playable);
        graph.Play();

        return graph;
    }

    private Vector3 GetSmartJumpscarePosition(Transform player, float distance)
    {
        Vector3 origin = player.position + Vector3.up * 1.5f;
        bool hitRight = Physics.Raycast(origin, player.right, 1.0f, obstacleLayers);
        bool hitLeft = Physics.Raycast(origin, -player.right, 1.0f, obstacleLayers);
        if (!hitRight)
            return player.position + (player.right * distance);
        else if (!hitLeft)
            return player.position + (-player.right * distance);
        else
            return player.position + (-player.forward * distance);
    }

    private void ApplyJumpscareEffects(float progress)
    {
        if (m_Vignette != null)
        {
            m_Vignette.intensity.Override(Mathf.Lerp(0f, 0.65f, progress));
            m_Vignette.smoothness.Override(Mathf.Lerp(0.2f, 1f, progress));
        }
        if (m_Aberration != null)
            m_Aberration.intensity.Override(Mathf.Lerp(0f, 1f, progress));
        if (m_LensDistortion != null)
        {
            m_LensDistortion.intensity.Override(Mathf.Lerp(0f, -0.4f, progress));
            m_LensDistortion.scale.Override(Mathf.Lerp(1f, 0.9f, progress));
        }
        if (m_FilmGrain != null)
            m_FilmGrain.intensity.Override(Mathf.Lerp(0f, 1f, progress));
        if (m_ColorAdjustments != null)
            m_ColorAdjustments.saturation.Override(Mathf.Lerp(0f, -50f, progress));
    }

    private Transform RecursiveFindChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(childName))
                return child;
            Transform found = RecursiveFindChild(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }

   
}
