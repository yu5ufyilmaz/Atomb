using UnityEngine;

public class TutorialManager : MonoBehaviour
{
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
        isTutorialEnabled = !isTutorialEnabled;
        SetupTutorialMode();
    }

    /// <summary>
    /// Değişen isTutorialEnabled durumuna göre gerekli objeleri açar/kapatır.
    /// </summary>
    private void SetupTutorialMode()
    {
        if (tutorialEventsParent != null)
        {
            tutorialEventsParent.SetActive(isTutorialEnabled);

            if (!isTutorialEnabled)
            {
                Debug.Log("[TutorialManager] Eğitim KAPATILDI! Tutorial ana objesi devre dışı.");

                if (PlayerInteraction.Instance != null)
                {
                    PlayerInteraction.Instance.isTutorialMode = false;
                    PlayerInteraction.Instance.allowedTutorialObjects.Clear();
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
