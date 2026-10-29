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
        Subscribe();
        EvaluateConditions();
    }

    private void OnEnable() { if (conditions != null) { Subscribe(); EvaluateConditions(); } }

    private void Subscribe()
    {
        foreach (var cond in conditions)
        {
            cond.OnConditionChanged -= EvaluateConditions;
            cond.OnConditionChanged += EvaluateConditions;
        }
    }

    private void EvaluateConditions()
    {
        if (!isActiveAndEnabled) return;
        var tutorial = TutorialManager.Instance;
        if (tutorial != null && tutorial.IsRunning && transform.IsChildOf(tutorial.tutorialEventsParent.transform)
            && GameManager.Instance != null && !GameManager.Instance.isGameStarted) return;
        if (HasTriggered && triggerOnlyOnce)
            return;

        if (CheckConditions())
        {
            HasTriggered = true;
            FireActions();
            OnEventTriggered?.Invoke(); // Görev bittiğinde tüm dinleyicilere bağırır
        }
    }

    public void RefreshConditions() { if (conditions != null) EvaluateConditions(); }
    public bool ConditionsAreMet => isActiveAndEnabled && conditions != null && CheckConditions();

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
            if (action is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
            action.Execute();
        }
    }

    private void OnDisable()
    {
        if (conditions == null) return;
        foreach (var cond in conditions)
        {
            if (cond != null)
                cond.OnConditionChanged -= EvaluateConditions;
        }
    }
}
