using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PasswordLocationEntry
{
    public string note; // Örn: "Sayfa 3 Sağ Üst" (Kendin için not)
    public int pageIndex;
    public Rect hotspotUV;
}

[CreateAssetMenu(fileName = "PasswordData", menuName = "Atomb/Password Data", order = 0)]
public class PasswordData : ScriptableObject
{
    [Header("Görsel Kimlik")]
    public Texture2D pageTexture;
    public int totalPages = 8;

    [Header("Olası Şifre Konumları")]
    [Tooltip("Bu kitap türü için şifrenin çıkabileceği TÜM olası yerleri buraya ekle.")]
    public List<PasswordLocationEntry> possibleLocations = new List<PasswordLocationEntry>();
}
