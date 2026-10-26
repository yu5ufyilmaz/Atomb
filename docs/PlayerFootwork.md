# Karakter ayak hareketleri

Etkin sahne: `Assets/Scenes/scn_LaboratorySynthesis.unity`. Karakter: `Senekas_Player` üzerindeki `PlayerFootwork`. Özgün sahne ve ortak Animator controller değiştirilmez; kopya `Assets/Animations/Player/LaboratoryFootwork.controller` yalnız temel IK Pass'i açar ve mevcut klip/durum/geçişleri korur.

Yerinde dönüşte ayaklar dünya konumunda destek olur, sırayla kısa adımlar atar ve yeni yöne basar. Normal yürüyüş/koşunun ayak kaldırma hareketi korunur. Her ayak kendi altındaki collider'a göre yerleşir; erişilemeyecek hedefler, aşırı eğim ve boşluklar reddedilir. Basamakta alçak ayağa erişmek için normal gezinmede sınırlı/yumuşak pelvis ağırlık aktarımı kullanılır; root/capsule ve kamera hiyerarşisi taşınmaz.

Düzeltme yalnız başlayan, duraklatılmamış oyunda; etkin hareket bileşeni, capsule ve Humanoid Animator ile, dondurulmamış ve yere basan karakterin `Base Layer.Blend Tree` durumunda çalışır. Kitapta, tahta/Turing yaklaşma ve oturma/kalkmada, introda, saklanma/jumpscare ve diğer özel durumlarda devreden çıkar. Ayak hedefleri etkileşim ve teleport sonrasında yeniden kurulur. Yeni bileşeni taşımayan karakterlerin dönüş davranışı korunur.

Inspector ayarları dünya birimindedir: zemin erişimi `.3m`, azami pelvis düşümü `.2m`, yerinde gövde dönüşü `120°/s`, adım süresi `.28s`, adım yüksekliği `.06m`. Taban mesafesi gerçek modelin desteklenen düz duruşundan kalibre edilir. Ayak IK hedefi ile kemik pivotunun farklı konumu/dönüşü hesaba katılır.

Doğrulama menüleri: `Senzora > Player Footwork > Apply to Laboratory Copy` edit modunda yalnız çalışma kopyasına kurar. `Validate Play Footwork` geçici bir Play test alanında gerçek karakterin ayak kemiklerini ve collider'ları ölçer; sonunda alanı kaldırıp karakter durumunu geri alır.

26 Ekim 2026: eski davranışta alçak ayak18cm havada kaldı. Son sürümde iki taraflı18cm basamak temasları2.5cm toleransı içinde, +/-90 ve180° yerinde adımlar, yürüyüş/duruş,12/35° eğimler, desteksiz ayak, frozen ve SitIdle kontrolleri geçti. Görsel basamak pozu `Temp/player-footwork-step.png` içinde incelendi. Kitap/defter/tahta/ölçüm/özgün Turing tam akışı da yeni bileşenle geçti. Daha yüksek veya farklı geometride basamaklar bacağın erişim ve eğim sınırlarına tabidir.
