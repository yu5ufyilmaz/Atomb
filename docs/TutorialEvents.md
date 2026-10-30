# Tutorial eventleri ve konuşmalar

Sahne: `Assets/Scenes/scn_LaboratorySynthesis.unity`.

`TutorialSequence` altındaki mevcut eventler ilerlemeyi, whitelist'i, kamera ve düşman aksiyonlarını yönetir. `Konuşma Eventleri` altındaki 25 event konuşmaları yönetir. Çalışma zamanında `TutorialManager` tarafından event diyaloglarının yerine geçen ayrı bir anlatıcı bulunmaz.

Her konuşma düğümünde:

- `EventLogicController` mevcut koşul sistemini kullanır.
- İlk `PrerequisiteCondition` konuşmanın başlayacağı eventi, ikincisi biteceği eventi seçer. `Require Triggered` kapalıyken eventin henüz tetiklenmemiş olması gerekir.
- Gereken düğümlerde `ChalkboardTutorialCondition` kitabın, kartların, tahtanın veya ölçüm okuyucusunun canlı durumunu gözler. Diyalog metni veya event sırası bu bileşende tutulmaz.
- `DialogAction`: giriş için `Subtitle ID`, farklı hatırlatmalar için `Reminder Subtitle IDs`, bekleme için `Reminder Delay`. Kayıtlar ve çeviriler `MainSubtitles.asset` içindedir. `Follow Conditions` kapalıyken önceki tek seferlik diyalog davranışı sürer; açıkken eventin AND/OR koşulları yeniden değerlendirilir. Bu özellik tutorial dışındaki eventlerde de kullanılabilir.

Her aksiyon kendi konuşma geçmişini tutar. Kitabı kapatıp açmak veya aynı tutorial oturumunu kapatıp açmak giriş repliğini tekrar oynatmaz. Yeni araştırma oturumu geçmişi sıfırlar. Koşulun kaybolması yalnızca o aksiyona ait konuşmayı durdurur; başka konuşmalar ve duraklama sırasında hatırlatma sayacı işlemez. İlerleme, hatırlatma beklemesini yeniler. Eski, çakışan tutorial `DialogAction` bileşenleri sahnede kapalı tutulur; diğer aksiyonlar korunur.

`LookAction`, gecikmesi dahil kamera odağı boyunca dünya etkileşimini kilitler. Peş peşe gelen kamera aksiyonları sırayla çalışır. Aksiyon kapatılırsa veya hedefi yok olursa kilit bırakılır. Açılış sekansı ve duraklama sırasında kitap alınamaz.

Sahneyi yeniden bağlamak için: `Senzora > Tutorial > Konuşmaları Event Sistemine Bağla`. Bu bir Editor hazırlık aracıdır; oyunda düğüm üretmez. Mevcut düğümlerin Inspector ayarlarını standart değerlere döndürür.

Doğrulama menüleri: `Senzora > Doğrulama > Tutorial İç Ses` (koşullar/veri), `Tutorial İç Ses Gerçek Oyun` (erken kitap, kamera sırası/iptali, gerçek kitap tekrarları, hatırlatma ve dış konuşma), `Tutorial Gerçek Oyun Akışı` (kitaptan sigortaya tam akış). Testten önce Play'e gir; gerçek oyun testi geliştirme oturumunu kullanır.

## Yeni oyunda öğretici seçimi

`MainMenuManager.PlayGame`, yeni oyunu ve açılışı başlatmadan önce öğretici sorusunu gösterir. Evet öğretici eventlerini ve alıştırmasını açar; Hayır öğretici kısıtlamalarını kaldırıp normal sigorta, basınç ve düşman sistemlerini açar. Öğretici başlangıçta kapalı olan sahneler de desteklenir. İki seçimde de mevcut açılış oynar ve oyuncu kontrolü açılış bitince açılır. Kayıttan devam etme akışı soru göstermez.

`TutorialChoicePrompt`, menünün yazı tipi ve renkleriyle çalışma zamanında oluşturulur; sahnelere elle UI bağlamak gerekmez. Metinler `tr.json` ve `en.json` üzerinden çevrilir. Menüye Dön, Escape ve gamepad geri tuşu seçim yapmadan menüye döner. Seçim onaylanana kadar yeni oyun kaydı sıfırlanmaz.

`Senzora > Doğrulama > Öğretici Seçimi - Arayüz`, `Öğretici Seçimi - Evet Play` ve `Öğretici Seçimi - Hayır Play` kontrolleri eklenmiştir. Play kontrollerini menüde, yeni bir Play oturumunda başlat; kayıtları korumak için `sessionOnly` kullanırlar. Gerçek açılış, öğretici etkinliği, araştırma alıştırmasının seçime uyması, normal sistemler, iptal, çift gönderim ve iki dilde taşma kontrol edilir. Sonuçlar `outputs/tutorial-choice-*-checks.txt`, ekran görüntüleri `outputs/tutorial-choice-{tr,en}.png` konumundadır.
