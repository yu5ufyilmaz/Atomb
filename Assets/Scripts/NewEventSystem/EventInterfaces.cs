using System;

public interface ICondition
{
    event Action OnConditionChanged;
    bool IsMet();
}

public interface IAction
{
    void Execute();
}