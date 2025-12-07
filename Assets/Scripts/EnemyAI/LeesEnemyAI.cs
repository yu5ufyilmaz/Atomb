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
    private Vector3 lastPlayerPos;

    [Header("Referanslar")]
    public Transform playerTransform;
    public Camera playerCamera;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        if (playerCamera == null)
            playerCamera = Camera.main;

        DespawnLees();
        InvokeRepeating(nameof(CheckSpawnLogic), 5f, spawnCheckInterval);
    }

    private void Update()
    {
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
            TriggerDeath("Scenario B: Odadan dışarı kaçıldı! (Flight)");
            return;
        }

        bool isVisible = CheckIfVisible();

        // HENÜZ FARK EDİLMEDİ (Scenario A)
        if (!hasBeenSpotted)
        {
            if (isVisible)
            {
                hasBeenSpotted = true;
                currentReactionTimer = 0f;
                Debug.Log("Lees: FARK EDİLDİ!");
            }
            else
            {
                currentIgnoranceTimer += Time.deltaTime;
                if (currentIgnoranceTimer >= maxIgnoranceTime)
                    TriggerDeath("Scenario A: Süre doldu (Ignorance)");
            }
        }
        // FARK EDİLDİ (Scenario C & D)
        else
        {
            if (isVisible)
            {
                // HALA BAKIYOR (Scenario C)
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
                if (playerSpeed > movementTolerance)
                {
                    TriggerDeath("Scenario D Hatası: Arkasını döndün ama hareket ettin!");
                }
                else
                {
                    currentSurvivalTimer += Time.deltaTime;
                    // Bakmadığı sürece tepki süresi azalabilir (opsiyonel)
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

    // --- TETİKLEYİCİ ---
    public void TriggerDeath(string reason)
    {
        Debug.LogError($"ÖLÜM: {reason}");

        // Eğer zaten Jumpscare modundaysak tekrar çalışma
        if (currentState == LeesState.Jumpscare)
            return;

        StartCoroutine(ExecuteSmartJumpscare());
    }

    // --- AKILLI JUMPSCARE (SAĞA/SOLA GEÇME) ---
    private IEnumerator ExecuteSmartJumpscare()
    {
        currentState = LeesState.Jumpscare;

        // 1. Müsait Tarafı Bul (Sağ mı Sol mu?)
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
            // İki taraf da doluysa (dar koridor), mecburen sağa koyalım (duvar içinden çıksın)
            spawnOnRight = true;
            targetPos =
                playerTransform.position + (playerTransform.right * (jumpscareDistance * 0.5f));
        }

        // Yükseklik ayarı
        targetPos.y = playerTransform.position.y + jumpscareYOffset;

        // 2. Işınla ve Döndür
        transform.position = targetPos;
        transform.LookAt(
            new Vector3(
                playerTransform.position.x,
                transform.position.y,
                playerTransform.position.z
            )
        );

        ShowModel(true);

        // 3. Jumpscare Manager'ı Tetikle
        if (JumpscareManager.Instance != null)
        {
            JumpscareManager.Instance.StartDirectionalJumpscare(transform, spawnOnRight);
        }
        else
        {
            Debug.LogError("JumpscareManager sahnede bulunamadı!");
        }

        yield return null;
    }

    // --- SPAWN SİSTEMİ ---
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

        // Şans Hesapla
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

        // Eğer hiçbir nokta uygun değilse çık (Görünürde doğmasın)
        if (bestPoint == null)
        {
            Debug.LogWarning("Lees: Uygun spawn noktası bulunamadı. Pas geçiliyor.");
            return;
        }

        spawnRoom = currentRoom;

        transform.position = bestPoint.position;
        // Yüzünü oyuncuya dön (Y ekseninde)
        Vector3 targetPostition = new Vector3(
            playerTransform.position.x,
            transform.position.y,
            playerTransform.position.z
        );
        transform.LookAt(targetPostition);

        GlobalEnemyManager.Instance.RegisterAttackStart();
        currentState = LeesState.Active;

        // Saldırı başladığında şansı sıfırla
        timeSpentInCurrentRoom = 0;

        // Sayaçları Sıfırla
        currentIgnoranceTimer = 0;
        currentReactionTimer = 0;
        currentSurvivalTimer = 0;
        hasBeenSpotted = false;
        lastPlayerPos = playerTransform.position;

        ShowModel(true);
    }

    private Transform GetSafeSpawnPoint()
    {
        List<Transform> validPoints = new List<Transform>();

        foreach (Transform point in currentRoom.spawnPoints)
        {
            if (point == null)
                continue;

            Vector3 directionToPoint = (point.position - playerTransform.position).normalized;
            float dotProduct = Vector3.Dot(playerTransform.forward, directionToPoint);

            // Oyuncunun arkasında mı? (< -0.2)
            if (dotProduct < -0.2f)
            {
                // Ekranda değil mi?
                if (!IsPointOnScreen(point.position))
                {
                    validPoints.Add(point);
                }
            }
        }

        // Eğer uygun nokta varsa birini seç
        if (validPoints.Count > 0)
        {
            return validPoints[Random.Range(0, validPoints.Count)];
        }

        // YEDEK PLAN: Eğer hiç uygun nokta yoksa ve oyunun durmasını istemiyorsak
        // Rastgele bir noktada doğsun (Bunu istersen kapatabilirsin)
        if (currentRoom.spawnPoints.Count > 0)
        {
            Debug.LogWarning("Lees: Güvenli nokta yok, rastgele doğuyor.");
            return currentRoom.spawnPoints[Random.Range(0, currentRoom.spawnPoints.Count)];
        }

        return null;
    }

    // --- YARDIMCI FONKSİYONLAR ---
    private bool CheckIfVisible()
    {
        // 1. Ekran Kontrolü
        Vector3 viewPos = playerCamera.WorldToViewportPoint(transform.position);
        bool onScreen = (
            viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1 && viewPos.z > 0
        );

        if (!onScreen)
            return false;

        // 2. Raycast Kontrolü (Duvar vs.)
        Vector3 origin =
            eyesPosition != null ? eyesPosition.position : transform.position + Vector3.up * 1.6f;
        Vector3 direction = playerCamera.transform.position - origin;
        float distance = direction.magnitude;

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, distance + 0.5f, obstacleMask))
        {
            // Eğer oyuncuya çarptıysa GÖRÜYOR demektir
            if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
            {
                return true;
            }
            // Başka bir şeye (duvar) çarptıysa GÖREMİYOR
            return false;
        }

        // Hiçbir şeye çarpmadıysa (boşluktaysa) görüyor kabul et
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

        // Cooldown'ı başlat
        currentCooldownTimer = spawnCooldownAfterDespawn;
    }

    private void ShowModel(bool show)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = show;
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = show;
    }

    // Editör İçin
    public float GetCurrentSpawnChance()
    {
        if (currentRoom == null || !currentRoom.isDangerous)
            return 0f;
        float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
        return Mathf.Clamp(chance, 0f, 90f);
    }
}
