using UnityEngine;

// Her bir sembol için ayrı oluşturacağın dosya
[CreateAssetMenu(fileName = "NewSymbol", menuName = "Senzora/Notebook/Symbol Data")]
public class SymbolDataSO : ScriptableObject
{
    [Tooltip("Bu sembolün benzersiz ID'si")]
    public int symbolID;

    [TextArea(3, 10)]
    public string description;
}
