# Senzora çalışma alanı ve prosedürel sentez uygulama planı

Amaç: Mevcut kitap, fiziksel defter, etkileşim ve kayıt akışlarını koruyarak sürüklenebilir belge çalışma alanını beş aşamalı grafik bulmacasına bağlamak.

Mimari: Mevcut MonoBehaviour ve SO alanları korunur. Görevler ve kurallar serileştirilebilir veri olarak üretilir; gerçek bağ grafiği bunlara göre doğrulanır. Defterin çalışma alanı görünümü aynı runtime sayfa verisini paylaşır; eldeki fiziksel modelin parenting düzenine müdahale etmez.

## İşler
- [x] Veri: SO genişletmeleri, deterministik beş görev, kurallar/topoloji/kapasite doğrulaması, kayıt.
- [x] Tahta: açık/kapalı uç, tek/çift bağ, grafik bileşenleri, atomik üretim ve deftere transfer.
- [x] UI: başlıktan sürüklenen kitap/defter, özel bloklar, sınırsız sayfa, hedef yığını, kalem ve silgi.
- [x] Entegrasyon: kitap açma/kapama, makine slotu koruması, yeni oyun ve kayıt yükleme.
- [ ] Doğrulama: Unity derleme; deterministik üretim ve negatif kural testleri; kayıt roundtrip ve UI etkileşim kontrolleri.

## Arayüzler
- ProceduralSynthesisGenerator: Instance, ActiveQuest, Run, EnsureRun(), ResolveFormula(id), CompleteActiveQuest().
- SynthesisGraphValidator.Validate(graph,quest,out reason), ValidateRecipe(graph,recipe,out reason).
- ChalkboardManager.SpawnFormula(data,screenPosition), TryGetBoardPoint(screenPosition,out position).
- NotebookUI.SetWorkspaceMode(bool), TryStoreBoardFormula(formula,screenPosition).
- NotebookWorkspaceController.TryDropBlock(block,screenPosition), DeleteBlock(block).

## Kararlar ve inceleme odağı
- İstek doğrudan uygulama yetkisi veriyor; ayrı tasarım onayı beklenmiyor.
- Sahnelerdeki üç test formülü anlamlı zincire yetmiyor. Assetler korunur; mevcut prefabdan uyumlu, kurgusal runtime modüller oluşturulur. Inspector kataloğu bunu değiştirebilir.
- UI üstünden dünya tıklaması, çoklu bağlı molekülün kopyalanması, çözümsüz üretim, eski kayıtta eksik alanlar ve çift bağ kapasitesi özellikle kontrol edilir.
- Önceden değiştirilmiş timeline ve font assetleri korunur. Git iç içe: asıl Unity deposu Atomb/ altındadır.

Doğrulama sonucu: runtime ve Editor C# derlemesi exit 0; gerçek validator 16/16; modellenmiş üretici 7.963 kontrol, 128 tohum, 640 aşama, 123 farklı yapı. Unity native menü testleri açık kullanıcı editörünün proje kilidi nedeniyle çalıştırılamadı. Kullanım ve test menüsü docs/SynthesisWorkspace.md içinde.

Son bağımsız inceleme: silinen ara üründen doğan ilerleme kilidi tamamlanmış ürün arşiviyle giderildi; basınç ölümü sonrası çalışma alanı DeathUIManager içinde kapatıldı. Her iki senaryo için Editor regresyon kontrolleri eklendi; son runtime ve Editor derlemesi exit 0. Native testler için editör menüsü kullanılmalıdır.
