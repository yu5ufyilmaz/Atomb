using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ReactionZone : MonoBehaviour
{
    [Tooltip("Şu an bölgenin içinde bulunan tebeşir parçaları")]
    public List<DraggableFormula> currentItems = new List<DraggableFormula>();

    private void OnTriggerEnter(Collider other)
    {
        // Giren obje sürüklenebilir bir tebeşir parçası mı?
        DraggableFormula item = other.GetComponent<DraggableFormula>();
        if (item != null && !currentItems.Contains(item))
        {
            currentItems.Add(item);
            Debug.Log($"[ReactionZone] {item.formulaData.itemName} bölgeye girdi.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Çıkan objeyi listeden sil
        DraggableFormula item = other.GetComponent<DraggableFormula>();
        if (item != null && currentItems.Contains(item))
        {
            currentItems.Remove(item);
            Debug.Log($"[ReactionZone] {item.formulaData.itemName} bölgeden çıktı.");
        }
    }
}
