# Kitap optimizasyonu ve fiziksel tahta — 28 Ekim 2026

Gerçek üç hedef / 47 kitap dağıtımı, seed `27102026` ile ölçüldü. Kitap kimlikleri, sayfalar, kart sayıları ve SHA256 içerik değerleri korunuyor. Dönüş başına dört ısınma ve 21 ölçüm, üç tekrar: her dolu örnekte 63 ölçüm. Süre `UpdateTurn + Canvas.ForceUpdateCanvases` CPU maliyetidir; toplam oyun FPS'i veya GPU süresi değildir.

| Örnek | Kart / sayfa | Önce medyan / p95 | Sonra medyan / p95 | Tekrar açılış önce → sonra | Dönüş başlangıcı önce → sonra |
|---|---:|---:|---:|---:|---:|
| Devre Günlükleri | 7 / 4 | 600,16 / 665,02 ms | 0,91 / 1,24 ms | 197,14 → 1,82 ms | 740,76 → 6,11 ms |
| Akışkanların Davranışı | 8 / 4 | 617,47 / 713,53 ms | 0,92 / 1,36 ms | 199,17 → 2,22 ms | 684,91 → 6,14 ms |
| Deney Masası | 6 / 4 | 575,66 / 658,98 ms | 0,94 / 1,38 ms | 179,21 → 1,69 ms | 670,77 → 6,64 ms |

Önceki ölçüm 27 Ekim editör günlüğünden korunmuştur: `UserSettings/PuzzleWorkshop/book-prior-measurements.log`. Unity yeniden açılırken Temp içindeki eski JSON silindi; yeni ölçümle eski içerik parmak izleri ayrıca karşılaştırıldı. Önceki oturumdaki `Optimization Check Regression` da aynı içerikle geçti. Güncel ham sonuç `Temp/book-optimization-after.json`; 4 ms medyan bütçesi ve her örnekte 63 ölçüm ayrıca doğrulandı. Bu oturumdaki ilk hazırlama + ilk Canvas maliyetleri 52–233 ms; ilk font/mesh hazırlığının sıfır takılma garantisi verilmez.

## Uygulanan değişiklikler

- UV üçgenleri, barycentrik eşleşmeler, TMP yazı noktaları ve grafik geometrisi önbellekte tutulur. Mürekkep kıvrılan gerçek kâğıt üçgenleri üzerinde hareket eder.
- Her kare yalnız görünür ve hareket eden sayfanın mürekkebi güncellenir. Sayfa nesneleri, mesh ve bufferlar yeniden kullanılır. Kapalı kitabın Canvas'ı ve gizli sayfalar girdiye kapalıdır; metin bileşenlerini tekrar etkinleştirerek yazı üretimi tetiklenmez.
- Dikdörtgen kartlar ortak köşeli ağ kullanır. Şeffaf sürükleme bölgeleri ayrıntılı geometri üretmez; UV üzerinden tıklama korunur. Sayfa dönüşünde veya kapanışta devam eden kaynak sürüklemesi iptal edilir.
- Araştırma arayüzündeki Defteri Aç/Kapat düğmeleri kaldırıldı; Tab ve × korundu. Belge panosu ve ölçüm cihazı fiziksel gövdeli dünya nesneleridir. Açık belgeler gerçek çıkış/yeniden girişte açık kalır; dışarıdayken oyuncu girdisini yakalamaz.
- Defterdeki büyük hedef kartı korunur. Tahta hedefi 270×246'dır; kaynak yönlendirmeleri tam metin olarak kaydırılır. Kapalı panoda hedef yenilenirken de metin yüksekliği hesaplanır.

## Doğrulama ve sınırlar

`Optimization Cached Projection Regression`: ileri/geri dönüşte 2450 nokta, en büyük dünya hatası yaklaşık 0,0000014 m. `Book Surface / Projection Regression` ve `Layout Regression` geçti.

`Fiziksel Tahta Deneyimi`: Tab/×, fiziksel düğmeler, kapalı/açık panoda hedef metni, tekerlekle kaydırma, gerçek çıkış/yeniden girişte belge görünürlüğü ve kaydedilen açık durum. `Araştırma Arayüzü`: sürüklenen kağıdın zemini, parça rolleri ve hedef kaynakları. Canlı doğal kitap hareketinde ileri/geri dönüş, duraklatma, toplama, dönüş sırasında çıkış ve konumun geri gelmesi doğrulandı.

Önceki oturumun tam canlı testi 47 kitabı dolaşıp üç hedefi farklı sırada gerçek defter/tahta/Turing üzerinden tamamladı; başka hedefin ölçümünü reddetti, snapshotı geri yükledi ve normal kayıt dosyasını değiştirmedi.

Aynı tam test son üretim derlemesinde 28 Ekim'de tekrar geçti: 47 kitap, üç hedefin gerçek terminal teslimi, başka hedefin ölçümünü reddetme, snapshot geri yükleme ve normal kayıt koruması. Son Play oturumunda çalışma zamanı hatası yoktu; test sonrasında Play durduruldu.

Önceki oturumda toplam editör karesi kitap kapalıyken bile yaklaşık 530 ms idi. Yeniden açılan editörde 30 karelik CPU+Editor Profiler kaydı medyan **22,10 ms**, p95 **24,98 ms** verdi (odak açık, deep profiling kapalı). GameView ve SceneView çizimleri de editör maliyetine dahildir. Önceki 500 ms durumunun nedeni bu yeni kayıtla ispatlanmış değildir; güncel oturumda tekrarlanmadı. Bağımsız paket FPS'i doğrulanmadı.

Son üretim koduyla gerçek `Interact / NextPage / PreviousPage / ForceExit` canlı ölçümü tamamlandı: boşta 30 kare medyan **18,43 ms**, p95 **23,62 ms**. Dolu kitapların açılış/çevirme/kapanış medyanları yaklaşık **19–24 ms**, kitap açıkken kamera hareketi **17–21 ms**. En uzun tek kare 98,09 ms; tamamen takılmasız çalışma iddiası yoktur. İlk açılış, tekrar açılış, iki yönde ikişer dönüş ve boş kitap dahil ölçüldü. Bunlar bu bilgisayardaki toplam editör kareleridir; kitap CPU mikro ölçümüyle karıştırılmamalıdır. Ham kayıt `book-optimization-live-after.json`.

Tahsis sayacı kalibrasyonu bu Unity editöründe başarısız; JSON'daki sıfırlar tahsis olmadığına kanıt değildir. Canlı Profiler tahsisleri tüm kareye aittir. Profiler kontrolü önceki kayıt ayarlarını geri yükler.

Komutlar: **Senzora → Book Motion → Optimization Capture After / Optimization Check Regression / Optimization Cached Projection Regression / Optimization Live Frames After (Play) / Optimization Live Main Thread Breakdown (Play)**. Karşılaştırma için Before ve After JSON'ları aynı Temp dizininde tutulmalıdır. Yerel kanıtlar `UserSettings/PuzzleWorkshop/evidence` altında saklanır.

## Önceki teşhis (üretim düzeltmesinden önce, 27 Ekim)

Kapsam: kullanıcının son değişikliklerinden sonra `scn_LaboratorySynthesis` ve `scn_labratory` sahnelerindeki 106 kitap örneğinin ayarları incelendi. Geçici preview sahnelerinde Atlas ve üç normal kitap örneğinin sayfa dönüşü ölçüldü. Üretim kodu ve kayıtlı sahneler değiştirilmedi; yalnız editör ölçüm aracı eklendi.

Bu bir **editör CPU mikro ölçümüdür**, doğrudan GPU veya toplam oyun FPS testi değildir. Değerler üç ısınma örneğinden sonraki dokuz örneğin medyanıdır.

| Kitap / yol | Gerçek sayfa sayısı | Sayfa hesaplama | UI mesh yenileme | Yalnız kâğıt mesh'i |
|---|---:|---:|---:|---:|
| Parça Atlası | 4 | 24,33 ms | 58,03 ms | 3,67 ms |
| Normal kitap örneği 1 | 8 | 3,13 ms | 0,81 ms | 3,15 ms |
| Normal kitap örneği 2 | 8 | 3,53 ms | 1,02 ms | 3,68 ms |
| Normal kitap örneği 3 | 8 | 3,00 ms | 0,86 ms | 3,58 ms |

Atlas dönüşünde 47 Graphic ve 1612 TMP metin köşesi bulunuyor. `animatedInk` listesini yalnız ölçüm sırasında boşaltıp aynı kâğıt dönüşünü sürdürünce hesaplama yaklaşık 24 ms'den 3,7 ms'ye indi. Büyük yük kâğıt mesh'inden çok basılı içerik ve UI yenileme yolunda.

Kök nedenler:

- `BookPageBlockSurface.Motion.RefreshInkGeometry` sabit ve görünmeyen sayfalar dahil bütün grafikleri dolaşıyor; metin köşelerini yeniden yansıtıyor, diğer grafikler için `SetVerticesDirty` çağırıyor.
- `BookPagePrintedGraphic.Project` her köşe için koordinat dönüşümleri, kamera sorguları ve sayfa haritalaması yapıyor. Dönen yüzde `MapTurningPage` dört ayrı UV yüzey sorgusu yapıyor; `MapPagePoint` her sorguda üçgen listesi tarıyor.
- `RefreshPages` dönüşün başında ve sonunda not nesnelerini silip yeniden üretiyor; bu da sürekli kare yükünün yanında giriş/bitiş takılmaları için ayrı bir kaynak.

Diğer kitaplar:

- Sorular, İlkeler ve Deney Kayıtları şu an ikişer sayfalı. Döndürülecek ikinci forma yok; bu sayfa çevirme sorunu mevcut içerikleriyle tetiklenmiyor. Daha fazla sayfa eklenirse aynı basılı içerik yolunu kullanacaklar. Açılış/kapanış maliyeti bu ölçümün konusu değildir.
- Laboratuvar kopyasındaki 53 kitap fiziksel sayfa yoluna ayarlı. İçeriksiz normal kitapların aynı ölçekte UI yükü yok, fakat kâğıt dönüşünün birkaç ms maliyeti var.
- `scn_labratory` içindeki 53 kitapta fiziksel sayfa yolu kapalı; burada incelenen CPU kıvrım/basılı içerik darboğazı o sahnede devreye girmiyor. Diğer GPU/sahne maliyetleri ölçülmedi.

Önerilen düzeltme: sayfa başında UV üçgen eşleşmeleri ve temel konumları önbelleğe almak; her kare yalnız görünen, gerçekten dönen yüzdeki mürekkebi güncellemek; sabit sayfaları ve gizli yüzleri yeniden üretmemek. Sonra aynı ölçümü ve gerçek Play FPS karşılaştırmasını tekrarlamak.

Yeniden çalıştırma: Edit modunda `Senzora > Book Motion > Audit Page Turn CPU`. Ham sonuç: `Temp/book-performance-audit.json`. Bellek tahsis sayacı bu editörde sıfır döndü; bu sonuç tahsis olmadığına kanıt olarak kullanılmadı.
