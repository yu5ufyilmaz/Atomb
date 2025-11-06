using UnityEngine;

[CreateAssetMenu(fileName = "PasswordData", menuName = "Atomb/Password Data", order = 0)]
public class PasswordData : ScriptableObject
{
    [Tooltip("Not defterine eklenecek ve Turing makinesinde kontrol edilecek ID. Örn: INFINITY_=_123")]
    public string passwordID;

    [Tooltip("Bu şifreyi içeren sayfaların bulunduğu Texture Atlas (Pages0-7.png gibi)")]
    public Texture2D pageTexture;

    [Tooltip("Şifrenin bulunduğu sayfa (Shader'daki index'e göre. Genellikle sağ sayfalar 1, 3, 5...)")]
    public int passwordPage;

    [Tooltip("BU TEXTURE'A ÖZEL şifrenin tıklanabilir alanı (UV koordinatları)")]
    public Rect passwordHotspotUV;
}