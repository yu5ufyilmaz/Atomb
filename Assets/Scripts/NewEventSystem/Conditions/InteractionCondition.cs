using System;
using UnityEngine;

public class InteractionCondition : MonoBehaviour, ICondition
{
    public enum CompletionMode
    {
        OnInteractStart, // Objeye ilk tıklandığında
        OnInteractExit, // Objeyle etkileşim bittiğinde / arayüzden çıkıldığında
    }

    public event Action OnConditionChanged;

    [Tooltip("Hangi objeyle etkileşime girilecek?")]
    public GameObject targetInteractable;

    [Tooltip("Koşulun ne zaman tamamlanacağı")]
    public CompletionMode completionMode = CompletionMode.OnInteractStart;

    private bool conditionMet = false;

    public bool IsMet() => conditionMet;

    private void Start()
    {
        if (targetInteractable == null)
        {
            targetInteractable = gameObject;
        }

        // Tıklama (Başlangıç) ve Çıkış olaylarına abone oluyoruz
        PlayerInteraction.OnPlayerInteracted += HandlePlayerInteracted;
        PlayerInteraction.OnPlayerInteractionExited += HandleInteractionExit;
    }

    private void HandlePlayerInteracted(GameObject interactedObj)
    {
        if (interactedObj == targetInteractable)
        {
            if (completionMode == CompletionMode.OnInteractStart)
            {
                TriggerConditionMet();
            }
        }
    }

    private void HandleInteractionExit(GameObject exitedObj)
    {
        // Kapanan obje hedefimizse ve mod OnInteractExit ise koşulu sağla
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

    private void OnDestroy()
    {
        // Hata almamak için abonelikleri iptal et
        PlayerInteraction.OnPlayerInteracted -= HandlePlayerInteracted;
        PlayerInteraction.OnPlayerInteractionExited -= HandleInteractionExit;
    }
}
