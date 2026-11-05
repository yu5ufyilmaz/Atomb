using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }
    public bool IsRunning => isActiveAndEnabled && isTutorialEnabled
        && tutorialEventsParent != null && tutorialEventsParent.activeInHierarchy;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    [Header("Tutorial Ayarları")]
    [Tooltip("İçinde tüm tutorial eventlerini barındıran Ana Obje (Parent GameObject)")]
    public GameObject tutorialEventsParent;

    [Tooltip("Eğitim modunun şu anki durumu")]
    public bool isTutorialEnabled = true;

    [Tooltip("Eğitimde ölen oyuncu, ölüm animasyonundan sonra bu dolabın içinde devam eder (boşsa normal ölüm ekranı).")]
    public InteractableHidingSpot deathRespawnSpot;

    // 4 Nov: where the player stood when the office door was locked; a death out of hiding rewinds here.
    private InteractableDoor lockDoor;
    private Vector3 lockPosition;
    private Quaternion lockRotation;
    private float lockYaw, lockPitch;

    public void RecordLockCheckpoint(InteractableDoor door)
    {
        var mover = FindFirstObjectByType<StarterAssets.CharacterController>();
        if (mover == null) return;
        lockDoor = door;
        lockPosition = mover.transform.position;
        lockRotation = mover.transform.rotation;
        lockYaw = mover.IntendedLookYaw;
        lockPitch = mover.IntendedLookPitch;
    }

    // 3 Nov playtest: a tutorial death keeps its jumpscare, then the lesson resumes inside the wardrobe.
    public bool TryRewindDeath()
    {
        // Only Guderian's catch: the wardrobe is his lesson, and other deaths leave their own enemy state behind.
        if (!IsRunning || deathRespawnSpot == null || GameManager.Instance == null || GuderianAI.Instance == null
            || GuderianAI.Instance.currentState != GuderianAI.GuderianState.Jumpscare) return false;
        // The locker catch leaves the spot occupied: caught while hidden (peeking, leaving) vs. caught in the open.
        bool wasHidden = deathRespawnSpot.IsOccupied;
        if (JumpscareManager.Instance != null) JumpscareManager.Instance.ResetAfterRewind();
        GuderianAI.Instance.ResetAfterRewind();
        GameManager.Instance.CancelDeath();
        if (!wasHidden && lockDoor != null)
        {
            // Back to the lock moment: same spot and view, door locked and shut, Guderian approaching again.
            var body = FindFirstObjectByType<CharacterController>();
            if (body != null)
            {
                body.enabled = false;
                body.transform.SetPositionAndRotation(lockPosition, lockRotation);
                body.enabled = true;
            }
            FindFirstObjectByType<StarterAssets.CharacterController>()?.ForceCameraRotation(lockYaw, lockPitch);
            lockDoor.SnapShutAndLocked(); // the player must not see it swing shut or hear the lock
            GuderianAI.Instance.RestartAttackForRewind();
            return true;
        }
        deathRespawnSpot.PlaceInside();
        // The lesson's "hide" step listens for the wardrobe interaction.
        PlayerInteraction.NotifyInteracted(deathRespawnSpot.gameObject);
        // Back to the search: he is inside and looking again; the "he left" step waits for that search to end.
        if (wasHidden) GuderianAI.Instance.ResumeSearchingForRewind(GameManager.Instance.allRooms.Find(r => r.hidingSpots.Contains(deathRespawnSpot)));
        return true;
    }

    public void Start()
    {
        SetupTutorialMode();
    }

    /// <summary>
    /// UI üzerinden (örneğin bir Toggle veya Buton ile) direkt çağrılacak metot.
    /// Dışarıdan parametre almaz, mevcut durumu tersine çevirir.
    /// </summary>
    public void ToggleTutorial()
    {
        SetTutorialEnabled(!isTutorialEnabled);
    }

    public void CompleteTutorial()
    {
        SetTutorialEnabled(false);
    }

    public void SetTutorialEnabled(bool enabled)
    {
        isTutorialEnabled = enabled;
        SetupTutorialMode(initializeNormalGameplay: true);
    }

    /// <summary>
    /// Değişen isTutorialEnabled durumuna göre gerekli objeleri açar/kapatır.
    /// </summary>
    private void SetupTutorialMode(bool initializeNormalGameplay = false)
    {
        if (tutorialEventsParent != null)
        {
            bool wasActive = tutorialEventsParent.activeSelf;
            tutorialEventsParent.SetActive(isTutorialEnabled);

            if (!isTutorialEnabled)
            {
                Debug.Log("[TutorialManager] Eğitim KAPATILDI! Tutorial ana objesi devre dışı.");

                if (PlayerInteraction.Instance != null)
                {
                    PlayerInteraction.Instance.isTutorialMode = false;
                    PlayerInteraction.Instance.UpdateTutorialWhitelist(new System.Collections.Generic.List<GameObject>());
                }
                if (wasActive || initializeNormalGameplay)
                {
                    if (BreakerBox.Instance != null) BreakerBox.Instance.isSystemActive = true;
                    if (PressureSystemManager.Instance != null) PressureSystemManager.Instance.isSystemActive = true;
                    if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = false;
                    if (GameManager.Instance == null || (GameManager.Instance.isGameStarted && GameManager.Instance.activeInteraction == null))
                        FindFirstObjectByType<StarterAssets.CharacterController>()?.SetFrozen(false, false, false);
                }
            }
            else
            {
                Debug.Log("[TutorialManager] Eğitim AÇILDI! Tutorial ana objesi devrede.");

                if (PlayerInteraction.Instance != null)
                {
                    PlayerInteraction.Instance.isTutorialMode = true;
                }
            }
        }
        else
        {
            Debug.LogWarning(
                "[TutorialManager] Lütfen Inspector'dan 'Tutorial Events Parent' objesini ata!"
            );
        }
    }
}
