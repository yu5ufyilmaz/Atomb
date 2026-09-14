using System;
using UnityEngine;

public class HidingCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip("Hangi dolaba/saklanma alanına girildiğinde tetiklenecek?")]
    public InteractableHidingSpot targetHidingSpot;

    [Tooltip("Oyuncu dolaba GİRDİĞİNDE mi (True), ÇIKTIĞINDA mı (False) tetiklensin?")]
    public bool triggerOnEnter = true;

    private bool conditionMet = false;
    private bool lastOccupiedState = false;

    public bool IsMet() => conditionMet;

    private void Start()
    {
        if (targetHidingSpot == null)
        {
            targetHidingSpot = GetComponent<InteractableHidingSpot>();
        }

        if (targetHidingSpot != null)
        {
            lastOccupiedState = targetHidingSpot.IsOccupied; // Mevcut property'den durumu al
        }
    }

    private void Update()
    {
        // Eğer koşul zaten sağlandıysa sistemi yorma
        if (conditionMet || targetHidingSpot == null) 
            return;

        bool currentState = targetHidingSpot.IsOccupied;

        // Dolabın dolu/boş durumunda bir değişiklik olduysa[cite: 2]
        if (currentState != lastOccupiedState)
        {
            // İstenen duruma (Girme veya Çıkma) ulaşıldıysa[cite: 2]
            if (currentState == triggerOnEnter)
            {
                conditionMet = true;
                OnConditionChanged?.Invoke(); // EventLogicController'a haber ver
            }
            lastOccupiedState = currentState;
        }
    }
}