# Kara tahta ve tutorial uygulama planı

**Amaç:** `scn_LaboratorySynthesis` eğitiminde eski not/Tab bölümünün yerine gerçek kitap → defter → tahta → ölçüm işlemini öğretmek.

**Yaklaşım:** Mevcut ResearchPuzzleTrial, kart arşivi ve sahnenin olay zinciri kullanılacak. Ayrı bir eğitim sistemi veya yeni bağımlılık eklenmeyecek. Başlangıç deneyi sabit üç parçalı olacak; kitapta hedef, bağlantı ipucu ve üç parça bulunacak. Normal araştırma kitapları korunacak. Eğitim kapalıyken normal araştırma oturumu çalışacak.

Alternatifler: Eski nota yalnızca açıklama eklemek tahta kullanımını doğrulamaz. Tüm düşman/vana eğitimini yeniden yazmak gereksiz kapsam oluşturur. Seçilen uygulama mevcut olayların ilk iki koşulunu gerçek kart toplama ve ölçüm koşullarıyla değiştirir; Turing kabulü yeni ölçüm sisteminden gelir.

## İşler

- [ ] Tahta regresyon kontrollerini yaz ve mevcut panelin kapatıldığında fiziksel gövdesini bıraktığını doğrula. Tutamağı tahta içine al; panelle beraber arkalığı da gizle. Kapalı panoda bırakma/çizim engellenmemeli; açma durumu kaydedilmeli. Kullanıcının seçtiği araştırma sahnesinin belge paneli değiştirilir.
- [ ] Şalter kontrolünde sistem kapalı/oyun başlamamış/duraklamış/eğitim sürerken otomatik risk hesabını engelle. Sayaç her karede aynı koşulu kullanmalı. Eğitimdeki tek kontrollü ForceTrip çağrısını koru. Ön koşul gerçekleşmeden input/etkileşim ve düşman durumu biriktirilmesini engelle; devre dışı olaylar çalışmamalı.
- [ ] Sabit başlangıç deneyini mevcut araştırma oturumuna ve masadaki kitaba ekle. Toplama koşulu hedef, ipucu ve üç parçanın arşivde varlığını doğrulasın. Tahta adımı hedef ve ipucunu sabitleme, panoyu kapatma ve doğru ölçüm gerektirsin. Sonuç Turing'de kabul edilince vana eğitimine ilerlesin.
- [ ] Aktif adımı gösteren, tıklamaları yakalamayan kalıcı görev metni ekle. Eski sahnedeki bağımsız kapı hatırlatmasını ön koşula bağla. Tutorial aç/kapat, yeni oyun ve tamamlanma durumlarını tutarlı kıl.
- [ ] Unity batch doğrulaması, sahne bağlantıları ve gerçek Play akışını kontrol et. Son diffi incele; doğrulama kapsamını ve kalan sınırları raporla.

## Doğrulama odağı

Kapalı pano görünür mesh/girdi bırakmamalı. Eğitim kapalıyken sistemler kilitli kalmamalı. Şifre/ölçüm kabulü eğitimin ortasında tüm etkileşimleri açmamalı. Yanlış/eksik kart veya hatalı ölçüm ilerletmemeli. Yeni oyun ders içeriğini çoğaltmamalı ve eski oturum ölçümü kabul edilmemeli.

**Çalışma tercihi:** Kullanıcının açık proje sahnesine doğrudan uygulanır; commit/push yapılmaz. Sahne seçimi kullanıcı tarafından `scn_LaboratorySynthesis` olarak netleştirildi.
