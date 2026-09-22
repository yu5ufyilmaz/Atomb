using UnityEngine;

[CreateAssetMenu(fileName = "NewPuzzleItem", menuName = "Senzora/Puzzle/Item Data")]
public class PuzzleItemSO : ScriptableObject
{
    [Tooltip("Kayıt sistemi için benzersiz ID (Örn: symbol_01, fuse_red)")]
    public string itemID;

    [Tooltip("Eşyanın ekranda görünecek ismi")]
    public string itemName = "Bilinmeyen Eşya";
    public int notebookSymbolID = -1;

    [TextArea(2, 4)]
    public string itemLore;

    [Header("Modeller")]
    [Tooltip("Yerde dururken (dünyada) görünecek model/prefab")]
    public GameObject worldPrefab;

    [Tooltip("Kitapta veya ekranda incelerken çıkacak olan model")]
    public GameObject overlayPrefab;

    [Header("Ekrana Geliş (Overlay) Ayarları")]
    [Tooltip("Ekrana ilk geldiğinde düz durması için gereken rotasyon düzeltmesi")]
    public Vector3 defaultRotationOffset = Vector3.zero;

    // "public Vector3 defaultScale" SATIRINI TAMAMEN SİLDİK.
}
