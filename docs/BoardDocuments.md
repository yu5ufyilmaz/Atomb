# Tahta belgeleri ve taşınabilir defter

## Laboratuvar eğitimi

`scn_LaboratorySynthesis` sahnesinde tutorial varsayılan olarak açıktır; başlık ekranındaki ayardan kapatılabilir.

- Masadaki kitabın ilk iki sayfası **İlk İz** alıştırmasıdır. Hedefi, **Bağlantı Sırası** ipucunu ve üç parçayı deftere sürükle. Yalnız kitabı kapatmak veya Tab'a basmak dersi tamamlamaz.
- Tahtada **Hedefler** ve **Bilgiler** bölümlerindeki kartları panoya tak. Sol üstteki hedef kartı panonun tek düğmesidir: **BELGELERİ AÇ** hedef ayrıntısını ve yanındaki panoyu birlikte açar, **BELGELERİ TOPLA** kağıtlarla birlikte fiziksel gövdeyi de kaldırır. Düğme tahta sınırları içindedir; pano sağ sütundaki ölçüm cihazı ile deftere binmez.
- **Parçalar** bölümünden **Atım Kaynağı → Numune N-12 → Ölçer** düzenini kur, okları giriş halkalarına bağla ve **DENEYİ ÖLÇ** düğmesine bas. Doğru ölçümü deftere alıp F ile çık.
- Kapı ve düşman eğitiminden sonra ölçümü Turing okuyucusuna bırakıp Enter ile sına. Hazır tutorial şifresi verilmez. Vana aşaması basıncın 20 veya altına düşmesini, son aşama gerçekten atan şalterin kaldırılmasını bekler.
- Ekranın sol üstündeki aktif görev işlem tamamlanana kadar kalır. Eğitimde otomatik basınç artışı ve rastgele şalter kesintisi bekler; vana sonrası yazılmış tek kesinti korunur. Eğitim bitince normal sistemler ve serbest etkileşim açılır.
- Normal araştırma kitaplarının sayfaları korunur; alıştırma masadaki kitabın başına eklenir.

Kontroller: **Senzora > Doğrulama > Tutorial ve Kapanan Pano** (Edit), **Tutorial Gerçek Oyun Akışı** (bu sahnede Play). İkinci kontrol fiziksel kitap, defter bırakma, tahta portları, Turing okuyucusu ve sahnenin eğitim olaylarını kullanır; vana basıncı testte `ReducePressure` çağrısıyla azaltılır.

## Tahta çalışma alanı

`scn_SynthesisBlockout` sahnesinde **Play** ile dene. **1** kitap, **2** tahta, **F** etkileşimden çıkış.

- Kitabın üst bandını veya defterin **SAHA DEFTERİ / TUTUP TAŞI** başlığını sol tuşla tutup sürükle. Defter konumu kitap ile tahta arasında geçerken korunur; ekran dışına taşmaz.
- Sağ alttaki **DEFTERİ KAPAT** düğmesi veya defter başlığındaki **×**, defteri düğmeye doğru küçülterek kapatır. **DEFTERİ AÇ** aynı konuma geri getirir. Notlar, seçili sekme ve sayfa korunur; kapalı defter tahtanın girdisini engellemez.
- Defterdeki **Hedefler** bölümünden bir hedefi tahtanın solundaki **GÖREV** kağıdına bırak. Şema artık tahtanın üzerindedir ve tahta ile birlikte hareket eder.
- **Bilgiler** sekmesi bütün defter sayfalarındaki kural / ipucu kartlarını bir arada gösterir. Eski notların da burada görünür; asıl sayfaları ve kayıt biçimi korunur. Kitap notlarını bu bölüme veya normal Notlar sayfasına bırakabilirsin.
- Bilgiler her bulmacanın başlığının altında iplerle bağlanan yuvalarda gösterilir. **Henüz bulunmadı** yuvaları, içeriğini açıklamadan eksik bilgi sayısını belirtir. Tahtada yalnız seçili hedefin grubu görünür; başka hedefin kartı kabul edilmez. Hedef değiştirince önceki hedefin kartları saklanır.
- Tahtadaki **DEFTERDE** yuvası bilginin bulunduğunu ancak henüz panoya takılmadığını belirtir; **EKSİK** ise bilgi henüz toplanmamıştır. Eski notlar içerikleri tek bir hedefe uyuyorsa eşleştirilir; belirsiz notlar defterde ayrı tutulur.
- Defterdeki bir bilgi kartını tahtanın **BİLGİLER / İPUÇLARI** alanına sürükle. Defterdeki aslı korunur, tahtaya bağımsız bir kopyası takılır. Aynı bilgi ikinci kez takılmaz. Fare tekerleğiyle takılı notları kaydır.
- Tahtadaki bilgi kağıdının **x** düğmesi yalnız tahtadaki kopyayı kaldırır. Defter kartına sağ tıklamak asıl notu siler.
- Belge şeridi formül çalışma alanından ayrılmıştır; parçalar kağıtların altında kaybolmaz. Etkileşimden çıkınca belgeler tahtada görünür kalır. Duraklatmada ve etkileşim dışında belge girdisi kapanır.
- Tahtaya takılan bilgiler ve seçili hedef tahta kaydına dahildir; defter bilgileri mevcut sayfa kaydını kullanır.

Kontrol menüsü: **Senzora > Doğrulama > Tahta Belgeleri ve Defter Taşıma**.

## Sabit tebeşir

- Defterden tahtaya bırakılan eskiz, yazıldığı yerde sabittir. Sol tıkla tutup taşıma veya Q/E ile döndürme yapılmaz; gövdeye sağ tıklamak eskizi ve ona bağlı çizgileri siler. Yeniden yerleştirmek için defterdeki asıl kartı tekrar kullan.
- Uçtan uca sol sürükleme bağ çizer; aynı uçlar arasında ikinci çizim çift bağ oluşturur. Uca sağ tık çift bağı teke düşürür, tek bağı siler.
- Bağ ve önizleme tahta yüzeyinde kalır; kuru tebeşirin taneli ve kesintili iziyle gösterilir. Bitmiş izler uçların nabız animasyonundan etkilenmez.
- Serbest yazılar da yerinde kalır; sağ tuşla silinir. Eski fiziksel model prefablarının taşıma davranışı korunur.

Kontrol menüleri: **Senzora > Doğrulama > Defter Aç Kapat ve Sabit Tebeşir**, **Bulmacaya Bağlı İpuçları**.
