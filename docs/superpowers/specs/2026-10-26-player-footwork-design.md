# Karakterin dönüş adımları ve ayakların zemine oturması

Durum: kullanıcının dönüş ve basamak düzeltmesi isteği doğrultusunda uygulama kapsamı. Bağımsız rig/animasyon incelemeleri tamamlandı; ek bir kullanıcı tercihi gerektiren konu bulunmadı. İlk uygulama mevcut laboratuvar çalışma kopyasıyla sınırlıdır.

## İstenen sonuç

Karakter yerinde dönerken ayakları yere yapışık bir heykel gibi kaymamalı; küçük ve sırayla atılan dönüş adımları görünmeli. İki ayak farklı yükseklikte bir basamağa bastığında her biri kendi yüzeyine oturmalı. Mevcut yürüyüş, koşu, oturma/kalkma, kitap, tahta, Turing, saklanma ve jumpscare pozları korunmalı.

## Projede doğrulanan durum

- Çalışma sahnesi `Assets/Scenes/scn_LaboratorySynthesis.unity`; gerçek karakter `Senekas_Player`, Humanoid rig, 3,75 ölçek ve yaklaşık 1,8 m dünya yüksekliğinde capsule kullanıyor.
- `CharacterController.Move` gövdeyi dururken de kameraya çeviriyor; Animator'a yalnız doğrusal VelocityX/VelocityZ gidiyor. Böylece yerinde dönüş idle ayak pozuyla gerçekleşiyor.
- Etkin locomotion durumu `Base Layer.Blend Tree`; mevcut temel Animator katmanında IK Pass kapalı, çalışan ayak yerleştirme bileşeni yok.
- `Left Turn`, `Right Turn` ve `BackTurn` klipleri PanicTurn/jumpscare durumlarına bağlı. Normal dönüş için bu kliplerin mevcut bağları ve import ayarları değiştirilmeyecek.
- Kamera kafa kemiğine bağlı. Kalça veya Animator.bodyPosition değişiklikleri birinci şahıs kamerasını da taşır.
- Animation Rigging 1.4.1 kurulu, ancak karaktere bağlı çalışan bir ayak rig'i yok. Paket dosyalarındaki mevcut kullanıcı değişiklikleri korunacak.

## Değerlendirilen yollar

1. **Önerilen: mevcut Humanoid Animator'ın üzerinde sınırlı ayak IK ve dönüşte sıralı adım.** Mevcut animasyonlar kaynak olarak kalır; her ayağın hedefi kendi zeminine uyarlanır. Yerinde dönerken bir ayak destek olurken diğeri kısa bir yay çizerek yeni yöne basar. Yeni animasyon paketi veya RigBuilder hiyerarşisi gerektirmez. Parametreler gerçek model üzerinde görsel olarak ayarlanmalıdır.
2. **Yeni, özel hazırlanmış dönüş klipleri ve ayrı dönüş durumları.** Sanatçı tarafından hazırlanmış iyi kliplerle en yüksek animasyon kalitesi elde edilebilir. Projede hazır gündelik dönüş klipleri bulunmadığından ilave animasyon üretimi/temini gerekir; bu ilk düzeltme için daha geniş kapsamdır.
3. **Var olan PanicTurn kliplerini normal dönüşe bağlamak.** Kamera ve üst gövde hareketleri ile jumpscare bağları nedeniyle mevcut etkileşimleri koruma hedefiyle uyuşmaz; önerilmez.

## Önerilen davranış ve sınırlar

- İlk uygulama yalnız mevcut laboratuvar çalışma kopyasındaki karakterde etkin olur. Ortak Animator controller'ın ayrı bir kopyasında yalnız gerekli temel IK geçişi açılır; mevcut state, transition, trigger ve klipler korunur.
- Her ayak kendi altındaki collider'a bakar. Yüzey yüksekliği, eğimi, taban/ayak bileği mesafesi ve gerçek dünya ölçeği hesaba katılır. Kendi karakterine, trigger'lara, aşırı dik veya erişilemeyecek yüzeylere basılmaz.
- Normal yürüyüşte animasyondaki ayağı kaldırma hareketi korunur; havadaki ayak zorla yere çivilenmez. Destek ayağı daha güçlü, adım atan ayak daha düşük IK etkisi kullanır.
- Yerinde dönüşte iki ayak aynı anda yerde sürüklenmez. Destek ayağı dünya konumunu korur, diğer ayak kısa bir kaldırma/yön değiştirme/yeniden basma hareketi yapar; dönüş sürerse destek değişir. Hızlı yön değişimi, ileri yürümeye geçiş ve teleport eski ayak hedeflerini temizler.
- Destek ayağının gövdeye göre mesafesi ve açısı sınırlıdır. Hızlı 90/180 derece dönüşte bacak burulmadan yeni adım başlatılır veya hedef bırakılır.
- Gövdenin hedef yönü, animasyonlu/efektli render kamerasının yönünden geri beslenmez; oyuncunun amaçladığı bakış yönü kullanılır. İlk şahıs bakış tepkisi korunur.
- Karakter kökü/capsule ve kamera hiyerarşisi ayak IK tarafından değiştirilmez. Alçak basamağa erişmek için gerektiğinde yalnız serbest gezinmede sınırlı ve yumuşak bir kalça/diz ağırlık aktarımı uygulanabilir; baş kamerasına etkisi görsel olarak kontrol edilir. Çok yüksek bir basamak bacağın erişimini aşarsa bacak esnetilmez; çözüm ağırlığı azaltılır.
- Sistem ancak oyun başlamış, pause kapalı, karakter hareket bileşeni/capsule/Animator etkin, karakter yere basıyor ve hareket dondurulmamışken çalışır. Mevcut ve sonraki temel Animator durumu normal locomotion olmak zorundadır.
- Kitap, makine yaklaşması/oturma, intro, kalkma, saklanma, düşme ve jumpscare sırasında ayak düzeltmesi etkisizdir. Özellikle Turing yaklaşımında activeInteraction henüz atanmamış olabileceği için yalnız etkileşim bayrağına güvenilmez.

## Uygulamadan sonra doğrulama

- Gerçek modelde düz zemin, sol/sağ ayağın ayrı ayrı 10 ve 20 cm basamak üzerinde olması, uygun erişimde 25 cm, kenar boşluğu ve eğim kontrolü. Desteklenen temaslarda taban-yüzey farkı hedefi yaklaşık 2 cm altında; bacak uzaması veya diz ters dönmesi olmamalı.
- Yerinde 90 derece sağ/sol, geri dönüş ve hızlı yön değişimi: ayaklar sırayla kalkıp basmalı, kök durduğu yerde doğrusal olarak kaymamalı. Yürüyüş/strafe/koşu adım döngüsü devam etmeli.
- Kalça ağırlık aktarımı sınırlı ve yumuşak olmalı; ayak düzeltmesi kamera/gövde yönünü değiştirmemeli, ilk şahıs kamerada sert sıçrama yaratmamalı.
- Kitaplarda mevcut gövde/kamera sabitliği testi ve kapanışta kontrolün geri gelmesi yeniden çalıştırılır.
- Intro/kalkma, tahta, gerçek Turing yaklaşma/oturma/okuma/çıkış akışlarında IK etkisinin kapalı olduğu ve sonrasında serbest gezinmeye döndüğü doğrulanır. Saklanma ve PanicTurn durumları ayrıca kontrol edilir.
- Özgün sahneler, ortak animasyon klipleri, mevcut controller ve yerel kayıt dosyası değişmemeli. Sonuç yalnız otomatik kontrollerle değil gerçek karakterin ayakları görünürken görsel incelemeyle değerlendirilir.

## Teknik dayanak

Unity'nin [OnAnimatorIK](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnAnimatorIK.html) ve [Humanoid ters kinematik](https://docs.unity3d.com/6000.3/Documentation/Manual/InverseKinematics.html) akışı, mevcut Humanoid klibinin üzerine ayak hedefi uygulamak için kullanılabilir. Bu tasarımın kapsamı yeni bir tüm vücut animasyon sistemi kurmak değil, mevcut locomotion'un ayak davranışını tamamlamaktır.
