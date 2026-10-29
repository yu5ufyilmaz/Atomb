# Ortak oyun dili

Kullanıcı kitaplar/parçalar ve İngilizce arayüz arasındaki dil ayrımını kaldıran, yeni dil eklenebilen, dile göre UI düzenlenebilen ve ayarlardan seçilebilen bir sistem istedi. İlk tam çeviri kapsamını İngilizce ve Türkçe olarak seçti. Mevcut Almanca altyazılar korunur; Almancanın tamamlandığı iddia edilmez.

Mevcut SettingsManager yalnız languageIndex kaydediyor. Dil seçici sahnelerde bağlı değil; GlobalSubtitleManager ayrı dil kullanıyor. Bazı ayar başlıkları sprite içine basılmış. Kitap metinleri bulmaca fingerprint, dağıtım ve kayıt eşitliği içinde kullanıldığından kanonik metinler değiştirilmeyecek.

Yaklaşım: mevcut Unity/TMP üzerinde kaynak dosyalarından yüklenen dil varlıkları, anahtar ve kaynak metinli JSON tabloları, tek dil değişim olayı ve açık metin/görsel/yerleşim bağları. Yeni dil kodları enum değişikliği istemez. Eksik çeviri önce paketin fallback diline, sonra İngilizceye, son olarak özgün kaynağa düşer. Eski bileşik kayıtlar için katalogdaki numaralı formatlar ve metnin yapısal parçaları gösterim sırasında çözülür.

Alternatif: resmi Unity Localization paketi; bu projede yeni Addressables bağımlılıkları ve geniş varlık taşıması gerektirir. Mevcut kayıtların kanonik metnini çevirmek ise hash ve karşılaştırmaları bozabilir. Seçilen yaklaşım mevcut veriyi korur, ek paket istemez ve dil/font/UI farklarını dışarıdan tanımlar.

Dil kodu ayarda kalıcı olur; eski 0=tr, 1=en, 2=de seçimi geriye uyumlu taşınır. Kullanıcının seçimi testlerde değiştirilip bırakılmaz. İki oyun içi ayar paneline gerçek seçenek eklenir; ana menüdeki ayarlar da erişilebilir olmalıdır. Seçim mevcut açık/kapalı UI, altyazı, kitap, defter, tahta ve cihaz yazılarını yeniler; kayıt/bulmaca/konum/sayfa korunur. Sayfalara basılan önbellek dil revizyonuyla yenilenir.

LocalizedText kaynak/format argümanlarını saklar. Dil değişirken font ve UI baz değerleri önce geri yüklenir; ardından dile özel değerler uygulanır. LocalizedUIVariant alternatif çocuk nesne, sprite ve doku seçimine izin verir. Kaynak raster kitap dokuları yerelleştirilmiş doku varyantlarıyla değiştirilebilir; oyun verisi ve UV bölgeleri korunur.

Doğrulama: gerçek katalogda anahtar/kaynak/fallback/format/boş çeviri/kimlik koruma; dil ekleme; eski ayar geçişi; UI baz değerlerinin EN→TR→EN dönüşü; iki dropdown ve seçim olayı; açık fiziksel kitap ve defterde canlı değişim; aynı puzzle fingerprint ve ölçüm kodu; altyazı yenileme; gerçek Unity derlemesi ve ilgili mevcut akış kontrolleri.
