using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFormulaItem", menuName = "Senzora/Chalkboard/Formula Item")]
public class FormulaItemSO : ScriptableObject
{
    [Tooltip("Kodlarda kontrol etmek için benzersiz ID (örn: nitro_grubu)")]
    public string itemID;
    
    [Tooltip("Ekranda veya UI'da görünecek isim")]
    public string itemName;
    
    [Tooltip("Bu bir merkez çekirdek mi, yoksa eklenecek bir dal mı?")]
    public bool isCore; 
    
    [Tooltip("Kaç tane bağ yapabilir?")]
    public int connectionCapacity = 1; 
    
    [Tooltip("Tahtada yaratılacak 3D/2D tebeşir Prefab'ı")]
    public GameObject chalkPrefab; 

    [Header("Defter ve İpuçları")]
    [TextArea(2, 5)]
    [Tooltip("Kitapta görünen açıklama; deftere yalnızca özel blok kopyalanır.")]
    public string description;
    [Tooltip("Yanıltıcı/bozuk parçalar üretim için seçilmez, kitaplarda yine gösterilir.")]
    public bool isDecoy;
    [Tooltip("Deftere bırakılan bloğun el çizimi görsel varyasyonları.")]
    public Sprite[] sketchSprites = new Sprite[0];

    [Header("Başlangıç Uçları")]
    [Tooltip("Bu prefabın kendi FormulaNode dizisindeki kapalı uç indeksleri.")]
    public List<int> initiallyBlockedNodes = new List<int>();
    [Tooltip("Bu prefabın kendi FormulaNode dizisindeki kararsız uç indeksleri.")]
    public List<int> initiallyUnstableNodes = new List<int>();

    [Header("Prosedürel Ara Ürün Uyumluluğu")]
    [Tooltip("Ara ürünün miras aldığı parçanın bağlantı kimliği; normal assetlerde boş kalır.")]
    public string connectionAliasID;
    [Tooltip("Örnek katalog üretiminde aynı tipteki uçları serbestçe eşleştirir. Eski prefab kurallarını değiştirmez.")]
    public bool useUniversalNodeRules;
}
