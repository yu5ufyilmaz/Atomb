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
