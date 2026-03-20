using System.Collections;
using System.Collections.Generic;
using StarterAssets;
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

    [Tooltip("0.0 = Tam ekran, 0.2 = Ekranın kenarlarından %20'lik kısım güvenli")]
    [Range(0f, 0.4f)]
    public float screenEdgeBuffer = 0.1f;

    private int visionLayerMask;

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
    public float movementTolerance = 0.5f;
    public float movementGraceTime = 0.5f;

    private float currentMovementGraceTimer = 0f;

    [Header("Ses Efektleri")]
    public AudioSource audioSource;
    public float audioFadeDuration = 2.0f;
    public AudioClip stareSound;
    public AudioClip jumpscareSound;

    [Header("Jumpscare Ayarları")]
    public float jumpscareDistance = 1.2f;
    public float jumpscareYOffset = -0.5f;

    // --- YENİ EKLENEN KISIM ---
    public JumpscareProfile leesJumpscareProfile;

    // ---------------------------

    [Header("Spawn Sıklığı")]
    public float spawnCooldownAfterDespawn = 20f;
    private float currentCooldownTimer = 0f;

    // --- DEBUG VERİLERİ ---
    [Header("DEBUG AYARLARI")]
    public bool showDebugLogs = true;

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

    private float currentIgnoranceTimer;
    private float currentReactionTimer;
    private float currentSurvivalTimer;

    private bool hasBeenSpotted = false;
    private bool hasTurnedAway = false;
    private Vector3 lastPlayerPos;

    private Coroutine audioFadeRoutine;

    [Header("Referanslar")]
    public Transform playerTransform;
    public Camera playerCamera;
    private UnityEngine.CharacterController targetCharacterController;
    private StarterAssetsInputs playerInputs;

    // RAM Optimizasyonu: Önbelleklenmiş renderer ve collider'lar
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;

    // RAM Optimizasyonu: Spawn noktası shuffle için yeniden kullanılabilir liste
    private List<Transform> shuffleBuffer = new List<Transform>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        // RAM Optimizasyonu: Renderer ve Collider'ları önbelleğe al
        cachedRenderers = GetComponentsInChildren<Renderer>();
        cachedColliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }

        if (playerTransform != null)
        {
            targetCharacterController =
                playerTransform.GetComponent<UnityEngine.CharacterController>();
            playerInputs = playerTransform.GetComponent<StarterAssetsInputs>();
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.volume = 0f;
        }

        visionLayerMask = ~LayerMask.GetMask("Player", "UI", "IgnoreRaycast", "TransparentFX");

        DespawnLees();
        InvokeRepeating(nameof(CheckSpawnLogic), 5f, spawnCheckInterval);
    }

    private void Update()
    {
        if (!GameManager.Instance.isGameStarted)
            return;
        if (GlobalEnemyManager.Instance != null && GlobalEnemyManager.Instance.stopAllEnemies)
            return;

        if (currentCooldownTimer > 0)
        {
            currentCooldownTimer -= Time.deltaTime;
#if UNITY_EDITOR
            debugCooldownTimer = currentCooldownTimer;
#endif
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

#if UNITY_EDITOR
        debugReactionTimer = currentReactionTimer;
        debugSurvivalTimer = currentSurvivalTimer;
        debugIgnoranceTimer = currentIgnoranceTimer;
        if (showDebugLogs)
            debugIsVisible = CheckIfVisible();
        debugHasBeenSpotted = hasBeenSpotted;
#endif
    }

    private void HandleActiveLogic()
    {
        float playerSpeed =
            Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = playerTransform.position;

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

        if (!hasBeenSpotted)
        {
            if (logicVisible)
            {
                hasBeenSpotted = true;
                hasTurnedAway = false;
                currentReactionTimer = 0f;
                currentMovementGraceTimer = 0f;

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
        else
        {
            if (logicVisible)
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
            else
            {
                hasTurnedAway = true;
                bool isInputting = (playerInputs != null && playerInputs.move != Vector2.zero);

                if (isInputting && playerSpeed > movementTolerance)
                {
                    currentMovementGraceTimer += Time.deltaTime;
                    if (currentMovementGraceTimer >= movementGraceTime)
                    {
                        TriggerDeath(
                            $"Scenario D: Arkasını döndün ve {movementGraceTime} saniye boyunca hareket ettin!"
                        );
                    }
                }
                else
                {
                    currentMovementGraceTimer = 0f;
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

    private bool CheckIfVisible()
    {
        if (playerCamera == null)
            return false;

        Vector3 targetPoint =
            (eyesPosition != null) ? eyesPosition.position : transform.position + Vector3.up * 1.5f;
        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetPoint);
        if (viewportPoint.z <= 0)
            return false;

        if (
            viewportPoint.x < screenEdgeBuffer
            || viewportPoint.x > (1f - screenEdgeBuffer)
            || viewportPoint.y < screenEdgeBuffer
            || viewportPoint.y > (1f - screenEdgeBuffer)
        )
            return false;

        Vector3 origin = playerCamera.transform.position + playerCamera.transform.forward * 0.3f;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        RaycastHit hit;

        if (
            Physics.Raycast(
                origin,
                direction,
                out hit,
                distance,
                visionLayerMask,
                QueryTriggerInteraction.Collide
            )
        )
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
#if UNITY_EDITOR
                if (showDebugLogs)
                    Debug.DrawLine(origin, hit.point, Color.green);
#endif
                return true;
            }
            else
            {
#if UNITY_EDITOR
                if (showDebugLogs)
                    Debug.DrawLine(origin, hit.point, Color.red);
#endif
                return false;
            }
        }
        return false;
    }

    public void TriggerDeath(string reason, bool spawnBehind = false)
    {
        if (currentState == LeesState.Jumpscare)
            return;

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

        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);

        if (audioSource)
        {
            audioSource.Stop();
            audioSource.volume = 1.0f;
            if (jumpscareSound)
                audioSource.PlayOneShot(jumpscareSound);
        }

        if (leesAnimator != null)
            leesAnimator.SetTrigger(JumpscareTrigger);

        if (spawnBehind)
            StartCoroutine(ExecuteBehindJumpscare());
        else
            StartCoroutine(ExecuteSmartJumpscare());
    }

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
            audioSource.Stop();
    }

    // --- JUMPSCARE GÜNCELLEMELERİ ---
    private IEnumerator ExecuteBehindJumpscare()
    {
        currentState = LeesState.Jumpscare;
        ShowModel(true);
        if (JumpscareManager.Instance != null)
        {
            // Yeni Profil sistemini kullanıyor
            JumpscareManager.Instance.StartJumpscare(
                transform,
                leesJumpscareProfile,
                true,
                JumpscareStyle.ForcedBehind
            );
        }
        yield return null;
    }

    private IEnumerator ExecuteSmartJumpscare()
    {
        currentState = LeesState.Jumpscare;
        ShowModel(true);
        if (JumpscareManager.Instance != null)
        {
            // Yeni Profil sistemini kullanıyor
            JumpscareManager.Instance.StartJumpscare(
                transform,
                leesJumpscareProfile,
                true,
                JumpscareStyle.SmartDisplacement
            );
        }
        yield return null;
    }

    // -------------------------------

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
        {
            Debug.Log("Lees: Spawn iptal (Görüş açısında yer yok)");
            return;
        }

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
        Debug.Log($"Lees Spawn Oldu! Nokta: {bestPoint.name}");
    }

    private Transform GetSafeSpawnPoint()
    {
        if (currentRoom == null || currentRoom.spawnPoints.Count == 0)
            return null;

        // RAM Optimizasyonu: Yeni Liste yerine önbellek kullan
        shuffleBuffer.Clear();
        shuffleBuffer.AddRange(currentRoom.spawnPoints);

        // Fisher-Yates shuffle
        for (int i = 0; i < shuffleBuffer.Count; i++)
        {
            Transform temp = shuffleBuffer[i];
            int randomIndex = Random.Range(i, shuffleBuffer.Count);
            shuffleBuffer[i] = shuffleBuffer[randomIndex];
            shuffleBuffer[randomIndex] = temp;
        }

        foreach (Transform point in shuffleBuffer)
        {
            if (!IsPositionVisibleToPlayer(point.position))
                return point;
        }
        return null;
    }

    private bool IsPositionVisibleToPlayer(Vector3 position)
    {
        if (playerCamera == null)
            return false;

        // 1. EKRAN KONTROLÜ (Genişletilmiş)
        // Sadece ekranın içi değil, kenarlardan biraz dışarısını da "görüyor" sayıyoruz (-0.2 ile 1.2 arası).
        // Böylece kafanı milim çevirince adam dibinde bitmez.
        Vector3 vp = playerCamera.WorldToViewportPoint(position);
        bool inScreen = vp.z > 0 && vp.x > -0.2f && vp.x < 1.2f && vp.y > -0.2f && vp.y < 1.2f;

        if (!inScreen)
            return false; // Ekranda (veya yakınında) değilse -> GÖRÜNMÜYOR (Spawn Olabilir).

        // 2. ENGEL KONTROLÜ (Spawn Noktasından -> Kameraya)
        // Işını tersten atıyoruz ki karakterin kendisine çarpıp "duvar var" sanmasın.
        Vector3 dirToCam = playerCamera.transform.position - position;
        float dist = dirToCam.magnitude;

        // ÖNEMLİ: obstacleMask sadece DUVARLARI içermeli.
        if (Physics.Raycast(position, dirToCam.normalized, out RaycastHit hit, dist, obstacleMask))
        {
            // Işın yolda bir şeye çarptı.

            // Eğer çarptığı şey OYUNCU ise, arada engel yok demektir -> GÖRÜNÜYOR (Spawn OLMA).
            if (hit.transform.root == transform.root || hit.transform.CompareTag("Player"))
                return true;

            // Oyuncu değilse (Duvar, Dolap vs.) -> GÖRÜNMÜYOR (Spawn OLABİLİR).
            return false;
        }

        // Hiçbir şeye çarpmadan kameraya ulaştıysa -> Arası boş -> GÖRÜNÜYOR (Spawn OLMA).
        return true;
    }

    public void DespawnLees()
    {
        if (GlobalEnemyManager.Instance != null && currentState == LeesState.Active)
            GlobalEnemyManager.Instance.RegisterAttackEnd();

        StartFadeAudio(null, false);
        currentState = LeesState.Hidden;
        ShowModel(false);
        currentCooldownTimer = spawnCooldownAfterDespawn;
    }

    private void ShowModel(bool show)
    {
        // RAM Optimizasyonu: Önbelleklenmiş array'leri kullan
        if (cachedRenderers != null)
        {
            foreach (var r in cachedRenderers)
                if (r != null)
                    r.enabled = show;
        }
        if (cachedColliders != null)
        {
            foreach (var c in cachedColliders)
                if (c != null)
                    c.enabled = show;
        }
    }

    public void EnterRoom(RoomManager room) => currentRoom = room;

    public void ExitRoom(RoomManager room)
    {
        if (currentRoom == room)
            currentRoom = null;
    }

    public float GetCurrentSpawnChance() =>
        (currentRoom != null && currentRoom.isDangerous)
            ? Mathf.Clamp(
                baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond),
                0f,
                90f
            )
            : 0f;
}
