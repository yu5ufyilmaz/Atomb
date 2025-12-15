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
    public float standoffKillDistance = 2.0f;
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

    // --- DEBUG ---
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

    // Ses Kontrolü
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
                Debug.Log("Guderian: Oyuncu erken çıktı! Yakala.");
                StopAllCoroutines();
                TriggerPositionedJumpscare(JumpscareType.InFrontOfPlayer);
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
        if (playerCurrentRoom == room)
            playerCurrentRoom = null;
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
        if (currentState != GuderianState.Hidden)
            return;
        if (!GlobalEnemyManager.Instance.CanAttack())
            return;
        if (!room.canGuderianSpawn || room.roomDoor == null)
            return;
        if (room.doorOutsidePoint == null || room.doorInsidePoint == null)
            return;

        activeRoom = room;
        GlobalEnemyManager.Instance.RegisterAttackStart();
        StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        InteractableDoor door = activeRoom.roomDoor;
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;

        // --- ADIM 0: KAPI AÇIKSA ---
        if (door.isOpen)
        {
            currentState = GuderianState.Approaching;
            debugStatus = "Kapı AÇIK! Yaklaşıyor...";
            SetPositionWithOffset(activeRoom.doorOutsidePoint.position, false);
            guderianModel.SetActive(false);

            for (int i = 0; i < 5; i++)
            {
                if (footstepSounds.Length > 0)
                    PlaySoundAtDoor(footstepSounds[Random.Range(0, footstepSounds.Length)]);
                yield return new WaitForSeconds(footstepInterval);
            }
            TriggerPositionedJumpscare(JumpscareType.AtDoor);
            yield break;
        }

        // --- ADIM 1: YAKLAŞMA ---
        currentState = GuderianState.Approaching;
        debugStatus = "Adım Sesleri Geliyor...";
        SetPositionWithOffset(activeRoom.doorOutsidePoint.position, false);
        guderianModel.SetActive(false);

        for (int i = 0; i < 5; i++)
        {
            if (footstepSounds.Length > 0)
                PlaySoundAtDoor(footstepSounds[Random.Range(0, footstepSounds.Length)]);
            debugApproachProgress = (float)(i + 1) / 5;

            float waitTimer = 0f;
            while (waitTimer < footstepInterval)
            {
                waitTimer += Time.deltaTime;
                if (door.isOpen)
                {
                    TriggerPositionedJumpscare(JumpscareType.AtDoor);
                    yield break;
                }
                yield return null;
            }
        }

        // --- ADIM 2: ZORLAMA ---
        currentState = GuderianState.Breaching;
        bool isDoorLocked = door.IsLocked();
        float breachTime = isDoorLocked ? lockedDoorBreachTime : closedDoorBreachTime;
        debugStatus = isDoorLocked ? "Kapı KİLİTLİ (Kırıyor)..." : "Kapı KAPALI (Açıyor)...";
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

        // --- ADIM 3: KARAR ---
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

        // --- ADIM 4: GİRİŞ ---
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

        // YENİ: Arama sesini yumuşakça başlat (Fade In)
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
        debugStatus = "Odadan Çıkıyor...";

        // --- DÜZELTME BAŞLANGICI ---
        // Eğer bir oda tanımlıysa kapıdan çık, yoksa direkt yok ol.
        if (
            activeRoom != null
            && activeRoom.doorInsidePoint != null
            && activeRoom.doorOutsidePoint != null
        )
        {
            yield return StartCoroutine(MoveToTarget(activeRoom.doorInsidePoint.position));
            yield return StartCoroutine(MoveToTarget(activeRoom.doorOutsidePoint.position));

            if (activeRoom.roomDoor != null && activeRoom.roomDoor.isOpen)
                activeRoom.roomDoor.SetOpen(false);
        }
        else
        {
            // Oda yoksa 1 saniye bekle ve kaybol (Editör testleri için)
            yield return new WaitForSeconds(1.0f);
        }
        // --- DÜZELTME BİTİŞİ ---

        guderianModel.SetActive(false);

        // YENİ: Çıkarken sesi yavaşça kapat (Fade Out) - 2 saniye
        FadeAudio(null, 2.0f, false);

        currentState = GuderianState.Hidden;
        debugStatus = "Gitti.";

        if (GlobalEnemyManager.Instance != null)
            GlobalEnemyManager.Instance.RegisterAttackEnd();

        cooldownTimer = minTimeBetweenAttacks;

        // Odayı temizle ki bir sonraki sefere temiz başlasın
        activeRoom = null;
    }

    // --- YENİ SES SİSTEMİ (Hard Cut Önleyici) ---
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

    // ---------------------------------------------

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

        // Jumpscare anında sesi hemen kes ve patlat
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

        // --- DÜZELTME BAŞLANGICI ---
        // Eğer bir oda yoksa (Editörden basıldıysa), varsayılan olarak oyuncunun dibinde doğsun.
        if (activeRoom == null)
        {
            // Odaya bağlı olmayan acil durum pozisyonu
            Vector3 emergencyPos = player.position + (player.forward * 1.0f);
            SetPositionWithOffset(emergencyPos, true);
            LookAtTargetFlat(player);
            shouldPlayAnim = false;
            // Tip ne olursa olsun, oda yoksa oyuncunun önüne ışınla ve çık.
        }
        else
        {
            // Oda varsa eski mantık çalışsın
            switch (type)
            {
                case JumpscareType.AtDoor:
                    if (activeRoom.doorOutsidePoint != null)
                        SetPositionWithOffset(activeRoom.doorOutsidePoint.position, false);
                    LookAtTargetFlat(player);
                    shouldPlayAnim = false;
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
                    if (activeRoom.doorInsidePoint != null)
                        SetPositionWithOffset(activeRoom.doorInsidePoint.position, true);
                    LookAtTargetFlat(player);
                    shouldPlayAnim = false;
                    break;
            }
        }
        // --- DÜZELTME BİTİŞİ ---

        guderianModel.SetActive(true);
        Debug.LogError($"GUDERIAN YAKALADI! Tip: {type}");

        if (JumpscareManager.Instance != null)
            JumpscareManager.Instance.StartJumpscare(transform, shouldPlayAnim);
        else
            StartCoroutine(ExitSequence()); // ExitSequence'da da hata almamak için orayı da düzelteceğiz.
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

    public void TriggerJumpscare()
    {
        TriggerPositionedJumpscare(JumpscareType.InFrontOfPlayer);
    }

    public bool IsCampingPlayer(InteractableHidingSpot spot)
    {
        return (currentState == GuderianState.Searching && activeRoom != null);
    }

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
        {
            Vector3 lookTarget = new Vector3(
                transform.position.x,
                player.transform.position.y,
                transform.position.z
            );
            player.transform.LookAt(lookTarget);
        }

        guderianModel.SetActive(true);
        if (audioSource)
        {
            if (audioFadeRoutine != null)
                StopCoroutine(audioFadeRoutine);
            audioSource.volume = 1f;
            audioSource.PlayOneShot(jumpscareSound);
        }

        if (JumpscareManager.Instance != null)
            JumpscareManager.Instance.StartJumpscare(transform, false);
    }

    public void ForceLeave()
    {
        if (currentState == GuderianState.Hidden)
            return;
        StopAllCoroutines();
        StartCoroutine(ExitSequence());
    }

    public float GetCurrentChance() => currentSpawnChance;

    public float GetTimeUntilNextSpawnCheck() => Mathf.Max(0, checkInterval - spawnCheckTimer);

    public bool IsOnCooldown() => cooldownTimer > 0;

    private void PlaySoundAtDoor(AudioClip clip)
    {
        if (clip == null)
            return;
        if (activeRoom != null && activeRoom.roomDoor != null)
        {
            AudioSource doorSource = activeRoom.roomDoor.DoorAudioSource;
            if (doorSource != null)
            {
                doorSource.PlayOneShot(clip);
                return;
            }
        }
        if (audioSource)
            audioSource.PlayOneShot(clip);
    }
}
