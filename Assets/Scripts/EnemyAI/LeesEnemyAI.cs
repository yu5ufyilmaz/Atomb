using System.Collections;
using System.Collections.Generic;
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

    [Header("Görüş Ayarları")]
    public LayerMask obstacleMask;
    public Transform eyesPosition;

    [Header("Oda ve Şans Sistemi")]
    [SerializeField]
    private RoomManager currentRoom;
    private RoomManager spawnRoom;

    [Tooltip("Lees saldırana kadar biriken toplam tehlike süresi")]
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

    [Header("Jumpscare Ayarları")]
    [Tooltip("Lees yanına ışınlandığında ne kadar uzakta dursun?")]
    public float jumpscareDistance = 1.2f;

    [Tooltip("Yükseklik ayarı (Kapsülse 1.0, Modelse 0)")]
    public float jumpscareYOffset = 0f;

    [Header("Spawn Sıklığı")]
    public float spawnCooldownAfterDespawn = 20f;
    private float currentCooldownTimer = 0f;

    // --- DEBUG VERİLERİ (Editör İçin) ---
    [HideInInspector]
    public float debugCooldownTimer;

    [HideInInspector]
    public float debugReactionTimer;

    [HideInInspector]
    public float debugSurvivalTimer;

    [HideInInspector]
    public float debugIgnoranceTimer;

    [HideInInspector]
    public float debugPlayerSpeed;

    [HideInInspector]
    public bool debugIsVisible;

    [HideInInspector]
    public bool debugHasBeenSpotted;

    // Sayaçlar ve Durumlar
    private float currentIgnoranceTimer;
    private float currentReactionTimer;
    private float currentSurvivalTimer;

    private bool hasBeenSpotted = false;
    private bool hasTurnedAway = false;

    private Vector3 lastPlayerPos;

    [Header("Referanslar")]
    public Transform playerTransform;
    public Camera playerCamera;

    // YENİ: Oyuncunun kontrolcüsüne referans (Etkileşim kontrolü için)
    private UnityEngine.CharacterController targetCharacterController;

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
            targetCharacterController =
                playerTransform.GetComponent<UnityEngine.CharacterController>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        DespawnLees();
        InvokeRepeating(nameof(CheckSpawnLogic), 5f, spawnCheckInterval);
    }

    private void Update()
    {
        if (GlobalEnemyManager.Instance.stopAllEnemies)
            return;
        // 1. Cooldown Sayacı
        if (currentCooldownTimer > 0)
        {
            currentCooldownTimer -= Time.deltaTime;
            debugCooldownTimer = currentCooldownTimer;
            return;
        }

        // 2. GİZLİ DURUM: Şans Biriktirme
        if (currentState == LeesState.Hidden && currentRoom != null && currentRoom.isDangerous)
        {
            timeSpentInCurrentRoom += Time.deltaTime;
        }

        // 3. AKTİF DURUM: Senaryolar
        if (currentState == LeesState.Active)
        {
            HandleActiveLogic();
        }

        // Debug Verilerini Güncelle
        debugReactionTimer = currentReactionTimer;
        debugSurvivalTimer = currentSurvivalTimer;
        debugIgnoranceTimer = currentIgnoranceTimer;
        debugIsVisible = CheckIfVisible();
        debugHasBeenSpotted = hasBeenSpotted;
        debugPlayerSpeed =
            Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
    }

    // --- SENARYO MANTIĞI ---
    private void HandleActiveLogic()
    {
        float playerSpeed =
            Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = playerTransform.position;

        // SCENARIO B: FLIGHT (Odadan Kaçış)
        if (currentRoom != spawnRoom)
        {
            TriggerDeath("Scenario B: Odadan dışarı kaçıldı! (Flight)", true);
            return;
        }

        // --- KONTROL DURUMUNA GÖRE GÖRÜŞ ---
        bool isPlayerControllable = true;
        if (targetCharacterController != null)
            isPlayerControllable = targetCharacterController.enabled;

        // Gerçekte görüyor muyuz?
        bool actuallyVisible = CheckIfVisible();

        // Mantıkta kullanacağımız görüş:
        bool logicVisible = actuallyVisible;

        // KURAL: Eğer oyuncunun kontrolü yoksa (Animasyondaysa) VE henüz Lees ile göz göze gelmediyse:
        // Kameranın Lees'in üzerinden geçmesini "görmek" sayma. (Kör taklidi yap)
        // Böylece animasyon sırasında yanlışlıkla ölmezsin.
        if (!isPlayerControllable && !hasBeenSpotted)
        {
            logicVisible = false;
        }

        // HENÜZ FARK EDİLMEDİ (Scenario A)
        if (!hasBeenSpotted)
        {
            if (logicVisible)
            {
                hasBeenSpotted = true;
                hasTurnedAway = false;
                currentReactionTimer = 0f;
                Debug.Log("Lees: FARK EDİLDİ! (Oyuncunun kontrolü açık)");
            }
            else
            {
                // Animasyonda olsan bile burası çalışır. Süre işlemeye devam eder.
                currentIgnoranceTimer += Time.deltaTime;
                if (currentIgnoranceTimer >= maxIgnoranceTime)
                    TriggerDeath("Scenario A: Süre doldu (Ignorance) - Etkileşimde olsan bile!");
            }
        }
        // FARK EDİLDİ (Scenario C & D)
        else
        {
            if (logicVisible)
            {
                // --- KURAL: GERİ DÖNÜP BAKARSAN ÖLÜRSÜN ---
                if (hasTurnedAway)
                {
                    TriggerDeath("HATA: Arkasını döndükten sonra tekrar baktı!");
                    return;
                }

                // HALA BAKIYOR (Scenario C - İlk Bakışma)
                currentReactionTimer += Time.deltaTime;
                currentSurvivalTimer = 0f;

                if (currentReactionTimer >= maxReactionTime)
                {
                    TriggerDeath("Scenario C: Çok uzun süre baktın! (Staring)");
                }
            }
            else
            {
                // ARKASINI DÖNDÜ (Scenario D)
                hasTurnedAway = true;

                if (playerSpeed > movementTolerance)
                {
                    TriggerDeath("Scenario D Hatası: Arkasını döndün ama hareket ettin!");
                }
                else
                {
                    currentSurvivalTimer += Time.deltaTime;
                    currentReactionTimer = Mathf.Max(0, currentReactionTimer - Time.deltaTime);

                    if (currentSurvivalTimer >= survivalWaitTime)
                    {
                        Debug.Log("Lees: Başarılı kurtuluş. Gidiyorum.");
                        DespawnLees();
                    }
                }
            }
        }
    }

    // --- TETİKLEYİCİ VE ÖLÜM YÖNETİMİ ---
    public void TriggerDeath(string reason, bool spawnBehind = false)
    {
        if (currentState == LeesState.Jumpscare)
            return;

        // YENİ MANTIK: Eğer oyuncu bir cihazdaysa (kontrolü yoksa), önce çıkart sonra öldür.
        if (targetCharacterController != null && !targetCharacterController.enabled)
        {
            StartCoroutine(ForceExitAndKillRoutine(reason, spawnBehind));
            return;
        }

        // Oyuncu serbestse, direkt öldür
        ExecuteDeathNow(reason, spawnBehind);
    }

    // Bu Coroutine, oyuncunun animasyonunun bitmesini bekler
    private IEnumerator ForceExitAndKillRoutine(string reason, bool spawnBehind)
    {
        Debug.LogWarning(
            $"Lees: Oyuncu cihaz başında yakalandı ({reason}). Çıkış yapması bekleniyor..."
        );

        // 1. GameManager üzerinden aktif cihazı bul ve zorla çıkart
        // Bu işlem cihazın "ExitSequence"ını tetikler (Model görünür olur, animasyon oynar)
        if (GameManager.Instance != null && GameManager.Instance.activeInteraction != null)
        {
            GameManager.Instance.activeInteraction.ForceExit();
        }

        // 2. Emniyet süresi (Maksimum bekleme süresi - takılı kalırsa diye)
        float maxWait = 6.0f;
        float timer = 0f;

        // 3. Oyuncunun CharacterController'ı açılana kadar bekle
        // Cihaz scriptleri, çıkış animasyonu bitince CharacterController.enabled = true yapar.
        while (
            targetCharacterController != null
            && !targetCharacterController.enabled
            && timer < maxWait
        )
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 4. Küçük bir gecikme (Kamera yerine tam otursun, model render edilsin)
        yield return new WaitForSeconds(0.2f);

        // 5. Ve şimdi Jumpscare'i patlat
        ExecuteDeathNow(reason, spawnBehind);
    }

    private void ExecuteDeathNow(string reason, bool spawnBehind)
    {
        Debug.LogError($"ÖLÜM: {reason}");

        if (spawnBehind)
        {
            StartCoroutine(ExecuteBehindJumpscare());
        }
        else
        {
            StartCoroutine(ExecuteSmartJumpscare());
        }
    }

    private IEnumerator ExecuteBehindJumpscare()
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
        {
            JumpscareManager.Instance.StartJumpscare(transform, true);
        }

        yield return null;
    }

    private IEnumerator ExecuteSmartJumpscare()
    {
        currentState = LeesState.Jumpscare;

        bool rightIsClear = !Physics.Raycast(
            playerTransform.position + Vector3.up,
            playerTransform.right,
            1.5f,
            obstacleMask
        );
        bool leftIsClear = !Physics.Raycast(
            playerTransform.position + Vector3.up,
            -playerTransform.right,
            1.5f,
            obstacleMask
        );

        Vector3 targetPos;
        bool spawnOnRight;

        if (rightIsClear)
        {
            spawnOnRight = true;
            targetPos = playerTransform.position + (playerTransform.right * jumpscareDistance);
        }
        else if (leftIsClear)
        {
            spawnOnRight = false;
            targetPos = playerTransform.position - (playerTransform.right * jumpscareDistance);
        }
        else
        {
            spawnOnRight = true;
            targetPos =
                playerTransform.position + (playerTransform.right * (jumpscareDistance * 0.5f));
        }

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
        {
            JumpscareManager.Instance.StartJumpscare(transform, spawnOnRight);
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
        if (!GlobalEnemyManager.Instance.CanAttack())
            return;

        float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
        chance = Mathf.Clamp(chance, 0f, 90f);

        if (Random.Range(0f, 100f) < chance)
        {
            SpawnLeesInRoom();
        }
    }

    public void SpawnLeesInRoom()
    {
        if (currentRoom == null || currentRoom.spawnPoints.Count == 0)
            return;

        Transform bestPoint = GetSafeSpawnPoint();

        if (bestPoint == null)
        {
            Debug.LogWarning("Lees: Uygun spawn noktası bulunamadı. Pas geçiliyor.");
            return;
        }

        spawnRoom = currentRoom;

        transform.position = bestPoint.position;
        Vector3 targetPostition = new Vector3(
            playerTransform.position.x,
            transform.position.y,
            playerTransform.position.z
        );
        transform.LookAt(targetPostition);

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
    }

    private Transform GetSafeSpawnPoint()
    {
        if (currentRoom == null || currentRoom.spawnPoints.Count == 0)
            return null;

        List<Transform> validPoints = new List<Transform>();

        foreach (Transform point in currentRoom.spawnPoints)
        {
            if (point == null)
                continue;

            Vector3 directionToPoint = (point.position - playerTransform.position).normalized;
            float dotProduct = Vector3.Dot(playerTransform.forward, directionToPoint);

            if (dotProduct < -0.2f)
            {
                if (!IsPointOnScreen(point.position))
                {
                    validPoints.Add(point);
                }
            }
        }

        if (validPoints.Count > 0)
        {
            return validPoints[Random.Range(0, validPoints.Count)];
        }

        validPoints.Clear();
        foreach (Transform point in currentRoom.spawnPoints)
        {
            if (point != null && !IsPointOnScreen(point.position))
                validPoints.Add(point);
        }

        if (validPoints.Count > 0)
            return validPoints[Random.Range(0, validPoints.Count)];

        return currentRoom.spawnPoints[Random.Range(0, currentRoom.spawnPoints.Count)];
    }

    private bool CheckIfVisible()
    {
        Vector3 viewPos = playerCamera.WorldToViewportPoint(transform.position);
        bool onScreen = (
            viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1 && viewPos.z > 0
        );

        if (!onScreen)
            return false;

        Vector3 origin =
            eyesPosition != null ? eyesPosition.position : transform.position + Vector3.up * 1.6f;
        Vector3 direction = playerCamera.transform.position - origin;
        float distance = direction.magnitude;

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, distance + 0.5f, obstacleMask))
        {
            if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
            {
                return true;
            }
            return false;
        }

        return true;
    }

    private bool IsPointOnScreen(Vector3 targetPos)
    {
        Vector3 viewPos = playerCamera.WorldToViewportPoint(targetPos);
        return (viewPos.x > 0 && viewPos.x < 1 && viewPos.y > 0 && viewPos.y < 1 && viewPos.z > 0);
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

    public void DespawnLees()
    {
        if (GlobalEnemyManager.Instance != null && currentState == LeesState.Active)
            GlobalEnemyManager.Instance.RegisterAttackEnd();

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

    public float GetCurrentSpawnChance()
    {
        if (currentRoom == null || !currentRoom.isDangerous)
            return 0f;
        float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
        return Mathf.Clamp(chance, 0f, 90f);
    }
}
