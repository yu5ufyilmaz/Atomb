using UnityEngine;

// Added only to runtime pulse-* instances. Existing prefab node rules are preserved.
public sealed class SignalPort : MonoBehaviour
{
    public bool output;

    public static void Configure(DraggableFormula formula)
    {
        string id = formula.formulaData != null ? formula.formulaData.itemID : "";
        if (!SignalTrial.IsPart(id)) return;
        var nodes = formula.GetOwnNodes();
        int inputs = SignalTrial.Inputs(id), outputs = SignalTrial.Outputs(id);
        for (int i = 0; i < nodes.Length; i++)
        {
            bool active = i < inputs + outputs;
            nodes[i].gameObject.SetActive(active);
            if (!active) continue;
            bool isOutput = i >= inputs; int group = isOutput ? outputs : inputs; int index = isOutput ? i - inputs : i;
            nodes[i].transform.localPosition = new Vector3(isOutput ? -1.55f : 1.55f, group == 2 ? (index == 0 ? .65f : -.65f) : 0, 0);
            nodes[i].connectionType = ConnectionType.Normal; nodes[i].doubleBondSocket = false;
            var port = nodes[i].GetComponent<SignalPort>() ?? nodes[i].gameObject.AddComponent<SignalPort>();
            port.output = isOutput;
        }
    }

    public static bool Compatible(FormulaNode first, FormulaNode second)
    {
        var a = first.GetComponent<SignalPort>(); var b = second.GetComponent<SignalPort>();
        if (a == null && b == null) return true;
        return a != null && b != null && first.gameObject.activeInHierarchy && second.gameObject.activeInHierarchy
            && a.output != b.output && !first.isOccupied && !second.isOccupied;
    }
}
