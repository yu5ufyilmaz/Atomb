using UnityEngine;

public class PuzzleInventoryManager : MonoBehaviour
{
    public static PuzzleInventoryManager Instance { get; private set; }

    [Header("Sembol Envanteri")]
    public bool hasSymbol = false;
    public int currentSymbolID = -1; // -1: Sembol yok, 0-3: Semboller

    [Header("Overlay Durumu")]
    public bool isOverlayActive = false; // 3D obje ekranda aktif mi?

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // Sembolü dünyadan topladığımızda çağrılacak
    public void PickupSymbol(int symbolID)
    {
        hasSymbol = true;
        currentSymbolID = symbolID;
        Debug.Log($"[PuzzleInventory] Sembol alındı! ID: {symbolID}");
    }

    // Bulmaca çözüldüğünde sembolü silmek için
    public void RemoveSymbol()
    {
        hasSymbol = false;
        currentSymbolID = -1;
        isOverlayActive = false;
    }
}
