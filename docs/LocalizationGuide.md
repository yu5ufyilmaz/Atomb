# Localization Guide (Dil ve Çeviri Rehberi)

Bu proje, çalışma zamanında sahneyi yeniden başlatmadan dil değiştirebilen, kaynak metin ve anahtar (key) tabanlı bir yerelleştirme (localization) mimarisi kullanır.

## 1. Dosya Yapısı
- **Dil Varlıkları (`LocalizationLanguage`)**: `Assets/Resources/Localization/Languages/` altında bulunur (`Turkish.asset`, `English.asset`).
- **Çeviri Tabloları (`JSON`)**: `Assets/Resources/Localization/Tables/` altında bulunur (`tr.json`, `en.json`).
- **Çalışma Zamanı Çözümleyici**:
  - `Assets/Scripts/Localization/GameLocalization.cs`
  - `Assets/Scripts/Localization/LocalizationCatalog.cs`
  - `Assets/Scripts/Localization/LocalizedText.cs`
  - `Assets/Scripts/Localization/LocalizedUIVariant.cs`
- **Doğrulama Aracı**: Unity üst menüsünden `Tools > Localization > Validate Localization Catalog` veya komut satırından `tools/check-localization.ps1`.

## 2. Yeni Metin veya Çeviri Ekleme
`Assets/Resources/Localization/Tables/en.json` ve `tr.json` dosyalarındaki `entries` dizisine yeni bir kayıt ekleyin:
```json
{
  "key": "ui.example.greeting",
  "source": "Merhaba Araştırmacı",
  "text": "Hello Researcher"
}
```
- **`key`**: Sabit anahtar (altyazılar veya özel anahtarlı çağrılar için).
- **`source`**: Kodda veya sahnede yazan özgün (kanonik) metin veya format şablonu (`"SAYFA {0}"`).
- **`text`**: İlgili dilde ekranda gösterilecek karşılığı (`"PAGE {0}"`).

> **Önemli Kural**: Kanonik bulmaca verileri (`PuzzleDefinitionSO`, `NotebookBlockData`, parmak izi / fingerprint hesapları, ölçüm kodları) asla doğrudan değiştirilmez; çeviri yalnızca UI sunum katmanında (`GameLocalization.Set`, `GameLocalization.SetFormat`, `GameLocalization.Translate`) uygulanır.

## 3. Kod İçinde Kullanım
- **TMP_Text Bileşenine Metin Atama**:
  ```csharp
  GameLocalization.Set(labelComponent, "ARAŞTIRMA DEFTERİ");
  ```
- **Parametreli Format Atama**:
  ```csharp
  GameLocalization.SetFormat(pageLabel, "SAYFA {0}", pageIndex + 1);
  ```
- **Anlık String Çevirisi (Prompt vb.)**:
  ```csharp
  return GameLocalization.Translate("[Sol Tık] Kitabı Aç");
  ```

## 4. Kitap Sayfası Görsellerini (Raster Texture) Yerelleştirme
`BookClueData` veya `PasswordData` ScriptableObject varlıklarında:
1. Varsayılan sayfa dokusunu `pageTexture` alanında tutun.
2. Dil bazlı alternatif görselleri Inspector'daki **`localizedPageTextures`** listesine (`languageCode = "en"`, `pageTexture = ...`) ekleyin.
3. UV koordinatları (`uvRect`) ve bulmaca kimlikleri tüm dillerde aynı kalır; dil değiştiğinde açık kitap ve defter önizlemesi otomatik güncellenir.

## 5. Yeni Bir Dil Ekleme
1. `Assets/Resources/Localization/Tables/` altına yeni dilin JSON tablosunu (örn. `de.json`) ekleyin.
2. Unity'de `Create > Senzora > Localization > Language` menüsüyle `Assets/Resources/Localization/Languages/` altında yeni bir `LocalizationLanguage` varlığı oluşturun (`code = "de"`, `nativeName = "Deutsch"`, `fallbackCode = "en"`).
3. `tables` dizisine oluşturduğunuz JSON dosyasını atayın. Ayarlar menüsündeki dil seçici yeni dili otomatik olarak listeler.

## 6. Font ve Yerleşimi Koruma
- Türkçe ve İngilizce dil varlıklarında `defaultFont` boş bırakılır; sahnede veya defter görünümünde seçilmiş font ve materyal korunur. Yalnızca gerçekten gerekli dil bazlı değişiklikleri `LocalizedText.overrides` üzerinden açın.
- DM Sans, Special Elite ve Liberation Sans fontlarında Türkçe karakterler kendi kaynak fontlarından dinamik olarak eklenir; atlas dolarsa aynı font için yeni atlas sayfası açılır. Dinamik atlasların okunabilir olması gerekir.
- Metin çevirisi, konum, boyut, hizalama veya font ayarlarını sıfırlamaz. Türkçe ayar satırları sahnedeki DM Sans fontu ve materyaliyle 30 punto, normal stil ve sola hizalı gösterilir; aynı sütundaki etiketler kontrol alanından önce ortak bir sol kenar kullanır. İngilizceye dönünce özgün ayarlar geri yüklenir.
- Ana menüdeki eşleşmiş `TextThin`/`TextBold` etiketleri, çeviride İngilizce metnin sol kenarını korur ve tek satıra sığar. İngilizceye dönünce özgün hizalama ve boyut geri yüklenir; Animator'ın konum animasyonları korunur.
- Görsel başlıklarda İngilizce özgün sprite kullanılır. Çeviri gerektiğinde başlığın kendi alanında, menüdeki mevcut font ve materyalle bir metin gösterilir; İngilizceye dönünce sprite geri gelir.
- `SignalPartLibrary`, `ColorMixtureLibrary`, `PuzzleDefinitionSO` veya kodda üretilen `ChalkboardTutorial` metinleri değişirse iki çeviri tablosunun `source` alanları da güncellenmelidir. `tools/check-localization.ps1` güncel 19 parçayı, aktif arşiv havuzu dahil 10 hedefin ad/açıklama ve gözlem notlarını, eğitim hedefi ve sayfalarını denetler.
- Unity'de `Tools > Localization > Validate Presentation` menüsü gerçek font atlaslarını, menünün render edilmiş sol kenarını, sahnedeki ana menü ve duraklatma ayar panellerini, başlık görsellerini ve parça/hedef/eğitim notlarının dil geçişlerini kontrol eder.
