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

    [Header("Bağ Görünümü")]
    [Tooltip("Çift bağ gerektiren yuvaların FormulaNode dizisindeki indeksleri. 0/2, 1/2, 2/2 olarak işaretlenir.")]
    public List<int> doubleBondNodes = new List<int>();
    // Eski asset/kayıt uyumluluğu; kapalı uç açma mekaniği artık kullanılmaz.
    [HideInInspector]
    public List<int> initiallyBlockedNodes = new List<int>();
    [HideInInspector]
    public List<int> initiallyUnstableNodes = new List<int>();

    [Header("Prosedürel Ara Ürün Uyumluluğu")]
    [Tooltip("Ara ürünün miras aldığı parçanın bağlantı kimliği; normal assetlerde boş kalır.")]
    public string connectionAliasID;
    [Tooltip("Örnek katalog üretiminde aynı tipteki uçları serbestçe eşleştirir. Eski prefab kurallarını değiştirmez.")]
    public bool useUniversalNodeRules;
}
