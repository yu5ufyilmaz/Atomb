using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ReactionZone : MonoBehaviour
{
    [Tooltip("Şu an bölgenin içinde bulunan tebeşir parçaları")]
    public List<DraggableFormula> currentItems = new List<DraggableFormula>();
    private Collider zoneCollider;

    private void Awake() { zoneCollider = GetComponent<Collider>(); }

    public bool ContainsFormula(DraggableFormula formula)
    {
        if (formula == null || !formula.gameObject.activeInHierarchy) return false;
        if (zoneCollider == null) zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null || !zoneCollider.enabled) return false;
        Vector3 center = formula.transform.position;
        ChalkboardManager manager = ChalkboardManager.Instance;
        if (manager != null && manager.boardTransform != null)
        {
            // Bulmaca iki boyutludur; eski sahnede çember collider'ı tahtanın arkasındadır.
            Vector3 normal = manager.boardTransform.forward;
            center -= normal * Vector3.Dot(center - zoneCollider.bounds.center, normal);
        }
        return (zoneCollider.ClosestPoint(center) - center).sqrMagnitude < 0.000001f;
    }

    // Transform ile taşınan collider'lar Rigidbody olmasa da son konumdan doğrulanır.
    public void RefreshItems()
    {
        Physics.SyncTransforms();
        currentItems.Clear();
        ChalkboardManager manager = ChalkboardManager.Instance;
        if (manager == null || manager.boardTransform == null) return;
        foreach (DraggableFormula formula in manager.boardTransform.GetComponentsInChildren<DraggableFormula>())
            if (ContainsFormula(formula)) currentItems.Add(formula);
    }

    private void OnTriggerEnter(Collider other)
    {
        DraggableFormula formula = other.GetComponentInParent<DraggableFormula>();
        if (ContainsFormula(formula) && !currentItems.Contains(formula)) currentItems.Add(formula);
    }

    private void OnTriggerExit(Collider other)
    {
        DraggableFormula formula = other.GetComponentInParent<DraggableFormula>();
        if (formula != null && !ContainsFormula(formula)) currentItems.Remove(formula);
    }
}
