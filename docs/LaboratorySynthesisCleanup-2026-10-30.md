# Laboratory Synthesis — ilk temizlik etabı

30 Ekim 2026. Sahne: `Assets/Scenes/scn_LaboratorySynthesis.unity`.

## Uygulanan değişiklikler

- Referansları tekrar taranan beş kullanılmayan script ve dört PasswordData SO kaldırıldı; meta dosyalarıyla birlikte 18 dosya. Scriptler: LocalizedUIVariant, CreditsManager, PlayerReferences, ObjectiveAction, DesktopMenuInteract (279 satır).
- Beş fazla FormulaCatalog nesnesi kaldırıldı; kalan katalog araştırma sistemi altında. İki yeni ayarlar prefabı ve adaptörü korunarak 169 eski UI alt nesnesi temizlendi.
- İki missing-script kalıntısı kaldırıldı. Kayıp reflection ve occlusion asset referansları temizlendi. Occlusion yeniden bake edilmedi.
- GameManager, DebugMenuManager ve GlobalSubtitleManager sahneye ait kalıyor; yok olduklarında kendi singleton referanslarını bırakıyor. GameManager kopyası Awake devamını çalıştırmıyor. Kalıcı SettingsManager köke ayrılıyor.
- Kitapların oluşturduğu materyaller ve collider mesh'i kitapla birlikte temizleniyor. Emission vurgusu gerektiğinde hazırlanıyor. Araştırma kurulumundaki emission kapatma bu kaynakların sahibi kitap üzerinden yürütülüyor. UniversalHighlighter özgün materyalleri ödünç alıyor ve kendi vurgulama kopyalarını temizliyor.
- Güncel sahne kontrolü 47 kitap / 3 hedefi doğruluyor. Tarihsel dört kitaplı kopya kontrolü ayrı Legacy menüsünde tutuldu.

## Hierarchy

Kök sayısı 22'den 13'e indi. `CAMERAS` altında açılış/final kameraları, `AUDIO` altında AmbianceMusic, `UI` altında Canvas ve Handheld_Device_Canvas toplandı. Araştırma sistemi MANAGERS altında, LEES / Head Resistance ENEMIES altında diğer düşmanlarla kardeş durumda. SettingsManager kökte.

3193 başlangıç nesnesinden 174 eski nesne kaldırıldı, üç grup eklendi: sonuç 3022 nesne. Kalan nesnelerin GlobalObjectId ve dünya pozları kontrol edildi; 47 kitap kimliği/sırası, hedef havuzu, öğretici tercihi ve sessionOnly koruması aynı. Oyuncu rig'i ve makine/kitap iç hiyerarşileri taşınmadı.

## Doğrulama

- Sahne: 3022 nesne, 47 kitap, tek katalog; missing script ve eski ayarlar alt nesnesi sıfır. Eksik asset GUID veya boşa düşen yerel fileID yok.
- Güncel içerik/model: 47 kitap, üç hedef, 48 dağıtım örneği geçti.
- Kitap geometrisi: 2450 nokta, en büyük hata 0.000001431 m.
- Ayarlar veri/UI kontrolleri ve 112 LEES bağlantı doğrulaması geçti.
- Manager ömrü ve kitap kaynak ömrü testleri önce hatayı üretti, düzeltmeden sonra geçti. UniversalHighlighter testi iki hatadan sıfır hataya indi; eski kitap emission odak görünümü korunuyor.
- Öğretici Hayır: 25, Evet: 24 Play doğrulaması geçti.
- Tekrarlanan emission kapatma dahil 11 kitap materyali/mesh temizlendi; kaynak kitabın tüm renderer materyalleri korundu. Laboratuvar ana menü ve pause ayarlar panelleri geçti. Gerçek sahne geçişi ayarlar testi de geçti: ana menü → scn_labratory, ekran değişikliği/geri alma, gerçek tuş atama, ses miksajı ve altyazılar.

**Açık bulgu:** Uzun 47 kitap/üç hedef testi 20 kitabı tamamladıktan sonra İngilizce `Kayıt C-43 / DENEYİN KOŞULLARI` başlığında TMP metin taşmasına takıldı. Testin sonundaki üç hedef/Turing akışına ulaşılamadı; bu test yeşil olarak raporlanmıyor. Hata kaydı: `outputs/laboratory-cleanup-2026-10-30/research-live-failure.json`. Bu etapta ilgili metin, çeviri ve kart yerleşimi kodları değiştirilmedi.

## Sonraki işler ve kararlar

1. İngilizce uzun kayıt başlıklarının fiziksel kitap/kart alanına sığması; ardından 47 kitap ve üç hedef uçtan uca testini tekrar çalıştırmak.
2. Occlusion yeniden bake ve aynı kamera rotasında CPU/GPU/Memory Profiler karşılaştırması. Bu etap için FPS/bellek kazancı iddiası yok.
3. Altı kapalı eski kitap ve altı eski formül kökünün liste/event/node bağlantılarını çözmek. Mevcut referansları nedeniyle korunuyorlar.
4. Çalışma mevcut SNZ-003 checkout'unda sürdürüldü; kullanıcıya ait önceden değişmiş dosyalar korunuyor. Yeni worktree mevcut kaydedilmemiş içeriği eksik bırakacaktı. Commit/push yapılmadı.
5. Ortak sahnelerin kullandığı script/texture/prefablar korunuyor. Yanlış silme riskine karşı kullanım kanıtı olmayanlar otomatik kaldırılmadı.

Bağımsız statik kod/sahne incelemesinde doğrulanmış kritik veya önemli üretim hatası bulunmadı. FPS/GPU/bellek ölçümleri ve değişmeyen eski oyun davranışlarının tamamı bu incelemenin dışında; bunlar için kapsamlı doğrulama yapıldığı iddia edilmiyor.

Yedekler ve ayrıntılı kanıtlar: `outputs/laboratory-cleanup-2026-10-30/`. `before-disk.zip` dosyaları, `before-editor.unity` ilk açık ve kaydedilmemiş sahneyi korur. Baseline ve değişiklik manifesti yeniden yazılmaya karşı korunuyor.

Kullanıcı SettingsSave.json dosyasının SHA-256 değeri başlangıçla aynı; normal SenzoraLocalSave.json başlangıçta yoktu, testlerde oluşturulmadı.
