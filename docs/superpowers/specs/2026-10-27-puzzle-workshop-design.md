# Puzzle Atölyesi ve Rastgele Puzzle Havuzu

Tarih: 27 Ekim 2026
Durum: Kullanıcı uygulamayı onayladı; uygulama ve doğrulama sürüyor.

## 27 Ekim kullanıcı yönlendirmesi: kullanım kolaylığı ve ikinci puzzle türü

Kullanıcı yarım kalan işin tamamlanmasını, yoğun atölye arayüzünün kolaylaştırılmasını ve dalga dışındaki puzzle türlerinin eklenebilmesini istedi. İlk çalışan yeni aile olarak **Renk ve karışım** seçildi.

- Başlangıçta hazır örnek, rastgele üretim ve elle oluşturma açıkça ayrılır. Şema, kurallar, kitaplar ve kaydetme ayrı sekmelerde gösterilir; seyrek kullanılan sayısal sınırlar gelişmiş ayarlarda kalır.
- Parça adları ve etkileri okunur kartlarla gösterilir; seçilen parçayı bağa ekleme ve otomatik yerleştirme sağlanır. Kaydetme için varsayılan proje klasörü kullanılır, dosya yolu bilmek gerekmez.
- Editör her zaman çalışma kopyasıyla düzenler. Yeni/aç/kapat/geçiş öncesi taslaklar UserSettings altında kurtarılabilir olarak saklanır; asıl puzzle yalnız Kaydet ile güncellenir.
- İkinci aile aynı kitap/defter/tahta/ölçüm/Turing akışını kullanır; davranış ve hedef karşılaştırması türe göre ayrılır. Sinyal ailesi geriye uyumlu kalır.
- Renk ailesinde taşıyıcı sıvıya kırmızı, yeşil ve mavi pigment dozları eklenir; çözücü yoğunluğu azaltır, yoğunlaştırıcı taşıyıcıyı yarıya indirir; dağıtıcı miktarları ikiye böler, karıştırıcı kolları toplar. Bu bir oyun modelidir; gerçek boya fiziği iddiası yoktur.
- Hedef; renk oranları, pigment yoğunluğu ve toplam miktardır. Başarı gizli bir örnek şemanın kopyalanmasına bağlı değildir. Hedef ve ölçüm renk örneği/yoğunluk/miktarla gösterilir; rastgele üretim de aynı simülatörle çözüm kanıtlar.
- İlk renk örnekleri havuza eklenir; editör ve oyun akışında gerçek renk puzzleı doğrulanır. Rehber hangi türün hangi parçaları/kuralları kullandığını ve yeni türün giriş noktalarını açıklar.

Uygulanabilirlik incelemesi: Ayrı Unity geliştirici incelemesinde uygulama planını engelleyen önemli bir açık soru bulunmadı. Bu inceleme uygulamanın veya oyun testlerinin yapıldığı anlamına gelmez.

## Amaç ve kabul edilen kapsam

Kullanıcı kod yazmadan puzzle hazırlayabilmeli, parçaları ve bağlantıları elle kurabilmeli, kuralları/yasakları düzenleyebilmeli ve bir düğmeyle mantıklı rastgele puzzle üretebilmeli. Hazırlanan puzzlelar havuza kaydedilmeli; yeni oyunda havuzdan seçim yapılmalı ve gerekli bilgiler sahnedeki kitaplara rastgele dağıtılmalı. Kullanıcı, farklı işlevleri olan yeni numuneler eklenmesine de izin verdi.

Oyuncunun mevcut kitap → defter → tahta → ölçüm → Turing döngüsü korunur. Rastgele üretimin güvencesi, en az bir geçerli çözümün gerçekten simüle edilmesidir. Tek çözüm veya otomatik eğlence/zorluk garantisi verilmez.

## Mevcut durum ve kısıtlar

- Unity 6000.3.10f1; mevcut C#, ScriptableObject, Unity Editor ve uGUI araçları kullanılır. Yeni paket gerekmez.
- `SignalTrial` yedi parça tanımını, tek Çift Kayıt hedefini ve dört kitabın içeriğini kodda sabit oluşturur.
- `SignalExperiment.CurrentQuestID`, hedef çizimi ve bazı araştırma-belge yolları tek görev kimliğini varsayar. Yeni puzzle desteği bunların birlikte güncellenmesini gerektirir.
- `SignalPort` giriş/çıkışları parça kimliğinden çözer. Mevcut simülasyon yönlü bağlantıları denetler; kullanılmayan veya ölçere ulaşmayan bileşenleri tümüyle doğrulamaz.
- `ResearchPuzzleTrial` tam dört araştırma kitabı ister; ilk entegrasyonda bu dört mevcut kitap kullanılır. Daha fazla kitap desteği aynı dağıtım yordamıyla mümkün olmalı; bütün dekoratif kitaplar otomatik olarak etkinleştirilmez.
- Çalışma sahnesi `scn_LaboratorySynthesis`, küçük deneme sahnesi `scn_SynthesisBlockout`tur. Çalışma sahnesindeki kayıt yöneticisi şu an `sessionOnly` kullanır.
- Build Settings ve Splash hedefi eski sahne düzenindedir. Çalışma sahnesinde çalışması, paketlenmiş oyunun kendiliğinden bu sahneyi açtığı anlamına gelmez; bu ayrım kurulumda açık gösterilmelidir.
- `GameData` tahta ve defteri saklar; puzzle seçimi, kitap dağılımı ve Turing ölçüm tablosu için henüz kayıt alanı yoktur.
- Oyuncu, kamera, kitaplar ve makinelerin mevcut fiziksel konumları ile etkileşim kontrolleri korunur.

## Yaklaşımlar

1. Yalnız elle şema hazırlama: tam kontrol sağlar; çok sayıda puzzle üretmek yavaştır. Kullanıcının rastgele üretim isteğini karşılamaz.
2. Yalnız kurallardan üretme: hızlıdır; kullanıcının kendi düzenini hazırlama isteğini karşılamaz.
3. Ortak veriyle elle hazırlama ve otomatik üretme: seçilen yaklaşım. Kullanıcı iki yolu birlikte istedi. Üretilen düzen, elle hazırlananla aynı editörde açılır ve aynı doğrulamadan geçer.

Uygulayıcının önerisi: Tasarım aracı Unity Editor içinde çalışır; ayrı `scn_PuzzleWorkshop` sahnesiyle bağlantılı bir Puzzle Atölyesi penceresi kullanılır. Düzenleme ve asset kaydetme Play dışında yapılır. Bu, kullanıcının henüz ayrıca seçmediği uygulama kararıdır; tasarım onayında görünür biçimde belirtilir. Play, hazırlanan puzzleı mevcut oyun araçlarıyla denemek içindir. Bağımsız oyuncu buildinde içerik editörü bu aşamanın kapsamı değildir.

## Atölyede kullanıcının yapacağı işler

`Senzora > Puzzle Atölyesi > Aç` atölye sahnesini ve penceresini açar. Mevcut kaydedilmemiş sahne değişiklikleri Unity'nin olağan kaydetme akışıyla korunur. Kurulum mevcut sahne veya puzzle assetlerini sessizce ezmez.

Pencerede dört çalışma alanı bulunur:

- Puzzle bilgileri: ad, kalıcı kimlik, oyuncuya gösterilen hedef açıklaması, havuza dahil edilme.
- Parçalar ve kurallar: kullanılabilir katalog, zorunlu/yasak parçalar, toplam parça aralığı, toplam numune aralığı, parça türü başına alt/üst adet sınırları, en fazla bağlantı derinliği ve istenen iz sayısı (1 veya 2).
- Şema: parçayı ekleme, sürükleme, giriş/çıkıştan bağlantı kurma, bağlantıyı/parçayı silme; seçili parçanın özelliklerini görme. Tek kaynak ve tek ölçer ortak yapısal kuraldır.
- Sonuç ve kitap bilgileri: örnek çözümün ölçümü, hedef grafiği, parça açıklamaları, kurallar ve gözlemler; hangi kitap/sayfaya dağıtılacaklarını gösteren önizleme.

Ana düğmeler: Yeni Puzzle, Rastgele Puzzle Üret, Doğrula, Kaydet, Farklı Kaydet, Puzzle Aç ve Oyunda Dene. Kaydet mevcut asseti günceller; Farklı Kaydet yeni kimlik üretir. Normal şema düzenleme Unity Undo/Redo ile geri alınabilir.

Elle hazırlamada kullanıcı örnek çözümü kurar; doğrulanmış ölçüm hedef olur. İsterse hedef değerlerini ayrıca değiştirir; değişiklik, çözüm yeniden hedefi karşılayana kadar puzzleı doğrulanmamış yapar. Şema değişince sessizce eski hedefle yayınlanmaz. Kullanıcı Kaydet sırasında hedefi örnekten güncellemeyi açıkça seçer.

Hatalı taslak saklanabilir ancak havuzda etkinleştirilemez. Kullanıcı hata nedenini görür. Aynı kimlikle farklı puzzle oluşturulamaz.

Oyunda Dene, kaydedilmiş mevcut taslağı atölyenin izole deneme ortamında açar; kitap, defter, tahta ve Turing yolu gerçek oyun bileşenlerini kullanır. Hazır çözüm tasarımcı önizlemesinde görülebilir; oyuncu denemesi boş tahta/defterle başlar. Play'den çıkmak kaydedilmiş puzzleı kaybettirmez.

## Veri ve ortak deney kuralları

Puzzle tanımı ayrı bir ScriptableObject olur. İçeriği:

- Kalıcı puzzle kimliği, veri sürümü, başlık ve açıklama.
- Kullanılabilen parça tanımları ve sayısal kısıtlar.
- Tasarımcıya ait örnek çözüm grafiği ve düğüm konumları.
- Hedef izleri (zaman, yükseklik, genişlik) ve açık karşılaştırma toleransı.
- Kitapta gösterilecek parçalar, kural notları ve gözlemler.
- Üretim seed'i, doğrulama durumu ve havuzda etkinlik.

Ayrı bir havuz asseti etkin puzzle referanslarını taşır. İlk sürümde her yeni oyunda bir puzzle eşit olasılıkla seçilir. İkinci oyunda öncekinin tekrar çıkması mümkündür; tekrar etmeme rotasyonu bu kapsamda gerekli değildir. Rastgele üretmek, otomatik olarak havuza kaydetmek değildir.

Parça tanımları mevcut çizim prefabını ve formül kimliğini kullanır; giriş/çıkış sayıları, numune/işlem ayrımı ve simülasyon davranışı ortak tanımdan okunur. Aynı tanım atölye, fiziksel portlar, kitap çizimleri ve ölçüm tarafından kullanılır. Parça başına dağılmış yeni kimlik koşulları çoğaltılmaz. Mevcut Çift Kayıt, bu veri modelinin ilk puzzle kaydı olur; davranışı karşılaştırmalı testle korunur.

Bilimsel gerçeklik iddiası olmayan, oyuna ait ilk ek numune önerileri:

| Numune | Açık davranış | Farkı |
|---|---|---|
| N-12 | Gelen izin genişliğini 0,025 yapar; zaman ve yükseklik korunur. | Mevcut kısa tepki. |
| N-34 | Gelen izin genişliğini 0,075 yapar; zaman ve yükseklik korunur. | Mevcut geniş tepki. |
| N-08 — Zayıf tepki | Genişliği 0,025 yapar, yüksekliği yarıya indirir. | Ayrı sönümleyici olmadan zayıf kısa tepki. |
| N-21 — Geç tepki | Genişliği 0,025 yapar, zamanı 0,16 birim ileri taşır. | Mevcut geciktiricinin 0,32 adımından farklı gecikme. |
| N-46 — Çift tepki | Her giriş izinden aynı yükseklik/genişlikte iki iz çıkarır; ikincisi 0,32 sonra gelir. | Tek çıkış hattında iki tepki; port sayısını artırmaz. |

Numune davranışı her gelen ize uygulanır; numuneleri arka arkaya bağlamanın sonucu tanımlıdır. Çift tepki iz sayısını büyütebildiği için simülasyon ve üretim parça/derinlik/iz bütçesiyle sınırlanır. İlk içerikte hedefte en fazla iki görünür iz kullanılır. Kaynak, dağıtıcı, geciktirici, sönümleyici ve ölçerin mevcut temel davranışları korunur. Daha fazla yeni bileşen, bu ilk katalogla doğrulanan döngüden sonra eklenebilir.

## Rastgele Puzzle Üret düğmesi

1. Ayarları kontrol eder: alt sınır üst sınırı aşamaz; zorunlu parça yasak olamaz; seçilen sayılar parça bütçesine sığmalıdır.
2. Yalnız izin verilen katalogdan tek kaynak ve tek ölçer arasında, yönlü ve döngüsüz aday düzenler kurar. Doğrulanmamış bağlantıları rastgele yığmak yerine sınırlı aday üretimi kullanılır.
3. Her parça kaynaktan erişilebilir ve ölçere ulaşabilir olmalıdır. Tekli hattın uçları, numune ve işlem alt sınırları, tür başına sayılar ve derinlik aynı doğrulayıcıdan geçer. Dağıtıcının iki çıkışı da kullanılır; ölçer bir veya iki giriş kullanabilir.
4. Ortak simülatör sonucu hesaplar. Boş/sıfır, görünür grafik sınırlarının dışında veya desteklenenden fazla iz içeren hedefler reddedilir. Gerekli iz sayısı görünür birleşimden sonra denetlenir.
5. En az bir numune bulunan, belirtilen koşulları karşılayan ölçüm hedef olarak kaydedilir. Üretilen grafiğin kendisi geçerli çözümün kanıtıdır.
6. Kullanıcıya hedef, örnek çözüm ve ipucu taslağı gösterilir. Sonucu düzenleyebilir, yeniden üretebilir veya kaydedebilir.

Üretimde ayrı yerel rastgele sayı üreteci kullanılır; oyunun diğer rastgele süreçleri etkilenmez. Aynı seed ve aynı katalog/ayar sürümü aynı sonucu verir. Varsayılan seed her düğme basımında yenilenir; kullanıcı seed'i sabitleyebilir. Deneme sayısı sınırlıdır; uygun sonuç bulunamazsa anlaşılır hata gösterilir ve son geçerli taslak korunur. Bütün olası kombinasyonları tarama veya her tutarlı görünen ayar kümesine mutlaka çözüm bulma garantisi yoktur.

Aynı örnek çözümün yalnız düğüm yerlerini değiştirerek yeniden üretilmesi çeşitlilik sayılmaz. Üretici, son adaylarla aynı bağlantı/parça yapısını mümkün olduğunda eler. Derinlik ve parça sayısı zorluk göstergeleridir; insanın çıkarım zorluğunu garanti etmez.

## Ölçüm ve başarı

Ölçülebilirlik ile puzzle koşullarına uygunluk ayrıdır. Oyuncunun geçerli portlarla çalışan ancak adet/yasak/hedef koşullarına uymayan denemesi yine ölçüm üretir; Turing bu denemenin neden başarı sayılmadığını açıklar. Bozuk yön, port taşması ve döngü gibi yapısal hatalar ölçülemez.

Kurallar tahtadaki bütün bileşenleri sayar; bağlantısız numune ekleyerek adet şartı sağlanamaz. Kullanılmayan ya da ölçere gitmeyen parçalar uygunluk hatasıdır. Üretici bunları hiç kabul etmez; oyuncuya açık geri bildirim verilir.

Başarı, hedef ölçüm ve görünür kısıtlara göre değerlendirilir; örnek çözümün düğüm sırasına/yerine/bağlantılarının birebir eşine göre değerlendirilmez. Gerekli notları önceden toplamamış olmak gizli başarı şartı değildir.

Eşzamanlı aynı genişlikteki izler tek iz olarak birleştirilip yükseklikleri toplanır. Hedef, sonuç grafiği ve karşılaştırma aynı normalleştirmeyi kullanır; üst üste binen izlerin görünümüyle gizli kayıt sayısı arasında fark olmaz. İlk sürüm üretici, farklı genişlikte eşzamanlı/örtüşen izlerden oluşan belirsiz hedefler üretmez. Elle hazırlanan bu tür hedefler doğrulamada açıklanarak engellenir. Kaydırılmamış mutlak zaman, yükseklik ve genişlik ortak toleransla karşılaştırılır.

Her ölçüm, puzzle kimliği/sürümü ve üretildiği anın sonucu ile ilişkilidir. Sonradan hedef değiştirmek veya başka puzzle seçmek eski ölçümü yeni hedef için geçerli yapmaz. Turing'in okuyucu animasyonu ve elle kod girişi korunur.

## Kitaplara dağıtım

Oturum kurulurken seçilen puzzleın hedefi, izinli parça atlası ve çözüm için gerekli genel kurallar/gözlemler tam bir içerik kümesi olarak hazırlanır. Yeni numunelerin davranışları da bu kümede açıklanır; üretici çözüm yolunu oyuncuya hazır tarif olarak yazmaz. Kullanıcı metinleri ve ek gözlemleri düzenleyebilir.

Dağıtım iki görünür ayarla yapılır: hedefin masa kitabında tutulması (varsayılan) ve kalan bilgilerin karıştırılması; isterse hedef dahil hepsinin karıştırılması. Kaynak başlıkları hangi konunun nerede araştırılacağını anlatır. İlgisiz kitap ismiyle içerik saklanmaz; oturum etiketleri dağıtımla tutarlıdır.

Yalnız bağlanmış, etkin ve oyuncunun puzzleı çözmeden erişebildiği kitaplar adaydır. Atölye ve sahne kurulumunda erişilebilirlik tasarımcı tarafından işaretlenir; otomatik dünya erişilebilirliği kanıtı iddia edilmez. Her gerekli blok en az bir kez yerleştirilir; parça/kural türü başına dengeli sayfalar üretilir. Mevcut fiziksel sayfa düzenine uygun olarak sayfa başına en fazla iki blok kullanılır. Dağıtımın önizlemesinde eksik veya yinelenen kimlikler denetlenir. Gerekirse kitap sayfa sayısı çift sayıya tamamlanarak artırılır; bilgi kırpılıp kaybolmaz.

Geçerli kitap yoksa veya kayıtlı dağıtım bir kitap kimliğini çözemiyorsa sessizce yeniden karıştırılmaz. Bağlantı sorunu gösterilir ve mevcut kayıt korunur. Kitaplara kalıcı sahne kimliği verilir; yalnız nesne adı veya rastgele dizi sırası kimlik olarak kullanılmaz.

## Yeni oyun, devam ve sahne entegrasyonu

Yeni Oyun, etkin havuzdan bir puzzle seçer; bağımsız bir dağıtım seed'iyle kitapları doldurur. Normal etkileşime açılmadan önce parça kataloğu, hedef ve kitaplar hazır olur. Sahne Start sırasına güvenen ikinci bir rastgele seçim yapılmaz. Yeni oyun aynı açık sahnede başlatıldığında da puzzle oturumu ve ölçüm tablosu sıfırlanır.

Devam Et, kayıtlı puzzle kimliği/sürümü ve tanım anlık görüntüsünü, kitap/blok yerleşimini, tamamlanmayı ve ölçüm kimliklerini geri yükler. Katalog asseti sonradan düzenlense bile mevcut oyunun hedefi sessizce değişmez. Bir puzzle asseti silinse bile kayıtlı tanım ve geçerli parça davranış sürümüyle oturum sürer; desteklenmeyen davranış sürümü varsa anlaşılır uyumsuzluk hatası verilir. Yeni alanı olmayan eski kayıtta, kaydın tahta/defter verileri korunarak Çift Kayıt uyumluluk oturumu oluşturulur; yeni havuzdan rastgele hedef seçilmez.

Puzzle oturumu kayıt yüklemesinde tahta ve defterden önce katalog ve kitapları hazırlar; mevcut `ISaveable` dolaşım sırasına bağımlı olmaz. Turing'in ölçüm kimliği → ölçüm sonucu eşlemesi de saklanır; eski defter çıktıları yükleme sonrasında kullanılabilir. Devam sırasında aktif okuyucu animasyonu varsa ilk sürüm yalnız tamamlanmış ölçümü geri yükler; yarım kalmış besleme güvenle boş okuyucuya döner ve defterdeki asıl kart tekrar kullanılabilir.

Atölye ve `scn_SynthesisBlockout` gerçek kullanıcının kalıcı kayıt dosyasını okumaz/yazmaz. Kayıt testi ayrı geçici dosyada yapılır. `scn_LaboratorySynthesis` üzerinde havuz bağlanır; bu sahnede kalıcı oyun seçeneği mevcut `sessionOnly` korumasından açıkça ayrılır. Varsayılan geliştirme testi oturumluk kalır. Çalışma sahnesinin Yeni Oyun/Devam davranışı izole kayıtla doğrulanır.

Paketlenmiş oyun için kurulum menüsü, Splash hedefi/Build Settings ile havuz bağlı çalışma sahnesinin tutarlılığını denetler ve gerekiyorsa çalışma sahnesini giriş akışına bağlayan açık bir kurulum işlemi sağlar. Eski ana sahnenin sessizce üstüne yazılmaz. İlk teslimde hangi giriş yolunun havuzu kullandığı belgelenir; sadece atölye önizlemesinin rastgele çalışması tamamlanma sayılmaz.

## Teslim ve kapsam dışı

Teslim: Atölye sahnesi ve penceresi; ortak puzzle/parça tanımları; elle çizim ve sınırlı rastgele üretici; yeni numuneler; ilk havuzda Çift Kayıt ile en az üç işlevsel farklı hedef; çalışma sahnesine havuz bağlantısı; kitap dağıtımı ve oturum kaydı; Türkçe kullanım rehberi.

Kapsam dışı: Yeni bilimsel alan/simülatör, üçüncü taraf graph editörü, oyun içinden asset yayınlama, ağdan içerik indirme, çoklu eşzamanlı görev zinciri, puzzle tamamlama için yeni ödül/final kapı tasarımı ve tek çözüm ispatı.

## Doğrulama ve kabul ölçütleri

- Mevcut Çift Kayıt çözümü aynı sonucu verir; mevcut ölçüm → defter → Turing akışı geçer.
- Elle puzzle kurulabilir, doğrulanabilir, kaydedilip tekrar açılabilir; şema konumları ve kurallar korunur. Hatalı taslak havuza giremez.
- Yeni numunelerin farkları tekli ve art arda kullanımda ölçülür; fiziksel portları, kitap/defter çizimleri ve açıklamaları tutarlıdır.
- En az 100 seed ve farklı izin/yasak/adet kümelerinde üretilen her başarılı puzzleın kayıtlı örnek çözümü aynı ortak doğrulayıcıdan geçer ve hedefi karşılar. İmkânsız veya üretilemeyen koşullar sınırlı sürede, son taslağı bozmadan hata verir.
- Alternatif geçerli düzen kabul edilir; doğru ölçümlü ancak yasak/adet şartına uymayan, bağlantısız bileşenle kuralı sağlamaya çalışan düzen reddedilir. İz normalleştirme ve ortak toleranslar sınır örnekleriyle denenir.
- Farklı seedlerle dağıtım değişebilir; her seferinde gerekli bilgiler eksiksizdir. Aynı oturum yüklenince puzzle ve kitap/sayfa/blok yerleşimi birebir korunur.
- Yeni Oyun açık sahnede yeniden seçim yapar; Devam Et yeniden seçim yapmaz. Yükleme, katalog/tahta/defter kayıt sırasından bağımsızdır. Eski kayıt uyumluluğu ve kaldırılmış puzzle asseti senaryosu denenir.
- Kaydedilmiş ölçüm kartı yüklemeden sonra gerçek Turing okuyucusunda kullanılabilir. Başka puzzlea ait ölçüm kabul edilmez.
- Atölyede Play sonrası assetler kalır; kaydedilmemiş mevcut sahne/puzzle sessizce ezilmez. Kullanıcının gerçek yerel kayıt dosyası test öncesi ve sonrasında aynı kalır.
- Unity derlemesi ve mevcut ilgili geliştirici kontrolleri geçer. Kitap, defter, hedef ve ölçüm görselleri gerçek oyun görünümünde taşma/okunabilirlik ve sürükleme açısından kontrol edilir. Çalışma sahnesinin fiziksel pozları korunur.

## Uygulayıcıya bırakılan kararlar

Sınıf adları ve dosya bölümü; pencere ölçüleri, renkleri ve çizim ayrıntıları; sınırlı aday üretiminin döngüsüz şablonları; makul deneme/iz bütçeleri; varsayılan küçük/orta/büyük ayar aralıkları ve ilk havuzun diğer üç hedefi. Bunlar yukarıdaki gözlenebilir davranışı değiştirmeden seçilebilir. Paket giriş kurulumu mevcut sahne referansları incelenerek en az değişiklikle yapılır; kalıcı kayıt koruması farkı kullanıcı rehberinde görünür olur.
