using System.Collections.Generic;
using UnityEngine;

// 1. AÇILIR LİSTE (DROPDOWN) SEÇENEKLERİMİZ
public enum ConnectionType
{
    Normal,
    Core,
    Branch,
    DoubleBond,
    Special,
}

public class FormulaNode : MonoBehaviour
{
    [Header("Bağlantı Kuralları")]
    [Tooltip(
        "Eğer bu seçenek aktifse, aşağıdaki listeye bakmaz ve HER formülün bağlanmasına izin verir."
    )]
    public bool acceptAnyFormula = true;

    [Tooltip(
        "Sadece belirli formüllerin bağlanmasını istiyorsan 'Accept Any Formula' tikini kaldır ve SO dosyalarını buraya ekle."
    )]
    public List<FormulaItemSO> allowedFormulas = new List<FormulaItemSO>();

    // 2. ARTIK YAZI YAZMAK YOK, AÇILIR LİSTEDEN SEÇECEKSİN
    [Tooltip(
        "Kendi içindeki bağ türlerini eşlemek için (Örn: Sadece 'Core' bağları 'Core' ile birleşsin)"
    )]
    public ConnectionType connectionType = ConnectionType.Normal;

    public bool isOccupied => connectedNode != null;

    [HideInInspector]
    public FormulaNode connectedNode;

    [HideInInspector]
    public DraggableFormula parentFormula;

    private void Awake()
    {
        parentFormula = GetComponentInParent<DraggableFormula>();
    }

    public bool CanConnect(DraggableFormula otherFormula, FormulaNode otherNode)
    {
        if (isOccupied)
            return false;

        // Enum'lar birbiriyle eşleşmiyorsa direkt reddet
        if (this.connectionType != otherNode.connectionType)
            return false;

        if (acceptAnyFormula)
            return true;

        if (allowedFormulas.Count > 0)
        {
            bool isAllowed = false;
            foreach (var allowedSO in allowedFormulas)
            {
                if (allowedSO != null && allowedSO.itemID == otherFormula.formulaData.itemID)
                {
                    isAllowed = true;
                    break;
                }
            }
            if (!isAllowed)
                return false;
        }

        return true;
    }

    public void ConnectTo(FormulaNode otherNode)
    {
        connectedNode = otherNode;
        otherNode.connectedNode = this;
    }

    public void Disconnect()
    {
        if (connectedNode != null)
        {
            connectedNode.connectedNode = null;
            connectedNode = null;
        }
    }
}
