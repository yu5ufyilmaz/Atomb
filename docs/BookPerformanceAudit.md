# Kitap sayfa geçişi CPU incelemesi — 27 Ekim 2026

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
