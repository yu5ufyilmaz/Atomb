# Ayarlar menüsü

Ana menüden **Ayarlar**, oyun sırasında duraklatma menüsünden **Ayarlar** açılır. İki giriş aynı dört sekmeyi ve aynı oyuncu tercihlerini kullanır.

| Sekme | Ayarlar |
|---|---|
| Ses | Genel ses, müzik, efektler, konuşma |
| Görüntü | Pencere modu, çözünürlük, yenileme hızı, kalite, dikey eşitleme, FPS sınırı, parlaklık, hareket bulanıklığı, film greni, yürüme kamera sallantısı |
| Kontroller | Fare/gamepad hassasiyeti, ayrı ters Y eksenleri, analog ölü bölgesi, mevcut klavye/fare ve gamepad atamaları |
| Dil ve Altyazılar | Dil, altyazı görünürlüğü, boyutu, arka planı ve canlı örnek |

Ses, kontrol ve okunabilirlik değişiklikleri hemen uygulanır; kısa bir bekleme sonrası veya menü kapanırken kaydedilir. Pencere modu/çözünürlük/hız seçimi **Görüntüyü uygula** ile denenir. **Koru** seçilmezse 15 saniye sonunda önceki doğrulanmış ayara dönülür. Oyun duraklatıldığında da bu süre işler. Dikey eşitleme açıkken FPS sınırı ekran eşitlemesi tarafından belirlenir.

Uzun listeler fare tekerleği veya sağdaki kaydırma çubuğuyla gezilir. Klavye/gamepad odağı aşağıdaki bir ayara geçtiğinde liste kendiliğinden kayar. Gamepad LB/RB tuşları sekmeler arasında geçer. Açık seçim listesinden Escape ile çıkıldığında ayarlar menüsü açık kalır.

Tuş ataması için mevcut tuşun üzerine basıp yeni kontrolü seç. Escape/İptal veya zaman aşımı atamayı iptal eder. Aynı bağlamdaki çakışmalar uyarılır; farklı bağlamların paylaştığı tuşlar otomatik silinmez. Atama ve sıfırlama oyun içi tuş ipuçlarına da yansır. Gamepad için şu an oyunda bulunan hareket, bakış, zıplama ve koşma bağları yönetilir; fare konumuna bağlı bulmacalar bu menü değişikliğiyle sanal imlece dönüştürülmez.

**Bu bölümü sıfırla**, yalnız seçili bölümü sıfırlar. Kontroller için verilen onay kişisel tuş atamalarını da kapsar. Ses içermeyen zorunlu tutorial düşünceleri, altyazı tercihi kapalı olsa da gösterilir.

## İçerik ve düzenleme

- Yeni ayar etiketleri/onay metinleri `Content/GameContent.xlsx` dosyasının **Metinler** sekmesindedir; kodları `settings.` ile başlar. Mevcut içerik aktarımı aynı şekilde kullanılır.
- Görsel referanslar `Assets/Resources/Settings/SettingsMenuStyle.asset` içinde; ortak giriş prefabı `Assets/Resources/Settings/SettingsMenu.prefab` dosyasındadır. Mevcut DM Sans ve ayarlar çubuk/kutu görselleri kullanılır.
- Oyuncu tercihleri `Application.persistentDataPath/SettingsSave.json`, kişisel tuş atamaları mevcut `Senzora.InputOverrides.v1` PlayerPrefs kaydındadır. Bunlar içerik Excel'ine yazılmaz. Eski kayıtlar yüklenirken yeni alanlara varsayılanlar eklenir; bozuk kayıt yedeklenir.
- `Senzora/Ayarlar/Menüyü Kur`, paylaşılan arayüz ve sahne/ses bağlantılarını yeniden kurar. Açık kirli sahne varken ilerlemez; önce bilinçli olarak kaydetmek gerekir.
- `Senzora/Doğrulama/Sekmeli Ayarlar` izole kayıt/font/yerleşim kontrollerini ve PNG'leri; `Senzora/Doğrulama/Sekmeli Ayarlar Gerçek Oyun` gerçek ana menü/pause, ekran geri alma, tuş yakalama ve tüketici kontrollerini çalıştırır. Sonuçlar `Temp/settings-menu*.result` içindedir. Testler oyuncunun gerçek tercih ve tuş kayıtlarını korur.

## Doğrulama

29 Ekim 2026: Unity ayar testleri ve gerçek ana menü/pause akışı, kitap/tutorial regresyonları, çeviri sunumu ve Excel senkronu geçti. TR/EN dört sekme ve beş onay penceresi 1280×720, 1366×768, 1920×1080, 2560×1440 ve 2560×1080 için doğrulandı. Son C# derlemesi 0 hata verdi; projede derleyici ve önceki içerik uyarıları bulunuyor.

Gamepad giriş kontrolleri Input System'e gönderilen sanal cihaz olaylarıyla sınandı. Fiziksel gamepad hissi ve monitörün gerçek tam ekran/Hz geçişi donanım üzerinde ayrıca denenmelidir; Unity Editor bu fiziksel ekran geçişini uygulamaz.
