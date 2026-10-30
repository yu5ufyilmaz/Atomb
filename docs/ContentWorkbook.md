# Metin, altyazı ve kontrol çalışma kitabı

Ana dosya: **`Content/GameContent.xlsx`**. Düzenlemelerinizi burada yapın, kaydedin ve Excel'i kapatın. Unity'de **Senzora > İçerik Tablosu > Aç** menüsünden **Doğrula**, ardından **Oyuna Aktar** kullanın. Oyun Excel kurulu olmasını gerektirmez; aktarılmış JSON ve Unity verilerini okur.

## Hangi sekmede ne var?

| Sekme | Düzenleme |
|---|---|
| Metinler | Arayüz, kitap, puzzle, hedef, açıklama ve diğer metinlerin Türkçe/İngilizce karşılıkları. Kod ve Kaynak oyun çağrılarıyla bağlantıdır. |
| Diyaloglar | Konuşma kodu, açıklama notu, isteğe bağlı ses dosyasının `Assets/...` proje yolu. |
| Altyazilar | Her satır bir cümle. Diyalog aynı konuşmaya bağlar; Sıra 1, 2, 3...; Süre saniyedir. Türkçe/İngilizce ve isteğe bağlı Almanca aynı satırdadır. |
| Baglantilar | DialogAction satırlarında giriş konuşması, iki hatırlatma ve bekleme süresi. Diğer alanlar ve `salt okunur` satırlar envanterdir. Koşullar Unity Inspector'da kalır. |
| Kontroller | Sabit eylem ve bağlama kimliğine karşılık gelen varsayılan tuş yolu. `Değiştirilebilir` çalışma zamanındaki yeniden atama iznidir. Eylem, kimlik, bağlam ve parça sütunlarını değiştirmeyin. |
| Kilavuz | Dosyanın içinde de taşınan kısa kullanım açıklamaları. |

Sarı alanlar düzenleme alanıdır; gri alanlar kimlik/kaynak/bağlantı bilgisidir. Boş çeviriler ayrıca işaretlenir. Dosyada formül kullanmayın; metin ve sayısal değer girin. Hücre içi satır sonları desteklenir. Satırları Excel filtresiyle sıralamak diyalog sırasını değiştirmez; **Sıra** alanı belirler.

## Sıralı konuşma örneği

`Diyaloglar` içinde `lab_intro` kodunu oluşturduktan sonra `Altyazilar` içinde:

| Diyalog | Sıra | Süre (sn) | Türkçe | İngilizce |
|---|---:|---:|---|---|
| lab_intro | 1 | 3 | Laboratuvara bakmalıyım. | I should inspect the laboratory. |
| lab_intro | 2 | 4 | `[{input:ToggleNotebook}]` ile defterimi açabilirim. | I can open my notebook with `[{input:ToggleNotebook}]`. |

Mevcut bir `DialogAction` bağlantısının Giriş alanına `lab_intro` yazın. Yeni event oluşturmak gerekiyorsa Unity'de mevcut event sistemini kullanın; ardından **Bağlantı Envanterini Yenile** ile yeni bağlantıyı tabloya alın. Bu işlem var olan hedeflerin Excel'deki konuşma seçimlerini ve diğer sekmeleri korur. Silinmiş hedeflerin satırları kaybolmaz; doğrulama bildirir, siz bilinçli olarak kaldırırsınız.

JSON'un `subtitle.<diyalog>.0` gibi teknik anahtarları otomatik oluşur. Excel'deki sıra 1'den, JSON'daki segment indeksi 0'dan başlar. Aynı altyazıyı ayrıca Metinler sekmesine eklemeyin.

## Tuşlar ve ipuçları

Metne gerçek harf yazmak yerine **`{input:Cancel}`** gibi eylem kodunu yazın. `[{input:Cancel}] Kitabı kapat` varsayılan olarak `[F] Kitabı kapat` görünür; Cancel G'ye atanırsa `[G] Kitabı kapat` olur. Dil ve atama değişimleri açık yazılara uygulanır.

Sık kullanılan eylemler: `Interact`, `Cancel`, `ToggleNotebook`, `PreviousPage`, `NextPage`, `PreviousTab`, `NextTab`, `DeviceUp`, `DeviceDown`, `DeviceLeft`, `DeviceRight`, `Confirm`, `Peek`, `ChargeFlashlight`, `Scroll`. Bunlar çevrilmez. `{0}` gibi diğer metin değişkenleri de her dilde korunmalıdır.

Kontrol yolu örnekleri: `<Keyboard>/f`, `<Keyboard>/g`, `<Keyboard>/tab`, `<Mouse>/leftButton`. Yönlü hareketin `Move` satırlarında Parça sütunu `up/down/left/right` olur. Bir tuş birden fazla bağlamda kullanılabilir; yürürken W ile saklanırken gözetlemek ayrı eylemlerdir. Excel varsayılanları oyuncunun kendi kayıtlı atamasını sıfırlamaz.

Tuş resmiyle çizilmiş mevcut kontrol panellerinde `InputBindingIcon` ilgili bağlamayı izler. Atama görselin üzerindeki tuşla aynıysa özgün çizim kullanılır; değişince güncel tuş yazısı görünür. Yeni panellerde aynı bileşen Inspector'dan bağlanabilir.

Oyuncu **Ayarlar > Kontroller** sekmesinden mevcut atamalarını değiştirebilir. Aynı altyapı başka koddan da kullanılabilir:

```csharp
GameInput.Rebind("Cancel", "<Keyboard>/g");
GameInput.RebindBinding(bindingGuid, "<Keyboard>/g"); // Hareket parçaları dahil tek bağlama.
GameInput.ResetBinding(bindingGuid);
GameInput.ResetBindings();
bool justPressed = GameInput.Pressed("Cancel");
bool isHeld = GameInput.Held("Peek");
bool justReleased = GameInput.Released("Interact");
```

Atamalar Input System'in override biçimiyle `PlayerPrefs` içinde `Senzora.InputOverrides.v1` anahtarında tutulur. `StarterAssetsInputs` etkinleşince oyuncunun InputActionAsset örneğine de uygulanır. Bağlamsal okuyucu, cihaz etkileşimi sırasında StarterAssetsInputs kapalı olsa da çalışır. Menüdeki gerçek pointer tıklaması ve özel debug kısayolları dünya etkileşimine yeniden bağlanmaz.

## Aktarım kuralları

- Play modundan çıkın. Kaydedilmemiş sahne, altyazı asseti veya açık prefab düzenlemesi varsa aktarım durur; otomatik kaydetmez.
- Geçersiz kimlik, eksik sıra, sıfır/negatif süre, uyumsuz kontrol yolu, bozuk değişken veya formül hücresi yapısal hatadır. Önce düzeltin.
- Mevcut metin/diyalog kodunu yalnız hücreyi değiştirerek yeniden adlandırmayın. Kullanan yerleri de Unity'de taşımanız gerekir.
- Eksik çeviri uyarıları görünür kalır; oyun mevcut dil geri dönüşünü kullanır. İki dil de boş bir altyazı segmenti süreyi korur, görünür yazı üretmez.
- Eski `pressure_warning` ve `breaker_trip` megafon referanslarının veritabanında karşılığı bulunmuyor. Bu satırlar salt okunur envanterde uyarıyla görünür; konuşma metni uydurulmaz. Unity'deki tüketici bilinçli olarak geçerli konuşmaya bağlanabilir.
- Altyazıların ses dosyası Diyaloglar'dan gelir. Bir eventte ayrıca `DialogAction.dialogClip` atanmışsa o override korunur ve Baglantilar'da görünür.
- `Content/GameContent.import.json`, son aktarımın izini tutar. Çalışma kitabı veya üretilen veri değişmişse pencere bildirir; eski aktarım ile build alınması engellenir. Bu dosyayı elle değiştirmeyin.

## Doğrulama

Unity: **Senzora > Doğrulama > İçerik Tablosu ve Tuşlar**. Düzenleme modunda çalışır. İzole kontroller, geçersiz aktarım, yazım öncesi dosya kilidi ve aktarım sonrası doğrulama hatasından geri dönüşü sınar; kendi geçici sahnesini kaldırır. Sonuç `Temp/content-workbook.result` içindedir.

**İçerik Tablosu Gerçek Oyun**, gerçek kitap çıkışında F → G değişimini, Input System basış/bırakışlarını, sıralı altyazı süre/dil geçişini ve tutorial regresyonlarını Play modunda sınar. Kalıcı oyun kaydı kapalıdır; sonunda önceki sahne düzenine döner. Sonuç `Temp/content-workbook-play.result` içindedir.

Bağımsız kontroller:

```powershell
pwsh -NoProfile -File tools/check-content-workbook.ps1
pwsh -NoProfile -File tools/check-input-hints.ps1
pwsh -NoProfile -File tools/check-localization.ps1
pwsh -NoProfile -File tools/check-tutorial-voice.ps1
```
