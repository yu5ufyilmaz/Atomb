# Kitap / Tahta Blockout

`Assets/Scenes/scn_SynthesisBlockout.unity` sahnesini tek başına açıp Play'e basın. Oda, masa, kitap, tahta ve basit oyuncu Play sırasında oluşturulur. Edit modunda tek kurucu nesne görünmesi normaldir.

- WASD / fare: hareket ve bakış.
- Sol tık: bakılan kitap veya tahta ile etkileşim.
- 1: kitap denemesine geçiş; 2: tahta denemesine geçiş.
- F: etkileşimden çıkış. Escape de test oyuncusunda çıkışı tetikler.
- Kitap: sabit üç parçayı deftere sürükle.
- Tahta: defterdeki parçaları tahtaya taşı, uçları ve bağları dene, bağımsız parçayı deftere geri al.

Kurucudaki Enable Procedural Quests varsayılan olarak kapalıdır. Bu sürüm etkileşim prototipidir: görev üretimi, kayıt dosyaları, düşmanlar, basınç, ölüm ve ana menü yoktur. Yeni oyuncu ana oyuncu prefabına veya animatörüne bağımlı değildir. Build Settings değiştirilmedi.

Ana laboratuvar sahnesi veya oyuncu prefabı bu çalışma sırasında değiştirilmedi. Kamera ve oyuncu pozu etkileşim bitince geri yüklenir. Odayı başka oyun sahneleriyle additive olarak çalıştırmayın.

Doğrulama: runtime C# kodu mevcut Unity DLL'lerine karşı derlendi. Açık ana editörde Play testi otomatik olarak çalıştırılmadı.

Gerçek P_Book prefabı kullanılır. Kitaptaki ayrı pencere gizlenir; ilk sol sayfadaki üç blok doğrudan modelin sayfa yüzeyine yerleştirilir. A/D sayfa çevirir; çevirme sırasında bloklar gizlenir. Sayfanın üstündeki Kitabı Taşı bandı modeli sürükler. Kaynak blok kitapta kalır, sürükleme görseli ekran canvas'ına geçer ve deftere kopyalanır. Kitabın test kopyasının emisyon ve hover parlaması kapalıdır; orijinal materyal assetleri değiştirilmez. İzole Unity UI testi sayfa kaynaklarını, ekran katmanına geçişi, bırakma yönlendirmesini ve sayfa görünürlüğünü doğruladı; gerçek modelin görsel yerleşimi editörde kontrol edilmelidir.
