using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TriggerCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;
    public string targetTag = "Player";

    private bool isInside = false;

    public bool IsMet() => isInside;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isInside = true;
            OnConditionChanged?.Invoke(); // Merkeze "Durum değişti!" haberini yolla
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            isInside = false;
            OnConditionChanged?.Invoke(); // Merkeze "Durum değişti!" haberini yolla
        }
    }
}
