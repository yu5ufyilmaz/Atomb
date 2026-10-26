# Deney parçaları — mevcut envanter ve önerilen görev ayrımı

Durum: katalog ve uygulama notu. **Çift Kayıt** testinde ilk altı aile, iki numune tepkisi, ayrı çizimler ve yönlü giriş/çıkışlar uygulanmıştır. Güçlendirici ve Süzgeç öneri olarak durur. Aşağıdaki eski beşli envanter, korunmuş İkinci İz prototipinin durumudur.

## Şu anda çalışan beş parça

Kaynak: `Assets/Scripts/PuzzleSystems/Chalkboard/SecondTraceExperiment.cs`.

| Parça | Prototipteki gerçek davranışı | Açıklanması / ayrılması gereken nokta |
|---|---|---|
| Uyarıcı | İz modelindeki yolların başlangıcı. | Deneyi başlatan atımın kaynağı olduğu açık olmalı. |
| Numune | Ölçüm yolunun içinden geçmesi gereken bileşen. Kendine özgü bir dönüşüm uygulamıyor. | Hangi numunenin neden araştırıldığı, çıktıda gözlenebilen bir tepkiye bağlanmalı. |
| Algılayıcı | Yolların bittiği yer; ilk ve gecikmiş izler burada hesaplanıyor. | Sonucu kaydeden parça olduğu görünür olmalı. |
| Referans | İçinden geçen yol gecikmiş ve yarı güçlü iz sayılıyor; karşılaştırma yapmıyor. | Bu görev için ad yanıltıcı. Gecikme ve zayıflama aynı parçaya örtük biçimde yüklenmiş. |
| Sönümleyici | İçinden geçen yolun iz gücünü beşte bire indiriyor. | Etkisi açık; giriş/çıkış ve çizim kimliği ayrılmalı. |

Beş parça aynı dört uçlu şablonu kullanıyor. Hatlar yönsüz; bugün görünen uçlar giriş/çıkış ayrımı yapmıyor. Kapasiteyi azaltmak tek başına fazla görünen uçları kaldırmaz.

## Önerilen sekiz parça ailesi

Ortak akış: **Atım üret → numuneden tepki al → tepkiyi işle / yönlendir → kaydet.**

| Parça | Tek ve gözlenebilir görevi | Önerilen bağlantı | Önerilen çizim |
|---|---|---|---|
| Atım Kaynağı | Deneme başladığında bir atım yollar. | 0 giriş / 1 çıkış | Tek çıkışlı daire; içinde bir atım işareti. |
| Numune | Gelen atıma kendine özgü tepki verir. N-01 dar bir iz, N-02 geniş bir iz üretebilir. | 1 giriş / 1 çıkış | Numune kabı; kimlik ve kendi tepki çizimi. |
| Ölçer | Kendisine ulaşan izleri kaydeder; onları değiştirmez. | Tek kanallı: 1 giriş / 0 çıkış. Çift kanallı: 2 giriş / 0 çıkış. | Soldan girişli küçük kayıt ekranı. |
| Sönümleyici | Gelen izin yüksekliğini azaltır; zamanı ve sayısını değiştirmez. | 1 giriş / 1 çıkış | Büyük iz → küçük iz. |
| Geciktirici | Gelen izi daha geç iletir; yüksekliğini ve sayısını değiştirmez. | 1 giriş / 1 çıkış | Eşit yükseklikte, sağa kaymış iki iz. |
| Dağıtıcı | Gelen tepkiyi iki ayrı çıkışa iletir; iki kolun aynı başlangıçtan doğduğunu görünür kılar. | 1 giriş / 2 çıkış | Y biçiminde gövde. |
| Güçlendirici | Gelen izin yüksekliğini artırır; zamanı ve sayısını değiştirmez. | 1 giriş / 1 çıkış | Küçük iz → büyük iz. |
| Süzgeç | Üzerinde belirtilen tepki türünü geçirir; diğerini tutar. Örneğin kısa atımları geçirir, uzunları tutar. | 1 giriş / 1 çıkış | Üzerinde geçirdiği iz şekli bulunan ızgara. |

Çift Kayıt'ta Dağıtıcı ve Geciktirici ayrı görevlerle uygulanmıştır; numuneler kısa ve uzun tepki üretir. Sönümleyici bu deney ailesinde yüksekliği yarıya indirir. Güçlendirici ve Süzgeç henüz uygulanmamıştır. Dağıtıcı burada uyarım bilgisini iki kola ileten kurgusal bir deney bileşenidir; enerji bölüşümü için ek bir hesap kuralı önerilmemektedir.

## Referansın anlamı

Referans, davranışını önceden bildiğimiz bir **numune** olabilir. Bilinmeyen numuneyle karşılaştırmada anlam kazanır. Kendi başına ikinci atım göndermesi veya gecikme üretmesi gerekmez. Her questte bulunması zorunlu değildir. İkinci İz'deki parçaya bu nedenle Geciktirici denmesi önerilir.

## İkinci İz için ilk altı aile

Atım Kaynağı, Numune, Dağıtıcı, Geciktirici, Sönümleyici ve çift kanallı Ölçer.

Başlangıç düzeninde Kaynak → Numune → Dağıtıcı vardır. Dağıtıcının bir çıkışı Ölçer'e, diğeri Geciktirici üzerinden Ölçer'in ikinci girişine gider. Böylece iki izin kaynağı anlaşılır: aynı tepki iki yoldan farklı zamanda ulaşmıştır. Sönümleyicinin yeri oyuncunun araştıracağı karardır. Güçlendirici ve Süzgeç bu ilk denemenin parça listesini büyütmek için eklenmez.

## Kitapta kullanılacak açıklama biçimi

Her parça kaydı şunları göstermeli:

1. Bir cümle: ne alır ve neyi değiştirir / üretir?
2. Önce → sonra çizimi: değişen özellik görünür olmalı.
3. Gereken sayıda, giriş ve çıkışı ayırt edilen uç.

Örnek: **Geciktirici — İçine giren izi daha sonra iletir. İzin yüksekliği değişmez.** Kitap çiziminde eşit yükseklikte bir iz zaman boyunca sağa kayar. Bu bilgi görevden önce bulunabilir ve farklı deneylerde tekrar kullanılabilir.

Numunenin tepki özellikleri, işlem parçalarının etkileri ve bağlantı kuralları tutarlı kalır. Questler bu ortak kurallara farklı hedefler verir; parça, her questte farklı bir gizli anlam kazanmaz.
