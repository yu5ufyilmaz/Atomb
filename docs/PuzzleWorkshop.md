# Puzzle Atölyesi

Unity menüsü: **Senzora → Puzzle Atölyesi → Aç**.

Sahne: `Assets/Scenes/scn_PuzzleWorkshop.unity`. Üretim/düzenleme Unity Editor'da, Play kapalıyken yapılır. **Oyunda Dene**, seçtiğin kaydedilmiş puzzleı kitap–defter–tahta–Turing akışında açar.

## Hızlı başlangıç

1. **Başlangıç** sekmesinde **Renk ve karışım** veya **Sinyal deneyi** seç.
2. **Hazır örneği aç**, **Rastgele oluştur** veya **Kendim kuracağım** düğmesini kullan.
3. **Şema** sekmesindeki parça satırlarında etkileri oku; **+** ile ekle. Boş tuvale sağ tıklayıp listeden seçersen parça tıkladığın yere konur. Kaynak ve ölçer şemada zaten varsa **+** kapalıdır.
4. Tek giriş/çıkışlı bir parçayı seçip yenisini eklersen arkasına bağlanır. Analizör seçiliyken eklenen işlem, uygun tek hattın önüne girer. Diğer bağlantılar için çıkış okunu giriş halkasına sürükle veya önce oka, sonra halkaya tıkla. **Esc** bağlamayı iptal eder.
5. Parçaya herhangi bir yerinden tıklayınca seçilir; başlığından sürükleyince taşınır. **Delete** seçili parçayı siler; sağ tık menüsünde **Parçayı Sil** ve **Bağlarını Sil** vardır. Fare tekerleği yakınlaştırır; kaydırma çubuklarıyla gezilir. **Otomatik Düzenle** parçaları sıraya dizer, **Sığdır** bütün şemayı gösterir. Unity Undo/Redo kullanılabilir.
6. Hedef şemayı otomatik izler: her parça/bağ değişikliğinden sonra şema ölçülebiliyorsa hedef onun sonucu olur. Elle ayarlamak için **Hedefi kilitle**'yi işaretle; bir izin sayısını elle değiştirmek de hedefi kilitler. Kilitliyken **Şemadan Al** hedefi bir kez şemadan alır. Pencerenin üstündeki durum satırı her düzenlemeden sonra geçerliliği gösterir.
7. Üst çubukta puzzle adını yaz; yeni oyunda seçilmesini istiyorsan **Havuzda kullan**'ı işaretle. Oyuncunun okuyacağı açıklama **Kitaplar** sekmesinin başındadır. **Kaydet**'e bas; dosya otomatik olarak `Assets/Scripts/Scriptables/Puzzles` altında oluşur. Havuza yalnız geçerli puzzle kaydedilir; işaretsizse taslak olarak saklanır.
8. **Oyunda Dene** ile çözümü oyuncu gibi dene. Tahta ve defter boş başlar; örnek çözüm oyuncuya gösterilmez.

Kayıtlı bir örneği değiştirip ayrı puzzle yapmak için **Kopyasını Kaydet** kullan; yeni kimlik ve yeni dosya oluşur. Normal **Kaydet**, açtığın kaydı günceller.

## Editörü değiştirmek

Pencerede bir şeyin değişmesini isterken alanın adını ve onu çizen fonksiyonu söylemen yeterli. Hepsi `Assets/Scripts/Editor/PuzzleWorkshopWindow*.cs` dosyalarındadır.

| Ekrandaki alan | Fonksiyon | Dosya |
|---|---|---|
| Başlık, sekme çubuğu, durum satırı | `OnGUI` | `PuzzleWorkshopWindow.cs` |
| Üst çubuk: Yeni, Aç, asset alanı, ad, Havuzda kullan, Kaydet, Kopyasını Kaydet, Oyunda Dene | `Toolbar` | `PuzzleWorkshopWindow.cs` |
| Başlangıç sekmesi, hazır puzzlelar, korunan taslaklar | `Home`, `FamilyCard` | `PuzzleWorkshopWindow.cs` |
| Şema sekmesinin solundaki parça listesi | `Palette` | `PuzzleWorkshopWindow.cs` |
| Kurallar sekmesi | `Settings` | `PuzzleWorkshopWindow.cs` |
| Rastgele üretim | `Generate` | `PuzzleWorkshopWindow.cs` |
| Durum satırındaki geçerlilik mesajı | `ValidateDocument` | `PuzzleWorkshopWindow.cs` |
| Her düzenleme: Undo, taslak koruma, hedefin şemayı izlemesi | `Mutate`, `TakeTarget` | `PuzzleWorkshopWindow.cs` |
| Şema tuvali: düğmeler, yakınlaştırma, tekerlek, sağ tık menüsü, Delete, sürükleyerek bağlama | `Graph` | `PuzzleWorkshopWindow.Graph.cs` |
| Tek parça kartı: giriş halkası, sembol, çıkış oku, parça sağ tık menüsü | `DrawNode` | `PuzzleWorkshopWindow.Graph.cs` |
| Parça ekleme ve otomatik zincirleme | `AddPart` | `PuzzleWorkshopWindow.Graph.cs` |
| Bağ kurma / bağ silme / parça silme | `Connect`, `DeleteBonds`, `DeleteNode` | `PuzzleWorkshopWindow.Graph.cs` |
| Hedef kayıt paneli, Hedefi kilitle | `Target`, `DrawPlot`, `MixturePreview` | `PuzzleWorkshopWindow.Graph.cs` |
| Kitaplar sekmesi: açıklama, gözlem notları, dağıtım önizlemesi | `Notes` | `PuzzleWorkshopWindow.Graph.cs` |
| Otomatik Düzenle | `LayoutGraph` | `PuzzleWorkshopWindow.Graph.cs` |
| Taslaklar, Aç menüsü, kaydetme | `RememberDraft`, `OpenPicker`, `Save`, `SaveToPath` | `PuzzleWorkshopWindow.State.cs` |
| Şema yerleşim kontrolü | `CheckGraphPlacement` | `PuzzleWorkshopWindow.LayoutChecks.cs` |

## Renk ve karışım

Bu aile dalga grafiği kullanmaz. Hedefte üç bilgi birlikte tutmalıdır: **pigment oranlarından oluşan renk, pigment yoğunluğu ve toplam miktar**. Renk örneğinin yanında sayılar ve K/Y/M oranları yazılıdır; yalnız rengi ayırt etmeye dayanmaz.

Bu bir doz tabanlı oyun modelidir, gerçek boya/kimya simülasyonu değildir.

| Parça | Etkisi |
|---|---|
| Taşıyıcı Kaynağı | 1 doz renksiz taşıyıcı verir. |
| Kırmızı / Yeşil / Mavi Pigment | İlgili renkten 1 doz pigment ekler. |
| Çözücü | 1 doz taşıyıcı ekler; yoğunluğu azaltır. |
| Yoğunlaştırıcı | Taşıyıcının yarısını uzaklaştırır; pigmenti korur. |
| Sıvı Dağıtıcı | Bütün bileşenleri iki eşit kola böler; miktarı çoğaltmaz. |
| Karıştırma Kabı | İki girişin bileşenlerini toplar. İki giriş de bağlanmalıdır. |
| Karışım Analizörü | Sonucu ölçer. |

Örnek: kaynak → kırmızı → mavi → analizör, 1 doz taşıyıcı ve 2 doz pigment içerir: **3 doz, %66,7 yoğunluk, eşit kırmızı/mavi oranı**. Çözücü eklersen renk oranları korunur; miktar 4, yoğunluk %50 olur. Dağıtıcıyla iki kola ayrılıp kırmızı/mavi eklendikten sonra karıştırılan alternatif düzen aynı sonucu üretebilir ve koşullara uyuyorsa kabul edilir.

Hazır renk puzzleları: **Mor Karışım**, **Seyreltilmiş Mor**, **Yoğun Yeşil**.

## Sinyal deneyi

Önceki Çift Kayıt davranışı korunur. Hedef; izlerin zamanı, yüksekliği ve genişliğidir. Ek numuneler:

- N-08: kısa tepki, yarı yükseklik.
- N-21: kısa tepki, 0,16 gecikme.
- N-46: her giriş izinden 0,32 aralıklı iki tepki.

İşlem parçaları koşulların izin verdiği kadar tekrarlanabilir. Hedefte en fazla iki okunabilir iz desteklenir. Örtüşen/belirsiz hedefler yayınlanamaz.

## Kurallar ve rastgele üretim

**Kurallar** sekmesinde toplam parça/numune aralıklarını, izinleri ve parça başına adet sınırlarını ayarla. **Gelişmiş** bölümünde hat derinliği, karşılaştırma toleransı ve sabit seed vardır.

**Bu Kurallarla Rastgele Üret**, önce geçerli bir örnek düzen kurar, aynı oyun simülatöründe ölçer ve sonucu hedef yapar. Aynı ayarlar ve seed aynı sonucu verir. En fazla 512 aday denenir. Çelişen veya üretilemeyen kurallarda son taslak korunur; hata açıklanır. Üretim tek çözüm veya belirli bir insan zorluğu garantisi vermez.

Yasak/eksik parçalar, adet koşulları, bağlantısız parçalar ve açık uçlar doğrulanır. Oyuncunun ölçülebilen fakat puzzle koşullarına uymayan denemesi çıktı üretebilir; Turing hedefi onaylamaz. Çözüm örnek şemanın birebir kopyası olmak zorunda değildir.

## Kitaplar ve oyun havuzu

### Üç hedefli laboratuvar denemesi

`scn_LaboratorySynthesis` için **Senzora → Puzzle Atölyesi → Laboratuvarda Üçlü Kör Denemeyi Kur** ayrı bir üçlü araştırma kümesi bağlar. Bir hedef ilk kitapta, diğer hedefler farklı kitaplarda bulunur. Parçalar ve gözlemler sahnedeki bütün etkileşimli kitaplara karışır; ortak gözlemler tekrar edebilir. Hedeflerin yerleri ve not dağılımı yeni oyunda değişir.

Defterden tahtaya astığın hedef hangi deneyin ölçüleceğini seçer. Hedefleri istediğin sırada çözebilirsin; her ölçüm kendi hedefine bağlıdır ve tamamlanan diğer hedefler korunur. Kaynak çözüm şemaları kitaplara verilmez. Bu geliştirme sahnesinde Play'i durdurmak deneme oturumunu bitirir; normal oyun kayıt dosyası kullanılmaz.

Rastgele tek-puzzle moduna dönmek için **Havuzu Laboratuvara Bağla** kullanılabilir. Üçlü denemeyi yeniden kurmak için yukarıdaki kurulum komutunu çalıştır.

**Masa kitabı kuralı:** Masadaki "Deneycinin El Kitabı" (araştırma kitaplarının ilki) her oyunda, eğitimde de, iki ailenin kaynak ve ölçer kartlarını taşır: Atım Kaynağı, Çift Kanallı Ölçer, Taşıyıcı Kaynağı, Karışım Analizörü. Dağıtım (`PuzzleBookDistribution.Placement.cs`, `DeskParts`) bu kartları hedef sayfasından sonraki ilk boş yuvalara sabitler; yalnız deney kümesinin içerdiği kartlar sabitlenir, yer kalmazsa kalanı normal dağıtıma düşer. Eğitimde sinyal kaynağı ile ölçer ders sayfasına taşınır, kopya oluşmaz. Eğitimin numunesi Numune N-12 masada değildir: kütüphanedeki "Kısa ve Uzun Tepkiler" kitabının (Raf 4) başına "KISA TEPKİ NUMUNESİ" sayfasıyla eklenir. Masa kartları toplanınca kitap yanıp söner, iç ses kütüphaneyi söyler ve defterin Hedef sayfası numunenin ve aygıtların yerini gösterir (NUMUNELERİN YERİ / AYGITLARIN YERİ). Tahta adımı N-12 deftere alınınca açılır. Sahne bağlantısı: **Senzora → Tutorial → Yeni Tahta Dersini Bağla** (`Tutorial_SampleBook` eventi).

**Kitaplar** sekmesinde ek gözlemler yazabilir, hedefin masa kitabında kalmasını seçebilir ve dağıtım önizlemesini görebilirsin. Parça etkileri ve gereken kurallar otomatik eklenir; parça açıklaması Kurallar sekmesinden özelleştirilebilir. Sayfada en fazla iki bilgi bulunur.

**Havuzda kullan** işaretli kaydedilen geçerli puzzlelar `PuzzlePool.asset` üzerinden yeni oyunda eşit olasılıkla seçilir. Yeni oyunda kitapların içerik yerleşimi de değişir. Aynı puzzleın sonraki oyunda tekrar seçilmesi mümkündür. Çalışma laboratuvarı `scn_LaboratorySynthesis` bu havuza bağlıdır.

Devam Et, seçimi yeniden yapmaz: puzzleın kayıtlı kopyasını, kitap/sayfa yerleşimini, tahta/defteri, ölçüm kimliklerini ve tamamlanmayı yükler. Kaynak asseti sonradan değiştirmek eski oyunun hedefini değiştirmez. Başka oturuma ait ölçüm kartı kabul edilmez. Eksik kitap veya uyumsuz kayıt durumunda devam reddedilir; mevcut dosyaya otomatik kayıt yapılmaz. Yarım kalmış okuyucu animasyonu geri yüklenmez; defterdeki kart yeniden kullanılabilir.

Atölye ve blockout normal kayıt dosyasını kullanmaz. Çalışma laboratuvarında geliştirme için `sessionOnly` açıktır. Paketlenmiş oyunun Splash girişini bu havuzlu laboratuvara taşımak ve kalıcı kaydı etkinleştirmek için **Senzora → Puzzle Atölyesi → Paket Girişini Kur** vardır; eski ana sahne üzerine yazılmaz. **Paket Girişini Doğrula** hangi girişin etkin olduğunu bildirir.

## Taslak güvenliği

Asıl asseti doğrudan düzenlemek yerine çalışma kopyası kullanılır. Kaydetmeden Yeni/Aç/Kapat yaparsan değişen taslak `UserSettings/PuzzleWorkshop/drafts.json` altında korunur. **Başlangıç → Korunan taslaklar → Devam Et** ile geri al. Bu yerel kurtarma dosyası proje asseti veya oyun havuzu değildir; projeyi başkasına aktarırken puzzleları Kaydet ile assete dönüştür.

## Yeni puzzle ailesi ekleme

Ortak akış sabittir; yeni aile için yeni kitap/defter/Turing sistemi gerekmez:

1. `PuzzleKind` ve `PuzzleParts.ForKind` üzerinden tür ve parça kataloğunu tanımla.
2. `PuzzleSimulation.Measure/Validate` girişinde aileye ait davranış ve hedef doğrulamasını bağla. Sinyal ve renk bunun iki çalışan örneğidir.
3. Yeni sonuç veri tipini puzzle/ölçüm snapshotına ekle; hedef ve ölçüm görselini oluştur. Grafik yerine renk kartı kullanılabilmesi `MixtureGraphic` ile gösterilir.
4. `PuzzleGenerator` aday kurulumunu ve kitap notlarını yeni parçaların giriş/çıkış kurallarına göre bağla; üretilen çözümü aynı simülatörde doğrula.

Bu, mevcut iki aile arasında çalışan ayrımdır; henüz eklenmemiş bağlantı mantığı veya madde dönüşümü simülatörü varmış gibi davranılmaz.

## Konulu kitaplar ve otomatik kaynakça

Laboratuvardaki 47 kitabın kapağında ve sırtında kalıcı, benzersiz bir başlık vardır. Etkileşim yazısı kitap adını ve rafını gösterir; rafların da isimlikleri bulunur. `PuzzleBookIdentity` üzerinden konu kabulünü ve sayfa kapasitesini ayarlayabilirsin. Konular temel aygıtlar, sinyal numuneleri, sinyal işlemleri, pigmentler ve karışım işlemleridir; bir kitap birden fazla konuyu kabul edebilir.

Kaydedilmiş veya rastgele üretilmiş hedef aynı dağıtıcıdan geçer. Önce bütün kartlar uygun kitapların güvenli sayfa alanlarına rastgele yerleşir. Sonra hedefin **Araştırma Kaynakları** bölümü gerçek kitap adı, raf ve sayfa numarasından üretilir. Ortak gözlemler tek kart olarak tutulur; ilgisiz kitaplara kopya ipucu konmaz. Kitap dizinleri toplanabilir kart değildir. Hedefi deftere/tahtaya taşımak kaynak bilgisini de taşır; bağlantı sırasını açıklamaz.

Yeni oturum kitap ve sayfa seçimini değiştirir. Devam Et aynı yerleşimi, kaynakçayı ve kart alanlarını yükler. Yeterli uygun alan bulunmazsa sistem yanlış kitaplara yerleştirmek yerine kurulumu açıklamalı olarak reddeder.

**Tab** defteri açıp kapatır; defterdeki **×** de kapatır. Ayrı Defteri Aç/Kapat ekran düğmeleri yoktur. **Belgeler** tutamağı ve **Deneyi Ölç** cihazı doğrudan tahtanın üzerindedir. Açık bırakılan belge panosu tahtadan çıkınca dünyada açık kalır; tekrar girişte kullanılabilir. Tahtadaki hedef kartı daha küçüktür, uzun kaynak yönlendirmeleri tekerlekle kaydırılır; defterdeki büyük hedef kartı korunur. Parçaların başlığı başlatıcı, işlem veya sonlandırıcı rolünü gösterir; sürüklenen notun kâğıt zemini korunur.

Kitap sayfaları ve basılı grafikler yeniden kullanılmak üzere önbelleğe alınır. Sayfa dönüşünde yalnız hareket eden görünür mürekkep güncellenir. Görünmez sürükleme alanları ayrıntılı kağıt geometrisi oluşturmaz. Performans ölçümleri için **Senzora → Book Motion → Optimization Capture After / Optimization Check Regression**, gerçek Play kareleri için **Optimization Live Frames After (Play)** kullanılabilir. Mikro ölçüm toplam oyun FPS'i değildir; ayrıntılar `docs/BookPerformanceAudit.md` dosyasındadır.

Yeni kontroller: **Araştırma Dağıtımı**, **Ortak Konulu Kitap Kapasitesi**, **Araştırma Hedef Görseli**, Play sırasında **Araştırma Arayüzü**. Fiziksel geometri için **Senzora → Book Surface → Projection Regression / Layout Regression** kullanılabilir.

## Geliştirici kontrolleri

Kitap kapak/sırt isimleri ve raf tabelaları **Special Elite** fontunu kullanır. **Senzora → Araştırma Denemeleri → Açık Sahnenin Kitap İsimliklerini Düzelt** yalnız isimlikleri ve raf çıkış yönlerini günceller; mevcut sahnenin kopyasını `UserSettings/PuzzleWorkshop` altında korur. Tabelalar gerçek raf gövdesinin ön yüzüne yerleşir. Sahne değişikliklerini kaydetmek gerekir.

`InteractableBook → Raftan Alma` bölümünde çekilme süresi ve açıklık payı ayarlanır. Raftaki kapalı kitap önce dönmeden/kapağını açmadan dışarı kayar; sonra okuyucuya yaklaşır. Geri dönüşte önce kapalı şekilde rafın önüne gelir, sonra aynı yuvaya girer. Masadaki kitaplar normal yaklaşımı kullanır. **Senzora → Book Motion → Shelf Labels Check / Shelf Pickup Live Check / Shelf Capture Labels** kontrolleri vardır.

**Senzora → Puzzle Atölyesi → Doğrulama → Şema Yerleşimi**: gerçek editör penceresinde iki aileyi, farklı pencere boylarını, yakınlaştırmayı, kaydırmayı ve 64 parçanın sığdırılmasını denetler. Çizilen ilk/son düğüm ve kırpma alanı piksel konumlarıyla kontrol edilir; seçim, bağ kurma ve sürükleme gerçek IMGUI olaylarıyla sınanır. Görüntüleme/zoom belgeyi değiştirmemelidir. Kontrol geçici pencerede çalışır ve mevcut taslak dosyalarını geri koyar.

**Senzora → Puzzle Atölyesi → Doğrulama → Tümü (Edit Mode)**: eski sinyal kontrolleri, 360 seed, karışım korunum kuralları, hedef kopyalama, Undo, taslak kurtarma ve güvenli kaydetme.

**Renk Denemesini Hazırla → Play → Canlı Akış**: gerçek kitapları okur/toplar, defterden fiziksel tahtaya parça taşır, ölçer, snapshotı ve izole dosya kaydını geri yükler, ölçümü Turing'in gerçek çarklarında sınar. Normal kullanıcı kaydı korunur. Kullanıcı için çözüm kısayolu değildir; geliştirici doğrulamasıdır.
