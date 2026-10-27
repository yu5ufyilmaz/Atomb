# Puzzle Atölyesi

Unity menüsü: **Senzora → Puzzle Atölyesi → Aç**.

Sahne: `Assets/Scenes/scn_PuzzleWorkshop.unity`. Üretim/düzenleme Unity Editor'da, Play kapalıyken yapılır. **Oyunda Dene**, seçtiğin kaydedilmiş puzzleı kitap–defter–tahta–Turing akışında açar.

## Hızlı başlangıç

1. **Başlangıç** sekmesinde **Renk ve karışım** veya **Sinyal deneyi** seç.
2. **Hazır örneği aç**, **Rastgele oluştur** veya **Kendim kuracağım** düğmesini kullan.
3. **Şema** sekmesindeki parça kartlarında etkileri oku; **Şemaya Ekle** ile ekle.
4. Tek giriş/çıkışlı bir parçayı seçip yenisini eklersen arkasına bağlanır. Analizör seçiliyken eklenen işlem, uygun tek hattın önüne girer. Diğer bağlantılar için bir çıkış okuna, ardından giriş halkasına tıkla.
5. **Otomatik Düzenle** parçaları sıraya dizer; **Sığdır** bütün şemayı gösterir. Kaydırarak gezebilir, ölçek çubuğuyla büyütebilirsin. Seçilen parçanın **Bağlarını Sil** veya **Parçayı Sil** düğmeleri ve Unity Undo/Redo kullanılabilir.
6. Geçerli düzeni kurunca **Hedefi Şemadan Al** düğmesine bas. Bu, oyuncunun üretmesini istediğin sonucu belirler. Şemayı sonradan değiştirmek hedefi sessizce değiştirmez.
7. **Kaydet ve Dene** sekmesinde adı/açıklamayı yaz; **Kaydet ve Havuzda Kullan** veya **Taslak Olarak Kaydet** seç. Dosya otomatik olarak `Assets/Scripts/Scriptables/Puzzles` altında oluşur.
8. **Oyunda Dene** ile çözümü oyuncu gibi dene. Tahta ve defter boş başlar; örnek çözüm oyuncuya gösterilmez.

Kayıtlı bir örneği değiştirip ayrı puzzle yapmak için **Kopyasını Kaydet** kullan; yeni kimlik ve yeni dosya oluşur. Normal **Kaydet**, açtığın kaydı günceller.

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

**Kitaplar** sekmesinde ek gözlemler yazabilir, hedefin masa kitabında kalmasını seçebilir ve dağıtım önizlemesini görebilirsin. Parça etkileri ve gereken kurallar otomatik eklenir; parça açıklaması Kurallar sekmesinden özelleştirilebilir. Sayfada en fazla iki bilgi bulunur.

**Havuzda Kullan** ile kaydedilen geçerli puzzlelar `PuzzlePool.asset` üzerinden yeni oyunda eşit olasılıkla seçilir. Yeni oyunda kitapların içerik yerleşimi de değişir. Aynı puzzleın sonraki oyunda tekrar seçilmesi mümkündür. Çalışma laboratuvarı `scn_LaboratorySynthesis` bu havuza bağlıdır.

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

## Geliştirici kontrolleri

## Konulu kitaplar ve otomatik kaynakça

Laboratuvardaki 47 kitabın kapağında ve sırtında kalıcı, benzersiz bir başlık vardır. Etkileşim yazısı kitap adını ve rafını gösterir; rafların da isimlikleri bulunur. `PuzzleBookIdentity` üzerinden konu kabulünü ve sayfa kapasitesini ayarlayabilirsin. Konular temel aygıtlar, sinyal numuneleri, sinyal işlemleri, pigmentler ve karışım işlemleridir; bir kitap birden fazla konuyu kabul edebilir.

Kaydedilmiş veya rastgele üretilmiş hedef aynı dağıtıcıdan geçer. Önce bütün kartlar uygun kitapların güvenli sayfa alanlarına rastgele yerleşir. Sonra hedefin **Araştırma Kaynakları** bölümü gerçek kitap adı, raf ve sayfa numarasından üretilir. Ortak gözlemler tek kart olarak tutulur; ilgisiz kitaplara kopya ipucu konmaz. Kitap dizinleri toplanabilir kart değildir. Hedefi deftere/tahtaya taşımak kaynak bilgisini de taşır; bağlantı sırasını açıklamaz.

Yeni oturum kitap ve sayfa seçimini değiştirir. Devam Et aynı yerleşimi, kaynakçayı ve kart alanlarını yükler. Yeterli uygun alan bulunmazsa sistem yanlış kitaplara yerleştirmek yerine kurulumu açıklamalı olarak reddeder.

Araştırma sırasında ekranın üstündeki **Defter** ve tahtadayken **Belgeler** düğmeleri açık/kapalı durumda görünür kalır. **Tab** defteri açıp kapatır. Parçaların başlığı başlatıcı, işlem veya sonlandırıcı rolünü gösterir; sürüklenen notun kâğıt zemini korunur.

Yeni kontroller: **Araştırma Dağıtımı**, **Ortak Konulu Kitap Kapasitesi**, **Araştırma Hedef Görseli**, Play sırasında **Araştırma Arayüzü**. Fiziksel geometri için **Senzora → Book Surface → Projection Regression / Layout Regression** kullanılabilir.

**Senzora → Puzzle Atölyesi → Doğrulama → Şema Yerleşimi**: gerçek editör penceresinde iki aileyi, farklı pencere boylarını, yakınlaştırmayı, kaydırmayı ve 64 parçanın sığdırılmasını denetler. Çizilen ilk/son düğüm ve kırpma alanı piksel konumlarıyla kontrol edilir; seçim, bağ kurma ve sürükleme gerçek IMGUI olaylarıyla sınanır. Görüntüleme/zoom belgeyi değiştirmemelidir. Kontrol geçici pencerede çalışır ve mevcut taslak dosyalarını geri koyar.

**Senzora → Puzzle Atölyesi → Doğrulama → Tümü (Edit Mode)**: eski sinyal kontrolleri, 360 seed, karışım korunum kuralları, hedef kopyalama, Undo, taslak kurtarma ve güvenli kaydetme.

**Renk Denemesini Hazırla → Play → Canlı Akış**: gerçek kitapları okur/toplar, defterden fiziksel tahtaya parça taşır, ölçer, snapshotı ve izole dosya kaydını geri yükler, ölçümü Turing'in gerçek çarklarında sınar. Normal kullanıcı kaydı korunur. Kullanıcı için çözüm kısayolu değildir; geliştirici doğrulamasıdır.
