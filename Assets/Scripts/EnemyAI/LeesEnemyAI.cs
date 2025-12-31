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
    public LayerMask obstacleMask; // Eski maske (Spawn için kullanılabilir)
    public Transform eyesPosition;

    [Tooltip("0.0 = Tam ekran, 0.2 = Ekranın kenarlarından %20'lik kısım güvenli")]
    [Range(0f, 0.4f)]
    public float screenEdgeBuffer = 0.1f; // Dead Zone ayarı

    // PERFORMANS İÇİN CACHED LAYERMASK
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

    [Tooltip(
        "Oyuncu hareket etmemesi gerekirken hareket ederse, hızı bu değeri geçerse tetiklenir."
    )]
    public float movementTolerance = 0.5f; // Bunu 0.1'den 0.5'e çıkaralım, biraz esnek olsun.

    [Tooltip(
        "Arkasını döndüğünde yanlışlıkla hareket ederse, ölmeden önce tanınan süre (Refleks payı)."
    )]
    public float movementGraceTime = 0.5f; // YENİ: Yarım saniye hata payı

    // Bu değişkeni de private alanlara ekle (HandleActiveLogic içinde kullanacağız)
    private float currentMovementGraceTimer = 0f;

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

    // Input kontrolü
    private StarterAssetsInputs playerInputs;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
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

        // --- OPTİMİZASYON ---
        // LayerMask'i burada bir kere hesaplıyoruz. Update'de tekrar tekrar hesaplamayacak.
        // Player, UI, IgnoreRaycast ve TransparentFX HARİÇ her şeyi görecek.
        visionLayerMask = ~LayerMask.GetMask("Player", "UI", "IgnoreRaycast", "TransparentFX");

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

        // --- SADECE EDITORDE ÇALIŞACAK KISIM (Kasma Engelleme) ---
#if UNITY_EDITOR
        debugReactionTimer = currentReactionTimer;
        debugSurvivalTimer = currentSurvivalTimer;
        debugIgnoranceTimer = currentIgnoranceTimer;
        // Sadece debug açıksa Raycast at, yoksa atma.
        if (showDebugLogs)
            debugIsVisible = CheckIfVisible();
        debugHasBeenSpotted = hasBeenSpotted;
#endif
    }

    private void HandleActiveLogic()
    {
        // Oyuncunun anlık hızı
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
        bool logicVisible = CheckIfVisible(); // Optimize edilmiş görüş kontrolü

        if (!isPlayerControllable && !hasBeenSpotted)
            logicVisible = false;

        // --- HENÜZ FARK EDİLMEDİ (A) ---
        if (!hasBeenSpotted)
        {
            if (logicVisible)
            {
                hasBeenSpotted = true;
                hasTurnedAway = false;
                currentReactionTimer = 0f;
                // İlk görüşte hareket timer'ını sıfırla
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
        // --- FARK EDİLDİKTEN SONRA (C ve D) ---
        else
        {
            if (logicVisible) // Hala bakıyor (Scenario C)
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
            else // Arkasını döndü (Survival - Scenario D)
            {
                hasTurnedAway = true;

                // Oyuncu tuşlara basıyor mu?
                bool isInputting = (playerInputs != null && playerInputs.move != Vector2.zero);

                // KURAL: Tuşa basıyor VE hızı toleransı geçiyorsa tehlike başlar.
                if (isInputting && playerSpeed > movementTolerance)
                {
                    // Hemen öldürme! Sayacı başlat.
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
                    // Eğer durursa veya yavaşlarsa sayacı sıfırla (Affet)
                    currentMovementGraceTimer = 0f; // Veya yavaşça azalt: Mathf.Max(0, currentMovementGraceTimer - Time.deltaTime);

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

    // --- KRİTİK DÜZELTME: GÖRÜŞ KONTROLÜ ---
    private bool CheckIfVisible()
    {
        if (playerCamera == null)
            return false;

        // Hedef nokta
        Vector3 targetPoint =
            (eyesPosition != null) ? eyesPosition.position : transform.position + Vector3.up * 1.5f;

        // 1. EKRAN KONTROLÜ (Viewport) - Ucuz işlem
        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetPoint);
        if (viewportPoint.z <= 0)
            return false; // Arkamızda

        // Dead Zone (Kenar payı)
        if (
            viewportPoint.x < screenEdgeBuffer
            || viewportPoint.x > (1f - screenEdgeBuffer)
            || viewportPoint.y < screenEdgeBuffer
            || viewportPoint.y > (1f - screenEdgeBuffer)
        )
            return false;

        // 2. RAYCAST (OPTİMİZE EDİLMİŞ)

        // Origin'i kameranın 0.3 birim önüne aldık (Kendi kafamıza çarpmaması için)
        Vector3 origin = playerCamera.transform.position + playerCamera.transform.forward * 0.3f;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        RaycastHit hit;

        // 'visionLayerMask' kullanıyoruz (GetMask yok, performans dostu)
        // 'QueryTriggerInteraction.Collide' kullanıyoruz (Trigger olsa bile çarpar, içinden geçmez)
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
            // Çarptığımız şey Lees mi?
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                // SADECE EDITORDE ÇİZGİ ÇİZ (Build'de kasmaması için)
#if UNITY_EDITOR
                if (showDebugLogs)
                    Debug.DrawLine(origin, hit.point, Color.green);
#endif
                return true;
            }
            else
            {
                // Duvara veya başka bir şeye çarptı
#if UNITY_EDITOR
                if (showDebugLogs)
                    Debug.DrawLine(origin, hit.point, Color.red);
#endif
                return false;
            }
        }

        // Hiçbir şeye çarpmadıysa (Boşluğa gittiyse) -> Görmüyoruz
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

        float animDuration = 3.5f;
        if (spawnBehind)
            StartCoroutine(ExecuteBehindJumpscare(animDuration));
        else
            StartCoroutine(ExecuteSmartJumpscare(animDuration));
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

    private IEnumerator ExecuteBehindJumpscare(float duration)
    {
        currentState = LeesState.Jumpscare;
        ShowModel(true);
        if (JumpscareManager.Instance != null)
        {
            JumpscareManager.Instance.StartJumpscare(
                transform,
                true,
                duration,
                JumpscareStyle.ForcedBehind
            );
        }
        yield return null;
    }

    private IEnumerator ExecuteSmartJumpscare(float duration)
    {
        currentState = LeesState.Jumpscare;
        ShowModel(true);
        if (JumpscareManager.Instance != null)
        {
            JumpscareManager.Instance.StartJumpscare(
                transform,
                true,
                duration,
                JumpscareStyle.SmartDisplacement
            );
        }
        yield return null;
    }

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

        List<Transform> shuffledPoints = new List<Transform>(currentRoom.spawnPoints);
        // Fisher-Yates Shuffle
        for (int i = 0; i < shuffledPoints.Count; i++)
        {
            Transform temp = shuffledPoints[i];
            int randomIndex = Random.Range(i, shuffledPoints.Count);
            shuffledPoints[i] = shuffledPoints[randomIndex];
            shuffledPoints[randomIndex] = temp;
        }

        foreach (Transform point in shuffledPoints)
        {
            // Burada basit bir kontrol yapıyoruz spawn için
            if (!IsPositionVisibleToPlayer(point.position))
                return point;
        }
        return null;
    }

    private bool IsPositionVisibleToPlayer(Vector3 position)
    {
        if (playerCamera == null)
            return false;

        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(position);
        bool isInScreen = (
            viewportPoint.x > 0
            && viewportPoint.x < 1
            && viewportPoint.y > 0
            && viewportPoint.y < 1
            && viewportPoint.z > 0
        );

        if (!isInScreen)
            return false;

        Vector3 direction = position - playerCamera.transform.position;
        float distance = direction.magnitude;

        // Spawn kontrolü için obstacleMask kullanıyoruz (Basit duvar kontrolü)
        if (Physics.Raycast(playerCamera.transform.position, direction, distance, obstacleMask))
            return false;

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
}
