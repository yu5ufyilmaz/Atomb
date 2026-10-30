# scn_LaboratorySynthesis incelemesi — 30 Ekim 2026

Amaç: sahnenin performans risklerini, eski/kullanılmayan yapılarını ve rahat çalışılacak bir hierarchy için yapılacak işleri belirlemek. Bu çalışma rapordur; sahne, prefab, SO ve üretim kodları değiştirilmedi. Mevcut kaydedilmemiş Editör durumu incelenmedi.

Güncelleme: Kullanıcı onayıyla uygulanan ilk etabın sonuçları ve açık bulgular [temizlik raporunda](LaboratorySynthesisCleanup-2026-10-30.md). Aşağıdaki envanter ilk inceleme durumunu kaydeder.

Önerilen başlangıç: önce bozuk referanslar ve manager yaşam döngüsü, sonra materyal sahipliği, ardından doğrulanmış fazlalıklar ve hierarchy düzeni. Kapalı nesnelerin tamamını silmek veya yeni bir manager mimarisi kurmak gerekmiyor.

## Kapsam ve kanıtın sınırı

- Diskteki `Assets/Scenes/scn_LaboratorySynthesis.unity`, bağlı prefablar, özel C# kodları, script/SO GUID referansları, diğer sahneler ve Build Settings incelendi.
- GUID taraması sahne/prefab/SO/materyal/animasyon/controller/Timeline/lighting/meta dosyalarını kapsar. C# tip kullanımları, dinamik `AddComponent` yolları ve `Resources.Load` girişleri ayrıca değerlendirildi.
- Projede Unity `6000.3.10f1` kullanılıyor. İlk incelemede canlı Unity durumuna erişilmedi; yeni Play, derleme, CPU/GPU Profiler veya Memory Profiler ölçümü yapılmadı. Sonraki uygulama etabında açık Editöre bağlanıldı ve Play/derleme kontrolleri çalıştırıldı; Profiler ölçümü henüz yapılmadı. Aşağıdaki performans maddeleri, kodda doğrulanan davranışlar ve ölçüm bekleyen risklerdir; FPS kazancı değildir.
- Prefabların tüm örneklerini Unity üzerinden açıp sayan bir çalışma zamanı envanteri üretilmedi. Aşağıdaki sayılar, sahnede açık olarak saklanan nesne/bileşen kayıtlarıdır; `stripped` prefab kayıtları ve çalışma anında üretilen nesneler ayrıca dikkate alınmalıdır.
- `docs/BookPerformanceAudit.md` içindeki 28 Ekim ölçümleri tarihsel kanıttır. O rapordaki iyileştirilmiş sayfa geometrisi önbelleği güncel kodda var; eski 500–700 ms kitap sorununu tekrar güncel sorun olarak işaretlemiyorum.
- Çalışma kopyasında inceleme öncesinden çok sayıda değişiklik vardı. Git temizliği, geri alma ve silme yapılmadı.

## Mevcut durum

| Kontrol | Diskteki bulgu |
|---|---|
| Açık GameObject kayıtları | 3.047; 1.917 etkin, 1.128 etkisiz, 2 prefab üst nesnesi nedeniyle çevrimdışı etkinliği kesinleştirilemeyen |
| PrefabInstance | 40; bunların 35'i `Assets/Models/lambdeneme.prefab` |
| Hierarchy kökü | SceneRoots listesinde 22 giriş; 6'sı aynı isimli katalog nesnesi |
| Kitaplar | 53 `InteractableBook`; 47 etkin araştırma kitabı, 6 etkisiz eski örnek |
| Araştırma kitap kimliği/yüzeyi | 47 `PuzzleBookIdentity`, 47 `BookPageBlockSurface` |
| Etkin kitap animasyonları | 47 etkin Animator ve 47 etkin SkinnedMeshRenderer |
| Araştırma içeriği | `ResearchPuzzleTrial.books` 47 kitap; `simultaneousPuzzles` = `BlindLaboratory/ArchiveSet.asset` |
| Eski tahta örnekleri | 6 etkisiz `DraggableFormula` kökü |
| Katalog | 6 etkin `FormulaCatalog`; 3'ü boş, 3'ünde aynı `SignalPartTemplate.asset` |
| Eski ayar arayüzü | 2 panel kökü ve toplam 169 açık kayıtlı eski alt nesne; ayrıca 2 yeni SettingsMenu prefab örneği |
| Yerel fileID bağlantıları | Sahne içi `{fileID: ...}` taramasında çözülemeyen yerel bağlantı bulunmadı |
| Kaybolmuş dış GUID'ler | 2 script, 1 occlusion verisi, 1 eski custom reflection referansı |
| Build başlangıcı | Build Settings `scn_labratory` kullanıyor; `scn_LaboratorySynthesis` listede yok |
| Kayıt davranışı | `MANAGERS/SaveManager.sessionOnly = 1` |

1.128 etkisiz nesne, 1.128 gereksiz nesne demek değildir. Bu sayı kapalı UI'ları, karakter modellerini, sahne akışında açılan öğeleri ve eski örnekleri birlikte içeriyor. Benzer şekilde bir scriptin sahnede GUID referansının bulunmaması, dinamik kullanılan bir sınıfın gereksiz olduğunu göstermez.

## Öncelikli bulgular ve yapılacak işler

P1: ilk ele alınacak somut sorun. P2: temizlik veya performans riski. P3: profil ölçümüne/ilerideki entegrasyona göre ele alınacak iş.

| Öncelik / ID | Bulgu ve kanıt | Yapılacak iş / kabul ölçütü |
|---|---|---|
| P1 / L1 | `GameManager`, `GlobalSubtitleManager`, `DebugMenuManager`, `SettingsManager`, `MANAGERS` altında çocukken kendilerine `DontDestroyOnLoad(gameObject)` çağırıyor. Kod konumları sırasıyla `GameManager.cs:41`, `GlobalSubtitleManager.cs:107`, `DebugMenuManager.cs:26`, `SettingsManager.cs:53`. | Kalıcı kalması gerekenleri sahne kökü yap; sahneye ait kalacaklardan kalıcılık çağrısını çıkar. Önce hangilerinin gerçekten sahneler arası yaşayacağını belirle. Yeni bir üst klasör altında çocuk yapmak aynı sorunu sürdürür. Sahne değiştirip geri dönünce tek örnek ve geçerli UI/oyuncu referansı doğrulanmalı. |
| P1 / R1 | Sahne satır 12'deki `m_OcclusionCullingData` GUID'si `1733eafaf8fd3c045ae46d9009bc85be`, Assets/Packages/PackageCache içinde bulunmuyor. | Çalışma sahnesinin occlusion verisini Unity'de kontrol et; gerekiyorsa yeniden bake et. Camera occlusion seçeneğini ve Profiler'da görünmeyen odaların çizimini karşılaştır. Eksik GUID somut; mevcut FPS etkisi ölçülmedi. |
| P1 / M1 | `InteractableBook.Awake` sayfa/çevirme materyali kopyalıyor (`:196`, `:221`); `Start` her alt renderer'ın materyallerini ve ek highlight materyallerini üretiyor (`:278–308`). `ResearchPuzzleTrial.Install` 47 kitap için yeniden `renderer.materials` erişimi yapıyor (`:149`). Bu, kitap daha açılmadan çalışan bir yol. | Önce hangi materyalin kime ait olduğunu netleştir. Gereksiz highlight kopyalarını kaldır, ihtiyaç duyulan hazırlığı ilk kullanımda yapmayı ölç. Kapağın/sayfanın değişmesi gereken gerçek örnek materyallerini koru. Başlangıç süresi, materyal sayısı ve bellek önce/sonra ölçülmeli. |
| P1 / M2 | `InteractableBook` içinde `new Material` ve `new Mesh` var; iki partial dosyada bunları temizleyen `OnDestroy` yok. `UniversalHighlighter` da kopya materyaller oluşturuyor (`:62–70`) ve temizlemiyor. `BookPageBlockSurface.OnDestroy` kendi mesh'ini temizliyor; bu, kitap sınıfının ayrı kaynaklarını temizlemiyor. | Yalnız bu bileşenlerin ürettiği materyal/mesh örneklerini yaşam sonunda temizle. Ortak asset materyallerini silme. Kitap aç/kapat ve sahne yükle/boşalt döngülerinde Memory Profiler materyal/mesh sayısı birikmemeli. |
| P1 / R2 | `BOOKS/BookModel (2)` üzerinde `5fea29bb7c508c244a1f805a5fd3fc4d` script GUID'si yok (`scn_LaboratorySynthesis.unity:54257`). `lambdeneme (35)` prefab örneğine eklenen kapalı bileşende `102500beab4bda54c809590301d0019c` yok (`:117244`, component fileID `1511481915`). | Unity Inspector'da bu iki konumu doğrula; eski bileşense kaldır, gerekli bileşense doğru scripti bağla. Prefabın paylaşılan kaynağını ve yalnız bu örneğe ait override'ı ayırt et. Tüm loaded prefab/FBX içerikleri için nihai missing-script sayımı Unity'de yapılmalı. |
| P1 / T1 | `LaboratorySynthesisChecks.ValidateScene` hâlâ `trial.books.Distinct().Count() == 4` bekliyor (`:390`) ve orijinal sahneyle tam hierarchy yolu karşılaştırıyor. Güncel sahnede 47 kitap var. `LaboratorySynthesisSetup` da eski dört kitaplı kopyalama akışını kuruyor. | Eski migration araçlarını güncel üretim doğrulamalarından ayır. Dört kitap kabulünü güncellemeden `Validate Scene Copy` başarısını bekleme; bu kontrol bugünkü içerikte zaten uyuşmazlık üretiyor. Güncel 47 kitap / 3 hedef için `LaboratoryBlindPackChecks` ve ilgili kitap kontrollerini temel al. Eski araçları otomatik yeniden kurulum için kullanma. |
| P2 / C1 | Aynı isimli 6 `Prosedürel Sentez Görevleri` kökü aynı GUID ile artık `FormulaCatalog` yükler. Kod duplicate durumda yalnız bileşeni yok ediyor (`FormulaCatalog.cs:12`); boş GameObject kalıyor. Dış oyun bileşeninden bu 6 nesneye yerel referans bulunmadı; yalnız SceneRoots girişleri var. | Bir katalog bırak, diğer 5 kökü kaldır. Kalanı `FormulaCatalog` olarak adlandır. Yeni araştırma oturumu `ReplaceCatalog` ile içeriğini zaten dolduruyor. Eski isim/`m_EditorClassIdentifier` tek başına eski kodun çalıştığını göstermez; script GUID'si esas alınmalı. |
| P2 / C2 | Yeni `SettingsMenu` prefabları zaten sahnede. `SettingsUIManager.EnsureLanguageSelector` diğer çocukları her seferinde kapatıyor (`:23`). Eski iki panelin 169 alt nesnesi hâlâ dosyada ve adapter alanları onlara bağlı. Panel altlarından dış oyun bileşenlerine taşınan bir yerel referans bulunmadı. | İki panel kökünü, adapter'ı ve yeni prefabları koru. Eski 169 alt nesneyi kaldırıp adapter'ın eski slider/toggle alanlarını bu sahnede temizle. Ortak adapter alanlarını diğer sahneler geçmeden koddan topluca silme. Başlangıç ve Pause ayarlarını, dil/tuş atamasını doğrula. Beklenen kazanç hierarchy/bellek/yükleme sadeleşmesi; kapalı UI'dan büyük FPS kazancı varsayma. |
| P2 / C3 | 5 script dosyasına Assets içindeki taranan serialized dosyalarda GUID referansı, diğer proje C# dosyalarında tip kullanımı veya araç/Content girişlerinde dinamik kullanım bulunmadı. | Aşağıdaki adayları `.meta`larıyla kaldırma listesine al. Son karardan önce Unity compile/asset dependency kontrolü yap. Toplam 279 C# satırı. |
| P2 / C4 | 4 `PasswordData` SO'suna serialized GUID referansı bulunmadı; Resources altında değiller. | Aşağıdaki 4 SO'yu `.meta`larıyla temizlik listesine al. Bağlı sayfa dokularını otomatik silme; onların başka kullanıcıları olabilir. |
| P2 / C5 | `BOARD` altındaki 6 eski formül kökü kapalı. Güncel araştırma parçaları `SignalPartTemplate` üzerinden çalışma anında geliyor. Eski formüllerin birbirleriyle düğüm bağlantıları ve bazı liste referansları var. | Eski örnekleri grup olarak kaldırmadan önce `ReactionZone.currentItems` ve düğüm bağlantılarını temizle. Aktif `P_SignalPart.prefab`, `FormulaNode`, `DraggableFormula`, `FormulaCatalog` kodlarını koru. |
| P2 / C6 | 6 kapalı kitap araştırma dağıtımının 47 kitabına dahil değil. Bazıları hâlâ `PasswordManager.allBooksInLevel` gibi listelerde. | Önce bu sahnedeki eski şifre/tutorial olaylarının bu kitaplara ihtiyacını çıkar; sonra gerekmeyen kapalı örnekleri ve liste girişlerini birlikte kaldır. Sadece `SetActive(false)` gördüğümüz için dosya/prefab silme kararı verilmemeli. |
| P2 / H1 | 22 kökte sistemler, UI, kameralar, ses ve eski kataloglar karışık. `Office_Room` dalında 1.164 açık alt nesne var. `ReallyLastBook3 (n)` isimleri farklı raflarda tekrar ediyor; `NewProbs`/`WorkTableTestAssets` işlevi anlatmıyor. | Aşağıdaki hedef hierarchy'ye küçük adımlarla geç. Kitap adı için `PuzzleBookIdentity.bookID` değerini koru; okunur başlık ve raf kimliği kullan. Ortak büyük bir prefabı zorunlu kılmadan oda ve etkileşim birimleriyle ilerle. |
| P2 / H2 | `LaboratorySynthesisSetup.DeskBookPath` mevcut tam yolu sabit metinle kullanıyor (`:14`). Başka Editor kurulum/doğrulama araçları da mevcut isim ve yolları arıyor. `EventLogicController` tutorial kapsamını `IsChildOf(tutorialEventsParent)` ile belirliyor (`:44`). | Taşıma/yeniden adlandırmadan önce path kullanan araçları ve tutorial olay kapsamını güncelle. EVENTS içindeki olayları oda klasörlerine rastgele dağıtma. Prefab instance bağlantılarını koru. |
| P2 / H3 | `PasswordManager` kayıt yüklerken nesne adını karşılaştırıyor (`:188`, `:194`, `:202`); birçok kitap adı tekrarlı. Araştırma sistemi ayrıca sabit `bookID` kullanıyor. | Araştırma kimliklerini değiştirme. Eski şifre sistemi bu sahnede tutulacaksa yeniden adlandırmayı eski kayıtlarla birlikte değerlendir; gerekirse o sistem için de sabit kimlik kullan. Tüm oyuna şimdiden yeni bir registry eklemek gerekmiyor. |
| P2 / H4 | İçerik çalışma kitabı bağlantılarını `GlobalObjectId` ile tutuyor (`ContentWorkbookProject.cs:123`); hierarchy yolunu ayrıca okunur sütuna yazıyor. | Objeleri/bileşenleri silip yeniden yaratmak yerine mevcutlarını taşı. Temizlikten sonra içerik tablosunun bağlantı envanterini yenile ve silinen hedef bildirimlerini kontrol et; yalnız hierarchy adı değişti diye diyalog bağlantılarını yeniden yaratma. |
| P2 / P1 | 47 kitap Animator'ı etkin. 95 kayıtlı Animator'ın 92'si `m_CullingMode = 0`. Bu, kapalı kitapların sürekli animasyon güncelleme olasılığını artırıyor. | Profiler'da boşta Animation maliyetini ölç. Kitap kapalı ve hareket etmiyorken Animator'ı durdurma/culling seçeneğini küçük bir kitap grubunda dene. İlk açılış pozu, raf çekme, ileri/geri çevirme ve kapanış bozulmamalı. Sayısal maliyet henüz ölçülmedi. |
| P2 / P2 | `ChalkboardFreehandDrawing.Update` tahta açıkken her kare `GetComponentsInChildren<DraggableFormula>()` ile dizi üretiyor (`:43`); tıklamada `Physics.RaycastAll` kullanıyor (`:54`). | Aktif sürüklemeyi mevcut durumdan oku veya küçük bir parça listesi tut. Profiler tahsis gösteriyorsa tıklama raycast yolunu tamponlu hale getir. Büyük genel bir entity sistemi gerekmez; tahtada çok az parça varsa önceliği düşüktür. |
| P3 / P3 | `PressureSystemManager.UpdateHUD` her kare yüzde metnini formatlıyor ve `.text` yazıyor (`:199–202`). HUD ve defter/menüler aynı ana Canvas ağacını paylaşıyor. | Görünen tam sayı değişince metni güncelle; Profiler Canvas rebuild gösterirse dinamik HUD ve belge/menüleri uygun alt Canvas'larla ayır. Bütün UI için tek seferde yeni mimari kurma. |
| P3 / P4 | Global Volume'da Fog, DoF, Bloom ve SSAO gibi efektler var. Kalite seviyelerinin üçünde de mip streaming kapalı. 35 lamba prefab override'ı gölge ayarı taşıyor. | Development Build'de GPU/frame debugger ve bellek ölçümüyle karar ver. Fog/SSAO/DoF ve görünmeyen oda gölgelerini ayrı ayrı karşılaştır. Sahnedeki ışık sayısı, açık lamba sayısı veya dosya boyutu tek başına darboğaz kanıtı değildir. |
| P3 / C7 | `Assets/_Recovery` ve birden fazla tarihsel laboratuvar/test sahnesi var; bazıları diğer sistemlerin gerçek referanslarını taşıyor. | Kurtarma kopyalarını doğrulanmış yedekten sonra Assets dışındaki arşive taşıma listesine al. Build'deki `scn_labratory` ve kullanılan test sahnelerini bu incelemeye dayanarak silme. |

`DontDestroyOnLoad` yalnız kök GameObject'lerde çalışır. [Unity yaşam döngüsü belgesi](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/object/dontdestroyonload) bu koşulu açıklar. Materyal örneklerinin temizlenmesi çağıranın sorumluluğudur. [Unity Renderer.materials belgesi](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/renderer/materials) bu davranışı doğrular.

HDRP kullandığımız için bütün materyalleri düşünmeden `MaterialPropertyBlock` ile değiştirmek de doğru bir genel çözüm değil: SRP Batcher uyumluluğu ayrıca değerlendirilmelidir. [Unity batching belgesi](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/draw-call-batching/properties). UI ayırımı ve raycast hedeflerini azaltma önerileri de önce gerçek UI maliyetiyle doğrulanmalı. [Unity UI optimizasyon rehberi](https://unity.com/how-to/unity-ui-optimization-tips).

### Kesinleştirilmiş dosya adayları

Buradaki “referans yok”, yapılan statik taramanın sonucudur. Unity asset bağımlılık kontrolü ve derleme silme adımının son kontrolü olmalıdır; burada hiçbir dosya silinmedi.

| Dosya | Bulgudan çıkan karar |
|---|---|
| `Assets/Scripts/Managers/PlayerReferences.cs` | Kullanılmayan referans merkezi; 54 satır. Başka kod bu sınıfı kullanmıyor. |
| `Assets/Scripts/UI/DesktopMenuInteract.cs` | Sahne/prefab bağlantısı ve başka kod çağrısı olmayan eski masa menüsü; 46 satır. |
| `Assets/Scripts/Managers/CreditsManager.cs` | Sahne bağlantısı bulunmayan eski otomatik menü dönüşü; 27 satır. Credits sahnesinin kendisi bu nedenle silinmez. |
| `Assets/Scripts/NewEventSystem/Actions/ObjectiveAction.cs` | Olaylara bağlanmamış action; 40 satır. |
| `Assets/Scripts/Localization/LocalizedUIVariant.cs` | Kullanım örneği bulunmayan varyant bileşeni; 112 satır. Localization sisteminin geri kalanı kullanılıyor. |
| `Assets/Scripts/Scriptables/Passwords/PasswordData 2.asset` | Gelen serialized referans yok. |
| `Assets/Scripts/Scriptables/Passwords/PasswordData 3.asset` | Gelen serialized referans yok. |
| `Assets/Scripts/Scriptables/Passwords/PasswordData 4.asset` | Gelen serialized referans yok. |
| `Assets/Scripts/Scriptables/Passwords/PasswordData 5.asset` | Gelen serialized referans yok. |

### Eski göründüğü halde korunması gerekenler

- `SignalPartTemplate.asset`, `P_SignalPart.prefab`, `FormulaCatalog`, `FormulaItemSO`, `FormulaNode`, `DraggableFormula`: güncel araştırma/tahta akışı tarafından kullanılıyor.
- `SignalTrialContent`, `QuestBlueprintSO`, kitap sayfa/veri sınıfları ve partial dosyalar: runtime üretiliyor veya C# üzerinden kullanılıyor. Sahnede SO örneği görünmemesi kullanılmadıkları anlamına gelmiyor.
- `NotebookUI`, `NotebookWorkspaceController` ve partial dosyaları: ekran defteri ile eski fiziksel defter adaptörlerinin sorumlulukları karışık olsa da aktif sistemin parçası.
- `TutorialManager`, `PUZZLES/TutorialSequence` ve tutorial olayları: güncel sahnede tutorial etkin. Tarihsel belgelerde kapalı denmesi güncel sahne için ölçüt değil.
- `LeesResistanceController`, Hands, HUD ve Graphic: `LeesEnemyAI.resistance` doğrudan yeni sisteme bağlı. Yeni eklenmiş ve ilk bakışta ayrı duran kök, gereksiz değil.
- `PasswordManager`, `FinalDoorUnlocker`, eski osiloskop/spektrometre: aktif bileşenler ve olay bağlantıları var. Araştırma akışına geçiş tamamlanmadan “kullanılmıyor” diye çıkarılamaz. Son kapı ve eski şifre ilerleyişinin araştırma hedefleriyle nasıl birleşeceği ayrı iş.
- `SaveManager`: `sessionOnly=1` olması manager'ı gereksiz yapmıyor. Mevcut kayıt korumasını yanlışlıkla kaldırmamak gerekir.
- `BookPassword1–10.asset`, `PasswordData.asset`, `PasswordData 1.asset`, `TutorialData.asset`, `NewWordPool.asset`: bu sahnede ve diğer laboratuvar sahnelerinde referansları var. Dört sahipsiz SO ile karıştırılmamalı.
- `SceneIDMap`, `ProbeVolumePerSceneData`, `StaticLightingSky`: HDRP verileri. `SceneIDMap` scripti sahne içi MonoScript ile çözülüyor; eksik script değil.
- 7 HDRP reflection probe'un `m_ProbeSettings.mode=0` ayarı, mevcut paket kodunda **Baked**. Native ReflectionProbe üzerindeki farklı alanlara bakıp “7 probe her kare render alıyor” denmemeli.
- Lamba override'larında `8192` gölge çözünürlüğü değeri var ama 35 örneğin tamamında `m_UseOverride=0`. Bu değeri etkin 8K gölge maliyeti diye raporlamıyorum.
- Editor kurulum ve regresyon kontrolleri: oyuncu build'inin her kare yükü değiller. Aktif test amaçları varken sırf çok dosya oldukları için silinmemeli.

## Hierarchy için önerilen düzen

En küçük uygulanabilir düzen: gerçekten kalıcı olacak manager'lar ayrı kök kalır; sahneye ait sistemler, oyuncu, odalar, UI, ses, olaylar ve ışık verileri okunur gruplara ayrılır. Bu bir öneridir; henüz sahneye uygulanmadı.

```text
scn_LaboratorySynthesis
├─ 00_GameManager                  # Kalıcı olacaksa doğrudan sahne kökü
├─ 01_SettingsManager              # Kalıcı olacaksa doğrudan sahne kökü
├─ 02_SubtitleManager              # Kalıcı olacaksa doğrudan sahne kökü
├─ 03_DebugMenuManager             # Yalnız geliştirme amacıyla tutulacaksa
├─ 10_SceneSystems
│  ├─ ResearchSession              # ResearchPuzzleTrial + SignalExperiment + scene adapter
│  ├─ FormulaCatalog               # Tek örnek
│  ├─ SaveManager                  # Şimdilik oturumluk koruma
│  ├─ PressureSystem
│  ├─ ChalkboardManager
│  ├─ TutorialManager
│  └─ SceneFlow                    # Giriş, final, mevcut menü akışları
├─ 20_Player                       # Mevcut rig ve kamera alt düzeni korunur
├─ 30_World
│  ├─ Office
│  │  ├─ Architecture
│  │  ├─ Props
│  │  └─ Interactables
│  │     ├─ Books / Shelf_01…06
│  │     ├─ ResearchBoard
│  │     └─ Desk
│  ├─ ChemistryLab
│  ├─ ParticleAccelerator
│  ├─ ValveRoom
│  ├─ FuseRoom
│  ├─ Corridor
│  ├─ PersonnelSpace
│  └─ FinalRoom
├─ 40_Enemies                      # Lees + Resistance, Guderian, Adam
├─ 50_SequenceCameras              # Başlangıç/final kameraları
├─ 60_UI                           # Mevcut Canvas; gerekirse ölçüme göre alt Canvas
├─ 70_Audio                        # Müzik/ortam; fiziksel ses kaynakları objelerinde kalır
├─ 80_Events                       # Tutorial kapsamı ve referansları korunur
└─ 90_Lighting                     # Volume, probe ve HDRP veri nesneleri
```

Bu şema bütün nesneleri taşımak zorunda olduğumuz anlamına gelmez. Kamera/kemik bağları, makine çalışma pozları, kitap parent'ları ve sesin fiziksel konumu mevcut davranışın parçasıdır. Alt yapılar yerinde kalırken üst gruplar düzenlenebilir. Kalıcılık istenmeyen manager'lar `10_SceneSystems` altına alınmadan önce `DontDestroyOnLoad` çağrıları kaldırılmalıdır. Alternatif olarak ileride tek kalıcı bootstrap kökü düşünülebilir; bu inceleme için yeni bir bootstrap sistemi gerekmiyor.

### Mevcut → hedef eşlemesi

| Mevcut | Önerilen işlem |
|---|---|
| 6 × `Prosedürel Sentez Görevleri` | 5 kökü kaldır; tek `FormulaCatalog` bırak |
| `Laboratory Synthesis` | `10_SceneSystems/ResearchSession` |
| `MANAGERS` | Kalıcı köklerle sahne sistemlerini ayır; bütün grubu kalıcı yapma |
| `ENVIRONMENT/Office_Room` vb. | `30_World/Office` vb.; oda içindeki mevcut çalışan düzeni kademeli taşı |
| `ENVIRONMENT/WorkTableTestAssets` | Aktif masa içeriğine göre `Desk` olarak adlandır; 92 nesneyi topluca test artığı sayma |
| `NewProbs`, `InteractableProbs`, `InteractablesProbs` | Tek yazım standardı: `Props`, `Interactables` |
| `ReallyLastBook3 (n)` | Raf + sabit bookID ile takip et; görünür başlık rolünü yaz. `bookID` değişmez |
| `P_Book_Puzzle_symbol_01` | Aktif araştırma kitabı; eski isim yüzünden silme. Yeniden adlandırmadan önce Editor sabit yolunu güncelle |
| `LEES / Head Resistance` | Lees'in işlev grubuna al; kamera/rig bağlı elleri taşımadan mevcut transform ilişkisini koru |
| `StartSceneCams`, `FinalSceneCams` | `50_SequenceCameras` altında iki alt grup; Timeline referanslarını doğrula |
| `AmbianceMusic`, manager ses kaynakları | Kullanımına göre `70_Audio`; 3D kaynakları makine/odadan koparma |
| `ENVIRONMENT/LIGHTING`, HDRP kökleri | `90_Lighting`; otomatik üretilen HDRP nesnelerini taşımadan önce paket davranışını kontrol et |

Hierarchy için kurallar: boş taşıyıcıların scale değeri 1; taşıma sırasında world pose korunmalı; prefablar sırf klasörlemek için unpack edilmemeli; oda/etkileşim bazlı mevcut prefab yapıları korunmalı. Görsel gruplama uğruna çalışan animasyon rig'i veya tutorial olay kapsamı bozulmamalı.

## Uygulama sırası ve tamamlanma kontrolü

1. **Mevcut durumu sabitle:** 47 kitap kimliği, 3 hedef, oyuncu/kamera/makine world pozları, aktiflikler ve mevcut bağlantılar için referans envanteri al. Build yönlendirmesi ve `sessionOnly=1` davranışını kaydet.
2. **Referansları düzelt:** iki eski script bağlantısını Unity'de kontrol et; occlusion verisini düzelt/yeniden bake et. Eski custom reflection GUID'sini de temizle veya doğru cubemap'e bağla. `m_DefaultReflectionMode=0` olduğundan bu stale custom alanı tek başına aktif yansıma arızası kanıtı değildir.
3. **Yaşam döngüsünü düzelt:** kalıcı manager'ların kök düzenini ve sahne değişimindeki referans tazelemesini doğrula. Singleton sayısı her hedef sahnede 1 olmalı.
4. **Düşük riskli temizliği yap:** 5 fazla katalog kökü, 5 script, 4 SO; ardından eski ayar alt nesneleri ve bu sahnedeki eski adapter referansları. Her grup ayrı gözden geçirilebilir değişiklik olsun.
5. **Koşullu kalıntıları temizle:** 6 eski formül, 6 kapalı kitap, fiziksel defterin kullanılmayan yer tutucuları, `ENVIRONMENT/Symbols` gibi eski gruplar. Önce olay/liste/düğüm bağlantılarını çöz. Referanssız poz işaretçisi her zaman gereksiz değildir.
6. **Hierarchy'yi kademeli düzenle:** önce üst gruplar, sonra oda içleri, en son isimler. Path kullanan Editor araçlarını aynı değişiklikte güncelle. Ortak script/prefab silmenin diğer laboratuvar sahnelerini etkilemediğini doğrula.
7. **Performans değişikliklerini ölç:** materyal hazırlığı/temizliği, kapalı kitap Animator'ları, UI güncellemesi, tahta taraması; sonra GPU ve texture memory. Önce/sonra aynı build, aynı kalite ve aynı kamera rotası kullanılmalı.

Her temizlik grubundan sonra kabul kontrolleri:

- Unity derlemesi ve yeni missing script / missing reference olmaması.
- Başlangıç menüsü; tutorial **Evet** ve **Hayır** yolları; Pause, dil ve tuş ayarları.
- 47 kitap kimliği ve 3 hedef dağıtımı; kitabı aç/kapat/çevir, notu deftere ve tahtaya sürükle.
- Tahta parçaları, çizim ve ölçüm; doğru hedefin çıktısını Turing'e teslim et, yanlış hedef çıktısının reddini doğrula.
- Lees resistance, oda/kapı etkileşimleri, basınç/şalter ve son sahne olaylarının korunması.
- Scene reload / başka sahneye geçip dönme; kamera pozları, materyal ve manager sayıları.
- Oturumluk kayıt koruması; normal kayıt dosyasının istenmeden değişmemesi.

Güncel akış için projede bulunan girişler: `Senzora/Puzzle Atölyesi/Doğrulama/Üçlü Araştırma Modeli`, `Üçlü Araştırma Canlı`, `Kitaplara Normal Erişim`, `Üçlü Ölçüm Kimliği`; `Senzora/Doğrulama/Öğretici Seçimi - Evet Play / Hayır Play`; `Senzora/LEES Mücadelesi/Ana Oyun Bağlantısını Doğrula`; `Senzora/Book Motion/Optimization Cached Projection Regression`, raf kontrolleri ve canlı kare ölçümleri. Bunlar bu incelemede yeniden çalıştırılmadı. `Lab Migration/Validate Scene Copy` dört kitap varsayımı nedeniyle önce güncellenmeli; aynı grubun diğer kontrolleri de tarihsel akışa ait varsayımlar için gözden geçirilmelidir. Hierarchy değişiminde eski yolları varsayan kontroller de güncellenmelidir.

### Profil ölçümü için kısa senaryo

Development Build'de aynı kalite/çözünürlükte: boşta laboratuvar, raflara bakış ve yürüyüş, ilk kitap açılışı, tekrar açılış ve iki yönde çevirme, defter açık yürüyüş, tahta sürükleme/çizim, Turing okuyucusu, Lees karşılaşması. CPU/GPU frame time, p95 ve en uzun kare, GC tahsisi, materyal/mesh/texture belleği, draw call ve gölge maliyetini kaydet. Mevcut kanıt FPS veya MB tasarrufu söylemek için yeterli değil.

## Sadeliğe özel kısa liste

`ponytail-audit` kapsamı yalnız gereksiz yapı/sadelik; performans ve doğruluk maddeleri yukarıdaki ayrı incelemedir.

- `delete:` Kullanılmayan `LocalizedUIVariant` bileşeni; yerine yapı gerekmiyor. `Assets/Scripts/Localization/LocalizedUIVariant.cs`
- `delete:` Kullanılmayan `PlayerReferences` merkezi; bugün hiçbir çağıranı yok. `Assets/Scripts/Managers/PlayerReferences.cs`
- `delete:` Kullanılmayan masa menüsü giriş yolu. `Assets/Scripts/UI/DesktopMenuInteract.cs`
- `delete:` Bağlanmamış görev action'ı. `Assets/Scripts/NewEventSystem/Actions/ObjectiveAction.cs`
- `delete:` Bağlanmamış eski credits dönüş bileşeni. `Assets/Scripts/Managers/CreditsManager.cs`
- `delete:` 5 duplicate katalog kökü; yerine mevcut tek katalog. `Assets/Scenes/scn_LaboratorySynthesis.unity`
- `delete:` Eski ayar alt nesneleri; yerine sahnede zaten bulunan SettingsMenu prefabı. `Canvas/SettingMenuPanel`, `Canvas/PauseSettingMenuPanel`

Olası net: **279 C# satırı, 0 paket bağımlılığı**; ayrıca 4 sahipsiz SO ve sahne kalıntıları. Silme uygulanmadı. Paketler için kanıtlanmış bir kaldırma önerisi yok; HDRP yanında URP ayarlarının bulunması tek başına bir paketin kullanılmadığını ispatlamıyor.

## Ham inceleme çıktıları

`outputs/laboratory-synthesis-audit/`:

- `scene-inventory.json`: GameObject kimlikleri, bileşenler, etkinlik, prefab listesi ve doğrudan GUID'ler.
- `scene-hierarchy.txt`: okunur tam açık-kayıt hierarchy listesi.
- `scene-details.json`: renderer/Animator/ışık/Canvas/kitap ayrıntıları ve kataloglara gelen referanslar.
- `reference-inventory.json`: özel script ve SO'lara gelen serialized/C# referansları.
- `reference-graph.json`, `guid-map.json`: statik bağımlılık takibi.
- `analyze_scene.py`, `analyze_references.py`, `inspect_details.py`: üretim Assets'i dışında tutulan inceleme yardımcıları.

Referans grafiği GUID tabanlı serialized erişimi gösterir; binary asset bağımlılıklarının, reflection'ın veya runtime üretilen bütün nesnelerin tam Unity AssetDatabase grafiği değildir. Silme kararı için tek başına kullanılmamalıdır.
