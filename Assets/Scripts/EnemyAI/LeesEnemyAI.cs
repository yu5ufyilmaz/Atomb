using System.Collections;
using System.Collections.Generic;
using StarterAssets; // Input erişimi için gerekli
using UnityEngine;

public class LeesEnemyAI : MonoBehaviour
{
    public static LeesEnemyAI Instance;

    public enum LeesState
    {
        Hidden,
        Active,
        Jumpscare,
    }

    public LeesState currentState = LeesState.Hidden;

    [Header("Model & Animasyon")]
    public Animator leesAnimator;
    private static readonly int JumpscareTrigger = Animator.StringToHash("Jumpscare");

    [Header("Görüş Ayarları")]
    public LayerMask obstacleMask;
    public Transform eyesPosition;

    [Header("Oda ve Şans Sistemi")]
    [SerializeField]
    private RoomManager currentRoom;
    private RoomManager spawnRoom;

    [SerializeField]
    private float timeSpentInCurrentRoom;

    public float baseSpawnChance = 10f;
    public float chanceIncreasePerSecond = 2f;
    public float spawnCheckInterval = 5f;

    [Header("Senaryo Ayarları")]
    public float maxIgnoranceTime = 30f;
    public float maxReactionTime = 3.0f;
    public float survivalWaitTime = 15f;
    public float movementTolerance = 0.1f;

    [Header("Ses Efektleri")]
    public AudioSource audioSource;

    [Tooltip("Sesin açılma/kapanma süresi (Saniye)")]
    public float audioFadeDuration = 2.0f;

    [Tooltip("Oyuncu Leesi gördüğünde çalacak gerilim sesi (Loop olmalı)")]
    public AudioClip stareSound;

    [Tooltip("Saldırı anında çalacak ses")]
    public AudioClip jumpscareSound;

    [Header("Jumpscare Ayarları")]
    public float jumpscareDistance = 1.2f;
    public float jumpscareYOffset = -0.5f;

    [Header("Spawn Sıklığı")]
    public float spawnCooldownAfterDespawn = 20f;
    private float currentCooldownTimer = 0f;

    // --- DEBUG VERİLERİ ---
    [HideInInspector]
    public float debugCooldownTimer;

    [HideInInspector]
    public float debugReactionTimer;

    [HideInInspector]
    public float debugSurvivalTimer;

    [HideInInspector]
    public float debugIgnoranceTimer;

    [HideInInspector]
    public bool debugIsVisible;

    [HideInInspector]
    public bool debugHasBeenSpotted;

    // Sayaçlar
    private float currentIgnoranceTimer;
    private float currentReactionTimer;
    private float currentSurvivalTimer;

    private bool hasBeenSpotted = false;
    private bool hasTurnedAway = false;
    private Vector3 lastPlayerPos;

    // Ses Kontrolü İçin Coroutine
    private Coroutine audioFadeRoutine;

    [Header("Referanslar")]
    public Transform playerTransform;
    public Camera playerCamera;
    private UnityEngine.CharacterController targetCharacterController;

    // YENİ: Input kontrolü için (Sarhoşlukta ölmemek adına)
    private StarterAssetsInputs playerInputs;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;

        if (playerTransform != null)
        {
            targetCharacterController =
                playerTransform.GetComponent<UnityEngine.CharacterController>();
            // Input referansını al
            playerInputs = playerTransform.GetComponent<StarterAssetsInputs>();
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.volume = 0f; // Başlangıçta sessiz
        }

        DespawnLees();
        InvokeRepeating(nameof(CheckSpawnLogic), 5f, spawnCheckInterval);
    }

    private void Update()
    {
        if (GlobalEnemyManager.Instance != null && GlobalEnemyManager.Instance.stopAllEnemies)
            return;

        if (currentCooldownTimer > 0)
        {
            currentCooldownTimer -= Time.deltaTime;
            debugCooldownTimer = currentCooldownTimer;
            return;
        }

        if (currentState == LeesState.Hidden && currentRoom != null && currentRoom.isDangerous)
        {
            timeSpentInCurrentRoom += Time.deltaTime;
        }

        if (currentState == LeesState.Active)
        {
            HandleActiveLogic();
        }

        // Debug Güncellemeleri
        debugReactionTimer = currentReactionTimer;
        debugSurvivalTimer = currentSurvivalTimer;
        debugIgnoranceTimer = currentIgnoranceTimer;
        debugIsVisible = CheckIfVisible();
        debugHasBeenSpotted = hasBeenSpotted;
    }

    private void HandleActiveLogic()
    {
        // Oyuncunun hareket hızını hesapla
        float playerSpeed =
            Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = playerTransform.position;

        // Odadan kaçarsa (Scenario B)
        if (currentRoom != spawnRoom)
        {
            TriggerDeath("Scenario B: Odadan dışarı kaçıldı!", true);
            return;
        }

        bool isPlayerControllable = (
            targetCharacterController != null && targetCharacterController.enabled
        );
        bool logicVisible = CheckIfVisible();

        if (!isPlayerControllable && !hasBeenSpotted)
            logicVisible = false;

        // --- HENÜZ FARK EDİLMEDİ (A) ---
        if (!hasBeenSpotted)
        {
            if (logicVisible)
            {
                // İlk görüş anı
                hasBeenSpotted = true;
                hasTurnedAway = false;
                currentReactionTimer = 0f;

                // Sesi Fade-In ile başlat
                if (audioSource && stareSound)
                    StartFadeAudio(stareSound, true);

                Debug.Log("Lees: FARK EDİLDİ!");
            }
            else
            {
                currentIgnoranceTimer += Time.deltaTime;
                if (currentIgnoranceTimer >= maxIgnoranceTime)
                    TriggerDeath("Scenario A: Süre doldu (Ignorance)");
            }
        }
        // --- FARK EDİLDİKTEN SONRA (C ve D) ---
        else
        {
            if (logicVisible) // Hala bakıyor
            {
                if (hasTurnedAway)
                {
                    TriggerDeath("HATA: Arkasını döndükten sonra tekrar baktı!");
                    return;
                }
                currentReactionTimer += Time.deltaTime;
                currentSurvivalTimer = 0f;

                if (currentReactionTimer >= maxReactionTime)
                    TriggerDeath("Scenario C: Çok uzun süre baktın!");
            }
            else // Arkasını döndü (Survival)
            {
                hasTurnedAway = true;

                // --- HAREKET KONTROLÜ (DÜZELTİLDİ) ---
                // Oyuncu tuşlara basıyor mu? (Input var mı?)
                bool isInputting = (playerInputs != null && playerInputs.move != Vector2.zero);

                // Eğer tuşa basıyorsa ve hareket ediyorsa -> ÖLÜM
                // Tuşa basmıyorsa (sarhoşluktan kaysa bile) -> GÜVENLİ
                if (isInputting && playerSpeed > movementTolerance)
                {
                    TriggerDeath("Scenario D: Arkasını döndün ama hareket ettin (Tuşlara bastın)!");
                }
                else
                {
                    currentSurvivalTimer += Time.deltaTime;
                    currentReactionTimer = Mathf.Max(0, currentReactionTimer - Time.deltaTime);

                    if (currentSurvivalTimer >= survivalWaitTime)
                    {
                        Debug.Log("Lees: Başarılı kurtuluş.");
                        DespawnLees();
                    }
                }
            }
        }
    }

    public void TriggerDeath(string reason, bool spawnBehind = false)
    {
        if (currentState == LeesState.Jumpscare)
            return;

        // Oyuncu etkileşimdeyse önce çıkarsın, sonra öldürsün
        if (targetCharacterController != null && !targetCharacterController.enabled)
        {
            StartCoroutine(ForceExitAndKillRoutine(reason, spawnBehind));
            return;
        }
        ExecuteDeathNow(reason, spawnBehind);
    }

    private IEnumerator ForceExitAndKillRoutine(string reason, bool spawnBehind)
    {
        if (GameManager.Instance != null && GameManager.Instance.activeInteraction != null)
            GameManager.Instance.activeInteraction.ForceExit();

        float maxWait = 6.0f;
        float timer = 0f;

        while (
            targetCharacterController != null
            && !targetCharacterController.enabled
            && timer < maxWait
        )
        {
            timer += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
        ExecuteDeathNow(reason, spawnBehind);
    }

    private void ExecuteDeathNow(string reason, bool spawnBehind)
    {
        Debug.LogError($"ÖLÜM: {reason}");

        // Jumpscare anında sesi hemen kes ve patlat
        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);

        if (audioSource)
        {
            audioSource.Stop();
            audioSource.volume = 1.0f; // Sesi fulle
            if (jumpscareSound)
                audioSource.PlayOneShot(jumpscareSound);
        }

        if (leesAnimator != null)
            leesAnimator.SetTrigger(JumpscareTrigger);

        float animDuration = 3.5f;

        if (spawnBehind)
            StartCoroutine(ExecuteBehindJumpscare(animDuration));
        else
            StartCoroutine(ExecuteSmartJumpscare(animDuration));
    }

    // --- SES YUMUŞATMA (FADE) ---
    private void StartFadeAudio(AudioClip clip, bool fadeIn)
    {
        if (audioSource == null)
            return;

        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);
        audioFadeRoutine = StartCoroutine(FadeAudioRoutine(clip, fadeIn));
    }

    private IEnumerator FadeAudioRoutine(AudioClip clip, bool fadeIn)
    {
        float targetVolume = fadeIn ? 1.0f : 0.0f;
        float startVolume = audioSource.volume;
        float timer = 0f;

        if (fadeIn)
        {
            audioSource.clip = clip;
            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        while (timer < audioFadeDuration)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, timer / audioFadeDuration);
            yield return null;
        }

        audioSource.volume = targetVolume;

        if (!fadeIn)
        {
            audioSource.Stop();
        }
    }

    // --- JUMPSCARE COROUTINES ---
    private IEnumerator ExecuteBehindJumpscare(float duration)
    {
        currentState = LeesState.Jumpscare;
        Vector3 behindPos =
            playerTransform.position - (playerTransform.forward * jumpscareDistance);
        behindPos.y = playerTransform.position.y + jumpscareYOffset;

        transform.position = behindPos;
        transform.LookAt(
            new Vector3(
                playerTransform.position.x,
                transform.position.y,
                playerTransform.position.z
            )
        );

        ShowModel(true);

        if (JumpscareManager.Instance != null)
            JumpscareManager.Instance.StartJumpscare(transform, true, duration);

        yield return null;
    }

    private IEnumerator ExecuteSmartJumpscare(float duration)
    {
        currentState = LeesState.Jumpscare;
        Vector3 targetPos =
            playerTransform.position + (playerTransform.forward * jumpscareDistance);
        targetPos.y = playerTransform.position.y + jumpscareYOffset;

        transform.position = targetPos;
        transform.LookAt(
            new Vector3(
                playerTransform.position.x,
                transform.position.y,
                playerTransform.position.z
            )
        );

        ShowModel(true);

        if (JumpscareManager.Instance != null)
            JumpscareManager.Instance.StartJumpscare(transform, true, duration);

        yield return null;
    }

    // --- SPAWN LOGIC ---
    private void CheckSpawnLogic()
    {
        if (
            currentCooldownTimer > 0
            || currentState == LeesState.Active
            || currentState == LeesState.Jumpscare
            || currentRoom == null
            || !currentRoom.isDangerous
        )
            return;

        if (GlobalEnemyManager.Instance != null && !GlobalEnemyManager.Instance.CanAttack())
            return;

        float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
        if (Random.Range(0f, 100f) < Mathf.Clamp(chance, 0f, 90f))
            SpawnLeesInRoom();
    }

    public void SpawnLeesInRoom()
    {
        if (currentRoom == null || currentRoom.spawnPoints.Count == 0)
            return;

        Transform bestPoint = GetSafeSpawnPoint();
        if (bestPoint == null)
            bestPoint = currentRoom.spawnPoints[0];

        spawnRoom = currentRoom;
        transform.position = bestPoint.position;
        transform.LookAt(
            new Vector3(
                playerTransform.position.x,
                transform.position.y,
                playerTransform.position.z
            )
        );

        if (GlobalEnemyManager.Instance != null)
            GlobalEnemyManager.Instance.RegisterAttackStart();

        currentState = LeesState.Active;
        timeSpentInCurrentRoom = 0;
        currentIgnoranceTimer = 0;
        currentReactionTimer = 0;
        currentSurvivalTimer = 0;
        hasBeenSpotted = false;
        hasTurnedAway = false;
        lastPlayerPos = playerTransform.position;

        ShowModel(true);
        if (leesAnimator != null)
        {
            leesAnimator.Rebind();
            leesAnimator.Update(0f);
        }
    }

    private Transform GetSafeSpawnPoint()
    {
        if (currentRoom == null || currentRoom.spawnPoints.Count == 0)
            return null;

        foreach (Transform point in currentRoom.spawnPoints)
        {
            Vector3 dirToPoint = (point.position - playerTransform.position).normalized;
            float dot = Vector3.Dot(playerTransform.forward, dirToPoint);

            if (dot < -0.2f)
            {
                return point;
            }
        }
        return currentRoom.spawnPoints[Random.Range(0, currentRoom.spawnPoints.Count)];
    }

    public void DespawnLees()
    {
        if (GlobalEnemyManager.Instance != null && currentState == LeesState.Active)
            GlobalEnemyManager.Instance.RegisterAttackEnd();

        // Sesi yavaşça kapat
        StartFadeAudio(null, false);

        currentState = LeesState.Hidden;
        ShowModel(false);
        currentCooldownTimer = spawnCooldownAfterDespawn;
    }

    private void ShowModel(bool show)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = show;
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = show;
    }

    public void EnterRoom(RoomManager room)
    {
        currentRoom = room;
    }

    public void ExitRoom(RoomManager room)
    {
        if (currentRoom == room)
            currentRoom = null;
    }

    public float GetCurrentSpawnChance()
    {
        return (currentRoom != null && currentRoom.isDangerous)
            ? Mathf.Clamp(
                baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond),
                0f,
                90f
            )
            : 0f;
    }

    private bool CheckIfVisible()
    {
        if (playerCamera == null)
            return false;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        Collider col = GetComponent<Collider>();

        if (col != null)
        {
            if (!GeometryUtility.TestPlanesAABB(planes, col.bounds))
                return false;
        }
        else
        {
            Vector3 viewPos = playerCamera.WorldToViewportPoint(transform.position);
            if (viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1 || viewPos.z <= 0)
                return false;
        }

        Vector3 targetPoint =
            (eyesPosition != null) ? eyesPosition.position : transform.position + Vector3.up * 1.5f;
        Vector3 directionToTarget = targetPoint - playerCamera.transform.position;
        float distance = directionToTarget.magnitude;

        if (
            Physics.Raycast(
                playerCamera.transform.position,
                directionToTarget,
                distance,
                obstacleMask
            )
        )
        {
            return false;
        }

        return true;
    }
}
