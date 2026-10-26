# Kitap hareketi

26 Ekim 2026: Mevcut kitap rig'i ve sesleri kullanılır. Yeni paket veya fizik simülasyonu eklenmez.

- Varsayılan 0,5 saniyelik `moveDuration` ile kaldırma ve kapak açılması toplam yaklaşık 0,82 saniyede üst üste gerçekleşir. Dönüş biraz geriden gelir; hareket küçük bir yay izler. Sabit 1 saniyelik bekleme kaldırılmıştır.
- Kapanma ve yerine bırakma yaklaşık 0,72 saniyede birlikte gerçekleşir. Oyuncu kontrolü kapanma başlarken geri gelir.
- Fiziksel kitap sayfası UV yüzeyinden üretilen 24 × 8 bölümlü geçici bir mesh ile döner. Basılı notlar aynı eğriyi kullanır; ön/arka yüzler ve alttaki yaprak ayrı tutulur. Sayfa mesh'i iki taraflı gölge üretir.
- `pageFlipDuration` mevcut ayardır; laboratuvar kitaplarında 0,5 saniyedir. Yeni hareket fiziksel içerik kullanan kitaplarda etkinleşir; diğer kitapların materyal tabanlı dönüşü korunur.
- Kâğıt sesi dönüşün başlangıcından kısa süre sonra, kapak sesi açılırken veya kapanıp otururken çalar.
- Hareket sırasında sayfa görünür kalır; tıklama ve sürükleme kilitlenir. Duraklatma hareketi dondurur. Dönüş/açılış sırasında çıkış istenirse mevcut hareket tamamlanınca kitap kapanır.

Doğrulama menüleri:

- `Senzora > Doğrulama > Doğal Kitap Hareketi`: gerçek prefab üzerinde iki yön, yüz yönü, kıvrım, başlangıç/bitiş ve etkileşim geri dönüşü.
- `Senzora > Doğrulama > Kitap UV Çerçeveleri`: mevcut UV, eğri yüzey, tıklama ve ölçek kontrolleri.
- Laboratuvar Play modunda `Senzora > Book Motion > Preview Live`: Atlas kitabını açar; okunabilirlik, deftere aktarım, iki yön, duraklatma, çıkış ve orijinal poz kontrolü yapar. Ara kareleri `Temp/book-motion-*.png` altına kaydeder. Test oturumundaki defter içeriğine not ekler; kalıcı kayıt oluşturmaz.
