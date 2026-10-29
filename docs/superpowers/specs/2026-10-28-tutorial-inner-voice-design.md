# Tutorial: kafanın içindeki diğer kişi

Kullanıcı tüm tutorial boyunca kalıcı görev panelinin kaldırılmasını ve oyuncuyla konuşan ayrı bir kişinin altyazılarla yol göstermesini istedi. Oyuncu adımı kaçırırsa bir tuşa ihtiyaç duymadan, farklı cümlelerle hatırlatma yapılır. Ses emir veren bir anlatıcı gibi yazılmaz.

Seçilen yaklaşım mevcut gerçek ilerleme koşullarını ve GlobalSubtitleManager'ı kullanır. Alternatif olan oyuncunun tuşla hatırlatma istemesi kullanıcı tarafından reddedildi; kalıcı görev kutusu da kaldırılır. Mevcut sahne ve tamamlanmamış tutorial değişiklikleri korunarak yerinde çalışılır; commit/push yapılmaz.

Her alt adımın bir giriş ve iki hatırlatma repliği olur. İlk hatırlatma önceki replik bittikten sonra basit işlerde 10, okuma/deney işlerinde 30 saniye bekler. Sonraki hatırlatmaların arası uzar. Gerçek ilerleme beklemeyi yeniler; adım değişikliği eski repliği ve bekleyen hatırlatmayı iptal eder. Duraklama ve başka konuşmalar sırasında sayaç işlemez. Eğitim tamamlandığında veya kapatıldığında konuşma durur. Yeni oturum başlangıcı eski sayacı temizler.

Kitap açma ve kart toplama; tahta, belge sabitleme, deney ve ölçümü saklama; kapı kilidi, saklanma, Lees; Turing okuma/onay; vana, vanadan çıkış ve sigorta adımları kapsanır. Kontroller gerektiğinde doğal repliğe eklenir. Türkçe, İngilizce ve Almanca mevcut altyazı veritabanında tutulur. Yeni ses kaydı üretilmez; repliklere isteğe bağlı AudioClip bağlanabilir, kayıt yokken altyazı çalışır.

Kayıt bulunmayan eğitim replikleri, normal altyazı ayarı kapalı olsa da okunabilir kalır. Dış konuşmaların ses kaydı altyazıdan uzun sürerse iç ses sesin bitmesini bekler; altyazısız konuşmalar da buna dahildir. Reddedilen bir Turing ölçümünde aynı onayı tekrarlamak yerine doğru ölçüm kağıdını bulmaya yönlendiren ayrı bir durum vardır. Toplam 25 durum ve 75 replik.

Doğrulama: gerçek zamanlayıcı üzerinde ilk replik/10 saniye/farklı hatırlatma, ilerleme, adım değişimi, duraklama/başka konuşma, sıfırlama ve uzun bekleme kontrolleri; tüm repliklerin yerelleştirme kapsamı; Unity derlemesi ve mevcut tutorial akış kontrolleri.
