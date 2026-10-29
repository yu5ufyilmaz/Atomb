using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PasswordLocationEntry
{
    public string note; // Örn: "Sayfa 3 Sağ Üst"
    public int pageIndex;
    public Rect hotspotUV;
}

[CreateAssetMenu(fileName = "PasswordData", menuName = "Senzora/Password Data", order = 0)]
public class PasswordData : ScriptableObject
{
    [Header("Görsel Kimlik")]
    public Texture2D pageTexture;
    public List<LocalizedPageTexture> localizedPageTextures = new List<LocalizedPageTexture>();
    public int totalPages = 8;

    public Texture2D GetPageTexture(string languageCode = null)
    {
        string code = string.IsNullOrWhiteSpace(languageCode) ? GameLocalization.CurrentCode : languageCode;
        if (localizedPageTextures != null && !string.IsNullOrWhiteSpace(code))
        {
            for (int i = 0; i < localizedPageTextures.Count; i++)
            {
                var entry = localizedPageTextures[i];
                if (entry.pageTexture != null && string.Equals(entry.languageCode, code, System.StringComparison.OrdinalIgnoreCase))
                    return entry.pageTexture;
            }
        }
        return pageTexture;
    }

    [Header("Tutorial Ayarları")]
    [Tooltip("Eğer bu işaretlenirse, bu kitap sabit bir şifre taşır ve oyun sayacını etkilemez.")]
    public bool isTutorialData = false;

    [Tooltip("Tutorial için sabit şifre (Örn: START_=_001)")]
    public string tutorialPasswordID = "START_=_001";

    [Header("Olası Şifre Konumları")]
    [Tooltip("Bu kitap türü için şifrenin çıkabileceği TÜM olası yerleri buraya ekle.")]
    public List<PasswordLocationEntry> possibleLocations = new List<PasswordLocationEntry>();
}
