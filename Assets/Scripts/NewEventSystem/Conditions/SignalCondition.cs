using System;
using UnityEngine;

public class SignalCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    private bool conditionMet = false;

    public bool IsMet() => conditionMet;

    // Bu metodu başka bir scriptin içinden veya Inspector'daki bir UnityEvent'ten çağıracaksın.
    public void ReceiveSignal()
    {
        if (conditionMet)
            return; // Zaten tetiklendiyse tekrar yorma

        conditionMet = true;
        OnConditionChanged?.Invoke(); // EventLogicController'a "Şart sağlandı!" der
        Debug.Log(
            $"[SignalCondition] {gameObject.name} üzerindeki dış sinyal alındı ve koşul sağlandı!"
        );
    }
}
