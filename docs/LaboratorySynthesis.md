# Laboratuvar araştırma geçişi

27 Ekim 2026 temizliği: tek etkin puzzle **Çift Kayıt**tır. Eski sembol yerleştirme, başlangıç/ikinci alıştırma, A/B araştırmaları, İkinci İz ve beş aşamalı prosedürel üretim kaldırıldı. `SignalExperiment`, `SignalTrialContent` ve `FormulaCatalog` mevcut akışı yürütür. Ortak çizim şablonu `SignalPartTemplate.asset`, prefabı `P_SignalPart.prefab`dır; GUID bağlantıları korundu. Önceki kurulum menüleri kaldırıldı. Aşağıdaki notlar geçişin tarihsel kaydıdır; güncel temizlik kapsamı `docs/PuzzleCleanup.md` içindedir.

Çalışma sahnesi: `Assets/Scenes/scn_LaboratorySynthesis.unity`.

`scn_labratory` kopyasındaki gerçek oyuncu, Turing Machine, tahta, etkileşim noktaları ve kamera referansları kullanılır. Yeni deney içeriği `Laboratory Synthesis` nesnesindedir. Kitapların mevcut sahne konumları korunur; kapak ve sırtlarında araştırma adları bulunur.

Son yerleşim: hedef kitabı artık masadaki mevcut `BOOKS/P_Book_Puzzle_symbol_01` kitabıdır; kapağı/sırtı `HEDEFLER` olarak etiketlidir. Diğer üç araştırma kitabı RAF2/RAF3/RAF4'te kalır. Eski RAF1 hedef etiketi kaldırıldı. Masa kitabının eski sembol modu yalnız bu kopyada kapalıdır; masa/kitap dönüşümleri değiştirilmedi.

- Tab: defteri ekranın sol altından aç/kapat.
- Q / E: defter bölümleri. Fare tekerleği: bölümün sayfaları.
- Gezerken defter oyunu durdurmaz, hareketi kilitlemez ve fareyi serbest bırakmaz.
- Kitap, tahta ve Turing ölçüm okuyucusunda fare ve sürükleme çalışma alanı açılır.
- Turing'e girişte açık gezi defteri kapanır; oturma/kalkma ve kamera konumları ana sahnedeki değerlerdir.
- Ele bağlı defter ve makinelere fiziksel defter taşıyan bileşenler bu kopyada devre dışıdır.

Sunum düzeltmesi: kitap okuma mesafesi 5 yerine 7,5; kitap solda, defter sağ altta %80 ölçektedir (%20 küçültme). Gezi defteri aynı ölçekte sol alttan açılır. Dört etiketli araştırma kitabındaki notlar taşıma bandı olmadan gerçek sayfa UV yüzeyine basılır; ince kesik sınır ve fare vurgusu alınabilir bölgeyi gösterir. Sürüklerken yazı ve çizim birlikte opak bir kağıt kopyası üzerinde görünür. Kitabın boş sayfasını sürüklemek kitabı taşır; kitap çizimi kapalıdır. Diğer raf kitaplarının ortak prosedürel hedef UI'ı kapalıdır. Hedefler, parça atlası, ilkeler ve deney gözlemleri dört ayrı kitapta kalır.

Tahta çalışma sınırları, eski 2,5 × 1,04 alan yerine görünür modelden hesaplanan yaklaşık 3,24 × 1,42 alanı kullanır. Eski `SynthesisButton` deneme kübü kopyadan silindi; `Deneyi Ölç` UI düğmesi kullanılır. Bu ayarları yalnız çalışma kopyasında uygulayan komut: `Apply Presentation Fixes`.

Sunum sonrası doğrulama: beş açık araştırma sayfası grubunda en üst UI raycast sonucu gerçek basılı nota ulaşıyor; aynı notun gerçek sürükleme olaylarıyla arşive aktarılması geçti. Kitapların bütün notları, tahta ölçümü ve Turing okuyucusu yeniden test edildi. Dört tahta köşesi sentez collider'ı içinde; sembol kitapları dahil tüm raf kitaplarında ortak prosedürel hedef içeriği kapalı ve prefab override'ları kayıtlıdır.

Hassas fare etkileşimlerinde Lab profilinin lens bozulması görüntüyü `ScreenPointToRay` hesabından farklı yere çiziyordu. Geçiş sahnesinin geçici Volume override'ı yalnız kitap, tahta ve Turing ölçüm etkileşiminde lens bozulmasını/screen scale etkisini sıfırlar. Gezinti sırasında mevcut profil ve basınç efektleri sürer; ortak Volume Profile asset'i değiştirilmez. `Validate Board Pointer` etkin render stack'i ve üç farklı tahta konumunda fare-parça hizasını kontrol eder.

Son etkileşim doğrulaması: boş sayfadan 75/25 piksel taşıma ve geri dönüş; beş kitap sayfası grubunda opak/taşmayan kağıt önizlemesi ve gerçek sürükleme olayları; sıfır lens bozulması altında üç tahta konumunda bir pikselden az hedef farkı; ölçüm ve Turing akışı geçti. Görsel önizleme: `Temp/lab-note-drag.png` (`Preview Drag Feel`).

Deftere insanın kopyaladığı parça/hedef/bilgi notları [Caveat](https://github.com/googlefonts/caveat) el yazısı fontunu, kalem tonunu, eskiz çerçevesini ve küçük sabit eğimleri kullanır; tekdüze gri kart zeminleri yoktur. Kitap baskısı ve makineden çıkan ölçüm kartı kendi basılı görünümünü korur. Fontun Türkçe karakterleri doğrulandı; SIL OFL lisansı `Assets/Fonts/Caveat/OFL.txt` içindedir.

Kitap giriş/çıkışında lens düzeltmesi 0,2 saniyede yumuşak uygulanır. Karakter/kamera/Animator bileşenleri kapatılıp yeniden başlatılmaz; mevcut `SetFrozen` ve bakış girdisi kullanılır. Kapatma başladığı anda kontrol geri verilir; kitabın yerine dönmesi oyuncuyu bekletmez. Biriken bakış girdisi sıfırlanır.

Kitapta istemsiz dönüş düzeltmesi: `CharacterController.Move` dondurulmuşken gövdeyi ana kameraya doğru döndürüyordu; tam kamera kilidi bu dönüşü de durdurur. Kamera kafa kemiğine bağlı olduğu için kitap okurken karakter Animator hızı mevcut pozda sıfırlanır, kapatma başlar başlamaz önceki hızına döner. Cinemachine'in sahnedeki mevcut Perlin nefes sallantısı değişmez. Regresyon testi gövdeyi bakışa göre 25 derece geriden başlatır ve gerçek kareler boyunca gövde/kamera hedefi/temel kamera yönünün 0,1 dereceden fazla değişmediğini denetler; kasıtlı noise düzeltmesini temel yön ölçümünden ayrı tutar. Eski kod testte ilk karelerde 1,19 derece gövde ve 1,13 derece hedef dönüşüyle başarısız oldu. Düzeltmeden sonra dört kitapta sabitlik, kapanışta animasyon ve kontrolün geri gelmesi, tahta ve Turing dahil tam Play akışı geçti.

26 Ekim 2026 devam doğrulaması: Unity yeniden açıldıktan sonra kayıtlı kopyada `Validate Scene Copy` ve baştan sona `Validate Play Flow` yeniden geçti. Masadaki hedef kitabı, dört kitaptan toplama, Türkçe el yazısı ve taşmayan sürükleme önizlemeleri, şeffaf notların gerçek UI raycast ile yeniden tutulması, kitap kapanırken anında/sıfırlanmış kontrol, tahta fare hizası, ölçüm kartı, özgün Turing oturma/okuma/çıkış akışı doğrulandı. Görüntü `Temp/lab-notebook.png` olarak yenilendi; testten sonra Play kapatıldı. Yerel kayıt dosyasının son değişiklik zamanı değişmedi.

Kopyada eski tutorial sırası/etkileşim whitelist'i ve hazır eski tahta parçası kapalıdır; kitaplar ve makineler araştırma akışı için erişilebilir. Laboratuvarın mevcut oyun başlangıç akışı korunur. Build Settings ve menüdeki sahne geçişi orijinal laboratuvarı kullanmaya devam eder; bu çalışma sahnesi Editor'den açılır.

Bu ara geçiş kopyası oturumluk çalışır: Continue ve disk kaydı kapalıdır. Ölçüm kodlarının oturumlar arasında saklanması ayrı kayıt entegrasyonuna bırakıldı; bu sahnede yeni oyun açmak veya çıkmak ana oyunun kayıt dosyasını değiştirmez.

Yeniden oluşturma: `Senzora > Lab Migration > Create Laboratory Copy`. Mevcut kopyayı üzerine yazmaz. `Update Copy Bindings` yalnız kopyada eksik kitap yüzeyi bileşenlerini ekler ve eski tahta örneklerini kapatır; sonraki elle düzenlemelerde gerekmedikçe çalıştırmayın.

Doğrulamalar: `Validate Scene Copy` edit modunda oyuncu/makine alt nesnelerini ve sahne referanslarını orijinalle karşılaştırır. `Validate Notebook` Play modunda fare, oyun süresi, Q/E ve açılma yönünü denetler. `Validate Play Flow` geçici Play oturumunda gerçek kitaplardan not toplar, tahta ve ölçüm akışını çalıştırır, Turing okuyucusuna kağıt aktarır ve oyuna dönüşü kontrol eder. Bu geliştirici komutu notları topladığı için normal oynanış sırasında otomatik çalışmaz.

26 Ekim 2026 Unity doğrulaması: sahne karşılaştırması, dört gerçek kitap, deney tahtası, ölçüm çıktısı ve Turing okuyucu akışı geçti. `Validate Menu Start` temiz Play oturumunda mevcut Yeni Oyun/Timeline/kalkış akışını, menüde Tab korumasını ve hem kilitli hem serbest fareli defter davranışını doğruladı. Gezi defteri görüntüsü: `Temp/lab-notebook.png`.

İlk Play denemelerinde, oturumluk kayıt koruması eklenmeden önce mevcut otomatik kayıt sistemi yerel `SenzoraLocalSave.json` dosyasına yazdı. Önceki kayıt içeriği geri yüklenmedi. Koruma eklendikten sonraki testlerde dosyanın değişmediği kontrol edildi.
