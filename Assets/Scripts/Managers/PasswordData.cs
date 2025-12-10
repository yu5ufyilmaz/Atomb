using UnityEngine;

[CreateAssetMenu(
    fileName = "PasswordLocation",
    menuName = "Atomb/Password Location Data",
    order = 0
)]
public class PasswordData : ScriptableObject
{
    [Header("Görsel Ayarları")]
    public Texture2D pageTexture;

    [Tooltip("Texture içinde toplam kaç sayfa var? (Yatay şerit varsayılır)")]
    public int totalPages = 8; // Şu an 8 ama değişebilir dedin.

    [Tooltip("Şifrenin bulunduğu sayfa indeksi (0'dan başlar)")]
    public int passwordPage = 0;

    [Header("Otomatik Hesaplanacak Alan")]
    [Tooltip("Editörde çizilen alan (Sadece o sayfaya göre 0-1 arası değer)")]
    public Rect passwordHotspotUV;
}
