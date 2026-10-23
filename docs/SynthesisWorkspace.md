# Senzora sentez çalışma alanı

Kitap açılınca kitap ve defter pencereleri birlikte görünür. Başlıklarından sürüklenir, üst üste veya yan yana dizilir. Tahtada defter ve hedef şema açık kalır. Normal yürüyüşte Tab ve fiziksel makine slotu davranışı korunur.

## Oynanış

1. Kitaptaki dosya oklarıyla beş aşamanın hedeflerini, kural bloklarını ve parçaları inceleyin. Açıklama metni basılıdır; yalnız çerçeveli bloklar kopyalanır. Bozuk modül uyarısı blok dışında görünür.
2. İstenen blokları defter kağıdına bırakın. Hedefler üst tarafta yığılır. Taşı, Kalem, Silgi ve + Sayfa düğmeleriyle çalışma alanını düzenleyin. Silgi çizgiye temas eden karalamayı veya tıklanan bloğu kaldırır. Sayfa sayısı sınırlanmaz.
3. Tahtada defterdeki parçayı boş tahta yüzeyine sürükleyin. Birleşik molekülü taşıyın; Q/E veya tekerlekle döndürün. Uçtan uca çizgi çekin; aynı çift üzerinde ikinci çizim çift bağ yapar. Sağ tık (x) ucunu açar, çift bağı teke düşürür veya tek bağı siler.
4. Molekülün tamamını sentez çemberine yerleştirip mevcut sentez düğmesini kullanın. Eksik/yanlış kurallar defterin altında bildirilir; başarısız girişler tüketilmez.
5. Oluşan ara bileşeni defterin görünür sayfasına geri sürükleyin. Sonraki aşamada yeniden tahtaya taşıyın. Silinen veya kaybolan tamamlanmış ürünler kitabın Sentez Arşivi bölümünden tekrar alınabilir; henüz tamamlanmamış aşamaların ürünleri burada bulunmaz. Beşinci aşama zinciri tamamlar.

## Veri ve kurulum

Sahnede NotebookUI varsa ekran görünümü ve görev üreticisi otomatik kurulur. Yeni prefab veya sahne referansı bağlama zorunluluğu yoktur. Mevcut `NewNotebookData.asset`, `Scriptables/Formulas` altındaki yedi kurgusal modülü ve bir bozuk modülü referanslar; bu referanslar içeriklerin build'e dahil edilmesini de sağlar. A/B/C deneme assetleri korunmuştur.

Özel içerik için NotebookData.synthesisCatalog ve ProceduralSynthesisGenerator.formulaCatalog/outputCatalog kullanılır. FormulaItemSO üzerindeki kapasite, başlangıç kapalı/kararsız uç indeksleri, sketchSprites ve prefab düğüm tipleri üretimi belirler. Kitabın synthesisPages alanı basılı açıklamalar ve özel bloklar ekler. İsteğe bağlı QuestBlueprintSO ve RecipeSO.rules elle yazılan grafikleri destekler. Create menüleri Senzora altındadır.

`ChalkboardManager.useProceduralQuests` yeni sistemin sahne anahtarıdır. Prosedürel koşu etkin olduğunda klasik bir tarif aktif görevin kurallarını atlayamaz. Mevcut eski kayıtlar yeni alanlar eksikse boş defterle ve ilk açılışta yeni koşuyla devam eder.

Kayıt; beş hedefi, seed'i, aşamayı, defter sayfalarını, blokları, çizgileri ve tahtanın tüm parça/uç/bağ durumunu içerir. Çıktıyı henüz deftere taşımadan kayıt almak da desteklenir. Runtime SO kimlikleri tahta yüklenmeden önce oluşturulur.

## Doğrulama

Unity menüsü: **Senzora > Doğrulama > Tüm Sentez ve Çalışma Alanı** (`SenzoraWorkspaceValidation.RunAll`). Edit modunda çalıştırın. Açık sahneye kalıcı test nesnesi eklemez.

Kapsam: 64 seed / 320 aşama gerçek prefab çözülebilirliği, 20'den fazla farklı şema, deterministik tekrar, grafik/kural olumsuz senaryoları, kapasite, döngü koparma, çift bağ, kapalı uç, döndürülmüş tahta kayıt dönüşümü, bağımsız defter kopyaları, JSON, uzun çizgi ve silgi matematiği, varsayılan UI kartları, yalnız tamamlanmış ürünlerin arşivden geri alınması ve ölüm ekranında çalışma alanının kapanması.

Bu oturumda runtime ve Editor doğrulama kodları mevcut Unity DLL'lerine karşı derlendi. Gerçek grafik doğrulayıcısının 16 bağımsız kontrolü geçti. Üretici kaynak kodunun modellenmiş prefablarla çalıştırıldığı 128 seed / 640 aşamalık harness 7.963 kontrolü geçti; bu sonuç Unity Play Mode doğrulaması yerine geçmez. Gerçek Unity menü testleri, açık editör proje kilidini tuttuğu için bu oturumda çalıştırılamadı. Görsel ve fare etkileşimlerinin son kontrolü editörde yapılmalıdır.
