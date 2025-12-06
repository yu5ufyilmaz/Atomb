using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LeesEnemyAI : MonoBehaviour
{
    public static LeesEnemyAI Instance;

    public enum LeesState { Hidden, Active }
    public LeesState currentState = LeesState.Hidden;

    [Header("Görüş Ayarları")]
    public LayerMask obstacleMask; 
    public Transform eyesPosition; 

    [Header("Oda ve Şans Sistemi")]
    [SerializeField] private RoomManager currentRoom; 
    private RoomManager spawnRoom; 
    
    // ARTIK BU DEĞİŞKEN ODA DEĞİŞİNCE SIFIRLANMAYACAK
    [Tooltip("Lees saldırana kadar biriken toplam tehlike süresi")]
    [SerializeField] private float timeSpentInCurrentRoom; 
    
    public float baseSpawnChance = 10f;
    public float chanceIncreasePerSecond = 2f;
    public float spawnCheckInterval = 5f;

    [Header("Senaryo A (Fark Edilmeme)")]
    public float maxIgnoranceTime = 30f;

    [Header("Senaryo C & D (Tepki ve Kurtuluş)")]
    public float maxReactionTime = 3.0f;
    public float survivalWaitTime = 15f; 
    public float movementTolerance = 0.1f; 

    [Header("Spawn Sıklığı Ayarları")]
    public float spawnCooldownAfterDespawn = 20f;
    private float currentCooldownTimer = 0f;

    // DEBUG DATA
    [HideInInspector] public float debugCooldownTimer;
    [HideInInspector] public float debugReactionTimer;
    [HideInInspector] public float debugSurvivalTimer;
    [HideInInspector] public float debugIgnoranceTimer;
    [HideInInspector] public float debugPlayerSpeed;
    [HideInInspector] public bool debugIsVisible;
    [HideInInspector] public bool debugHasBeenSpotted;

    // Sayaçlar
    private float currentIgnoranceTimer;
    private float currentReactionTimer; 
    private float currentSurvivalTimer; 
    
    private bool hasBeenSpotted = false;
    private Vector3 lastPlayerPos; 

    [Header("Referanslar")]
    public Transform playerTransform;
    public Camera playerCamera;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        if (playerTransform == null) playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        if (playerCamera == null) playerCamera = Camera.main;

        DespawnLees();
        InvokeRepeating(nameof(CheckSpawnLogic), 5f, spawnCheckInterval);
    }

    private void Update()
    {
        // Cooldown Sayacı
        if (currentCooldownTimer > 0)
        {
            currentCooldownTimer -= Time.deltaTime;
            debugCooldownTimer = currentCooldownTimer; 
            return; 
        }

        // 1. GİZLİ DURUM: Şans Biriktirme
        // Sadece tehlikeli bir odadaysak süre artar.
        // Odadan çıkınca artmaz ama SIFIRLANMAZ (Kaldığı yerden devam eder).
        if (currentState == LeesState.Hidden && currentRoom != null && currentRoom.isDangerous)
        {
            timeSpentInCurrentRoom += Time.deltaTime;
        }

        // 2. AKTİF DURUM
        if (currentState == LeesState.Active)
        {
            HandleActiveLogic();
        }

        // Debug Verileri
        debugReactionTimer = currentReactionTimer;
        debugSurvivalTimer = currentSurvivalTimer;
        debugIgnoranceTimer = currentIgnoranceTimer;
        debugIsVisible = CheckIfVisible();
        debugHasBeenSpotted = hasBeenSpotted;
        debugPlayerSpeed = Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
    }

    // --- KRİTİK DÜZELTME BURADA ---
    // Odaya girip çıkarken süreyi sıfırlayan kodları kaldırdık.
    
    public void EnterRoom(RoomManager room) 
    { 
        currentRoom = room; 
        // timeSpentInCurrentRoom = 0f; // SİLİNDİ: Artık sıfırlanmıyor.
    }

    public void ExitRoom(RoomManager room) 
    { 
        if (currentRoom == room) 
        { 
            currentRoom = null; 
            // timeSpentInCurrentRoom = 0f; // SİLİNDİ: Artık sıfırlanmıyor.
        } 
    }

    // -----------------------------

    private void CheckSpawnLogic()
    {
         if (currentCooldownTimer > 0 || currentState == LeesState.Active || currentRoom == null || !currentRoom.isDangerous) return;
         if (!GlobalEnemyManager.Instance.CanAttack()) return;

         // Şans artık odalar arası birikimli artıyor
         float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
         chance = Mathf.Clamp(chance, 0f, 90f);

         if (Random.Range(0f, 100f) < chance) SpawnLeesInRoom();
    }

    public void SpawnLeesInRoom()
    {
        if (currentRoom == null || currentRoom.spawnPoints.Count == 0) return;

        Transform bestPoint = GetSafeSpawnPoint();
        if (bestPoint == null) return; // Güvenli nokta yoksa spawn olma

        spawnRoom = currentRoom;

        transform.position = bestPoint.position;
        Vector3 targetPostition = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        transform.LookAt(targetPostition);

        GlobalEnemyManager.Instance.RegisterAttackStart();
        currentState = LeesState.Active;
        
        // Sadece Lees SALDIRDIĞINDA şans sıfırlanır
        timeSpentInCurrentRoom = 0; 
        
        currentIgnoranceTimer = 0; 
        currentReactionTimer = 0; 
        currentSurvivalTimer = 0; 
        hasBeenSpotted = false;
        lastPlayerPos = playerTransform.position;

        ShowModel(true);
    }

    private void HandleActiveLogic()
    {
        float playerSpeed = Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = playerTransform.position;

        // Kaçış Kontrolü (Odadan çıkarsa)
        if (currentRoom != spawnRoom)
        {
            TriggerDeath("Scenario B: Odadan dışarı kaçıldı! (Flight)");
            return;
        }

        bool isVisible = CheckIfVisible();

        if (!hasBeenSpotted)
        {
            if (isVisible)
            {
                hasBeenSpotted = true;
                currentReactionTimer = 0f;
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
            if (isVisible) 
            {
                currentReactionTimer += Time.deltaTime;
                currentSurvivalTimer = 0f; 

                if (currentReactionTimer >= maxReactionTime)
                {
                    TriggerDeath($"Scenario C: Çok uzun süre baktın! ({maxReactionTime}s)");
                }
            }
            else 
            {
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
                        DespawnLees();
                    }
                }
            }
        }
    }

    private Transform GetSafeSpawnPoint()
    {
        List<Transform> validPoints = new List<Transform>();

        foreach (Transform point in currentRoom.spawnPoints)
        {
            if(point == null) continue;
            Vector3 directionToPoint = (point.position - playerTransform.position).normalized;
            float dotProduct = Vector3.Dot(playerTransform.forward, directionToPoint);

            if (dotProduct < -0.2f)
            {
                if (!IsPointOnScreen(point.position)) validPoints.Add(point);
            }
        }

        if (validPoints.Count > 0) return validPoints[Random.Range(0, validPoints.Count)];
        return null; 
    }

    private bool IsPointOnScreen(Vector3 targetPos)
    {
        Vector3 viewPos = playerCamera.WorldToViewportPoint(targetPos);
        return (viewPos.x > 0 && viewPos.x < 1 && viewPos.y > 0 && viewPos.y < 1 && viewPos.z > 0);
    }

    private bool CheckIfVisible()
    {
        Vector3 viewPos = playerCamera.WorldToViewportPoint(transform.position);
        if (!(viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1 && viewPos.z > 0)) return false;

        Vector3 origin = eyesPosition != null ? eyesPosition.position : transform.position + Vector3.up * 1.6f;
        Vector3 direction = playerCamera.transform.position - origin;
        RaycastHit hit;
        
        if (Physics.Raycast(origin, direction, out hit, direction.magnitude + 0.5f, obstacleMask)) return false;
        return true; 
    }

    public void DespawnLees()
    {
        if (GlobalEnemyManager.Instance != null && currentState == LeesState.Active)
            GlobalEnemyManager.Instance.RegisterAttackEnd();

        currentState = LeesState.Hidden;
        ShowModel(false);

        currentCooldownTimer = spawnCooldownAfterDespawn;
    }

    public void TriggerDeath(string reason)
    {
        Debug.LogError($"ÖLÜM: {reason}");
        DespawnLees(); 
    }

    private void ShowModel(bool show)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = show;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = show;
    }

    // EDİTÖR İÇİN: Şansı hesaplayıp döndürür
    public float GetCurrentSpawnChance()
    {
        // Safe Zone'da olsak bile BİRİKMİŞ şansı gösterir
        float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
        return Mathf.Clamp(chance, 0f, 90f);
    }
}