using UnityEngine;

// ISaveable eklendi!
public class PuzzleInventoryManager : MonoBehaviour, ISaveable
{
    public static PuzzleInventoryManager Instance;

    [Header("Sembol Envanteri")]
    public bool hasSymbol = false;
    public int currentSymbolID = -1;

    [Header("Overlay Durumu")]
    public bool isOverlayActive = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void PickupSymbol(int symbolID)
    {
        hasSymbol = true;
        currentSymbolID = symbolID;
        Debug.Log($"[PuzzleInventory] Sembol alındı! ID: {symbolID}");
    }

    public void RemoveSymbol()
    {
        hasSymbol = false;
        currentSymbolID = -1;
        isOverlayActive = false;
    }

    // ==========================================
    // ISAVEABLE ARAYÜZÜ ENTEGRASYONU (YENİ)
    // ==========================================
    public void LoadData(GameData data)
    {
        this.hasSymbol = data.hasSymbol;
        this.currentSymbolID = data.currentSymbolID;
        Debug.Log($"[SaveSystem] Envanter Yüklendi. Sembol Var: {hasSymbol} ID: {currentSymbolID}");
    }

    public void SaveData(ref GameData data)
    {
        // Şu anki envanter durumunu kayda yaz
        data.hasSymbol = this.hasSymbol;
        data.currentSymbolID = this.currentSymbolID;
    }
}
