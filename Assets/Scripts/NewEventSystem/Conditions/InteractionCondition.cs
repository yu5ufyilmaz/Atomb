using System;
using UnityEngine;

public class InteractionCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip(
        "Hangi objeyle etkileşime girildiğinde bu şart sağlansın? Boş bırakılırsa bu scriptin eklendiği objeyi baz alır."
    )]
    public GameObject targetInteractable;

    private bool conditionMet = false;

    public bool IsMet() => conditionMet;

    private void Start()
    {
        // Eğer dışarıdan obje atanmadıysa, scriptin eklendiği objeyi hedef kabul et
        if (targetInteractable == null)
        {
            targetInteractable = gameObject;
        }

        // Tıklama olayını dinlemeye başla
        PlayerInteraction.OnPlayerInteracted += HandlePlayerInteracted;
    }

    private void HandlePlayerInteracted(GameObject interactedObj)
    {
        // Eğer tıklanan obje bizim hedefimizse şartı sağla
        if (interactedObj == targetInteractable)
        {
            conditionMet = true;
            OnConditionChanged?.Invoke(); // Merkeze haber ver
        }
    }

    private void OnDestroy()
    {
        // Hafıza sızıntısını önlemek için aboneliği kaldır
        PlayerInteraction.OnPlayerInteracted -= HandlePlayerInteracted;
    }
}
