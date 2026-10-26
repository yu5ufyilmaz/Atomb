# İkinci İz — oynanabilir test

Sahne: `Assets/Scenes/scn_SynthesisBlockout.unity`. Play ile başlar; hazır çözüm veya toplanmış not verilmez.

Bu bir oynanış prototipidir. Tahta beş bileşenli kurgusal bir iz modeli kullanır; genel amaçlı fizik simülasyonu değildir. Aynı ilkeler birden fazla geçerli yerleşime izin verir.

## Oynamak

- **1:** Araştırma Soruları — tek quest ve eski deneyin düzeni.
- **3:** Parça Atlası — beş tekrar kullanılabilir çizim.
- **4:** Bağ İlkeleri — sönüm ve algılama eşiği görselleri.
- **5:** Deney Kayıtları — gecikmiş iz ve başarısız ana hat denemesi.
- **2:** Tahta — hedefi sabitle, topladığın çizimleri yerleştir, uçları tek çizgiyle bağla; **DENEYİ ÖLÇ**.
- Ölçüm kağıdını deftere sürükleyebilirsin. Bir sonraki ölçüm, defterdeki eski çıktıyı değiştirmez.
- **6:** Turing deneme kopyası — W/S grup, A/D çark, oklar veya tekerlek çevirme, Enter sınama, F çıkış.

Odada yürüyüp nesnelere tıklayarak da erişilebilir. Kitap isimleri kapak ve sırtlarında durur. Defterin **DEFTERİ KAPAT** düğmesi tahtayı açar; alt araçlar kalır.

Tahta sonuç üretir, görevi tamamlamaz. Görev ancak ölçüm kodu Turing'de kabul edilince tamamlanır. Terminalde son deftere alınmış ölçümün kodu gösterilir; kodu hatırlayarak girmek de mümkündür. Test kopyasında sabit `ECHOECHO =` öneki hazırdır; oyuncu ölçümün üç rakamını ayarlar. Asıl makinenin kontrolleri ve üretim sahnelerindeki şifre akışı korunur.

## Eski görevler

A/B araştırma içerikleri, eski quest assetleri ve kurulumları silinmedi. **Senzora > İkinci İz > Önceki A-B Denemelerine Dön** menüsü yeni modu kapatır. **Test Sahnesine Kur** yeni modu yeniden açar. Bunlar yalnız test sahnesini kaydeder; Play dışında kullanılır.

## Kontrol

25 Ekim 2026 / Unity 6000.3.10f1: gerçek kitaplardan notlar hedeften önce toplandı; defterden hedef ve parça bırakma, fiziksel uçları bağlama, ölçüm kağıdını deftere alma ve gerçek Turing çarklarından sınama akışı çalıştırıldı. Başlangıç ve ana hat kaybı reddedildi; dönüş kolu deneyi görevi tamamladı. Son görsel kontrolde hedef kitabı taşmadan okundu, terminalin tüm çarkları görünür bulundu ve konsolda hata yoktu. Eski A/B içeriklerinin fiziksel grafik/kayıt kontrolleri de geçti.

[Hedef kitabı](SecondTrace/Quest.png) · [Turing deneme kopyası](SecondTrace/Turing.png)

**Senzora > İkinci İz > Doğrula**: iki iz, ana hat kaybı, dönüş kolundaki iki geçerli yerleşim, kopuk referans ve dört kitaba ayrılan içerik.

**Canlı** menüsü, geliştirici için gerçek defter bırakma ve prefab bağlantılarını kullanarak üç denemeyi kurar. Normal Play sırasında otomatik çalışmaz. Önce kitaplardan notları toplamak ve tahtayı açmak gerekir.

<details><summary>Deneme mantığı / spoiler</summary>

Uyarıcı → Numune → Algılayıcı ilk izi oluşturur. Numune → Referans → Algılayıcı ikinci, gecikmiş yolu oluşturur. Sönümleyici ana yoldaysa iki iz de zayıflar. Referans yolundaysa gecikmiş iz algılama eşiğinin altında kalır, ilk iz korunur. Referans yolunun iki tarafı da geçerli çözümdür; referansı devreden çıkarmak karşılaştırılabilir ölçüm üretmez.

</details>
