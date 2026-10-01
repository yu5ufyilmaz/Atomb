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
}