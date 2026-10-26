# Test Sahnesinde İki Araştırma Örneği Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** scn_SynthesisBlockout içinde kitaplardan bağımsız sırayla toplanan bilgiyle çözülen iki farklı oynanabilir araştırma örneği.

**Architecture:** Test sahnesine bağlı ResearchPuzzleTrial bileşeni dört araştırma kitabını, geçici içerik kataloğunu ve test modu sunumunu yönetir. Ortak arayüz ve doğrulamadaki küçük dallar yalnız etkin test bileşeni ilgili tahtaya bağlıyken devreye girer. Önceki iki alıştırma ve üretim sahneleri korunur.

**Tech Stack:** Unity 6000.3.10f1, mevcut TMP/UGUI ve düğüm–bağ sistemi; yeni paket yok.

**Spec:** ../../PuzzleDesignFoundation.md; kullanıcının iki örneği mevcut test sahnesinde serbestçe hazırlama talebi. Bunlar oynanış prototipidir; üretim içeriğine taşıma bu işin kapsamı değildir.

## Global Constraints

- Yalnız Assets/Scenes/scn_SynthesisBlockout.unity sahnesi değiştirilir.
- Kullanıcının mevcut çalışma ağacı değişiklikleri korunur; sahne yeniden oluşturulmaz.
- Hedefler, parçalar ve genel bilgiler ayrı kitaplarda bulunur; hepsi önceden toplanmak zorunda değildir.
- Birinci örnek bilinen parçaların düzenini çıkarma; ikinci örnek başarısız bir deneyi teşhis etmedir.
- Bilgi kartları görev listesine otomatik dönüşmez; aynı not iki soruda da kullanılabilir.
- Başarısız deneme parçaları tüketmez. Çözüm bilgi kartlarının toplanma sayısına bağlanmaz.
- Gizli cevap şeması normal hedef görünümünde açığa çıkmaz; isteğe bağlı hile görünümü çözümü açıklar.
- Başka sahnelerde bu prototipin davranışı etkin değildir.

## Review Focus

- Bilgi ve parçaları hedeften önce toplama; hedef bulunmadan görev adı ifşası olmaması.
- Yanlış parça, fazladan parça, yanlış tek/çift bağ ve eksik/yanlış komşuluğun reddi.
- Aynı kaynak ve kural notunun iki hedefte de kullanılabilmesi.
- Hedef değişimi, tamamlanmış hedef ve kayıt dönüşünde çalışma durumunun korunması.
- Dört gerçek kitapta okunabilir sayfalar, ulaşılabilir fiziksel yerleşim ve ana sahnelere etki olmaması.

### Task 1: İçerik ve doğrulama

**Files:** Create ResearchTrialContent.cs; Create Editor/ResearchTrialValidation.cs.

- [x] Mevcut düğüm/bağ API'siyle iki gerçek çözüm ve ikinci örnek için başarısız başlangıç grafiği tanımla.
- [x] İki çözümün geçmesi; örnek B'nin iki kusurunun her biri tek başına düzeltilince hâlâ başarısız olması; not toplama kapısı bulunmaması için çalıştırılabilir kontroller yaz.
- [x] Hedef/parça/bilginin farklı kitaplara dağıtıldığını ve kaynakların çözüme yeterli olduğunu doğrula.

### Task 2: Teste özel oturum ve arayüz bağlantısı

**Files:** Create ResearchPuzzleTrial.cs; minimal modifications to SynthesisBlockoutScene, ChalkboardManager, ChalkboardDocuments, NotebookWorkspaceController.Information, QuestDiagramGraphic, NotebookWorkspaceController.Sections.

- [x] Test içeriğini kataloglara kaydet; dört kitabın sayfalarını yapılandır; araştırma modu yoksa mevcut akışı çalıştır.
- [x] Genel not arşivi ve serbest not sabitlemeyi sadece bu modda etkinleştir.
- [x] Hedef gösterimiyle doğrulanacak çözümü ayır; çözümü normal görünümde göstermediğini kontrol et.
- [x] Denemelerdeki geri bildirimleri gözlenebilir grafik koşullarına bağla; önceki alıştırma davranışını koru.
- [x] Bağımsız kitap erişimini test oyuncusuna ekle; normal yürüme/etkileşim korunsun.

### Task 3: Sahneye kurulum ve oynanış kontrolü

**Files:** Create Editor/ResearchTrialSceneSetup.cs; modify only scn_SynthesisBlockout.unity; Create docs/ResearchTrials.md.

- [x] Başlangıçta yalnız test bileşenini ve yeni kitap istasyonlarını sahneye ekleyen idempotent kurulum komutu yaz.
- [x] Mevcut sahneyi silmeden yeni içerikleri kur; üretim sahnelerini açma/kaydetme.
- [x] Unity derlemesi ve editör kontrollerini çalıştır; hataları gider.
- [x] Play modunda yanlış denemeyi, iki çözümü, B'nin A'dan önce çözülmesini, parça tüketilmemesini ve hedef başına grafik/tamamlanma durumunun korunmasını kontrol et.
- [x] Fiziksel kitaplardan hedef öncesi not alma ve gerçek defter koleksiyonuna eklenme akışını kontrol et.
- [x] Kullanım ve spoiler çözüm notlarını ayrı belgeye yaz; doğrulamada test sahnesi kapsamını kontrol et.
- [x] Son görsel kontrolde kitapların içerik bulunan sayfalarının okunabilirliğini ve hedef/parça kartlarının defterden tahtaya gerçek bırakma akışını doğrula.

## Kararlar ve ilerleme

- Kullanıcı bu örnekleri test sahnesinde denemek için serbest üretim yetkisi verdi; yeni genel mekanik tasarımını yeniden onaya götürmek yerine mevcut temel üzerinden oynanış prototipleri hazırlanır.
- Mevcut Unity checkout'unda çok sayıda önceki kullanıcı değişikliği var. Açık test projesini ve asset referanslarını korumak için aynı checkout kullanılır; çalışma ağacı sıfırlanmaz, commit atılmaz.
- 25 Ekim 2026: Unity Edit doğrulaması, canlı B-önce-A kontrolü, parça tüketmeme, hedefe ait grafik/tamamlanma durumunu geri yükleme ve gerçek kitap → defter koleksiyonu kontrolleri geçti. Dört kitabın içerikli sayfaları metin taşması kontrolünü, toplanmış B hedefi ve altı Atlas notu gerçek defter → tahta bırakma kontrolünü geçti. Son canlı kontrolde konsol hatası yok; editör test sahnesinde Play kapalı bırakıldı. Gerçek Game görüntüleri docs/ResearchTrials altında saklandı.
