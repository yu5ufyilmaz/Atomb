using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;

[RequireComponent(typeof(NavMeshAgent))]
public class GuderianAI : MonoBehaviour
{
    public static GuderianAI Instance;

    public enum GuderianState
    {
        Hidden,
        Approaching,
        Breaching,
        WaitingBehindDoor,
        Entering,
        Searching,
        Exiting,
        Jumpscare,
        Ambush,
    }

    [Header("Timeline Jumpscare Sistemleri")]
    public PlayableDirector timelineAtDoor; // Kapıyı kırdığında
    public PlayableDirector timelineBehindPlayer; // Pusuya düşüp arkadan yakaladığında
    public PlayableDirector timelineInFront; // Odanın ortasında dümdüz yakaladığında
    public PlayableDirector timelineLocker; // Dolaptan erken çıkınca
    public GuderianState currentState = GuderianState.Hidden;

    [Header("Hareket (NavMesh)")]
    public NavMeshAgent agent;

    [Header("Spawn Ayarları")]
    public float checkInterval = 5.0f;

    [Range(0, 100)]
    public float baseSpawnChance = 15.0f;
    public float chanceIncreaseStep = 5.0f;
    public float minTimeBetweenAttacks = 30.0f;

    private float spawnCheckTimer = 0f;
    private float cooldownTimer = 0f;

    [SerializeField]
    private float currentSpawnChance;
    private RoomManager playerCurrentRoom;

    [Header("Davranış Ayarları")]
    public float baseSearchDuration = 20f;
    public float timePerLight = 10f;
    public float footstepInterval = 0.8f;
    public float closedDoorBreachTime = 2.0f;
    public float lockedDoorBreachTime = 5.0f;
    public float walkSpeed = 2.5f;
    public float doorAnimationDelay = 1.0f;

    [Header("Jumpscare Ayarları")]
    public float jumpscareDistance = 1.0f;
    public float lookAtDoorThreshold = 60f;
    public float spawnYOffset = 0f;

    [Header("Görsellik & Ses & Animasyon")]
    public Animator animator; // Animasyon kontrolcüsü

    [SerializeField]
    private GameObject guderianModel;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip[] footstepSounds;

    [SerializeField]
    private AudioClip doorHandleSound;

    [SerializeField]
    private AudioClip doorOpenSound;

    [SerializeField]
    private AudioClip jumpscareSound;

    [SerializeField]
    private AudioClip searchHumSound;

    [SerializeField]
    private AudioClip[] rummageSounds;

    [HideInInspector]
    public string debugStatus;

    [HideInInspector]
    public float debugSearchProgress;

    [HideInInspector]
    public float debugBreachProgress;

    [HideInInspector]
    public float debugApproachProgress;

    [HideInInspector]
    public float debugCooldown;

    private RoomManager activeRoom;
    private float currentSearchTimer;
    private float calculatedSearchDuration;
    private Coroutine audioFadeRoutine;

    // Animasyon ID'leri (Performans için hashlenmiş)
    private int _animIDSpeed;
    private int _animIDAttack;

    // Performans: Player referansı önbelleği
    private Transform cachedPlayer;

    [Header("Jumpscare Ayarları")]
    // ... (Eski değişkenler kalabilir ama profile taşıdıklarımızı kullanacağız)
    public JumpscareProfile guderianJumpscareProfile; // <-- YENİ

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        // Agent'ı başlangıçta tamamen devre dışı bırakıyoruz ki "IsStopped" hatası vermesin
        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.enabled = false;
        }

        if (guderianModel)
            guderianModel.SetActive(false);
        currentSpawnChance = baseSpawnChance;

        // Animasyon parametre ID'lerini al
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDAttack = Animator.StringToHash("Attack");

        // Performans: Player referansını önbelleğe al
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            cachedPlayer = playerObj.transform;
    }

    private void Update()
    {
        if (GlobalEnemyManager.Instance != null && GlobalEnemyManager.Instance.stopAllEnemies)
        {
            return; // Eğer sistem durdurulmuşsa hiçbir sayacı ilerletme, burada kal!
        }
        // Global Durdurma Kontrolü
        if (GlobalEnemyManager.Instance.stopAllEnemies)
        {
            // Sadece Agent aktifse ve NavMesh üzerindeyse durdur
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                if (animator != null)
                    animator.SetFloat(_animIDSpeed, 0f); // Durunca animasyonu kes
            }
            return;
        }

        // Hız Senkronizasyonu ve Animasyon (Sadece aktifse)
        if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
        {
            agent.speed = walkSpeed;
            // NavMesh hızını animatöre gönder
            if (animator != null)
                animator.SetFloat(_animIDSpeed, agent.velocity.magnitude);
        }
        else
        {
            // Agent durduysa animasyonu da durdur (Idle)
            if (animator != null)
                animator.SetFloat(_animIDSpeed, 0f);
        }

        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            debugCooldown = cooldownTimer;
        }

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

        if (currentState == GuderianState.Searching)
        {
            currentSearchTimer -= Time.deltaTime;
            debugSearchProgress = currentSearchTimer / calculatedSearchDuration;

            if (!CheckIfPlayerHidden())
            {
                StopAllCoroutines();
                TriggerPositionedJumpscare(JumpscareType.InFrontOfPlayer);
                return;
            }

            if (currentSearchTimer <= 0)
                StartCoroutine(ExitSequence());
        }
    }

    public void SetCurrentRoom(RoomManager room)
    {
        playerCurrentRoom = room;
        spawnCheckTimer = -5f;
    }

    public void ClearRoom(RoomManager room)
    {
        if (playerCurrentRoom == room)
            playerCurrentRoom = null;
    }

    public void SetupAmbush(RoomManager room)
    {
        if (currentState != GuderianState.Hidden || !GlobalEnemyManager.Instance.CanAttack())
            return;

        Debug.Log($"GUDERIAN: {room.roomName} PUSU kuruyor!");
        GlobalEnemyManager.Instance.RegisterAttackStart();

        activeRoom = room;
        currentState = GuderianState.Ambush;
        debugStatus = "PUSUDA (Manuel Konum)";

        if (room.ambushSpawnPoint != null)
        {
            TeleportAgent(room.ambushSpawnPoint.position);
            transform.rotation = room.ambushSpawnPoint.rotation;
        }
        else if (room.doorInsidePoint != null)
        {
            TeleportAgent(room.doorInsidePoint.position);
            if (room.doorOutsidePoint != null)
            {
                Vector3 lookPos = new Vector3(
                    room.doorOutsidePoint.position.x,
                    transform.position.y,
                    room.doorOutsidePoint.position.z
                );
                transform.LookAt(lookPos);
            }
        }

        guderianModel.SetActive(false);
    }

    public void TriggerAmbushExecute()
    {
        if (currentState != GuderianState.Ambush)
            return;

        guderianModel.SetActive(true);
        TriggerPositionedJumpscare(JumpscareType.InFrontOfPlayer, "Walked into an ambush.");
    }

    private void AttemptSpawn()
    {
        if (!GlobalEnemyManager.Instance.CanAttack())
            return;
        float roll = Random.Range(0f, 100f);
        if (roll < currentSpawnChance)
        {
            TrySpawnGuderian(playerCurrentRoom);
            currentSpawnChance = baseSpawnChance;
        }
        else
        {
            currentSpawnChance += chanceIncreaseStep;
            currentSpawnChance = Mathf.Min(currentSpawnChance, 100f);
        }
    }

    public void TrySpawnGuderian(RoomManager room)
    {
        if (currentState != GuderianState.Hidden || !GlobalEnemyManager.Instance.CanAttack())
            return;
        if (!room.canGuderianSpawn || room.roomDoor == null)
            return;

        activeRoom = room;
        GlobalEnemyManager.Instance.RegisterAttackStart();
        StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        InteractableDoor door = activeRoom.roomDoor;

        // --- 1. ADIM ATMA (YAKLAŞMA) EVRESİ ---
        currentState = GuderianState.Approaching;
        debugStatus = "Adım Sesleri...";
        TeleportAgent(activeRoom.doorOutsidePoint.position);
        guderianModel.SetActive(false);

        // Kapı zaten açıksa biraz bekler
        if (door.isOpen)
        {
            debugStatus = "Kapı AÇIK! Yaklaşıyor...";
            debugApproachProgress = 1f;
            yield return new WaitForSeconds(footstepInterval * 5);

            if (!CheckIfPlayerHidden())
            {
                TriggerPositionedJumpscare(JumpscareType.AtDoor);
                yield break;
            }
        }
        else // Kapı kapalıysa adım seslerini çal
        {
            int totalSteps = 5;
            for (int i = 0; i < totalSteps; i++)
            {
                debugApproachProgress = (float)i / (float)totalSteps;

                if (footstepSounds.Length > 0)
                    PlaySoundAtDoor(footstepSounds[Random.Range(0, footstepSounds.Length)]);

                yield return new WaitForSeconds(footstepInterval);

                if (door.isOpen)
                {
                    if (CheckIfPlayerHidden())
                    {
                        break;
                    }
                    else
                    {
                        TriggerPositionedJumpscare(JumpscareType.AtDoor);
                        yield break;
                    }
                }
            }
            debugApproachProgress = 1f;
        }

        // --- 2. KAPI KIRMA / AÇMA EVRESİ ---
        if (!door.isOpen)
        {
            currentState = GuderianState.Breaching;
            bool isDoorLocked = door.IsLocked();
            float breachTime = isDoorLocked ? lockedDoorBreachTime : closedDoorBreachTime;
            debugStatus = isDoorLocked ? "Kırıyor..." : "Açıyor...";
            PlaySoundAtDoor(doorHandleSound);

            float breachTimer = 0f;
            while (breachTimer < breachTime)
            {
                breachTimer += Time.deltaTime;
                debugBreachProgress = breachTimer / breachTime;

                if (door.isOpen)
                {
                    if (!CheckIfPlayerHidden())
                    {
                        TriggerPositionedJumpscare(JumpscareType.AtDoor);
                        yield break;
                    }
                    else
                    {
                        break;
                    }
                }
                yield return null;
            }

            if (isDoorLocked)
                door.SetLocked(false);
        }

        // --- 3. PUSU KONTROLÜ (Kapı Arkası) ---
        if (!CheckIfPlayerHidden() && CheckIfPlayerLookingAtDoor())
        {
            currentState = GuderianState.WaitingBehindDoor;
            debugStatus = "PUSUDA (Kapı Arkası)...";
            while (currentState == GuderianState.WaitingBehindDoor)
            {
                if (door.isOpen)
                {
                    if (!CheckIfPlayerHidden())
                    {
                        TriggerPositionedJumpscare(JumpscareType.AtDoor);
                        yield break;
                    }
                    else
                    {
                        break;
                    }
                }

                if (!CheckIfPlayerLookingAtDoor() || CheckIfPlayerHidden())
                    break;

                yield return null;
            }
        }

        // --- 4. İÇERİ GİRİŞ ---
        currentState = GuderianState.Entering;
        debugStatus = "İçeri Giriyor...";
        if (!door.isOpen)
        {
            door.SetOpen(true);
            PlaySoundAtDoor(doorOpenSound);
        }
        yield return new WaitForSeconds(0.2f);

        // --- 5. ARAMA (SEARCH) ---
        if (CheckIfPlayerHidden())
        {
            guderianModel.SetActive(true);
            yield return StartCoroutine(MoveToTarget(activeRoom.doorInsidePoint.position));
            StartCoroutine(StartSearching());
        }
        else
        {
            TriggerPositionedJumpscare(JumpscareType.BehindPlayer);
        }
    }

    private IEnumerator StartSearching()
    {
        currentState = GuderianState.Searching;
        int activeLights = activeRoom.GetActiveLightCount();
        calculatedSearchDuration = baseSearchDuration + (activeLights * timePerLight);
        currentSearchTimer = calculatedSearchDuration;
        debugStatus = $"Arıyor... ({activeLights} Işık)";

        // Uğultu/Nefes sesi arka planda sürekli çalmaya devam eder
        FadeAudio(searchHumSound, 1.0f, true);

        while (currentState == GuderianState.Searching)
        {
            if (activeRoom.guderianPatrolPoints.Count > 0)
            {
                Transform targetPoint = activeRoom.guderianPatrolPoints[
                    Random.Range(0, activeRoom.guderianPatrolPoints.Count)
                ];

                // 1. Hedefe yürü (Bu sırada daha önce yazdığımız ayak sesleri çalacak)
                yield return StartCoroutine(MoveToTarget(targetPoint.position));

                // 2. Hedefe ulaştı, durup etrafı karıştıracak
                if (currentState == GuderianState.Searching)
                {
                    float searchWaitTime = Random.Range(2.0f, 4.0f); // Ses yoksa varsayılan 2-4 sn bekle

                    // Eğer karıştırma (çekmece) sesleri atandıysa birini seçip çal
                    if (rummageSounds != null && rummageSounds.Length > 0 && audioSource != null)
                    {
                        audioSource.PlayOneShot(searchHumSound); // Arama uğultusu devam ederken karıştırma sesi çal

                        // Bekleme süresini tam olarak sesin uzunluğuna eşitle!
                        // Böylece ses bitmeden yürümeye başlamaz.
                    }

                    // Karıştırma işlemi boyunca o noktada bekle
                    yield return new WaitForSeconds(searchWaitTime);
                }
            }
            else
            {
                // Odada devriye noktası yoksa olduğu yerde bekler
                yield return new WaitForSeconds(1f);
            }
        }
    }

    private IEnumerator ExitSequence()
    {
        currentState = GuderianState.Exiting;
        debugStatus = "Çıkıyor...";

        // ---> DÜZELTME BURADA: Guderian pes edip çıkışa yöneldiği an arama sesini anında sönümleyerek kapatıyoruz.
        FadeAudio(null, 0.5f, false);

        if (activeRoom != null && activeRoom.doorInsidePoint != null)
        {
            yield return StartCoroutine(MoveToTarget(activeRoom.doorInsidePoint.position));
            if (activeRoom.doorOutsidePoint != null)
                yield return StartCoroutine(MoveToTarget(activeRoom.doorOutsidePoint.position));
            if (activeRoom.roomDoor != null && activeRoom.roomDoor.isOpen)
                activeRoom.roomDoor.SetOpen(false);
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        guderianModel.SetActive(false);
        if (agent != null)
            agent.enabled = false;

        currentState = GuderianState.Hidden;
        debugStatus = "Gitti.";

        if (GlobalEnemyManager.Instance != null)
            GlobalEnemyManager.Instance.RegisterAttackEnd();

        cooldownTimer = minTimeBetweenAttacks;
        activeRoom = null;
    }

    private IEnumerator MoveToTarget(Vector3 target)
    {
        if (agent == null)
            yield break;

        // 1. Agent'ı aç
        if (!agent.enabled)
            agent.enabled = true;

        // ---> YENİ: Ayak sesi zamanlayıcısını başlat
        float stepTimer = footstepInterval;

        // 2. Yere Işınla (Warp)
        if (agent.Warp(transform.position))
        {
            agent.isStopped = false;
            agent.SetDestination(target);
        }
        else
        {
            Debug.LogWarning("Guderian NavMesh'e oturtulamadı! Transform ile gidiyor.");
            agent.enabled = false;
            while (Vector3.Distance(transform.position, target) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    walkSpeed * Time.deltaTime
                );

                if (animator != null)
                    animator.SetFloat(_animIDSpeed, walkSpeed);

                // ---> YENİ: Manuel yürürken ayak sesi çal
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    PlayGuderianFootstep();
                    stepTimer = footstepInterval;
                }

                yield return null;
            }
            yield break;
        }

        // 3. Yolun hesaplanmasını bekle
        while (agent.pathPending)
            yield return null;

        // 4. Hedefe varmayı bekle
        while (agent.enabled && agent.remainingDistance > agent.stoppingDistance + 0.1f)
        {
            if (agent.isStopped)
            {
                yield return null;
                continue;
            }

            // ---> YENİ: NavMesh ile odada devriye atarken ayak sesi çal
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                PlayGuderianFootstep();
                stepTimer = footstepInterval; // Scriptin başındaki 0.8 saniyelik süreyi kullanır
            }

            yield return null;
        }

        // Durdur
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.velocity = Vector3.zero;
            if (animator != null)
                animator.SetFloat(_animIDSpeed, 0f);
        }
    }

    // ---> YENİ: Ayak seslerini Guderian'ın kendi üzerinden çalan metot
    private void PlayGuderianFootstep()
    {
        if (footstepSounds != null && footstepSounds.Length > 0 && audioSource != null)
        {
            // 4 sesten birini rastgele seç
            AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];

            // Sesi direkt Guderian'ın üzerindeki AudioSource'dan çal (3D Ses)
            audioSource.PlayOneShot(clip);
        }
    }

    private void TeleportAgent(Vector3 position)
    {
        if (agent != null)
        {
            agent.enabled = false;
            transform.position = position;
            agent.enabled = true;

            if (!agent.Warp(position))
            {
                Debug.LogWarning("Teleport sırasında NavMesh bulunamadı!");
                agent.enabled = false;
            }
        }
        else
        {
            transform.position = position;
        }
    }

    private IEnumerator ExecuteTimelineJumpscareRoutine(PlayableDirector director, string reason)
    {
        if (guderianModel != null)
            guderianModel.SetActive(false);

        // 1. OYUNCU SCRİPTLERİNİ GARANTİ BUL VE FİŞİNİ ÇEK (Asla dönemez)
        var moveScript = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
        if (moveScript != null)
        {
            moveScript.SetFrozen(true, lockCameraInput: true, restrictRotation: false);
            moveScript.enabled = false;
        }

        var inputs = Object.FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.cursorInputForLook = false;
            inputs.move = Vector2.zero;
            inputs.enabled = false;
        }

        var physics = Object.FindFirstObjectByType<UnityEngine.CharacterController>();
        if (physics != null)
            physics.enabled = false;

        // 2. ÇÖZÜM BURADA: KAMERA BEYNİNİ AÇ VE YUMUŞAK DÖNMEYİ (BLEND) İPTAL ET
        if (Camera.main != null)
        {
            var brain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain != null)
            {
                brain.enabled = true;
                // Kameranın yavaşça dönmesini engelle, anında Timeline kamerasına geç (Cut)!
                brain.m_DefaultBlend.m_Style = Cinemachine.CinemachineBlendDefinition.Style.Cut;
                brain.m_DefaultBlend.m_Time = 0f;
            }
        }

        // 3. TIMELINE'I BAŞLAT
        director.Play();
        // 4. BİTMESİNİ BEKLE VE ÖLÜM EKRANINA GEÇ
        yield return new WaitForSeconds((float)director.duration);

        if (DeathUIManager.Instance != null)
            DeathUIManager.Instance.ShowDeathScreen(reason);
    }

    private void FadeAudio(AudioClip clip, float duration, bool fadeIn)
    {
        if (audioSource == null)
            return;
        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);
        audioFadeRoutine = StartCoroutine(FadeAudioRoutine(clip, duration, fadeIn));
    }

    private IEnumerator FadeAudioRoutine(AudioClip clip, float duration, bool fadeIn)
    {
        float targetVol = fadeIn ? 1f : 0f;
        float startVol = audioSource.volume;
        float t = 0f;
        if (fadeIn)
        {
            audioSource.clip = clip;
            audioSource.loop = true;
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        while (t < duration)
        {
            t += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVol, targetVol, t / duration);
            yield return null;
        }
        audioSource.volume = targetVol;
        if (!fadeIn)
            audioSource.Stop();
    }

    private enum JumpscareType
    {
        AtDoor,
        BehindPlayer,
        InFrontOfPlayer,
    }

    private void TriggerPositionedJumpscare(JumpscareType type, string reason = "You didn't hide.")
    {
        currentState = GuderianState.Jumpscare;
        debugStatus = "JUMPSCARE!";
        StopAllCoroutines();

        if (agent != null)
            agent.enabled = false;

        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);

        // --- YENİ: Hangi Timeline'ın oynayacağını seçiyoruz ---
        PlayableDirector selectedTimeline = null;
        switch (type)
        {
            case JumpscareType.AtDoor:
                selectedTimeline = timelineAtDoor;
                break;
            case JumpscareType.BehindPlayer:
                selectedTimeline = timelineBehindPlayer;
                break;
            case JumpscareType.InFrontOfPlayer:
                selectedTimeline = timelineInFront;
                break;
        }

        // Eğer Inspector'dan bu durum için bir Timeline atanmışsa YENİ SİSTEMİ KULLAN
        if (selectedTimeline != null)
        {
            StartCoroutine(ExecuteTimelineJumpscareRoutine(selectedTimeline, reason));
        }
        else
        {
            // --- ESKİ SİSTEM (Eğer Timeline atanmamışsa normal jumpscare çalışır) ---[cite: 8]
            if (audioSource)
            {
                audioSource.Stop();
                audioSource.volume = 1f;
                audioSource.PlayOneShot(jumpscareSound);
            }
            if (guderianModel != null)
                guderianModel.SetActive(true);

            if (animator != null)
            {
                animator.SetFloat(_animIDSpeed, 0f);
                animator.SetTrigger(_animIDAttack);
            }

            // Performans: Önbellekteki player referansını kullan
            Transform player = cachedPlayer;
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    cachedPlayer = playerObj.transform;
                    player = cachedPlayer;
                }
            }
            bool shouldPlayAnim = false; // Bu Player'ın animasyonudur

            if (activeRoom == null)
            {
                SetPositionWithOffset(player.position + (player.forward * 1.0f), true);
                LookAtTargetFlat(player);
            }
            else
            {
                switch (type)
                {
                    case JumpscareType.AtDoor:
                        if (activeRoom.doorOutsidePoint != null)
                            SetPositionWithOffset(activeRoom.doorOutsidePoint.position, false);
                        LookAtTargetFlat(player);
                        break;
                    case JumpscareType.BehindPlayer:
                        Vector3 behindPos = player.position - (player.forward * jumpscareDistance);
                        SetPositionWithOffset(
                            new Vector3(behindPos.x, player.position.y, behindPos.z),
                            true
                        );
                        LookAtTargetFlat(player);
                        shouldPlayAnim = true;
                        break;
                    case JumpscareType.InFrontOfPlayer:
                        LookAtTargetFlat(player);
                        break;
                }
            }

            if (JumpscareManager.Instance != null)
                JumpscareManager.Instance.StartJumpscare(
                    transform,
                    guderianJumpscareProfile,
                    shouldPlayAnim,
                    JumpscareStyle.Direct,
                    reason
                );
            else
                StartCoroutine(ExitSequence());
        }
    }

    public void TriggerLockerJumpscare(Transform lockerExitPoint)
    {
        currentState = GuderianState.Jumpscare;
        debugStatus = "DOLAP JUMPSCARE!";
        StopAllCoroutines();

        if (agent != null)
            agent.enabled = false;

        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);

        // --- YENİ (TİMELİNE) SİSTEMİ ---
        if (timelineLocker != null)
        {
            // OYUNCUYU ZORLA DÖNDÜREN HİÇBİR KOD YOK, OLDUĞU GİBİ KALIR.
            StartCoroutine(ExecuteTimelineJumpscareRoutine(timelineLocker, "You left too early."));
        }
        else
        {
            // --- ESKİ SİSTEM (Eğer Timeline atanmamışsa) ---
            if (audioSource)
            {
                audioSource.Stop();
                audioSource.volume = 1f;
                audioSource.PlayOneShot(jumpscareSound);
            }
            if (animator != null)
            {
                animator.SetFloat(_animIDSpeed, 0f);
                animator.SetTrigger(_animIDAttack);
            }

            GameObject player =
                cachedPlayer != null
                    ? cachedPlayer.gameObject
                    : GameObject.FindGameObjectWithTag("Player");

            if (lockerExitPoint != null)
            {
                Vector3 finalPos = lockerExitPoint.position;
                finalPos.y += spawnYOffset;
                transform.position = finalPos;
                transform.LookAt(finalPos - lockerExitPoint.forward);
            }

            // ESKİ SİSTEMDEKİ OYUNCUYU DÖNDÜRME KODU (Timeline varsa burası çalışmaz)
            if (player != null)
            {
                player.transform.LookAt(
                    new Vector3(
                        transform.position.x,
                        player.transform.position.y,
                        transform.position.z
                    )
                );
            }

            guderianModel.SetActive(true);
            if (JumpscareManager.Instance != null)
                JumpscareManager.Instance.StartJumpscare(
                    transform,
                    guderianJumpscareProfile,
                    false,
                    JumpscareStyle.Direct,
                    "You left too early."
                );
        }
    }

    private void SetPositionWithOffset(Vector3 targetPos, bool useSpawnOffset = true)
    {
        float finalY = targetPos.y + (useSpawnOffset ? spawnYOffset : 0f);
        Vector3 finalPos = new Vector3(targetPos.x, finalY, targetPos.z);

        if (agent != null)
            agent.enabled = false;
        transform.position = finalPos;
    }

    private void LookAtTargetFlat(Transform target)
    {
        if (target == null)
            return;
        Vector3 lookPos = new Vector3(target.position.x, transform.position.y, target.position.z);
        transform.LookAt(lookPos);
    }

    private bool CheckIfPlayerLookingAtDoor()
    {
        if (activeRoom == null || activeRoom.roomDoor == null)
            return false;
        // Performans: Önbellekteki player referansını kullan
        Transform player = cachedPlayer;
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                cachedPlayer = playerObj.transform;
                player = cachedPlayer;
            }
            else
                return false;
        }
        Vector3 dirToDoor = (activeRoom.roomDoor.transform.position - player.position).normalized;
        float angle = Vector3.Angle(player.forward, dirToDoor);
        return angle < lookAtDoorThreshold;
    }

    private bool CheckIfPlayerHidden()
    {
        foreach (var spot in activeRoom.hidingSpots)
            if (spot.IsOccupied)
                return true;
        return false;
    }

    public void TriggerJumpscare() => TriggerPositionedJumpscare(JumpscareType.InFrontOfPlayer);

    public void ForceLeave()
    {
        if (currentState != GuderianState.Hidden)
        {
            StopAllCoroutines();
            StartCoroutine(ExitSequence());
        }
    }

    public bool IsCampingPlayer(InteractableHidingSpot spot) =>
        (currentState == GuderianState.Searching && activeRoom != null);

    public float GetCurrentChance() => currentSpawnChance;

    public float GetTimeUntilNextSpawnCheck() => Mathf.Max(0, checkInterval - spawnCheckTimer);

    public bool IsOnCooldown() => cooldownTimer > 0;

    public bool IsCampingPlayerInRoom(RoomManager room) =>
        (activeRoom == room && currentState == GuderianState.Ambush);

    private void PlaySoundAtDoor(AudioClip clip)
    {
        if (clip == null)
            return;
        if (
            activeRoom != null
            && activeRoom.roomDoor != null
            && activeRoom.roomDoor.DoorAudioSource != null
        )
            activeRoom.roomDoor.DoorAudioSource.PlayOneShot(clip);
        else if (audioSource)
            audioSource.PlayOneShot(clip);
    }
}
