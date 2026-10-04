using System;
using UnityEngine;

public class EventLogicController : MonoBehaviour
{
    // Diğer olayların bu olayı dinleyebilmesi için event
    public event Action OnEventTriggered;

    public bool requireAllConditions = true;
    public bool triggerOnlyOnce = true;

    // Diğer scriptlerin okuyabilmesi için public property
    public bool HasTriggered { get; private set; } = false;

    private ICondition[] conditions;
    private IAction[] actions;

    private void Start()
    {
        conditions = GetComponents<ICondition>();
        actions = GetComponents<IAction>();

        foreach (var cond in conditions)
        {
            cond.OnConditionChanged += EvaluateConditions;
        }
    }

    private void EvaluateConditions()
    {
        if (HasTriggered && triggerOnlyOnce)
            return;

        if (CheckConditions())
        {
            HasTriggered = true;
            FireActions();
            OnEventTriggered?.Invoke(); // Görev bittiğinde tüm dinleyicilere bağırır
        }
    }

    private bool CheckConditions()
    {
        if (conditions.Length == 0)
            return false;

        foreach (var cond in conditions)
        {
            bool met = cond.IsMet();
            if (requireAllConditions && !met)
                return false;
            if (!requireAllConditions && met)
                return true;
        }
        return requireAllConditions;
    }

    private void FireActions()
    {
        foreach (var action in actions)
        {
            action.Execute();
        }
    }

    // Mekanik test sahnesinde koşulları beklemeden bu event zincirini sınamak için.
    public void DebugTrigger()
    {
        if (actions == null)
            actions = GetComponents<IAction>();

        HasTriggered = true;
        FireActions();
        OnEventTriggered?.Invoke();
    }

    public void DebugReset()
    {
        HasTriggered = false;
    }

    private void OnDestroy()
    {
        if (conditions == null)
            return;

        foreach (var cond in conditions)
        {
            if (cond != null)
                cond.OnConditionChanged -= EvaluateConditions;
        }
    }
}
