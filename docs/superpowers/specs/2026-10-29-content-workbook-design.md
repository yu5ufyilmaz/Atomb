# Excel üzerinden metin, diyalog ve kontrol yönetimi

## Amaç ve kapsam

Kullanıcı, yazıları farklı JSON/Inspector alanlarında aramak yerine tek bir Excel çalışma kitabından yönetmek istiyor. Özellikle sıralı altyazıların kodları, süreleri ve bağlantıları ile oyun kontrolleri ve değişen tuş ipuçları aynı düzen içinde bulunmalı. Kullanıcı, “tuşlar” ile menü butonlarının yazılarından ziyade oyun kontrollerini ve tuş ipuçlarını kastettiğini doğruladı.

Bu tasarım kullanıcı tarafından 2026-10-29 tarihinde onaylandı ve uygulandı. Ana çalışma kitabı `Content/GameContent.xlsx`, güncel kullanım kılavuzu `docs/ContentWorkbook.md` içindedir. Teknik ayrıntılar, aşağıda kullanıcı tarafından doğrulandı diye belirtilen kapsam dışında, uygulayıcının seçimidir.

## Mevcut proje

- `Assets/Resources/Localization/Tables/tr.json` ve `en.json` genel metinleri anahtarla ve eski kaynak metinle çözümler.
- `Assets/Scripts/Scriptables/Subtitles/MainSubtitles.asset`, `SubtitleEntry.id`, not, isteğe bağlı `voiceClip` ve sıralı segmentler içerir. Her segmentin saniye cinsinden süresi ve TR/EN/DE alanları vardır. Ekrandaki çeviri ayrıca `subtitle.<konuşma-kodu>.<sıfırdan-başlayan-sıra>` anahtarıyla JSON'dan okunur. Bu iki düzenleme noktasını tek kaynağa indirmek gerekir.
- `DialogAction`, olay koşulları altında bir giriş konuşması ve isteğe bağlı hatırlatmalar oynatır. Tutorial olay koşulları, kamera ve ilerleme mantığı yeniden yazılmamalı.
- Gerçek oyuncu sahnesi `Assets/StarterAssets/InputSystem/StarterAssets.inputactions` dosyasını kullanır. Kök dizindeki `InputSystem_Actions.inputactions` farklı bir assettir; ad benzerliği nedeniyle yanlış dosya düzenlenmemeli.
- Oyuncu Input System ile kontrol edilirken kitap, defter, makineler ve bazı koşullar sabit `KeyCode`/fare kontrolleri kullanıyor. Örneğin bazı kapı ipuçları E yazarken dünya etkileşimi sol tıklamayla çalışıyor. Yalnızca ipuçlarının yazısını değiştirmek bu tutarsızlığı çözmez.
- Şu an oyuncu ayarlarında tuş yeniden atama ekranı veya kayıtlı tuş atamaları bulunmuyor.

## Yaklaşım

**Öneri: Excel düzenleme kaynağı; Unity Editor doğrulayıp mevcut oyun verilerine aktarır.** Oyun, derlenmiş JSON/Unity assetlerini okumaya devam eder. Oyuncunun bilgisayarında Excel veya Python gerekmez.

Alternatifler:

1. Oyunun doğrudan XLSX okuması: dağıtıma çalışma kitabı okuyucusu ekler, bozuk çalışma kitabının hatalarını oyuna taşır. Bu projeye önerilmiyor.
2. Excel'den elle CSV dışa aktarma: çalışma zamanı basit kalır ama çok sayıda sekmeyi dışa aktarmak, satır sonları ve ayırıcılarla uğraşmak kullanıcıya yük olur.
3. **Editor içinde XLSX → doğrulama → aktarım:** mevcut oyun sistemleri korunur, tek kaynak ve tek aktarım işlemi sağlanır. Önerilen seçenek.

Importer XLSX'in standart ZIP/XML tablosunu Editor tarafında okur; oyun derlemesine XLSX bağımlılığı girmez. Paylaşılan metinler, satır içi metinler, Türkçe karakterler, çok satırlı hücreler, Excel'in göreli worksheet yolları desteklenir. İçeri alınan veri hücrelerinde formül kabul edilmez: hesap önbelleği eski olabilir. Filtreleme ve sıralama serbesttir; kimlik ve sıra sütunları esas alınır.

## Çalışma kitabı

Önerilen dosya: `Content/GameContent.xlsx`.

Başlangıçta mevcut bütün kayıtlarla doldurulur; boş bir şablon teslim edilmez. Sütun başlıkları sabit, açıklamalar Türkçe, düzenlenebilir alanlar belirgin, teknik kimlikler ayrı renkte olur. Başlık sabitleme, filtreler, okunabilir metin genişlikleri ve sarma kullanılır. Örnek satırlar gerçek verinin içine sahte oyun kaydı olarak eklenmez.

| Sekme | Satırın anlamı | Ana alanlar |
|---|---|---|
| `Metinler` | Altyazı dışında tek bir metin anahtarı | Kod, kaynak metin, Türkçe, İngilizce, açıklama/bağlam |
| `Diyaloglar` | Bir sıralı konuşma | Diyalog kodu, not, ses dosyasının proje yolu |
| `Altyazilar` | Bir konuşmanın tek cümlesi | Diyalog kodu, sıra, süre (sn), Türkçe, İngilizce, Almanca (isteğe bağlı) |
| `Baglantilar` | Bir sahne/prefab bileşeninin konuşma kullanımı | Kalıcı bileşen kimliği, sahne/prefab, nesne yolu, tür, giriş kodu, hatırlatma kodları, hatırlatma bekleme süresi; mevcut koşul özeti |
| `Kontroller` | Bir eylemin tek tuş ataması | Eylem kodu, bağlama kimliği, bağlam, parça, varsayılan kontrol yolu, oyuncu değiştirebilir mi, TR/EN eylem adı |
| `Kilavuz` | Düzenleme ve aktarım açıklaması | Sütunların anlamı, düzenlenebilir alanlar, değişken örnekleri, doğrulama mesajları, kaynak dosyalar |

### Altyazı sırası ve tek kaynak

- Excel sırası 1'den başlar. Aynı konuşmada kesintisiz 1, 2, 3... olmalı; fiziksel satır sırası oynatma sırasını değiştirmez.
- Diyalog kodu, bütün segmentleri `Diyaloglar` satırına bağlar. Örneğin `thought_book_0` kodunun 1. ve 2. cümleleri aynı konuşmada ardışık oynar.
- Aktarım segmentleri sıraya dizer, `MainSubtitles.asset` içindeki süre/metin alanlarını ve JSON altyazı anahtarlarını birlikte üretir. Sıra Excel'de 1 iken mevcut JSON anahtarı son eki 0 olur. Teknik farkı kullanıcı elle hesaplamaz.
- Altyazılar `Metinler` sekmesinde ikinci kez düzenlenmez.
- Var olan anahtarlar, kaynak metinler ve diyalog kodları başlangıçta aynen taşınır; mevcut çağrılar ve kayıtlar korunur. Yeni satır eklenebilir. Kullanılan bir kodun silinmesi/yeniden adlandırılması bağlantı hatası olarak gösterilir; sessizce koparılmaz.
- TR/EN eksikleri görünür raporlanır; mevcut eksikler var diye ilk aktarım kullanılamaz hale getirilmez. İsteğe bağlı Almanca boşsa mevcut dil geri dönüşü korunur. Kopyalanmış İngilizce metin otomatik Türkçe çeviri sayılmaz.

### Bağlantılar

- Olayın **ne zaman** çalışacağı mevcut Unity `EventLogicController` ve koşullarıyla tanımlanmaya devam eder. Excel yeni bir hardcoded tutorial oynatıcısına dönüşmez.
- `Baglantilar`, mevcut `DialogAction` bağlantılarını gösterir. Giriş/hatırlatma kodları ve hatırlatma beklemesi düzenlenebilir; bileşen kimliği, sahne/nesne yolu, koşul özeti salt okunur envanterdir.
- Diğer altyazı tüketicileri envanterde gösterilir; yalnızca desteklenen alanlar açıkça düzenlenebilir. Desteklenmeyen tüketiciye yazılabilir görünümü verilmez.
- Bağlantı hedefi yol adıyla tahmin edilmez. Unity'nin kalıcı nesne/bileşen kimliğiyle çözülür. Hedef kayıpsa aktarım durur ve satır bildirilir.
- Sahne eventleri Excel satırından oluşturulmaz/silinmez. Yeni bir event Unity'de hazırlanır, bağlantı envanteri yenilenir ve Excel'de konuşması seçilir.
- Bağlantı envanterini yenilemek, çalışma kitabındaki metinleri veya kullanıcı değişikliklerini üzerine yazmaz. Oyundan yeniden başlangıç dosyası çıkarma ayrı ve açık bir işlemdir.
- Diyalog ses dosyası mevcut veritabanı `voiceClip` alanına aktarılır. `DialogAction.dialogClip` açıkça atanmışsa mevcut önceliği korunur; envanter bu override'ı görünür kılar. Ses kaynağı/koşul referansları Excel importuyla sıfırlanmaz.

### Kontroller ve dinamik ipuçları

- İpucu gerçek bir harf yerine eylem kodu kullanır. Örneğin `[{input:Cancel}] Kitabı kapat`. İngilizce karşılığı `[{input:Cancel}] Close book`. Kod çevrilmez.
- Ekranda `{input:Cancel}` o anda geçerli tuş adıyla gösterilir. F → G atamasında kitap kapatma davranışı ve ipucu birlikte G olur.
- Varsayılan atamalar Excel'den mevcut Input System assetine aktarılır. Oyuncunun kendi ataması ayrı kaydedilir; Excel'den yeni varsayılan aktarılması onu sessizce sıfırlamaz. Varsayılana dönme işlemi yeni varsayılanı kullanır.
- Temel eylemler mevcut `Interact`, `Cancel`, `Pause`, `Move`, `Look`, `Jump`, `Sprint` üzerinden korunur. Defter, sayfa/sekme, cihaz yönleri/onarım/onay gibi gerçekten kullanılan bağlamsal eylemler ayrıca tanımlanır. Aynı fiziksel tuş farklı bağlamlarda farklı iş yapabilir; örneğin W ile yürümek ve saklanırken gözetlemek.
- Sabit tuş okuyan gerçek oyun tüketicileri ilgili eylemlere geçirilir. Tutorial'ın oyuncu tuşuna bağlı koşulları da eylemi dinleyebilir; mevcut özel fiziksel kombinasyonlar uyumluluk için açıkça korunur.
- Dil veya atama değiştiğinde açık altyazı ve görünen ipuçları yeniden çözülür. `{0}` gibi mevcut biçimlendirme değişkenleri ve rich text korunur.
- Başlangıç kapsamı mevcut klavye/fare oynanışı ve mevcut gamepad bağlamalarının kaybolmamasıdır. Yeni bir gamepad gezinme sistemi veya glif paketi eklenmez.
- İlk teslimde yeni bir kontrol ayarları ekranı tasarlamak kapsamda değildir. Excel varsayılanları ve çalışma zamanı yeniden atama/kaydetme API'si hazırlanır; mevcut ekranda bulunmayan yeni bir kontrol menüsü ayrıca istenirse bu API'yi kullanır. Bu kapsam tercihi uygulayıcının önerisidir, mevcut özellik gibi sunulmaz.

## Unity iş akışı

`Senzora > İçerik Tablosu` altında:

1. **Çalışma kitabını aç:** tek düzenleme dosyasını açar.
2. **Doğrula:** sekme/satır/alan içeren rapor üretir; oyun dosyası değiştirmez.
3. **Oyuna aktar:** önce bütünü doğrular, sonra onaylanmış alanları aktarır. Eksik hedef, geçersiz kontrol yolu, çift kod, hatalı sıra/süre gibi yapısal hata varsa hiçbir veri aktarılmaz.
4. **Bağlantı envanterini yenile:** mevcut yazıları koruyarak sahne bağlantılarını yeniler.

Otomatik dosya değişikliğiyle sahneye yazılmaz; kullanıcı açıkça aktarır. Excel açıkken kilitliyse anlaşılır hata verilir. Değişmeyen dosyalar yeniden yazılmaz. Aktarım öncesi açık sahnelerin kaydedilmemiş değişiklikleri korunur; kapsam dışı sahne kaydetme yapılmaz. Doğrulama sırasında sahneler sessizce değiştirilmez. Başarısız aktarım kısmi güncelleme bırakmamalı; dosya yazımları hazırlanıp topluca uygulanmalı, hata halinde önceki içerik geri yüklenmeli.

Çalışma kitabı yalnızca düzenleme kaynağıdır. JSON/asset/bağlamalar da sürüm kontrolünde tutulur ve oyun derlemesi için hazır kalır. Çalışma kitabı son aktarımdan beri değişmişse Editor ve build kontrolü bunu görünür kılar; otomatik olarak eski veriyi güncelmiş gibi sunmaz.

## Doğrulama ve kabul

1. Mevcut bütün metin/diyalog/sıra/süre/ses referansları ilk dışa aktarma ve geri aktarmada korunur; mevcut editler üzerine eski içerik geçirilmez.
2. İki segmentli bir konuşmada sıra, süre ve TR/EN metni değiştirilir; gerçek altyazı yöneticisi yeni sırayı ve süreleri uygular. Açık altyazıda dil değişimi korunur.
3. Bağlantı satırında bir konuşma değiştirilir; yalnızca hedef bileşenin ilgili alanı değişir. Tutorial koşulları, kamera ve yeniden açmada tekrar etmeme davranışı korunur.
4. F → G yeniden atamasında gerçek kitap/cihazdan çıkış G ile çalışır, F bırakılır, görünen ipucu G olur. Kayıt/yükleme ve varsayılana dönme kontrol edilir. Defter ve sayfa eylemleri de aynı kaynaktan okunur.
5. Aynı anahtar, olmayan konuşma/eylem, kırık nesne kimliği, yinelenen/eksik sıra, sıfır/negatif/NaN süre, bozuk değişken, geçersiz kontrol yolu, formül hücresi ve kilitli XLSX anlaşılır satır hatası verir. Hatalı import hiçbir hedefi değiştirmez.
6. Excel'de satır sıralama ve hücre içi satır sonları doğru içe alınır. İşlenmemiş `{input:...}` kodu oyuncuya gösterilmez.
7. Editor/runtime derlemesi, mevcut localization/tutorail kontrolleri ve değişen kontrol akışları uygun testlerle doğrulanır. Yeni görünür Excel sekmeleri render edilip okunabilirliği incelenir.

## Uygulayıcıya bırakılan ayrıntılar

Mevcut proje örüntülerine uygun dosya/sınıf adları, Editor penceresinin sade görünümü, XLSX okuma yardımcılarının iç yapısı, kesin kontrol eylemi listesi (gerçek çağrı envanterinden çıkarılmalı) ve testlerin uygun Unity/bağımsız araç dağılımı. Yeni paket yalnızca mevcut araçlar/standart kütüphane yetmiyorsa düşünülmeli. Kullanıcının mevcut değişiklikleri korunmalı; bu iş kapsamında commit veya yayın istenmedi.
