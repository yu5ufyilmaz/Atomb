using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GuderianAI : MonoBehaviour
{
    public static GuderianAI Instance;

    public enum GuderianState { Hidden, Approaching, Breaching, Entering, Searching, Exiting, Jumpscare, RhythmGame }
    public GuderianState currentState = GuderianState.Hidden;

    [Header("Spawn Ayarları (OTOMATİK GELME)")]
    public float checkInterval = 5.0f; 
    [Tooltip("Başlangıç şansı")]
    [Range(0, 100)] public float baseSpawnChance = 15.0f; 
    [Tooltip("Her başarısız denemede şans ne kadar artsın?")]
    public float chanceIncreaseStep = 5.0f; 
    
    public float minTimeBetweenAttacks = 30.0f; 
    
    private float spawnCheckTimer = 0f;
    private float cooldownTimer = 0f;
    
    // YENİ: Dinamik Şans Değişkeni
    [SerializeField] private float currentSpawnChance; 

    private RoomManager playerCurrentRoom; 

    [Header("Davranış Ayarları")]
    public float baseSearchDuration = 20f; 
    public float timePerLight = 10f;       
    public float footstepInterval = 0.8f;  
    public float closedDoorBreachTime = 2.0f; 
    public float lockedDoorBreachTime = 5.0f; 
    public float walkSpeed = 2.5f; 
    public float doorAnimationDelay = 1.0f; 

    [Header("Görsellik & Ses")]
    [SerializeField] private GameObject guderianModel;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] footstepSounds; 
    [SerializeField] private AudioClip doorHandleSound; 
    [SerializeField] private AudioClip doorOpenSound;   
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField] private AudioClip searchHumSound; 

    // --- DEBUG ---
    [HideInInspector] public string debugStatus;
    [HideInInspector] public float debugSearchProgress; 
    [HideInInspector] public float debugBreachProgress; 
    [HideInInspector] public float debugApproachProgress;
    [HideInInspector] public float debugCooldown; 

    private RoomManager activeRoom; 
    private float currentSearchTimer;
    private float calculatedSearchDuration;

    private void Awake() 
    { 
        if (Instance == null) Instance = this; 
        if (guderianModel) guderianModel.SetActive(false);
        
        // Başlangıçta şansı baza eşitle
        currentSpawnChance = baseSpawnChance;
    }

    private void Update()
    {
        // 1. COOLDOWN
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            debugCooldown = cooldownTimer;
        }

        // 2. SPAWN KONTROLÜ
        if (currentState == GuderianState.Hidden && cooldownTimer <= 0)
        {
            if (playerCurrentRoom != null && playerCurrentRoom.canGuderianSpawn)
            {
                spawnCheckTimer += Time.deltaTime;
                if (spawnCheckTimer >= checkInterval)
                {
                    spawnCheckTimer = 0f;
                    AttemptSpawn();
                }
            }
        }

        // 3. ARAMA MODU
        if (currentState == GuderianState.Searching)
        {
            currentSearchTimer -= Time.deltaTime;
            debugSearchProgress = currentSearchTimer / calculatedSearchDuration; 

            if (!CheckIfPlayerHidden())
            {
                StopAllCoroutines(); 
                TriggerJumpscare();
                return;
            }

            if (currentSearchTimer <= 0)
            {
                StartCoroutine(ExitSequence());
            }
        }
    }

    public void SetCurrentRoom(RoomManager room) 
    { 
        playerCurrentRoom = room; 
        spawnCheckTimer = -5f; 
    }
    
    public void ClearRoom(RoomManager room) 
    { 
        if (playerCurrentRoom == room) playerCurrentRoom = null; 
    }

    // --- YENİ: ARTAN ŞANS MANTIĞI ---
    private void AttemptSpawn()
    {
        if (!GlobalEnemyManager.Instance.CanAttack()) return;

        float roll = Random.Range(0f, 100f);
        Debug.Log($"Guderian Zar: {roll} < {currentSpawnChance} (Mevcut Şans)");

        if (roll < currentSpawnChance)
        {
            // BAŞARILI: Saldır ve şansı sıfırla
            TrySpawnGuderian(playerCurrentRoom);
            currentSpawnChance = baseSpawnChance; 
        }
        else
        {
            // BAŞARISIZ: Şansı arttır (Bir dahakine daha tehlikeli olacak)
            currentSpawnChance += chanceIncreaseStep;
            // %100'ü geçmesin
            currentSpawnChance = Mathf.Min(currentSpawnChance, 100f);
        }
    }

    public void TrySpawnGuderian(RoomManager room)
    {
        if (currentState != GuderianState.Hidden) return;
        if (!GlobalEnemyManager.Instance.CanAttack()) return;
        if (!room.canGuderianSpawn || room.roomDoor == null) return;
        
        if (room.doorOutsidePoint == null || room.doorInsidePoint == null) 
        { 
            Debug.LogError($"HATA: {room.name} noktaları eksik!"); 
            return; 
        }

        activeRoom = room;
        GlobalEnemyManager.Instance.RegisterAttackStart();
        StartCoroutine(AttackSequence());
    }

    // EDİTÖR İÇİN GETTER
    public float GetCurrentChance() => currentSpawnChance;

    private IEnumerator AttackSequence()
    {
        currentState = GuderianState.Approaching;
        debugStatus = "Adım Sesleri Geliyor...";
        debugApproachProgress = 0f;

        transform.position = activeRoom.doorOutsidePoint.position;
        transform.LookAt(activeRoom.doorInsidePoint); 
        guderianModel.SetActive(false); 

        for (int i = 0; i < 5; i++)
        {
            if (footstepSounds.Length > 0) 
                PlaySoundAtDoor(footstepSounds[Random.Range(0, footstepSounds.Length)]);
            
            debugApproachProgress = (float)(i + 1) / 5;
            yield return new WaitForSeconds(footstepInterval);
        }

        currentState = GuderianState.Breaching;
        InteractableDoor door = activeRoom.roomDoor;
        bool isDoorLocked = door.IsLocked();
        float breachTime = isDoorLocked ? lockedDoorBreachTime : closedDoorBreachTime;

        debugStatus = isDoorLocked ? "Kapı KİLİTLİ (Kırıyor)..." : "Kapı KAPALI (Açıyor)...";
        PlaySoundAtDoor(doorHandleSound);

        float breachTimer = 0f;
        while (breachTimer < breachTime)
        {
            breachTimer += Time.deltaTime;
            debugBreachProgress = breachTimer / breachTime; 
            yield return null;
        }

        currentState = GuderianState.Entering;
        debugStatus = "İçeri Giriyor...";
        
        if (isDoorLocked) door.SetLocked(false); 
        if (!door.isOpen) door.Interact(); 
        PlaySoundAtDoor(doorOpenSound);

        yield return new WaitForSeconds(0.2f); 
        
        guderianModel.SetActive(true);
        yield return StartCoroutine(MoveToTarget(activeRoom.doorInsidePoint.position));

        if (CheckIfPlayerHidden()) StartCoroutine(StartSearching());
        else TriggerJumpscare();
    }

    private IEnumerator StartSearching()
    {
        currentState = GuderianState.Searching;
        
        int activeLights = activeRoom.GetActiveLightCount();
        calculatedSearchDuration = baseSearchDuration + (activeLights * timePerLight);
        currentSearchTimer = calculatedSearchDuration;

        debugStatus = $"Arıyor... ({activeLights} Işık)";
        
        if (audioSource) { audioSource.clip = searchHumSound; audioSource.loop = true; audioSource.Play(); }

        while (currentState == GuderianState.Searching)
        {
            if (activeRoom.guderianPatrolPoints.Count > 0)
            {
                Transform targetPoint = activeRoom.guderianPatrolPoints[Random.Range(0, activeRoom.guderianPatrolPoints.Count)];
                yield return StartCoroutine(MoveToTarget(targetPoint.position));
            }
            yield return new WaitForSeconds(1f); 
        }
    }

    private IEnumerator ExitSequence()
    {
        currentState = GuderianState.Exiting;
        debugStatus = "Odadan Çıkıyor...";

        yield return StartCoroutine(MoveToTarget(activeRoom.doorInsidePoint.position));
        yield return StartCoroutine(MoveToTarget(activeRoom.doorOutsidePoint.position));

        guderianModel.SetActive(false);
        if (audioSource) audioSource.Stop();
        
        if (activeRoom.roomDoor != null && activeRoom.roomDoor.isOpen) activeRoom.roomDoor.Interact(); 

        currentState = GuderianState.Hidden;
        debugStatus = "Gitti.";
        GlobalEnemyManager.Instance.RegisterAttackEnd();

        cooldownTimer = minTimeBetweenAttacks;
    }

    private IEnumerator MoveToTarget(Vector3 target)
    {
        float timeout = 10f; 
        float currentTimer = 0f;
        
        while (Vector3.Distance(new Vector3(transform.position.x, transform.position.y, transform.position.z), target) > 0.1f)
        {
            currentTimer += Time.deltaTime;
            if (currentTimer > timeout) break; 

            Vector3 direction = (target - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
            }
            transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
            yield return null;
        }
    }
    
    private bool CheckIfPlayerHidden()
    {
        foreach (var spot in activeRoom.hidingSpots) if (spot.IsOccupied) return true;
        return false;
    }
    
    public void TriggerJumpscare()
    {
        currentState = GuderianState.Jumpscare;
        debugStatus = "JUMPSCARE!";
        
        guderianModel.SetActive(true);
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        transform.position = player.position + player.forward * 1.5f;
        transform.LookAt(player);

        if (audioSource) audioSource.PlayOneShot(jumpscareSound);
        
        StopAllCoroutines(); 
        Debug.LogError("GUDERIAN YAKALADI!");
        StartCoroutine(ExitSequence()); 
    }

    public void ForceLeave()
    {
        if (currentState == GuderianState.Hidden) return;
        Debug.Log("GM: Guderian zorla gönderiliyor.");
        StopAllCoroutines(); 
        StartCoroutine(ExitSequence()); 
    }

    public float GetTimeUntilNextSpawnCheck() => Mathf.Max(0, checkInterval - spawnCheckTimer);
    public bool IsOnCooldown() => cooldownTimer > 0;

    private void PlaySoundAtDoor(AudioClip clip)
    {
        if (clip == null) return;
        if (activeRoom != null && activeRoom.roomDoor != null)
        {
            AudioSource doorSource = activeRoom.roomDoor.DoorAudioSource;
            if (doorSource != null) { doorSource.PlayOneShot(clip); return; }
        }
        if (audioSource) audioSource.PlayOneShot(clip);
    }
}