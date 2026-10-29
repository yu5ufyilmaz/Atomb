using System;
using System.Collections.Generic;
using UnityEngine;

// Kitap sayfasındaki gerçek görüntüden kırpılacak tek bir bölge.
[Serializable]
public class BookClueRegion
{
    [Tooltip("Yalnızca editörde ayırt etmek için (örn: \"Sayfa 3 sol üst\").")]
    public string note;
    [Tooltip("Bu bölgenin temsil ettiği parça. Defter ve tahta bu parçayı kullanır.")]
    public FormulaItemSO formula;
    [Min(0)] public int pageIndex;
    [Tooltip("Sayfa içindeki bölge (0-1, sol alt köşe başlangıç). Görsel editörden sürükleyerek seçin.")]
    public Rect regionUV = new Rect(0.25f, 0.35f, 0.5f, 0.3f);
}

[Serializable]
public struct LocalizedPageTexture
{
    public string languageCode;
    public Texture2D pageTexture;
}

// Eski PasswordData'nın yerini alan yapı: kitabın sayfa dokusu (atlas) ve üzerindeki parça bölgeleri.
// Bölgeler sayfada görünen gerçek görüntüden kırpılır; elle yerleştirilmiş kart kullanılmaz.
[CreateAssetMenu(fileName = "BookClueData", menuName = "Senzora/Book Clue Data", order = 1)]
public class BookClueData : ScriptableObject
{
    [Header("Görsel Kimlik")]
    [Tooltip("Sayfaların yan yana dizildiği atlas doku (kitap materyalindeki doku).")]
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
                if (entry.pageTexture != null && string.Equals(entry.languageCode, code, StringComparison.OrdinalIgnoreCase))
                    return entry.pageTexture;
            }
        }
        return pageTexture;
    }

    [Header("Parça Bölgeleri")]
    public List<BookClueRegion> regions = new List<BookClueRegion>();

    // Kayıtlı bloklar yalnızca ID ve bölge indeksini saklar; görüntü buradan çözülür.
    // Kitap sahnede başladığında kendi verisini kaydeder. ID, asset adıdır; benzersiz tutun.
    private static readonly Dictionary<string, BookClueData> registry = new Dictionary<string, BookClueData>();

    public string ID => name;

    public static void Register(BookClueData data)
    {
        if (data != null) registry[data.ID] = data;
    }

    public static BookClueData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return registry.TryGetValue(id, out var data) && data != null ? data : null;
    }

    public bool TryGetRegion(int index, out BookClueRegion region)
    {
        region = regions != null && index >= 0 && index < regions.Count ? regions[index] : null;
        return region != null;
    }

    // Atlas dokusu üzerinde (0-1 UV) bu bölgenin dikdörtgeni; RawImage.uvRect doğrudan kullanır.
    public Rect GetAtlasRect(BookClueRegion region)
    {
        float pageWidth = 1f / Mathf.Max(1, totalPages);
        return new Rect((region.pageIndex + region.regionUV.x) * pageWidth, region.regionUV.y,
            region.regionUV.width * pageWidth, region.regionUV.height);
    }

    public Vector2 GetPixelSize(BookClueRegion region)
    {
        Texture2D tex = GetPageTexture();
        if (tex == null) return Vector2.one;
        Rect atlas = GetAtlasRect(region);
        return new Vector2(Mathf.Max(1f, atlas.width * tex.width), Mathf.Max(1f, atlas.height * tex.height));
    }
}
