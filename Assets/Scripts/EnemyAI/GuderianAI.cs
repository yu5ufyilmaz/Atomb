using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        RhythmGame,
        Ambush,
    }

    public GuderianState currentState = GuderianState.Hidden;

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

    [Header("Görsellik & Ses")]
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

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        if (guderianModel)
            guderianModel.SetActive(false);
        currentSpawnChance = baseSpawnChance;
    }

    private void Update()
    {
        if (GlobalEnemyManager.Instance.stopAllEnemies)
            return;
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

    // --- GÜNCELLENEN PUSU (AMBUSH) SİSTEMİ ---

    public void SetupAmbush(RoomManager room)
    {
        if (currentState != GuderianState.Hidden || !GlobalEnemyManager.Instance.CanAttack())
            return;

        Debug.Log($"GUDERIAN: {room.roomName} PUSU kuruyor!");
        GlobalEnemyManager.Instance.RegisterAttackStart();

        activeRoom = room;
        currentState = GuderianState.Ambush;
        debugStatus = "PUSUDA (Manuel Konum)";

        // 1. Önce Manuel Noktaya Bak
        if (room.ambushSpawnPoint != null)
        {
            transform.position = room.ambushSpawnPoint.position;
            transform.rotation = room.ambushSpawnPoint.rotation; // Yönünü de ayarla
        }
        // 2. Yoksa Eski Usül Kapı İçine Bak
        else if (room.doorInsidePoint != null)
        {
            transform.position = room.doorInsidePoint.position;
            if (room.doorOutsidePoint != null)
                transform.LookAt(
                    new Vector3(
                        room.doorOutsidePoint.position.x,
                        transform.position.y,
                        room.doorOutsidePoint.position.z
                    )
                );
        }

        guderianModel.SetActive(false);
    }

    public void TriggerAmbushExecute()
    {
        if (currentState != GuderianState.Ambush)
            return;

        guderianModel.SetActive(true);
        TriggerPositionedJumpscare(JumpscareType.InFrontOfPlayer);
    }

    public bool IsCampingPlayerInRoom(RoomManager room)
    {
        return (activeRoom == room && currentState == GuderianState.Ambush);
    }

    // ----------------------------------------

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

        if (door.isOpen)
        {
            currentState = GuderianState.Approaching;
            debugStatus = "Kapı AÇIK! Yaklaşıyor...";
            SetPositionWithOffset(activeRoom.doorOutsidePoint.position, false);
            guderianModel.SetActive(false);
            yield return new WaitForSeconds(footstepInterval * 5); // Basitleştirilmiş bekleme
            TriggerPositionedJumpscare(JumpscareType.AtDoor);
            yield break;
        }

        currentState = GuderianState.Approaching;
        debugStatus = "Adım Sesleri...";
        SetPositionWithOffset(activeRoom.doorOutsidePoint.position, false);
        guderianModel.SetActive(false);

        // Adım sesleri döngüsü
        for (int i = 0; i < 5; i++)
        {
            if (footstepSounds.Length > 0)
                PlaySoundAtDoor(footstepSounds[Random.Range(0, footstepSounds.Length)]);
            yield return new WaitForSeconds(footstepInterval);
            if (door.isOpen)
            {
                TriggerPositionedJumpscare(JumpscareType.AtDoor);
                yield break;
            }
        }

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
                TriggerPositionedJumpscare(JumpscareType.AtDoor);
                yield break;
            }
            yield return null;
        }

        if (isDoorLocked)
            door.SetLocked(false);

        if (CheckIfPlayerLookingAtDoor())
        {
            currentState = GuderianState.WaitingBehindDoor;
            debugStatus = "PUSUDA...";
            while (currentState == GuderianState.WaitingBehindDoor)
            {
                if (door.isOpen)
                {
                    TriggerPositionedJumpscare(JumpscareType.AtDoor);
                    yield break;
                }
                if (!CheckIfPlayerLookingAtDoor())
                    break;
                yield return null;
            }
        }

        currentState = GuderianState.Entering;
        debugStatus = "İçeri Giriyor...";
        if (!door.isOpen)
        {
            door.SetOpen(true);
            PlaySoundAtDoor(doorOpenSound);
        }
        yield return new WaitForSeconds(0.2f);

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
        FadeAudio(searchHumSound, 1.0f, true);

        while (currentState == GuderianState.Searching)
        {
            if (activeRoom.guderianPatrolPoints.Count > 0)
            {
                Transform targetPoint = activeRoom.guderianPatrolPoints[
                    Random.Range(0, activeRoom.guderianPatrolPoints.Count)
                ];
                yield return StartCoroutine(MoveToTarget(targetPoint.position));
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator ExitSequence()
    {
        currentState = GuderianState.Exiting;
        debugStatus = "Çıkıyor...";
        if (activeRoom != null && activeRoom.doorInsidePoint != null)
        {
            yield return StartCoroutine(MoveToTarget(activeRoom.doorInsidePoint.position));
            if (activeRoom.doorOutsidePoint != null)
                yield return StartCoroutine(MoveToTarget(activeRoom.doorOutsidePoint.position));
            if (activeRoom.roomDoor != null && activeRoom.roomDoor.isOpen)
                activeRoom.roomDoor.SetOpen(false);
        }
        else
            yield return new WaitForSeconds(1.0f);

        guderianModel.SetActive(false);
        FadeAudio(null, 2.0f, false);
        currentState = GuderianState.Hidden;
        debugStatus = "Gitti.";
        if (GlobalEnemyManager.Instance != null)
            GlobalEnemyManager.Instance.RegisterAttackEnd();
        cooldownTimer = minTimeBetweenAttacks;
        activeRoom = null;
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

    private void TriggerPositionedJumpscare(JumpscareType type)
    {
        currentState = GuderianState.Jumpscare;
        debugStatus = "JUMPSCARE!";
        StopAllCoroutines();
        if (audioFadeRoutine != null)
            StopCoroutine(audioFadeRoutine);
        if (audioSource)
        {
            audioSource.Stop();
            audioSource.volume = 1f;
            audioSource.PlayOneShot(jumpscareSound);
        }

        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        bool shouldPlayAnim = false;

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
                    // Eğer Pusu modundaysak ve özel bir noktamız varsa oradan hareket ettirmeyelim (Zaten oradadır)
                    // Ancak modele göre yönünü oyuncuya çevirmemiz gerekebilir.
                    LookAtTargetFlat(player);
                    break;
            }
        }

        guderianModel.SetActive(true);
        if (JumpscareManager.Instance != null)
            JumpscareManager.Instance.StartJumpscare(transform, shouldPlayAnim);
        else
            StartCoroutine(ExitSequence());
    }

    private void SetPositionWithOffset(Vector3 targetPos, bool useSpawnOffset = true)
    {
        float finalY = targetPos.y + (useSpawnOffset ? spawnYOffset : 0f);
        transform.position = new Vector3(targetPos.x, finalY, targetPos.z);
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
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        Vector3 dirToDoor = (activeRoom.roomDoor.transform.position - player.position).normalized;
        float angle = Vector3.Angle(player.forward, dirToDoor);
        return angle < lookAtDoorThreshold;
    }

    private IEnumerator MoveToTarget(Vector3 target)
    {
        float timeout = 10f;
        float currentTimer = 0f;
        Vector3 targetFlat = new Vector3(target.x, transform.position.y, target.z);
        while (
            Vector3.Distance(
                new Vector3(transform.position.x, transform.position.y, transform.position.z),
                targetFlat
            ) > 0.1f
        )
        {
            currentTimer += Time.deltaTime;
            if (currentTimer > timeout)
                break;
            Vector3 direction = (targetFlat - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(
                    new Vector3(direction.x, 0, direction.z)
                );
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    lookRot,
                    Time.deltaTime * 5f
                );
            }
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetFlat,
                walkSpeed * Time.deltaTime
            );
            yield return null;
        }
    }

    private bool CheckIfPlayerHidden()
    {
        foreach (var spot in activeRoom.hidingSpots)
            if (spot.IsOccupied)
                return true;
        return false;
    }

    public void TriggerJumpscare() => TriggerPositionedJumpscare(JumpscareType.InFrontOfPlayer);

    public bool IsCampingPlayer(InteractableHidingSpot spot) =>
        (currentState == GuderianState.Searching && activeRoom != null);

    public void TriggerLockerJumpscare(Transform lockerExitPoint)
    {
        currentState = GuderianState.Jumpscare;
        StopAllCoroutines();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (lockerExitPoint != null)
        {
            Vector3 finalPos = lockerExitPoint.position;
            finalPos.y += spawnYOffset;
            transform.position = finalPos;
            transform.LookAt(finalPos - lockerExitPoint.forward);
        }
        if (player != null)
            player.transform.LookAt(
                new Vector3(transform.position.x, player.transform.position.y, transform.position.z)
            );
        guderianModel.SetActive(true);
        if (audioSource)
        {
            audioSource.Stop();
            audioSource.volume = 1f;
            audioSource.PlayOneShot(jumpscareSound);
        }
        if (JumpscareManager.Instance != null)
            JumpscareManager.Instance.StartJumpscare(transform, false);
    }

    public void ForceLeave()
    {
        if (currentState != GuderianState.Hidden)
        {
            StopAllCoroutines();
            StartCoroutine(ExitSequence());
        }
    }

    public float GetCurrentChance() => currentSpawnChance;

    public float GetTimeUntilNextSpawnCheck() => Mathf.Max(0, checkInterval - spawnCheckTimer);

    public bool IsOnCooldown() => cooldownTimer > 0;

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
