# LEES mücadele atölyesi

Sahne: `Assets/Scenes/scn_LeesResistanceWorkshop.unity`

Unity menüsü: **Senzora → LEES Mücadelesi → Test Sahnesini Aç**. Play'e bas ve Game penceresine tıkla. LEES karşında durur. Fareyle onu görüp arkanı dön; yaklaşık 145 derecelik dönüş yeterlidir. Sabit kaldığında arkandan yaklaşır ve mücadele başlar.

- LEES sağa çekerse fareyi sola, sola çekerse sağa çek. Ok oyuncunun direnmesi gereken yönü gösterir.
- Baş simgesi/gösterge, başlangıç yönüne göre gerçek baş açısını gösterir. Gösterge uçlara ulaşırsa yakalanırsın.
- Baskı yüzdesi baş açısını ve mücadele boyunca biriken baskıyı birlikte gösterir. Hiç direnmeden beklemek kaybettirir.
- Dayanma çizgisi dolar ve süre biterse kurtulursun. İlk ayar 16 saniyedir.
- Yaklaşma/mücadelede WASD, yön tuşları veya Space'e basmak, sabit kalma kuralını bozarak kaybettirir. Bu atölyede karakter yürütülmez.
- **ESC:** duraklat/devam et. Pencere odağını kaybetmek de duraklatır; geri gelince tıkla.
- **R:** baştan dene. Kazanma/kaybetme sonrasında tekrar kullanılabilir.

Bu sahne bağımsızdır: gerçek GameManager, kayıt dosyası, ana oyun sahnesi veya LeesEnemyAI çalıştırmaz. Başarısızlık burada sonuç ekranıdır; ana oyunun ölüm/jumpscare sistemiyle sonraki entegrasyonda bağlanır.

## Hissi ayarlama

Sahnedeki **LEES Resistance Rig → Lees Resistance Controller** bileşenini seç:

| Alan | İlk değer | Etkisi |
|---|---:|---|
| Approach Seconds | 2,8 | Arkadan yaklaşma süresi |
| Survival Seconds | 16 | Dayanılması gereken süre |
| Pull Degrees Per Second | 54 | LEES'in başı çevirme gücü |
| Mouse Degrees Per Pixel | 0,075 | Fareyle karşılık verme hassasiyeti |
| Failure Angle | 52 | Başın güvenli başlangıç yönünden kaç derece dönebileceği |
| Direction Warning Seconds | 0,25 | Her yeni çekişte kuvvet başlamadan verilen haber |
| Motion Intensity | 0,9 | Ek eğilme, sarsılma ve FOV etkisi; 0 bunları kapatır |

Zorunlu baş dönüşü oynanışın parçasıdır; Motion Intensity sıfır olsa da devam eder. Fare hareketi piksel olarak okunur ve deltaTime ile ikinci kez çarpılmaz. Fiziksel fareyi kaldırıp yeniden konumlandırabilirsin; sürekli tek yönde büyük mesafe sürükleme gerektirmez.

Oyuncu denemesi sonrasında çekiş sertleştirildi: ilk sürümde hareketsiz kaybetme yaklaşık 8,5 saniyeydi; yeni varsayılanda mücadele başladıktan sonra yaklaşık **1,23 saniye**. Çekiş kısa darbelerle dalgalanır, sonlara doğru %50 güçlenir ve 1,8–2,6 saniye aralıklarla yön değiştirir. Küçük fare dokunuşları biriken baskıyı kolayca silmez. Aktif dirençten sonra fareyi bırakma ve yalnızca hafif düzeltme yapma senaryoları da kontrol edilir. Eski ayarları geri yüklediysen **Senzora → LEES Mücadelesi → Sert Çekiş Ayarını Uygula** komutu açık workshop sahnesine ve rig prefabına sert ayarları kaydeder; süreleri değiştirmez.

Sahnedeki koridor, ışıklar, LEES görseli ve HUD düzenlenebilir nesnelerdir. Mücadele sırasında yalnız kalp atışı kullanılır; çınlama döngüsü ve her çekiş değişimindeki korku/keman sesi kaldırıldı. Ana sahnede kalp atışı mevcut efekt mikserine bağlıdır ve oyunun ses ayarlarını izler. Gerçek ölüm anındaki jumpscare sesi korunur.

## Ana oyun bağlantısı

Sistem **scn_LaboratorySynthesis** çalışma sahnesine ve **scn_labratory** oyun giriş sahnesine kuruldu. Normal akışta LEES'i görüp gerçekten arkanı döndüğünde, sabit duruyorsan mücadele başlar. Eski pasif bekleme sayacı bu mücadeleyi atlayarak kurtuluş vermez. Başarı LEES'i kaldırır; yenilgi oyunun mevcut ölüm/timeline akışına gider. Hareket tuşuna mevcut grace süresinden uzun basmak da hareket kuralını bozar.

Mevcut ana kamera kullanılır; ikinci kamera veya AudioListener eklenmez. Yaklaşma boyunca oyuncunun bakışı ve hareketi açık kalır. LEES gerçekten kavradığında yalnız kamera kontrolü mücadeleye geçer; karakter kontrolcüsü, yürüme ve yerçekimi çalışmaya devam eder. Sabit durmak oyuncunun sorumluluğudur; hareket mevcut grace süresini aşarsa kaybettirir. Bitiş/iptalde kamera ve etkileşim durumları geri yüklenir. Mücadele kendi kendine oyunu duraklatmaz ve ana HUD'da duraklatma/yeniden deneme yazısı göstermez; odak kaybı da ayrı bir mini oyun duraklatması açmaz. Oyuncunun ESC ile açtığı normal oyun menüsü korunur.

Gerçek oyundaki yenilgi LEES jumpscare timeline'ına, ardından mevcut ölüm ekranına gider. Yeniden deneme yalnız aktif tutorial içinde açıkça güvenli (`isSafeSpawn`) başlatılan karşılaşmada uygulanır; bu durumda oyuncu öğrenene kadar mücadele tekrar başlar. Tutorial dışına taşınmış eski bir safe-spawn işareti ölümsüzlük vermez. LEES sahneden ayrılırken bu işaret temizlenir. Ana oyun düşmanı, test rig'inin sonuç/duraklatma seçenekleri yanlışlıkla açılmış olsa da başlangıçta gerçek oyun ayarlarını uygular.

Kurulum: **Senzora → LEES Mücadelesi → Elleri ve Ana Oyun Bağlantısını Kur**. Komut test rig'ini ve iki ana sahneyi kaydeder. Mevcut ek olay dinleyicilerini korur. `scn_labratoryfast`, giriş sahneleri ve Build Settings değiştirilmez.

Yeniden kullanılabilir test rig'i: `Assets/LeesResistanceWorkshop/LeesResistanceRig.prefab`. Başka bir sahneye elle bağlamak için:

1. `viewCamera`, `headPivot`, `hud` ve AudioSource referanslarını ata. Ana oyunda `playerMovement` alanına gerçek StarterAssets kontrolcüsünü ata; bunu `suspendDuringResistance` listesine ekleme. Kamera Cinemachine tarafından sürülüyorsa brain'i bu listeye ekle; yalnız kavrama başlarken kapatılır. Bağımsız test rig'inde `playerMovement` boş kalır.
2. LEES'in mevcut arkanı dönme/sabit kalma koşulları sağlandığında `BeginResistance()` çağır. Controller atanmış etkileşim bileşenlerini geçici olarak kapatır; hareket motorunu açık bırakır. Bitiş/iptalde önceki enabled durumlarını, kamera pozunu/FOV'unu ve cursor durumunu geri yükler.
3. `onEscaped` olayını LEES'in kaybolmasına, `onCaught` olayını oyunun ölüm/jumpscare girişine bağla. Olaylar kısa kamera bitişinden sonra bir kez çağrılır.
4. Entegrasyonda hareket kuralını mevcut düşman mantığından izlemeye devam et; bileşen tek başına oyuncunun dünya konumunu izlemez. Gerektiğinde `CatchForMovement()` çağır.
5. Etkileşim iptalinde `CancelResistance()` çağır. Entegrasyonda `keepResultVisible` kapalı kalsın. Atölyede bu seçenek R ile tekrar ekranını tutar.

## Ekran kenarlarındaki eller

Eller, mevcut LEES modelinin yalnız el geometrisi ve dokularından üretilir; önkol içermez. Bilek ekran dışında, kıvrılmış parmaklar iki kenarda kalır. Ayrı malzemede koyu, düşük parlaklıkta bir ten kullanılır. Kaynak prefab/model/animasyon değiştirilmez. Yaklaşmanın son bölümünde iki kenardan içeri girerler; çekilen taraftaki el daha fazla bastırır. Kurtuluşta geri çekilir, iptal ve ölüm geçişinde temizlenirler. Çalışırken iki küçük mesh kullanılır; tam LEES modeli kopyalanıp her karede bake edilmez.

Kameranın altındaki **LEES / Temple Grip → Lees Resistance Hands** bileşeninde `Size`, `Depth`, `Left Anchor` ve `Right Anchor` ile büyüklük/yakınlık/kenar konumu ayarlanabilir. Meshler `Assets/LeesResistanceWorkshop/LeesLeftGrip.asset` ve `LeesRightGrip.asset`, ayrı el malzemesi `LeesGripSkin.mat` dosyasındadır. Parmaklar bake edilmiş kavrama pozundadır; çalışma anındaki çekiş elin pozunu değiştirir.

Ana kameranın karakter iskeletinden miras aldığı yaklaşık 0,0708 ölçek, ilk sürümde elleri yaklaşık 14 kat küçültüyordu. El kökü bu ölçeği telafi eder. Boyut ayrıca kameranın gerçek viewport yüksekliğinden hesaplanır; ana kameranın fiziksel lens/gate-fit ayarı ile test kamerası aynı ekran kaplamasını verir.

Yakındaki ellerin dünya nesnelerine odaklanan otomatik odak yüzünden bulanıklaşmaması için, yalnız mücadelede kamera üzerindeki alan derinliği geçici kapatılır; eller gizlenirken önceki kamera override değerleri geri yüklenir. Dünya odak profili değiştirilmez. El ışığı yalnız eller görünürken açılır ve ayrı HDRP rendering layer üzerinden yalnız el meshlerini aydınlatır.

## Doğrulama

İlk bakış/yeniden spawn düzeltmesi: görüşten kısa süreli çıkma, animasyon/raycast engeli veya şok FOV'u artık oyuncunun arkasını döndüğü anlamına gelmez. `hasTurnedAway` yalnız gerçek yatay arkaya dönüşte işaretlenir; mini oyunu başlatan mevcut yön eşiği kullanılır. Başarılı yeni spawn, önceki şok efektini, direnişi, sesi ve hareket tolerans sayacını temizler. Gerçek arkaya dönüşten sonra LEES'e yeniden bakmak ölüm koşulu olmaya devam eder.

**İlk Bakış ve Yeniden Spawnı Doğrula** menüsü Play modunda izole gerçek kamera/collider fixture'ı kullanır. FOV daralması ve kısa engellenme önce eski kodda hatayı üretti; düzeltme sonrası ilk görüş, yan bakış, gerçek dönüş/geri bakış ve mücadele sürerken yeniden spawn dahil **14 kontrol geçti**. Sonuç `outputs/lees-first-sight-checks.txt` dosyasındadır; oyuncunun kaydı veya sahnesi kaydedilmez.

30 Ekim hareket/ölüm/el pozu düzeltmesi: **372 model, 209 atölye, 112 ana sahne ve 12 gerçek Play assertion geçti.** Sağ parmakların bükülme yönü ve elin pitch açısı ayna simetrisine göre düzeltildi; açık bilek kesiti ekran dışında kalır.

**Gerçek Ölüm Akışını Doğrula** menüsü ana sahnede Play modunda çalışır. Kaydı korumak için oturumluk kayıt modunu açar; NewGame/LoadGame çağırmaz. Gerçek karakterin mücadele sırasında yürüyebildiğini, hareketin güvenli öğreticide başarısızlık ve yeniden denemeye yol açtığını, öğretici dışında fareyi bırakmanın gerçek jumpscare timeline'ına ve ardından mevcut ölüm paneline gittiğini kontrol eder. Synthesis sahnesinde timeline yaklaşık 6,48 saniye oynadı; sonunda ölüm paneli aktif, zaman sıfır ve mücadele kapalı kaldı. Sonuç `outputs/lees-play-checks.txt` dosyasındadır. Test bitince Play modundan çıkılır.

Ana sahne kontrolleri ayrıca yaklaşma sırasında normal kamera kontrolünün açık olduğunu, kavramada yalnız başın devralındığını, karakterin kendiliğinden taşınmadığını, odak kaybının mini oyun duraklatması oluşturmadığını ve ana HUD'da test moduna ait duraklatma/yeniden deneme yazıları bulunmadığını doğrular.

`tools/check-lees-resistance.ps1`: saf C# kuralları; farklı seedlerde idle/ters yön/doğru yön, 30/60/144 FPS, pause, tekrar ve terminal durum kontrolleri.

**Senzora → LEES Mücadelesi → Sahneyi Oluştur ve Doğrula:** eksik dosyalar yoksa yalnız kontrolleri çalıştırır. **Doğrula:** mevcut sahneyi izole preview sahnesinde açarak referansları, kaydedilmiş ışık profilini, iptal/disable/pause geri yüklemelerini ve UI taşmalarını kontrol eder. Ana sahneleri kaydetmez.

Sonuç ve görseller `outputs/lees-workshop-checks.txt`, `outputs/lees-workshop-intro.png`, `outputs/lees-workshop-struggle.png` dosyalarına yazılır. Bunlar otomatik kontrol/önizlemedir; insanın oynarken değerlendireceği gerilim ve zorluk dengesi bu ilk sürümün ayarlanacak kısmıdır.

30 Ekim 2026 doğrulaması: **152 model assertion ve 199 Unity assertion geçti.** Unity runtime/editör derlemesi başarılı; başlangıç ve mücadele renderları görsel olarak incelendi. Odak kaybı, iptal/disable, başlangıçta kapalı ve yinelenmiş kontrol referansları, tek seferlik başarı/yakalanma olayları ve sonuçtan sonra iptal kontrol edildi. Projede önceden bulunan derleme uyarıları ve ana sahnenin HDRP `_WaterCameraHeightBuffer` uyarısı devam ediyor; bu çalışma bunları değiştirmedi.

Sertleştirme sonrası doğrulama: **372 model assertion ve 207 Unity assertion geçti.** 40 başlangıç seed'inde fare bırakıldığında kaybetme 1,23 saniye; 30/60/144 FPS'te aktif dirençten sonra bırakma en geç 2,25 saniyede kaybettiriyor. Hafif karşı hareket (200 piksel/saniye) yetersiz, doğru aktif karşılık kazanılabilir. Açık workshop sahnesi ve prefabın kaydedilmiş ayarları da kontrol edildi.

Ana oyun/eller sonrası: **372 model, 209 atölye ve 68 ana oyun bağlantısı assertion geçti.** **Ana Oyun Bağlantısını Doğrula** menüsü iki ana sahnenin preview kopyalarında gerçek kamera/kontrol referanslarını, ses mikserini, el meshlerini, oyun duraklatmasını, kaydedilmiş başarı/ölüm callback'lerini ve disable temizliğini sınar. Sonuç `outputs/lees-integration-checks.txt` dosyasındadır. Ölüm fixture'ı iki beklenen `ÖLÜM: He turned your head.` kaydı üretir; gerçek sinematik/kayıt sistemi bu izole testte çalıştırılmaz. Eller atölyenin kamera renderında görsel olarak kontrol edildi; ana oyun içinde insanla tam karşılaşma testi sonraki oynama değerlendirmesidir.

Yalnız el ve ana kamera ölçeği düzeltmesi sonrası: **372 model, 209 atölye ve 106 ana oyun assertion geçti.** Yeni görünürlük kontrolü eski sürümde ana kameranın yaklaşık 14 kat küçülttüğü elleri yakalayarak başarısız oldu; düzeltme sonrası iki sahnede geçti. Kontrol artık bileğin altında önkol bulunmadığını, gerçek kamera projeksiyonunda elin ekran kaplamasını, clipping sınırlarını, alan derinliği override'ının tam geri yüklenmesini, uyuyan kamera override'larının etkinleşmediğini ve el ışığının HDRP katmanını/açılıp kapanmasını da sınar. Her ana kamerayla el açık/kapalı renderları karşılaştırılır; iki kenarda da görünür piksel değişimi aranır. Son görseller `outputs/lees-hands-scn_LaboratorySynthesis.png` ve `outputs/lees-hands-scn_labratory.png` dosyalarındadır. Bunlar gerçek sahne kameralarıyla izole preview renderlarıdır; insanla tam karşılaşma Play testi değildir.
