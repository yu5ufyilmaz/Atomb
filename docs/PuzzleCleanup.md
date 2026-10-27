# Puzzle temizliği — 27 Ekim 2026

Korunan akış: `scn_LaboratorySynthesis` içindeki dört kitap → defter arşivi → yönlü tahta deneyi (**Çift Kayıt**) → ölçüm kağıdı → Turing okuyucusu. `scn_SynthesisBlockout` aynı puzzle için mevcut test odasıdır.

Kaldırılanlar: sembol envanteri/overlay/yerleştirme puzzle sistemi, üreticileri, eski sembol ipucu SO’ları ve defter sekmesi; başlangıç ve ikinci alıştırma SO’ları; eski A/B ve İkinci İz içerikleri, çizimleri ve kurulum araçları; tarif SO’ları ve beş aşamalı prosedürel görev üretimi; bunlara ait eski doğrulamalar ve belgeler.

Eski puzzle kitap prefablarının sahne örnekleri Unity üzerinden açıldı; kitap modelleri, animasyonları ve yeni araştırma kitabı bağlantıları korundu. Eski bileşenler ayrıldıktan sonra prefab/SO/script dosyaları `.meta`larıyla birlikte silindi. Mevcut sistemin script ve şablon adları değiştirilirken GUID’leri korundu.

Ortak parçalar: `FormulaCatalog` yalnız aktif deneyin parça kimliklerini çözer. `SignalPartTemplate.asset` ve `P_SignalPart.prefab`, çalışma anında oluşturulan yedi deney parçasının ortak çizim şablonudur; eski puzzle içerikleri değildir. Kitap/sayfa, defter, çizim, düğüm, bağlantı, tahta görüntüsü ve kayıt veri yapıları kullanılmaya devam eder.

Oyuncu, kamera ve etkileşim pozları temizlik öncesindeki mevcut sahneyle karşılaştırılır. Tarihsel `scn_labratory` kopyasındaki kamera pozları daha önce düzenlendiğinden bu eski sahne tek başına poz ölçütü değildir. Temizlikten önce mevcut 9 eksik-script konumu ayrıca kaydedildi; yeni eksik script oluşmaması kontrol edilir.

Build Settings ve başlangıç yönlendirmesi bu temizlikte değiştirilmedi. Çalışma sahnesinin oturumluk kayıt koruması sürer; kalıcı ölçüm kaydı ve final kapı ilerleyişi bu temizliğin kapsamına eklenmedi. Kontroller `Senzora > Çift Kayıt > Geliştirici` ve mevcut `Lab Migration` menülerindedir.

Doğrulama: Unity derlemesi, güncel sinyal model/port testleri, 2.873 korunan nesnenin poz karşılaştırması, silinen GUID ve yerel sahne bağlantıları taraması yapıldı. Gerçek kitap → defter → tahta → ölçüm → Turing okuyucusu akışı üç kez geçti; son başarılı ölçüm gerçek makine çarklarından gönderildi ve görev tamamlandı. Önceki 9 eksik-script kaydı artmadı. Eski ipucu olayları ayrılırken diğer olay koşulları ve aksiyonları korundu.

Son dosya taraması: 82 kaldırılmış GUID’ye kalan referans yok; beş değişen sahnede boşa düşen yerel fileID yok; sahipsiz `.meta` yok. Yaklaşık 6.200 satır C# kaldırıldı. Salt okunur son kod incelemesinde önemli çalışma zamanı gerilemesi bulunmadı. Yerel kayıt dosyası son test öncesi ve sonrasında aynı SHA-256 değerinde kaldı.
