# Başlatıcı-X başlangıç bulmacası

`Assets/Scenes/scn_SynthesisBlockout.unity` sahnesini tek başına açıp Play'e basın.

1. **1** ile kitaba geçin. İlk iki sayfada hedef, iki kural notu, U-235, BE-9 ve REF vardır. **D/A** diğer adayların olduğu sayfayı çevirir.
2. Çift çerçeveli **HEDEF** notunu defterin üstündeki **TOPLANAN HEDEFLER** alanına bırakın. Hedef, normal kağıda bırakılırsa açıklama gösterilir. **Hedefler** bölümünde toplanan hedefleri görebilirsiniz; bu koleksiyon sayfalardan bağımsızdır. Parça ve bantlı kural notlarını **Notlar** sayfasına bırakın. Sayfa okları ilerledikçe boş sayfalar otomatik açılır; dolu sayfaların küçük numaralı etiketleri defterin yanında görünür ve tıklanınca o sayfayı açar. Etiket listesi fare tekerleğiyle kaydırılır.
3. **F** ile çıkın, **2** ile tahtaya geçin. **Hedefler** bölümünden hedefi tahtaya sürükleyip şemayı sabitleyin. **Notlar** bölümünden U-235, BE-9 ve REF'i de tahtaya sürükleyin.
4. Bütün yuvalar doğrudan kullanılabilir; **(x) silme/uç açma adımı kaldırıldı**. REF kartında ve tahtadaki etiketinde **ÇİFT BAĞ (=)** yazar. Yuvası başlangıçta **= 0/2** gösterir.
5. **Sol tuşu basılı tutarak** BE-9'un ucundan U-235'in boş bir ucuna tek bağ çizin. REF'i U-235'in başka bir ucuna bağlayınca **= 1/2** görünür. Aynı iki uç arasında ikinci kez çizerek çift bağı tamamlayın: **= 2/2**. İlk çizgi, ikinci çizimin önizlemesi sırasında yerinde kalır. Yanlış yere bırakmak veya sürüklemeyi iptal etmek mevcut bağı silmez. Sağ tık bir çift bağı tek bağa indirir, tek bağı siler; bu yalnız yanlış bağlantıları düzeltmek içindir.
6. **SENTEZLE / TEST ET** düğmesine basın. Doğru çözüm **“Doğru bildin”** gösterir. Parçalar korunur; bu alıştırma yeni ürün üretmez.

Araç seçme düğmeleri yoktur: boş defter kağıdında, fiziksel kitap sayfasında ve boş tahtada **sol tık çizim, sağ tık silgi** işlevindedir. Silgi dokunduğu serbest kalem izini kaldırır. Defter kartları ve defter penceresi taşınmaz; kartı tahtaya sürüklemek kopyasını çıkarır. Defter kartına sağ tık kartı siler. Tahtadaki parçalar gövdesinden sol tuşla sürüklenir; sürüklerken **Q/E** veya fare tekerleği döndürür. Çerçeveli kitap notları çizimden önce sürükleme girdisi alır.

Defter ve tahta çizimleri oyun kaydına dahildir. Kitap üzerine çizimler aynı oyun oturumunda sayfa çevirip kitabı yeniden açınca korunur; kitap çizimlerinin oturumlar arası kaydı bu sürümde yoktur.

**Hile Aç/Kapat** defterin içinde salt okunur sentez rehberini gösterir. Rehberin okları tarif değiştirir; metnin üzerinde fare tekerleği işlem adımlarını kaydırır. Gerçek parça kimlikleri, tek/çift bağlar, bağlantı ilerlemesi, kurallar ve sonuç gösterilir. Mevcut alıştırma, varsa bu oyunun beş aşamalı zinciri ve yapılandırılmış eski tarifler listelenir. Bu tahtada çalışmayan tarifler **arşiv / kapalı** olarak işaretlenir. Rehbere bakmak görev açmaz veya parça üretmez; kapatmak notları ve sayfayı korur. Önceki kayıtlardaki hedefler otomatik olarak yeni ortak hedef koleksiyonuna aktarılır. Eski kayıtlardaki kapalı yuvalar da açık yüklenir.

Çözüm: **BE-9 — U-235 ═ REF**. PO-210 ve KALKAN yanıltıcı adaylardır. İlk örnekteki MOD köprüsü kuralı bu alıştırmaya dahil değildir.

İçerik `Assets/Scripts/Scriptables/Formulas/Starter/Quest_StarterX.asset` üzerinden düzenlenir. Sahnedeki `Synthesis Blockout > Starter Puzzle` bu hedefi seçer. Özgün kitabın atlası ve UV bölgeleri korunur; alıştırma Play sırasında kendi not kartlarını kullanır.

Editör kontrolü: **Senzora > Doğrulama > Başlatıcı-X Döngüsü**. Genel kontroller: **Senzora > Doğrulama > Tüm Sentez ve Çalışma Alanı**.
