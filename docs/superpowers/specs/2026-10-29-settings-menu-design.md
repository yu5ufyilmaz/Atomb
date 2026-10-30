# Sekmeli ayarlar menüsü

Durum: Kullanıcı 2026-10-29 tarihinde tasarımı ve uygulamayı onayladı. Tarayıcı taslağı yalnızca yerleşim ve etkileşim örneğidir; oyunun ayarlarını değiştirmez.

Yerleşim taslağı: `.superpowers/brainstorm/571-1793248961/content/settings-layout.html`. Mevcut DM Sans font dosyası ve ayarlar görselleriyle hazırlanmış, TR/EN arasında geçiş yapılabilen, dört sekmeli yerel önizleme. Sunucu durursa aynı proje diziniyle yeniden başlatılarak açılabilir.

## Amaç

Ses, görüntü, oyun kontrolleri, dil ve altyazı ayarlarını mevcut oyunun görsel diliyle tek, düzenli menüden yönetmek. Ana menü ve oyun içi duraklatma aynı ayarlar arayüzünü kullanmalı. Her gösterilen seçenek gerçek oyun davranışına bağlı olmalı. Türkçe karakterlerde font değişmesi, uzun çevirilerde hizanın bozulması ve kayıtlı değerin menüde yanlış gösterilmesi tekrar etmemeli.

Kullanıcının kararları: sekmeli menü; mevcut UI malzemeleri kullanılacak; tarayıcıda taslak gösterilecek. Aşağıdaki seçenekler ve ayrıntılar uygulayıcının önerisidir; önceki Excel tasarımı bunlara otomatik onay sayılmaz.

## Mevcut durum ve kısıtlar

- Unity 6000.3.10f1, uGUI/TMP, HDRP ve Input System kullanılıyor. Yeni UI paketi veya oyun çalışırken Excel okuyucusu gerekmiyor.
- `SettingsManager` genel/müzik/efekt sesi, kalite, çözünürlük, tam ekran, parlaklık, fare hassasiyeti, dil ve altyazı görünürlüğünü `SettingsSave.json` içinde saklıyor.
- Ses mikserinde Master/Music/SFX var; ayrı konuşma grubu yok. Sonradan oluşturulan DialogAction sesleri de hesaba katılmalı.
- Kalite adları gerçek asset sırasıyla High Fidelity, Balanced, Performant. Ekrandaki seçim bu isim/kimlikle eşleşmeli; yüksek kaliteyi index 2 varsaymak yanlış.
- `GameInput` yeniden atama, kalıcı override, tek atamayı/tüm atamaları sıfırlama ve canlı tuş ipucu yenilemesini zaten sağlıyor. Yeni bir paralel kontrol sistemi kurulmayacak.
- Gamepad hareket/bakış/zıplama/koşma bağları mevcut. Tüm oyunu gamepad ile bitirme desteği mevcut değil; bazı bulmacalar doğrudan fare konumu kullanıyor. Bu görev gamepad ayarlarını ve ayarlar menüsünde gamepad gezinmesini kapsar; bütün bulmacaları sanal imlece taşıma ayrı bir gameplay işi olarak kalır. Menü tam gamepad uyumluluğu iddia etmez.
- GlobalSubtitleManager ve bazı efekt/kamera olayları sahne/oynanış tarafından yönetiliyor. Ayarlar, tutorial sırasını veya korku olaylarının zamanlamasını değiştirmeyecek.
- `Content/GameContent.xlsx` çeviriler ve varsayılan kontrol politikalarının ana kaynağıdır. Oyuncunun kişisel ayarları bu Excel'e yazılmaz.
- Çalışma alanındaki önceki/user değişiklikleri korunacak; sahne/prefab düzenleme öncesi açık kirli sahneler sessizce kaydedilmeyecek. Kaydedilmemiş sahne gerekiyorsa uygulama o kısmı açıkça bildirecek.

## Değerlendirilen yaklaşımlar

1. **Sekmeli menü — kullanıcının seçimi.** Dört ana bölüm, daha kısa sayfalar ve kontrol atamaları için içeride kaydırma. Seçenek sayısı arttığında okunabilirliği korur; sekmeler ve odak sırası eklenmelidir.
2. Tek kaydırılan sayfa. Mevcut düzene daha az yapısal değişiklik getirir; ses ile kontrol atamaları arasında uzun kaydırma gerektirir. Kullanıcı bunu seçmedi.

## Görsel düzen

- Solda dört sekme: Ses, Görüntü, Kontroller, Dil ve Altyazılar. Sağda seçili bölümün başlığı ve hizalı satırları. Alt kısımda Geri, Bu Bölümü Sıfırla ve gerekiyorsa Görüntüyü Uygula. Global ayarları topluca silen tek tık butonu yok.
- Oyunun mevcut laboratuvar arka planı ana menüde, mevcut oyun görüntüsü duraklatmada kalır. Üzerine okunabilirliği sağlayan mevcut koyu perde/çizgi stili gelir.
- DM Sans ailesindeki mevcut TMP fontları; normal/hover ağırlığı mevcut stile uygun. BarInside/BarOutside, Box/x, QualityFrame/QualityArrow gibi mevcut malzemeler yeniden kullanılır. Yeni metinler, dil değişebilen yazı görselleri yerine TMP olur.
- Satırların etiket ve kontrol sütunu sabittir. Çeviri uzun diye font küçültmek veya başka fonta geçmek yerine etiket alanı yeterli genişlikte tutulur. Gerekli açıklamalar ayrı küçük satırdır.
- 1920x1080 referans CanvasScaler; 1280x720, 1366x768, 1920x1080 ve 2560x1440 görünümde doğrulama. Ultrawide'da okunabilir içerik genişliği sınırlı, arka plan esner. Kontrol listesi gerektiğinde kayar; sekmeler ve alt işlem satırı görünür kalır.
- Fare, klavye ve gamepad ile seçme/geri dönüş; görünür odak ve seçili sekme. ScrollRect odaklanan satırı görünür alana getirir. Açılışta ilk kullanılabilir kontrol seçilir, kapanışta çağıran menüye odak döner.

## Sekmeler ve gerçek işlevler

### Ses

| Seçenek | Aralık / davranış |
|---|---|
| Genel ses | %0–100, mikser Master; sıfır gerçekten sessiz |
| Müzik | %0–100, Music |
| Efektler | %0–100, SFX |
| Konuşma | %0–100, yeni Voice grubu; mevcut konuşma kaynakları ve dinamik DialogAction kaynakları yönlendirilir |

Konuşma sesi SFX altında ezilmez; Master altında ayrı kardeş gruptur. Konuşma kaynaklarının mevcut clip/loop/spatial/pause değerleri korunur. Bu görevde bulunamayan diğer sesler isim tahminiyle konuşma sayılmaz. Testte normal diyalog, tutorial iç ses ve sonradan oluşturulan ses kaynağı kapsanır.

### Görüntü

| Seçenek | Aralık / davranış |
|---|---|
| Ekran modu | Pencereli, kenarlıksız tam ekran, özel tam ekran; desteklenmeyen platform modu listelenmez |
| Çözünürlük | Monitörün desteklediği benzersiz genişlik/yükseklik çiftleri; geçerli yenileme hızıyla uygulanır |
| Yenileme hızı | Seçili çözünürlüğün desteklediği hızlar; kenarlıksız modda masaüstü hızı kullanıldığı açıklanır |
| Görüntü kalitesi | Gerçek üç preset: Yüksek / Dengeli / Performans; doğru QualitySettings kimliği |
| Dikey eşitleme | Açık/Kapalı; açıkken FPS sınırı etkisiz olarak belirtilir |
| FPS sınırı | 30, 60, 90, 120, 144, 165, 240, Sınırsız; vSync kapalıyken uygulanır |
| Parlaklık | -2…+2 pozlama, varsayılan 0; sahneye geçince yeniden uygulanır |
| Hareket bulanıklığı | Açık/Kapalı; HDRP MotionBlur gerçek override'ı |
| Film greni | Açık/Kapalı; HDRP FilmGrain override'ı |
| Yürüme kamera sallantısı | %0–100; mevcut head bob genliğini ölçekler, 0 nötr konuma döner |

Çözünürlük/mod/hız seçimleri taslak değerdir; Görüntüyü Uygula ile birlikte denenir. 15 saniyelik onay penceresinde Koru seçilmezse, Geri/Escape ile reddedilirse veya menü kapanırsa önceki doğrulanmış ekran ayarına dönülür. Sayaç unscaled time kullanır, oyun duraklatıldığında da işler. Deneme kalıcı kayda yazılmaz; çökme/yeniden başlatma son doğrulanmış ayarı yükler. Görüntü sekmesinden çıkarken uygulanmamış ekran seçimleri bırakılmaz: Uygula veya Vazgeç seçimi verilir.

Diğer seçenekler anında uygulanır ve kaydedilir. Kalite değişiminden sonra vSync/FPS ve kullanıcının efekt tercihleri tekrar uygulanır. Kullanıcı Volume tercihleri sahne profile assetlerini değiştirmez; runtime override profili katmanıyla uygulanır. Film greni kapalıyken korku olaylarının greni de görünmez; bu kullanıcı tercihi önceliklidir. Kamera olaylarının FOV geçişleri korunur; bu kapsamda genel FOV seçeneği eklenmez.

### Kontroller

| Seçenek | Aralık / davranış |
|---|---|
| Fare hassasiyeti | 0.1–5.0, varsayılan 1.0; yalnız fare |
| Fare Y eksenini ters çevir | Açık/Kapalı, varsayılan kapalı |
| Gamepad bakış hassasiyeti | 0.1–5.0, varsayılan 1.0; yalnız gamepad |
| Gamepad Y eksenini ters çevir | Açık/Kapalı, varsayılan kapalı |
| Analog ölü bölgesi | 0.05–0.40, varsayılan 0.125; hareket ve bakış için Input System işlemcisi, çift deadzone uygulanmaz |
| Tuş atamaları | Klavye/Fare ve Gamepad alt seçicisi; Excel'de oyuncu değiştirebilir olarak işaretli bağlar |

Atama satırlarında eylem adı, bağlam/parça, mevcut tuş, Değiştir ve Sıfırla bulunur. WASD bileşenleri ayrı satırdır; sabit vektör Look/Scroll gibi değiştirilemeyen bağlar bilgilendirme olarak gösterilir. Gamepad bağlantı durumu gösterilir, bağlı değilken kayıtlı tercihler yine düzenlenebilir.

Değiştir seçildiğinde yalnız seçili cihaz ailesinden uyumlu kontrol yakalanır. Menüye tıklayan ilk basış yakalanmaz. Escape ve görünür İptal butonu iptal eder; 10 saniyede zaman aşımı olur. Pause/geri gibi menü eylemleri atama yakalama sırasında çalışmaz, oynanışa girdi sızmaz. Escape atamak gerekiyorsa kalıcı bir Çıkış tuzağı oluşturmayan açık UI akışı seçilir; varsayılan olarak Escape yakalama iptalidir.

Çakışma kontrolü, atama listesi ve Excel bağlam bilgisini kullanır: farklı bağlamlarda aynı tuş yasaklanmaz. Aynı bağlamdaki başka eylemle yeni çakışma kullanıcıya iki eylemin adıyla gösterilir; mevcut atamalar sessizce silinmez. Kullanıcı Değiştirmeden Vazgeç veya Yine de Ata seçer. Mevcut varsayılan çakışmalar menü açılışında hata sayılmaz. Atama kabul edilince GameInput ve PlayerInput birlikte güncellenir, değişen tuş ipucu/ikon aynı anda yenilenir ve sonraki çalıştırmada korunur.

Gamepad gezinme, ayarlar arayüzündeki standart uGUI navigation ile yapılır. Yeni gamepad gameplay bağları otomatik varsayılıp mevcut bulmaca tasarımlarına eklenmez. Var olan gamepad bağları ve bunların yeniden atamaları yönetilir. Titreşim ayarı, henüz oyunda haptics üreticisi bulunmadığı için göstermelik eklenmez.

### Dil ve Altyazılar

| Seçenek | Aralık / davranış |
|---|---|
| Dil | GameLocalization.Languages'taki gerçek diller, kendi adlarıyla; anında tüm açık ayarlar/alt pencereler yenilenir |
| Altyazılar | Açık/Kapalı; ses içermeyen zorunlu tutorial düşüncelerinin mevcut görünme kuralı korunur |
| Altyazı boyutu | Küçük/Normal/Büyük: mevcut tasarım boyutunun %85/%100/%125'i |
| Altyazı arka planı | %0–100; yalnız altyazı arkasındaki okunabilirlik zemini |

Gerçek font, boyut, satır sarma ve arka planı kullanan kısa bir altyazı önizlemesi bulunur. Tercihler sahne değişimi ve ölüm/yeniden yükleme sonrası korunur. Ekran dışına taşmaması için büyük altyazılarda yeterli dikey alan ve sabit yatay güvenlik payı ayrılır. Altyazı kapalıyken mevcut oynayan isteğe bağlı altyazı da kapanır; sıra/süre ilerleyişi kaybolmaz. Tekrar açıldığında devam eden segment uygun süreyle görünür; eski segment baştan oynatılmaz.

## Kayıt ve yaşam döngüsü

- Mevcut SettingsSave.json geriye uyumlu genişletilir. Var olan ses/dil/hassasiyet değerleri korunur; eksik yeni alanlar açık varsayılanlarla doldurulur. Ekran için kırılgan resolutionIndex yerine genişlik/yükseklik/hız/mod saklanır; eski index bir defa migrate edilir. Monitör değiştiyse desteklenen en yakın güvenli moda dönülür.
- Dil indeksinin eski eşlemesi korunur; yeni kayıt dil kodu kullanır. Üç kalite adıyla kararlı eşleme sağlanır; eski indeks geçerliyse mevcut oyuncu tercihi korunur.
- Alan doğrulaması yüklemede yapılır; NaN/sonsuz/aralık dışı değerler ve bozuk JSON kontrollü varsayılanlarla ele alınır. Bozuk dosya yedeklenir, kullanıcı verisi sessizce kaybolmaz. Kaydetme hatası menüde kısa, çevrilmiş hata verir ve oyunu çökertmez. Dosya geçici dosyaya yazılıp güvenli biçimde değiştirilir.
- SettingsChanged olayı gerçek tüketicileri yeniler. Sürekli her kare JSON yazılmaz; slider bırakılınca/klavye düzenlemesi durunca veya menü kapanırken kayıt yapılır. Sahne yüklemesinde runtime volume, mikser ve oyuncu referansları yeniden bağlanır; beklemeler pause'dan bağımsızdır.
- Tuş override'ları GameInput'ın mevcut PlayerPrefs kaydında kalır. Bölüm sıfırlama varsayılanı yalnız o bölüme uygular; Kontroller sıfırlaması ayarlarla birlikte tuş override'larını kapsadığını onayda belirtir. Bölüm sıfırlamada onay gerekir. Görüntü sıfırlamada ekran değişiyorsa aynı 15 saniyelik koruma uygulanır.
- Kullanıcı ayarları içerik Excel'iyle karıştırılmaz. Yeni başlıklar, değer adları, kısa açıklamalar ve hata/onay metinleri TR/EN olarak mevcut çalışma kitabına eklenir, mevcut satırlar/kimlikler ve diğer sekmeler korunur; Unity importer ile üretilen JSON/stamp güncellenir.

## Entegrasyon

- Tek ortak Settings UI prefabı veya mevcut panellerin ortak view bileşeni kullanılır; ana menüde az seçenekli fallback ayrı tasarım olarak büyütülmez. Paylaşılan prefab yaklaşımı önerilir, uygulayıcı exact asset yerini seçebilir.
- Etkin build sahneleri scn_PressAnyKeyMenu, scn_MainMenu, scn_CreditsMenu ve scn_labratory. Credits sahnesine yeni ayarlar girişi eklemek gerekmez; mevcut MainMenu, scn_labratoryfast ve scn_LaboratorySynthesis ayar panelleri de aynı bileşenle uyumlu tutulur.
- MainMenuManager/PauseManager mevcut aç/kapat çağrıları korunur. Duraklatmada oyun, ses ve tutorial saati duraklatılmış kalır. Ayarlardan geri yalnız pause menüsüne döner; oyunu kendiliğinden sürdürmez. Kitap/etkileşim/kamera kilidi ayarlar açılışında bozulmaz.
- Eski dil/font düzeltmeleri yeni düzenle çakışmayacak şekilde güncellenir. Eski LocalizationPresentationChecks yeni sekmeli yapıda gerçek font, hiza ve taşma kontrolleri yapmaya devam eder; kontrolü kaldırarak test geçirme yapılmaz.

## Uygulayıcıya bırakılan kararlar

Sınıf/dosya adları, serialized referansların kurulacağı Editor aracı, mevcut font ailesinden uygun ağırlık, kesin satır yükseklikleri ve içerik kaydırma yüksekliği. Mevcut sistemleri kullan; genel bir ayar eklenti çatısı veya yeni paket ekleme. Belirtilen aralıklar, kayıt/geri alma davranışı ve gerçek işlev kapsamı bu takdir alanına girmez.

## Kabul ve doğrulama

1. Unity derlemesi hatasız; gerçek menü ve pause içinden dört sekme açılır. Menü aç/kapat/dil değişiminde event/row çoğalmaz.
2. Her ayarın gerçek etkisi sınanır: dört mikser kanalı, doğru kalite preset, vSync/FPS ilişkisi, runtime HDRP efekt/pozlama, fare/gamepad ayrımı ve head bob, altyazı boyutu/zemin.
3. Ekran deneme: Koru, Vazgeç, 15 sn timeout, pause timeScale=0, menü kapanışı, tekrar başlatma ve monitör listesi değişimi. Yalnız doğrulanmış ekran değeri kaydedilir.
4. Gerçek tuş yakalama: yeniden atama, iptal, timeout, çakışma uyarısı, context paylaşımı, sıfırlama, dil/ikon/ipucu anlık güncellemesi; önceki kitap çıkışı testi korunur.
5. Eski JSON, yeni JSON, bozuk/kısmi JSON, aralık dışı değer ve yazma hatası. Testler oyuncunun gerçek kayıtlarını yedekleyip geri yükler veya izole test yolları kullanır.
6. Türkçe/İngilizce bütün sekmeler ve onaylar: aynı fontla Türkçe harfler, sütun hizası, 720p/1080p/1440p ve ultrawide taşmama, gamepad/klavye odak sırası.
7. Ana menü → oyun → pause ayarları → oyuna dönüş; sahne yeniden yükleme, tutorial/altyazı pause ve mevcut ContentWorkbook/Tutorial regressions bozulmaz. Mevcut 29 içerik uyarısı yeni ayar etiketlerinde eksik çeviri bırakmayı mazur göstermez.

Builder incelemesi: 2026-10-29 tarihinde bağımsız Unity incelemesinde planlamayı engelleyen açık soru bulunmadı. Etkin build sahneleri listesine Credits eklendi.

Bu belge onaylandıktan sonra uygulama planı hazırlanıp kod değişiklikleri başlatılır. Otomatik commit/push yapılmaz.
