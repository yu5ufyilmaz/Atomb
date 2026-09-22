using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PuzzleInventoryManager : MonoBehaviour, ISaveable
{
    public static PuzzleInventoryManager Instance;

    [Header("Veritabanı (Database)")]
    [Tooltip("Oyundaki tüm PuzzleItemSO'ları buraya sürükle (Kayıt sistemi için şart)")]
    public List<PuzzleItemSO> allItemsDatabase = new List<PuzzleItemSO>();

    [Header("Envanter (Geleceğe Hazırlık)")]
    public List<PuzzleItemSO> inventoryItems = new List<PuzzleItemSO>();

    [Tooltip("Oyuncunun şu an elinde tuttuğu / seçili olan eşya")]
    public PuzzleItemSO activeItem;

    [Header("Overlay Durumu")]
    public bool isOverlayActive = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // YENİ ALMA MANTIĞI
    public void PickupItem(PuzzleItemSO item)
    {
        if (!inventoryItems.Contains(item))
        {
            inventoryItems.Add(item);
        }
        if (item.notebookSymbolID != -1 && NotebookUI.Instance != null)
        {
            NotebookUI.Instance.UnlockSymbolResearch(item.notebookSymbolID);
        }
        // Şimdilik tek kapasite mantığında çalıştığı için aldığımızı direkt eline veriyoruz
        activeItem = item;
        Debug.Log($"[PuzzleInventory] Eşya alındı: {item.itemName}");
    }

    public void RemoveActiveItem()
    {
        if (activeItem != null)
        {
            inventoryItems.Remove(activeItem);
            activeItem = null;
            isOverlayActive = false;
        }
    }

    public bool HasActiveItem() => activeItem != null;

    // ==========================================
    // ISAVEABLE (YENİ SİSTEME GÖRE GÜNCELLENDİ)
    // ==========================================
    public void LoadData(GameData data)
    {
        inventoryItems.Clear();
        activeItem = null;

        // 1. Envanterdeki eşyaları ID'ye göre veritabanından bul ve yükle
        if (data.inventoryItemIDs != null)
        {
            foreach (string savedID in data.inventoryItemIDs)
            {
                PuzzleItemSO foundItem = allItemsDatabase.FirstOrDefault(x => x.itemID == savedID);
                if (foundItem != null)
                    inventoryItems.Add(foundItem);
            }
        }

        // 2. Aktif olan eşyayı ayarla
        if (!string.IsNullOrEmpty(data.spawnedItemID))
        {
            activeItem = inventoryItems.FirstOrDefault(x => x.itemID == data.spawnedItemID);
        }

        Debug.Log(
            $"[SaveSystem] Envanter Yüklendi. Aktif Eşya: {(activeItem != null ? activeItem.itemName : "Yok")}"
        );
    }

    public void SaveData(ref GameData data)
    {
        // Elimizdeki eşyaların sadece string ID'lerini kayda yazıyoruz
        data.inventoryItemIDs = inventoryItems.Select(x => x.itemID).ToList();
        data.spawnedItemID = activeItem != null ? activeItem.itemID : "";
    }
}
