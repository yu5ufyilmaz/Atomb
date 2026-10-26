# Kitap–defter–tahta–makine döngüsü için oyun araştırması

Araştırma tarihi: 25 Ekim 2026. Bu belge bir uygulama kararı değildir. Kaynaklar geliştirici yazıları, geliştiricilerle söyleşiler ve resmî oyun açıklamalarıdır. Oyuncunun her ara aktarımı anladığını varsaymadan nasıl bir araştırma döngüsü kurulabileceğine bakar.

## Araştırmanın sorusu

İstenen döngü: kitaptan not al → defterde sakla → tahtada gözlemle ve hipotez kur → bir çıktı elde et → ilgili deney türünde Turing makinesinde sına → yeni bilgiyle çalışmaya dön.

Risk: Tek bir aktarım anlaşılmadığında sonraki araçların da kullanılamaması. Bir aracın kontrolünü öğrenmek, araştırmanın doğru sonucunu bulmak ve bütün araştırmaları belirli sırada yapmak ayrı tasarım sorunlarıdır.

## Birbirine bağlı araçları anlaşılır kullanan örnekler

### The Operator

**Kaynakta doğrulanan:** Oyuncuya belirli kanıtlar ve somut bir soru veriliyor. Geliştirici, bir analiz aracını hikâyedeki kanıtlama ihtiyacından ürettiğini açıklıyor. Kimyasal analiz örneğinde numuneyi depodan makineye taşıma otomatik; numuneye uygun ayar girme oyuncunun işi. Teknik bir arayüz bölümünde kullanılabilir tuşlar pencerenin altında gösteriliyor.

**Bizim oyuna çıkarım:** Makinenin neden kullanılacağı, eldeki çıktıda cevaplanmamış somut bir soruyla anlaşılmalı. Taşıma, arşivleme ve ayar seçimi arasından hangi işlemin gerçek karar içerdiği belirlenmeli.

**Sınır:** Söyleşi bütün araçların aynı öğretim yöntemini kullandığını veya bütün aktarımın otomatik olduğunu kanıtlamıyor. Beş aşamalı mevcut döngümüzün birebir karşılığı değildir.

[Bastien Giafferi söyleşisi — Game Developer](https://www.gamedeveloper.com/design/the-operator-is-a-crime-solving-game-delivered-entirely-with-ui)

### Strange Horticulture

**Kaynakta doğrulanan:** Müşterinin bitki ihtiyacı araştırılacak soruyu belirliyor. Oyuncu ansiklopedi çizimlerini ve betimlerini, raftaki bitkiyi ve yakın inceleme bulgularını karşılaştırıyor. Bazen çizim, bazen yaprak biçimi, bazen koku veya doku ayırt edici oluyor. Harita ve mektuplar yeni bitkilerin keşfinde kullanılıyor.

**Bizim oyuna çıkarım:** Kitaptaki çizim ile tahtadaki parça aynı görsel kimliği koruyabilir. Yeni kaynak, aynı temel karşılaştırmaya farklı türde kanıt sağlayabilir. Çeşitlilik için her defasında yeni kontrol öğretmek gerekmez.

**Sınır:** Bu oyun okumasız değildir. Bitki tanımlama sistemini doğrudan kopyalamak, uzun metin ve defter kullanımına ilişkin şikâyetimizi kendi başına çözmez.

[Rob Donkin’in kendi yazısı — Xbox Wire](https://news.xbox.com/en-us/2023/08/04/strange-horticulture-a-detective-game-about-plants/amp/)

[Bitki tanımlama tasarımı üzerine geliştirici söyleşisi](https://www.gamedeveloper.com/design/finding-right-plant-job-strange-horticulture)

### The Case of the Golden Idol

**Kaynakta doğrulanan:** Oyuncu sahne ve belgelerden terimler toplayıp sınırlı bir kelime havuzuyla çıkarımını ifade ediyor. İlk prototiplerde büyük çözümün yalnız doğru/yanlış denetlenmesi oyuncuları nerede yanıldığını bilemez hâle getirmiş. Kimlik belirleme ve başka ara bulmacalar ilerleme hissini desteklemek için eklenmiş. Tek tek her boşluğu doğrulamanın kolay deneme yoluyla çözülmeye yol açacağı da düşünülmüş.

**Bizim oyuna çıkarım:** Bütün ilerlemeyi son makine sonucuna bağlamak riskli. Tahtada elde edilen küçük ama anlamlı sonuçlar görünür kalabilir; her bağın cevabını tek tek vermek zorunlu değildir.

**Sınır:** Metin doldurma arayüzü bizim oyuna önerilmiyor. Alınacak ders, ara çıkarımın ve geri bildirimin ölçeğidir.

[Andrejs Klavins söyleşisi — Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol)

## Araştırma yollarını esnek tutan örnekler

### Outer Wilds

**Kaynakta doğrulanan:** Geliştiriciler oyuncunun kendi sorularının peşinden gitmesini hedefliyor. Alex Beachum, gezegenleri sırayla temizleme beklentisiyle oyunun soru odaklı yapısı arasındaki anlaşmazlığı anlatıyor; gemi günlüğünün bunu açıklamaktaki rolünü vurguluyor. Mobius'un geliştirme yazısı, serbest keşifte bile yolların nereye gittiği hakkında okunabilir işaretler gerektiğini açıklıyor.

**Bizim oyuna çıkarım:** Araştırma sırası serbest bırakılırken eldeki sorular ve bulgular erişilebilir kalmalı. Bir questte takılmak, başka araştırma kolunu açmayı engellememeli.

**Sınır:** Bilgi gerektiren ilerleme kapıları vardır. Bu oyun, her sorunun veya bütün araçların atlanabileceğini göstermez.

[Alex Beachum ile söyleşi](https://medium.com/@cordialkobold/interview-with-alex-beachum-creative-director-of-outer-wilds-a01bb9631e20)

[Mobius Digital — The Intentionality of Wandering](https://www.mobiusdigitalgames.com/news/the-intentionality-of-wandering)

[GDC: Kelsey Beachum — Sparking Curiosity-Driven Exploration](https://www.gdcvault.com/play/1027368/Independent-Games-Summit-Sparking-Curiosity)

### The Painscreek Killings

**Kaynakta doğrulanan:** Geliştiriciler testlerde oyunun bir bölümünün tek ipucu zinciriyle ilerlediğini görüp tasarımı değiştirmiş. Şüphelilerin araştırmaları paralel kollara ayrılmış; doğru sonuca az bilgiyle varmak mümkün olmuş. Bununla birlikte aktlar arasındaki bazı ilerlemeler belirli anahtarlara bağlanmış.

**Bizim oyuna çıkarım:** Bazı deneyler bütün araçları kullanabilir; başka araştırmalar daha kısa yollar sunabilir. Bağlantıları esnetmek, bütün ilerleme koşullarını kaldırmak anlamına gelmez.

**Sınır:** Oyunun okuma, dolaşma ve not tutma yükü bizim hedefimiz için doğrudan örnek alınmamalı. Burada incelenen şey paralel araştırma yapısıdır. Kaynak yazılarında hikâye spoilerları bulunur.

[EQ Studios — Devlog #4](https://eqstudios.blogspot.com/2017/12/making-of-painscreek-killings-4-how-to.html)

[EQ Studios — Devlog #2](https://www.eqstudios.com/post/painscreek-devlog-2-designing-the-story)

### Heaven’s Vault

**Kaynakta doğrulanan:** Oyuncu eski yazıları yorumluyor. Geliştiriciler yanlış veya eksik yorumların da hikâyeye girdi sağlayabildiğini; ilerlemenin sürekli bilgisayarın doğru cevabı onaylamasını beklemeye bağlanmadığını anlatıyor. Yorumların yine doğru anlamları var ve hatalar araştırmanın yönünü etkileyebiliyor.

**Bizim oyuna çıkarım:** Makineye girilen her eksik hipotezin sonucu yeni bir gözlem veya araştırma olanağı sağlayabilir. Questin tamamlanması için doğru sonucun gerekmesi ile başarısız denemede hiçbir ilerleme olmaması ayrı kararlar olabilir.

**Sınır:** Bu oyunun dallanan anlatı çözümü, sayısal deney doğrulayıcısına doğrudan taşınamaz. Yanlış cevabın başarı sayılması önerilmiyor.

[Inkle geliştiricileriyle tasarım söyleşisi](https://www.gamedeveloper.com/design/how-inkle-developed-its-own-ancient-language-for-i-heaven-s-vault-i-)

[Resmî oyun açıklaması](https://www.inklestudios.com/heavensvault/)

## Araştırmadan çıkan öneri — henüz onaylanmış tasarım değil

Araçların kullanımını açık öğretmek ile her questte aynı araç sırasını zorunlu kılmak birbirinden ayrılabilir.

- Notları elle alma ve tahtaya yerleştirme, hedeflenen dokunsal oynanışın parçası olarak kalabilir. Kartın adı, çizimi ve kimliği aktarıldığında korunmalı; oyuncu aynı bilginin izini kaybetmemeli.
- İlk öğretim örneklerinde aktarımın nasıl yapılacağı basit ve görünür olmalı. Puzzle'ın düşünme zorluğu sonraki içeriklerde artırılabilir.
- Tahta ile makinenin oyuncuya sağladığı farklı sonuç açıkça tanımlanmalı. Aynı hipotezi art arda iki araca aynı biçimde onaylatmak bağı yapay hissettirebilir.
- Makine yalnız onu gerektiren deney ailesinde zorunlu tutulabilir. Başka bir araştırmada makineye gitmeyi beklemek yanlış bir alışkanlık oluşturmamalı.
- Başarısız deneyin sağladığı bilgi, tamamlanma durumundan bağımsız saklanabilir. Başka araştırmaya geçip geri dönmek önceki çalışmayı silmemeli.

İlk incelenecek üç referans: The Operator'ın aracı somut bir amaçla sunması; Strange Horticulture'ın görsel kimliği koruyan araştırması; The Painscreek Killings'in zorunlu ipucu sırasını paralel kollara çevirmesi.

## Gözlemle sınanacak sorular

Yeni oyuncu herhangi bir yardım almadan elindeki notun ne işe yaradığını, hangi araca neden götüreceğini ve bir denemeden ne öğrendiğini söyleyebiliyor mu? Bir aşamada takıldığında başka anlamlı bir araştırma sürdürebiliyor mu? Bunlar doğrulanmadan yalnız son çözümün teknik olarak kabul edilmesi, döngünün anlaşılır olduğunu göstermez.

## Sonraki kullanıcı geri bildirimi: kitap arama isteği

The Operator, Strange Horticulture ve Golden Idol çalışma yüzeyleri, oyunun tamamı için mekânsal bir örnek olarak yeterli bulunmadı. Bu proje 3B ortamda, çoğunlukla sabit bir çalışma alanı çevresinde araştırma yapmayı ve bilgiye erişmek için hareket etmeyi içeriyor. Painscreek'in ipucundan sonraki konuma ilerleyen bütün oynanışı hedeflenmiyor. Bu araştırma aşamasının sorusu düşman tasarımı değil.

Kullanıcının olumlu referansı Sea of Thieves'teki küçük ayrıntı, yarım çizim ve dünyayla belgeyi karşılaştırma hissi. Şimdiki odak, oyuncunun bir kitabı görev emri aldığı için değil, orada aradığı ilişkiye rastlayabileceğini düşündüğü için karıştırması. Henüz yeni bir mekanik kararı alınmadı.

İlham olarak değerlendirilecek anlar: önce dünyada/çıktıda bir ayrıntıya takılmak; kitapta daha önce görülmüş bir çizimi sonradan tanımak; bulunan sayfanın daha önceki nesnelerin anlamını değiştirmesi. Araştırma sorusu oyuncu için tarif edilebilir, arayacağı kaynak da okunabilir olmalı. Zorunlu görev/parça/kural istasyonları şeklindeki kitap dağılımı bu merak hissini zayıflatabilir.

[Sea of Thieves — Vaults of the Ancients resmî tanıtımı](https://news.xbox.com/en-us/2020/09/09/brave-the-vaults-of-the-ancients-in-septembers-free-sea-of-thieves-update/): parçalardan oluşan haritanın ilerideki anahtara dair bilgi taşıması.

[TUNIC — resmî çıkış yazısı](https://news.xbox.com/en-us/2022/03/16/tunic-launches-today-with-xbox-game-pass/amp/): dünyaya dağılmış resimli kılavuz sayfalarının harita, bilgi ve sırlar taşıması. Burada karşılaştırılan şey bütün oyunun yapısı değil, sayfanın işlevsel bilgiye dönüşmesi.
