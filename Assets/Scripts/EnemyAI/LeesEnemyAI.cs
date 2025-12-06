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
    [SerializeField] private LeesRoom currentRoom; // Oyuncunun ŞU AN olduğu oda
    private LeesRoom spawnRoom; // Lees'in DOĞDUĞU oda (Referans)
    
    [SerializeField] private float timeSpentInCurrentRoom; 
    public float baseSpawnChance = 10f;
    public float chanceIncreasePerSecond = 2f;
    public float spawnCheckInterval = 5f;

    [Header("Senaryo A (Fark Edilmeme)")]
    public float maxIgnoranceTime = 30f;

    [Header("Senaryo C & D (Tepki ve Kurtuluş)")]
    public float maxReactionTime = 3.0f; // 3 Saniye kuralı
    public float survivalWaitTime = 15f; 
    public float movementTolerance = 0.1f; 

    // DEBUG DATA (Editör İçin)
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
        // 1. GİZLİ DURUM
        if (currentState == LeesState.Hidden && currentRoom != null && currentRoom.isDangerous)
        {
            timeSpentInCurrentRoom += Time.deltaTime;
        }

        // 2. AKTİF DURUM
        if (currentState == LeesState.Active)
        {
            HandleActiveLogic();
        }

        // DEBUG VERİ GÜNCELLEME
        debugReactionTimer = currentReactionTimer;
        debugSurvivalTimer = currentSurvivalTimer;
        debugIgnoranceTimer = currentIgnoranceTimer;
        debugIsVisible = CheckIfVisible();
        debugHasBeenSpotted = hasBeenSpotted;
        debugPlayerSpeed = Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
    }

    private void HandleActiveLogic()
    {
        // Hız hesabı
        float playerSpeed = Vector3.Distance(playerTransform.position, lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = playerTransform.position;

        // --- SENARYO B: FLIGHT (ODADAN KAÇIŞ) ---
        // YENİ MANTIK: Oyuncu şu anki odası, Lees'in doğduğu oda değilse (veya koridora çıktıysa) ÖLÜR.
        if (currentRoom != spawnRoom)
        {
            TriggerDeath("Scenario B: Odadan dışarı kaçıldı! (Flight)");
            return;
        }

        // Görüş Kontrolü
        bool isVisible = CheckIfVisible();

        // --- SENARYO A: IGNORANCE ---
        if (!hasBeenSpotted)
        {
            if (isVisible)
            {
                hasBeenSpotted = true;
                currentReactionTimer = 0f;
                Debug.Log("<color=red>LEES: GÖZ GÖZE GELDİK!</color>");
            }
            else
            {
                currentIgnoranceTimer += Time.deltaTime;
                if (currentIgnoranceTimer >= maxIgnoranceTime)
                    TriggerDeath("Scenario A: Süre doldu (Ignorance)");
            }
        }
        // --- SENARYO C & D ---
        else 
        {
            if (isVisible) 
            {
                // HALA BAKIYOR (Scenario C)
                currentReactionTimer += Time.deltaTime;
                currentSurvivalTimer = 0f; 

                if (currentReactionTimer >= maxReactionTime)
                {
                    TriggerDeath($"Scenario C: Çok uzun süre baktın! ({maxReactionTime}s)");
                }
            }
            else 
            {
                // ARKASINI DÖNDÜ (Scenario D)
                
                // Hareket Kontrolü
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
                        Debug.Log("<color=green>BAŞARI: Lees gidiyor.</color>");
                        DespawnLees();
                    }
                }
            }
        }
    }

    public void SpawnLeesInRoom()
    {
        if (currentRoom == null || currentRoom.spawnPoints.Count == 0) return;
        
        // DOĞDUĞU ODAYI KAYDET (Kaçış kontrolü için)
        spawnRoom = currentRoom; 

        Transform point = currentRoom.spawnPoints[Random.Range(0, currentRoom.spawnPoints.Count)];
        
        transform.position = point.position;
        transform.LookAt(new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z));

        GlobalEnemyManager.Instance.RegisterAttackStart();
        currentState = LeesState.Active;
        
        // Değişkenleri Sıfırla
        timeSpentInCurrentRoom = 0; 
        currentIgnoranceTimer = 0; 
        currentReactionTimer = 0; 
        currentSurvivalTimer = 0; 
        hasBeenSpotted = false;
        lastPlayerPos = playerTransform.position;

        ShowModel(true);
    }

    // --- STANDART FONKSİYONLAR ---
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

    public void EnterRoom(LeesRoom room) { currentRoom = room; timeSpentInCurrentRoom = 0f; }
    public void ExitRoom(LeesRoom room) { if (currentRoom == room) { currentRoom = null; timeSpentInCurrentRoom = 0f; } }

    private void CheckSpawnLogic()
    {
         if (currentState == LeesState.Active || currentRoom == null || !currentRoom.isDangerous) return;
         if (!GlobalEnemyManager.Instance.CanAttack()) return;

         float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
         chance = Mathf.Clamp(chance, 0f, 90f);

         if (Random.Range(0f, 100f) < chance) SpawnLeesInRoom();
    }

    public void DespawnLees()
    {
        if (GlobalEnemyManager.Instance != null && currentState == LeesState.Active)
            GlobalEnemyManager.Instance.RegisterAttackEnd();
        currentState = LeesState.Hidden;
        ShowModel(false);
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
}