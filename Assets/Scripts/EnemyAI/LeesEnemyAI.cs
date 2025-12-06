using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LeesEnemyAI : MonoBehaviour
{
    public static LeesEnemyAI Instance;

    public enum LeesState { Hidden, Active }
    public LeesState currentState = LeesState.Hidden;

    [Header("Görüş Ayarları (ÖNEMLİ)")]
    public LayerMask obstacleMask; // Duvarlar (Player ve Enemy OLMASIN)
    public Transform eyesPosition; // Gözlerin olduğu nokta (Boşsa kafa hizası)

    [Header("Oda ve Şans Sistemi")]
    [SerializeField] private LeesRoom currentRoom;
    [SerializeField] private float timeSpentInCurrentRoom; // Odada geçen süre
    public float baseSpawnChance = 10f;
    public float chanceIncreasePerSecond = 2f;
    public float spawnCheckInterval = 5f;

    [Header("Senaryo Ayarları")]
    public float maxIgnoranceTime = 30f; // A: Fark etmeme süresi
    public float maxRunDistance = 15f;   // B: Kaçma mesafesi
    public float maxStareTime = 3.0f;    // C: Bakma limiti
    public float survivalWaitTime = 15f; // D: Arkada bekleme süresi

    // Sayaçlar
    private float currentIgnoranceTimer;
    private float currentStareTimer;
    private float currentSurvivalTimer;
    
    private bool hasBeenSpotted = false;

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
        // 1. GİZLİ DURUM (Süre biriktirme)
        if (currentState == LeesState.Hidden && currentRoom != null && currentRoom.isDangerous)
        {
            timeSpentInCurrentRoom += Time.deltaTime;
        }

        // 2. AKTİF DURUM (Saldırı/Bekleme)
        if (currentState == LeesState.Active)
        {
            HandleActiveLogic();
        }
    }

    // --- SENARYOLARIN YÖNETİLDİĞİ YER ---
    private void HandleActiveLogic()
    {
        // --- SENARYO B: FLIGHT (Kaçış Kontrolü) ---
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance > maxRunDistance)
        {
            TriggerDeath("Scenario B: Kaçmaya çalıştı (Mesafe Aşıldı)");
            return;
        }

        // Görüş Kontrolü
        bool isVisible = CheckIfVisible();

        // --- SENARYO A: IGNORANCE (Henüz Fark Edilmedi) ---
        if (!hasBeenSpotted)
        {
            if (isVisible)
            {
                hasBeenSpotted = true;
                Debug.Log("<color=red>LEES: GÖZ GÖZE GELDİK! (Scenario C/D Başlıyor)</color>");
            }
            else
            {
                currentIgnoranceTimer += Time.deltaTime;
                if (currentIgnoranceTimer >= maxIgnoranceTime)
                {
                    TriggerDeath("Scenario A: Arkanda olduğunu fark etmedin (Süre Doldu)");
                }
            }
        }
        // --- GÖRÜLDÜKTEN SONRAKİ DURUM ---
        else 
        {
            // --- SENARYO C: STARING (Bakışma - Ölüm) ---
            if (isVisible)
            {
                currentStareTimer += Time.deltaTime;
                currentSurvivalTimer = 0f; // Bakış atarsa kurtulma sayacı sıfırlanır!

                if (currentStareTimer >= maxStareTime)
                {
                    TriggerDeath("Scenario C: Çok uzun süre baktın (Staring)");
                }
            }
            // --- SENARYO D: SURVIVAL (Arkanı Dönme - Kurtuluş) ---
            else
            {
                currentSurvivalTimer += Time.deltaTime;
                currentStareTimer = Mathf.Max(0, currentStareTimer - Time.deltaTime);

                if (currentSurvivalTimer >= survivalWaitTime)
                {
                    Debug.Log("<color=green>BAŞARI: Doğru tepki verildi. Lees gidiyor.</color>");
                    DespawnLees();
                }
            }
        }
    }

    // --- GÖRÜŞ VE DUVAR KONTROLÜ ---
    private bool CheckIfVisible()
    {
        // 1. Kamera Açısı Kontrolü
        Vector3 viewPos = playerCamera.WorldToViewportPoint(transform.position);
        bool onScreen = (viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1 && viewPos.z > 0);

        if (!onScreen) return false;

        // 2. Raycast (Duvar Kontrolü)
        Vector3 origin = eyesPosition != null ? eyesPosition.position : transform.position + Vector3.up * 1.6f;
        Vector3 direction = playerCamera.transform.position - origin;
        float distanceToPlayer = direction.magnitude;

        RaycastHit hit;
        
        // ObstacleMask katmanına çarparsa 'Duvar var' demektir
        if (Physics.Raycast(origin, direction, out hit, distanceToPlayer + 0.5f, obstacleMask))
        {
            Debug.DrawRay(origin, direction.normalized * hit.distance, Color.red); // Duvara çarptı (Görmüyor)
            return false;
        }

        Debug.DrawRay(origin, direction, Color.green); // Önü açık (Görüyor)
        return true; 
    }

    // --- ODA SİSTEMİ BAĞLANTILARI ---
    public void EnterRoom(LeesRoom room)
    {
        currentRoom = room;
        timeSpentInCurrentRoom = 0f;
    }

    public void ExitRoom(LeesRoom room)
    {
        if (currentRoom == room)
        {
            currentRoom = null;
            timeSpentInCurrentRoom = 0f;
        }
    }

    // --- SPAWN MANTIĞI ---
    private void CheckSpawnLogic()
    {
         if (currentState == LeesState.Active || currentRoom == null || !currentRoom.isDangerous) return;
         if (!GlobalEnemyManager.Instance.CanAttack()) return;

         float chance = baseSpawnChance + (timeSpentInCurrentRoom * chanceIncreasePerSecond);
         chance = Mathf.Clamp(chance, 0f, 90f);

         if (Random.Range(0f, 100f) < chance) SpawnLeesInRoom();
    }

    private void SpawnLeesInRoom()
    {
        if (currentRoom.spawnPoints.Count == 0) return;
        Transform point = currentRoom.spawnPoints[Random.Range(0, currentRoom.spawnPoints.Count)];
        
        transform.position = point.position;
        // Oyuncuya bakacak şekilde ayarla
        transform.LookAt(new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z));

        GlobalEnemyManager.Instance.RegisterAttackStart();
        currentState = LeesState.Active;
        
        // Değişkenleri Sıfırla
        timeSpentInCurrentRoom = 0; currentIgnoranceTimer = 0; currentStareTimer = 0; currentSurvivalTimer = 0; hasBeenSpotted = false;
        
        ShowModel(true);
    }

    private void DespawnLees()
    {
        if (GlobalEnemyManager.Instance != null && currentState == LeesState.Active)
            GlobalEnemyManager.Instance.RegisterAttackEnd();
        currentState = LeesState.Hidden;
        ShowModel(false);
    }

    private void TriggerDeath(string reason)
    {
        Debug.LogError($"ÖLÜM: {reason}");
        DespawnLees(); // Test için resetliyoruz
    }

    private void ShowModel(bool show)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = show;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = show;
    }
}