# LEES baş çevirme mücadelesi

İstek: LEES'i gördükten sonra arkasını dönen ve sabit duran oyuncu, başını zorla çeviren LEES'e fareyi ters yöne çekerek direnir. Puzzle gibi ayrı, oynanabilir bir çalışma sahnesi hazırlanır; ana oyuna bağlama sonraki iştir.

Uygulama tercihi: mevcut Unity 6 / HDRP / Input System / TMP kullanılır. Yeni paket eklenmez. Mevcut LeesEnemyAI, ana sahneler, kayıtlar ve oyun ayarları değiştirilmez. Çalışma mevcut checkout'ta, yeni dosyalarda yapılır; mevcut kullanıcı değişiklikleri korunur.

Yaklaşımlar: yalnız UI üzerindeki bir gösterge fiziksel baş çekişini eksik hissettirir; doğrudan LeesEnemyAI entegrasyonu kullanıcının sonraya bıraktığı ana oyun davranışını değiştirir. Tercih: bağımsız mücadele bileşeni, onu gösteren UI ve ayrı bir koridor sahnesi.

Akış: tıklayıp başla → LEES'i gör → fareyle arkanı dön → 2,8 saniye arkadan yaklaşma → 16 saniye mücadele → kurtuluş/yakalanma → R ile yeniden dene. Oyuncu yaklaşma ve mücadelede yer değiştirmeye çalışırsa test başarısız olur. ESC veya odak kaybı oyunu ve sesleri duraklatır; tıklama devam ettirir.

Çekiş: önden haber verilen, kısa aralıklarla değişen sağ/sol kuvvet. Fare sağa hareketi başı sağa, sola hareketi sola çevirir; böylece kuvvetin tersine hareket etmek direnç olur. Uzun süre hiçbir şey yapmamak baskıyı artırarak yenilgi getirir; sadece rastgele yön değişimleri bekleyerek kazanılmaz. Mouse delta zamanla ikinci kez çarpılmaz. Büyük tek kare girişleri sınırlanır. Sıfır delta-time durumu dondurur.

Görsel/ses: karanlık endüstriyel koridor ve mevcut LEES görseli; çekilen tarafta kırmızı çevresel çizgiler, kısa yön komutu, baş açısı göstergesi, baskı ve dayanma ilerlemesi; kamerada kontrollü eğilme/sarsılma/FOV baskısı. Mevcut LEES tinnitus, korku ve kalp sesi kullanılır. Hareket etkisi çarpanı ayrı ayarlanır.

Bağlantı: BeginResistance() ve CancelResistance(); başarı/yakalanma UnityEvent'leri. Bileşen kendisine verilen bakış/hareket scriptlerinin başlangıç durumunu saklar ve bitiş/iptal/disable durumunda geri yükler. Gerçek oyuncu ölümü bu prototipin işi değildir.

Kabul: sahne açılıp Play ile oynanabilir; doğru karşılıkla kazanılır, hareketsizlik ve yanlış yönle kaybedilir; her iki çekiş yönü çalışır; yeniden başlatma, iptal, duraklatma, odak kaybı kamera/kontrolü bozuk bırakmaz. Model kontrolleri ve Unity derlemesi çalıştırılır, gerçek sahne/UI renderı görsel olarak kontrol edilir.

Sayısal değerler ve görsel yerleşim uygulama varsayımlarıdır; oynayarak birlikte ayarlanacaktır.

Oyuncu geri bildirimiyle güncelleme: ilk sürüm çok zayıf bulundu. Çekiş 19'dan 54 derece/saniyeye, açı sınırı 64'ten 52 dereceye, yön uyarısı 0,45'ten 0,25 saniyeye ayarlanır. Kuvvet daha hızlı devreye girer, darbeli ve gittikçe güçlenen baskı uygular. Direnmeyi kesmek en geç 2,25 saniye içinde kaybettirmeli; yalnız 200 piksel/saniyelik küçük ters hareket yeterli olmamalı. Etkin doğru direnme 30/60/144 FPS'te kazanılabilir kalır. Bu ayarlar kaynak koduyla birlikte mevcut sahne ve prefabda da uygulanır.
