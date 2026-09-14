using System;
using UnityEngine;

public class GuderianStateCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip("Guderian hangi duruma geçtiğinde tetiklensin?")]
    public GuderianAI.GuderianState targetState = GuderianAI.GuderianState.Hidden;

    private bool conditionMet = false;

    public bool IsMet() => conditionMet;

    private void Update()
    {
        if (conditionMet || GuderianAI.Instance == null)
            return;

        // Guderian beklenen duruma (örneğin Hidden) geçti mi?
        if (GuderianAI.Instance.currentState == targetState)
        {
            conditionMet = true;
            OnConditionChanged?.Invoke(); // EventLogicController'a haber ver
        }
    }
}
