# Test defterinin görsel dili

Bu önceki kağıt/spiral denemesi, [LaboratoryLedger.md](LaboratoryLedger.md) içindeki yeni laboratuvar defteriyle değiştirilmiştir. Güncel font DM Sans Medium, güncel sprite LaboratoryLedger.png; yırtık etiketler ve Special Elite artık kullanılmaz.

Özgün spiralli `Notebook.png` korunur. Okunacak yazılar pütürlü zeminin üzerine doğrudan basılmaz: başlık, sayaç, sekmeler, yönlendirme ve düğmeler açık renkli kağıt etiketlere yerleşir. Notlarda aynı kağıt kullanılır; önceki mavi/sarı zeminler, bantlar ve kesik eskiz çerçeveleri kaldırılır. Veri kimlikleri ve arşivdeki asıllar değişmez.

Başlık ve düğmeler: mevcut **Special Elite**. Gövde: mevcut **Liberation Sans**, defter kartlarında en az 14 punto; ekrandaki ölçekle birlikte büyür. Uzun hedef açıklamaları için çizim ve metne ayrı alan ayrılır. Fontlar ve iki sprite, test sahnesindeki `NotebookWorkspaceController` üzerinden değiştirilebilir.

Boşta **Tab** defteri açar/kapatır. **Q / E** sırasıyla Parçalar, Bilgiler, Hedefler ve Ölçümler arasında dolaşır; uçta başa döner. **Fare tekerleği** mevcut bölümün sayfasını değiştirir; ilk ve son sayfada durur. Defter açıkken test oyuncusunun bakış, yürüme ve dünyayla etkileşimi bekler. Kapatınca imleç oyuna döner. Kitap, tahta veya makine etkileşiminde bu serbest defter kısayolları çalışmaz; mevcut etkileşim kontrolleri korunur.

## Kağıt varlığı

- Dosya: `Assets/Sprites/UI/UIInGame/ResearchPaper.png`
- Üretim: yerleşik ImageGen, şeffaf arka plan. Orijinal defter sprite'ı değiştirilmedi.
- İçe aktarım: Sprite / Single, 48 piksel kenarlar, 200 PPU, bilinear, sıkıştırmasız. UI içinde Sliced olarak kullanılır.

Kullanılan istem:

> Use case: photorealistic-natural. Asset type: a reusable blank paper sprite for a dark atmospheric first-person investigation game's notebook UI. Generate ONE isolated rectangular old field-research paper slip, almost frontal orthographic flat scan, landscape 4:3 aspect, filling nearly all the image with a tiny transparent margin. Warm grey ivory paper, restrained slight age, realistic small fibers, gentle irregular cut edges, a subtle dog-ear on one lower corner and minimal dark wear confined to outermost edges. The central 85% MUST be clean uniform pale ivory with extremely subtle grain so small black text will be legible when overlaid by game code. Muted desaturated palette, not bright yellow, not fantasy parchment, not cartoon. No handwriting, no text, no printed lines, no symbols, no border strokes, no tape, no pushpins, no objects. Soft very thin contact shadow and genuinely transparent background. This sprite will also be nine-sliced into small labels and paper tabs; preserve smooth blank texture and avoid large creases.

## Yeniden kontrol

Geliştirici menüleri yalnız elle çalıştırılır; normal oyun başlangıcında test notu eklenmez. `Defter Stili Deneme Notlarını Hazırla` Play oturumunda mevcut kitap içeriklerinin kopyalarını oluşturur. `Defter Görünümünü Doğrula`, `Defter Arşivini Doğrula`, `Kart Panosunu Doğrula` ve `Serbest Defteri Doğrula` görünüm, okunurluk, veri koruma ve etkileşim koşullarını denetler.

26 Ekim 2026: son derleme sonrası dört sekmenin sabit boyutu, uzun hedef açıklaması, bütün parça sayfaları, bütün bilgi sayfaları, veri kaybı olmadan JSON dönüşü, serbest defterde bölüm sırası/sarma, sayfalama, duraklatma ve kapatınca imlecin oyuna dönmesi geçti. Tahtada bağımsız parça yerleşimi, gerçek kart sürükleme, ölçüm arşivi ve Turing okuyucusu da geçti. Son konsolda hata/uyarı yoktu; Play durduruldu ve deneme notları sahneye kaydedilmedi. Görsel: `Assets/Screenshots/NotebookPaperFinal.png`.
