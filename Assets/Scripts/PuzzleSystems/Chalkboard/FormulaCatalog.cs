using System.Collections.Generic;
using UnityEngine;

// Resolves the current session's experiment parts for books, notebook and board snapshots.
public sealed class FormulaCatalog : MonoBehaviour
{
    public static FormulaCatalog Instance { get; private set; }
    public List<FormulaItemSO> formulaCatalog = new List<FormulaItemSO>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void RegisterFormula(FormulaItemSO formula)
    {
        if (formula == null || string.IsNullOrEmpty(formula.itemID)) return;
        foreach (var existing in formulaCatalog)
            if (existing != null && existing.itemID == formula.itemID) return;
        formulaCatalog.Add(formula);
    }

    public void RegisterCatalog(IEnumerable<FormulaItemSO> catalog)
    {
        if (catalog == null || ReferenceEquals(catalog, formulaCatalog)) return;
        foreach (var formula in catalog) RegisterFormula(formula);
    }

    public void ReplaceCatalog(IEnumerable<FormulaItemSO> catalog)
    {
        formulaCatalog.Clear();
        RegisterCatalog(catalog);
    }

    public FormulaItemSO ResolveFormula(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var formula in formulaCatalog)
            if (formula != null && formula.itemID == id) return formula;
        return null;
    }
}
