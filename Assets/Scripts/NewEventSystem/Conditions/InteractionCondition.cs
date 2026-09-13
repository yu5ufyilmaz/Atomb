using System;
using UnityEngine;

public class InteractionCondition : MonoBehaviour, ICondition
{
    public enum CompletionMode
    {
        OnInteractStart, // Objeye ilk tıklandığı an
        OnInteractExit, // Objeyle etkileşim bittiğinde / arayüzden çıkıldığında
    }

    public event Action OnConditionChanged;

    [Tooltip("Hangi objeyle etkileşime girilecek?")]
    public GameObject targetInteractable;

    [Tooltip("Şartın ne zaman tamamlanacağı")]
    public CompletionMode completionMode = CompletionMode.OnInteractStart;

    private bool conditionMet = false;

    public bool IsMet() => conditionMet;

    private void Start()
    {
        if (targetInteractable == null)
        {
            targetInteractable = gameObject;
        }

        // Tıklama (Başlangıç) olayını dinle
        PlayerInteraction.OnPlayerInteracted += HandlePlayerInteracted;

        // Eğer sınıfında çıkış (Exit) event'i varsa buraya abone olabiliriz
    }

    private void HandlePlayerInteracted(GameObject interactedObj)
    {
        if (interactedObj == targetInteractable)
        {
            if (completionMode == CompletionMode.OnInteractStart)
            {
                TriggerConditionMet();
            }
            // Eğer mod OnInteractExit ise burada sadece etkileşimin başladığını not alıp, çıkışını bekleyebiliriz.
        }
    }

    // Kitap kapatıldığında veya arayüzden çıkıldığında çağrılacak metot
    public void NotifyInteractionExit(GameObject exitedObj)
    {
        if (
            exitedObj == targetInteractable
            && completionMode == CompletionMode.OnInteractExit
            && !conditionMet
        )
        {
            TriggerConditionMet();
        }
    }

    private void TriggerConditionMet()
    {
        conditionMet = true;
        OnConditionChanged?.Invoke();
    }

    private void HandleInteractionExit(GameObject exitedObj)
    {
        // Eğer kapanan obje bizim hedefimizse ve mod OnInteractExit ise şartı sağla
        if (
            exitedObj == targetInteractable
            && completionMode == CompletionMode.OnInteractExit
            && !conditionMet
        )
        {
            conditionMet = true;
            OnConditionChanged?.Invoke();
        }
    }

    private void OnDestroy()
    {
        PlayerInteraction.OnPlayerInteracted -= HandlePlayerInteracted;
    }
}
