# Sekmeli Ayarlar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Onaylanan dört sekmeli ayarlar menüsünü gerçek oyun tüketicileriyle, kayıt ve güvenli ekran denemesiyle teslim etmek.

**Architecture:** SettingsManager mevcut kayıtların sahibi olmaya devam eder. SettingsData doğrulama/migrasyon ve ekran deneme işlemleri bağımsız sınanabilir; ortak uGUI view mevcut UI malzemeleriyle kurulur. GameInput override altyapısı yeniden kullanılır. Editor kurulum aracı ortak prefabı ve sahne referanslarını hazırlar.

**Tech Stack:** Unity 6000.3.10f1, C#, HDRP, TMP/uGUI, Input System, mevcut XLSX içerik importer ve artifact-tool.

**Spec:** docs/superpowers/specs/2026-10-29-settings-menu-design.md

## Global Constraints

- Mevcut font/görseller ve TR/EN Excel ana kaynak korunur. Yeni paket yok.
- Oyun ve ayar dosyalarının eski değerleri migrate edilir; kirli sahneler sessizce kaydedilmez.
- Ekran denemesi 15 saniye unscaled time; yalnız doğrulanmış ekran ayarı kaydedilir.
- Fare 0.1–5.0, gamepad 0.1–5.0, deadzone 0.05–0.40, pozlama -2…+2; altyazı ölçekleri 0.85/1/1.25.
- Otomatik commit/push yok. Mevcut SNZ-003 çalışma alanı kullanılır; yeni worktree önceki uygulanmış, commit edilmemiş altyapıyı taşımayacağından mevcut checkout tercih edilir.

## Review Focus

- Eski dosyada eksik bool/float alanları varsayılan değerini korumalı; bozuk kayıt çökertmemeli (Task 1).
- Bekleyen ekran denemesinde başka ayarı kaydetmek denenmiş ekranı kalıcılaştırmamalı (Task 1/4).
- Dil değişiminde açık dialog callback/odak korunmalı, yazılar yenilenmeli (Task 3/4).
- Rebind sırasında Pause ve gameplay girişleri sızmamalı; shared context tuşları sessizce silinmemeli (Task 3/4).
- Sahne değişimi veya kalite seçimi, kullanıcı HDRP/altyazı/gamepad tercihini kaybetmemeli (Task 2/4).

## Files and interfaces

- `Assets/Scripts/Managers/SettingsData.cs`: SettingsData, DisplaySettings, doğrulama ve eski kayıt migrasyonu; `Parse(string, Resolution[])`, `Normalize(int)`, `Copy()`.
- `Assets/Scripts/Managers/SettingsManager.cs`: mevcut public setter uyumluluğu, `Changed`, `SaveFailed`, `ChangedAndSave()`, `BeginDisplayTest(DisplaySettings)`, `ConfirmDisplayTest()`, `RevertDisplayTest()`, `DisplayTestRemaining`, `ResetSection(string)`; güvenli kayıt ve runtime tüketiciler.
- `Assets/Scripts/UI/SettingsMenuView.cs`: ortak sekmeli view, modal, odak/kaydırma, binding capture, ekran taslak değerleri.
- `Assets/Scripts/UI/SettingsUIManager.cs`: eski sahne ve test API'sinin geriye uyumlu giriş noktası, ortak view'a yönlendirme.
- `Assets/Scripts/UI/SettingsMenuStyle.cs`: mevcut malzeme referansları, satır/label/control üretimi; runtime UI'nın tek stil kaynağı.
- `Assets/Scripts/Player/GameInput.cs`: binding list/context/policy erişimi, capture sırasında gameplay okumasını askıya alma, gamepad deadzone.
- `Assets/Scripts/Player/CharacterController.cs`: ayrı fare/gamepad hassasiyeti/tersY ve headbob ölçeği.
- `Assets/Scripts/Managers/GlobalSubtitleManager.cs`: boyut/arka plan ve mevcut segment görünürlüğü.
- `Assets/Scripts/NewEventSystem/Actions/DialogAction.cs`: dinamik konuşma kaynağını gerçek Voice grubuna bağlama.
- `Assets/Scripts/Editor/SettingsMenuSetup.cs`: stil asset/prefab oluşturma, bütün mevcut panel ve mikser/ses referanslarını güvenli bir defalık hazırlama.
- `Assets/Scripts/Editor/SettingsMenuChecks.cs`, `SettingsMenuPlayChecks.cs`: native Editor ve gerçek Play doğrulaması, Temp sonuçları.
- `Content/GameContent.xlsx`, üretilen JSON/stamp ve `docs/SettingsMenu.md`: yeni ayar metinleri ve kullanım.

### Task 1: Kayıt ve ekran deneme işlemi

**Interfaces:** Yukarıdaki SettingsData ve SettingsManager imzaları; eski public setter'lar korunur.

- [x] Reflection tabanlı Editor testi: yeni alanlar, eski JSON varsayılanları, aralık/NaN, bozuk JSON ve pending display save assert'leri ekle; mevcut kodda FAIL gözle.
- [x] SettingsData migrasyon/doğrulama ve SettingsManager kayıt/ekran transaction uygulaması. JSON önce default nesneye FromJsonOverwrite ile yüklenir; index fallback bir defa çözülür.
- [x] Aynı test PASS: özgün kullanıcı kaydına dokunmayan izole path kullan; disk yazma hatasını kontrollü sınama.

### Task 2: Gerçek tüketiciler

**Interfaces:** `SettingsManager.Changed`, `Current`, `RouteVoice(AudioSource)` ve GameInput kontrol ayarları.

- [x] Ses/Volume/altyazı ve giriş ayrımı gerçek tüketici assertion'ları ekle, FAIL'i gözle.
- [x] Voice mikser grubu, runtime Volume, hassasiyet/invert/deadzone/headbob, altyazı ölçek/zemin/segment yenilemesini bağla. Kalite/sahne sonrası tekrar uygula.
- [x] Unity Editor assertion'ları ve derleme PASS; Play kısmı Task 4'te gerçek sahnede doğrulanır.

### Task 3: Ortak dört sekmeli UI ve içerik

**Interfaces:** SettingsMenuView.ShowSection(string), RequestClose(Action), SettingsMenuStyle.Create…; GameInput BindingPolicy/context ve mevcut RebindBinding/ResetBinding.

- [x] İki gerçek panelde dört sekme ve TR/EN etiket/odak/taşma kontrollerini ekle; eski UI'da FAIL gözle.
- [x] Onaylanan mevcut sprite/font stiliyle view, modal ve binding capture'ı kur. Ekran seçimlerini birlikte uygula; geri/sekme değişiminde bekleyen seçimleri sor.
- [x] Editor aracıyla ortak prefabı ve mevcut sahneleri bağla; kirli sahnelerde işlemi durdur. Sesleri gerçek türlerine göre yönlendir; isim tahminiyle diyalog sınıflandırma yok.
- [x] Yeni TR/EN metinleri artifact-tool ile mevcut Excel'in Metinler sekmesine ekle, mevcut 6 tab ve satırları koru. Import, workbook ve güncel çeviri kontrolleri PASS.
- [x] LocalizationPresentationChecks yeni view'ın gerçek satırlarına uyarlanır; font/hiza/overflow kapsamı korunur.

### Task 4: Uçtan uca doğrulama ve teslim

**Interfaces:** Task 1–3 public API'si ve gerçek build sahnelerindeki view/manager referansları.

- [x] Gerçek Play checks: pause içinde timeout+revert+confirm, oyun girdisine sızmayan rebind, iptal/timeout/çatışma; sayfa kapama ve kayıt roundtrip.
- [x] Ses dört kanal; Volume kalite/sahne sonrası; mouse/gamepad farklılığı; altyazı ölçek/zemin, geçerli segment ve zorunlu tutorial davranışı PASS.
- [x] TR/EN bütün sekme/dialoglar 720p/1080p/1440p/ultrawide native render; görünür screenshot'ları incele.
- [x] `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity quiet -p:RunAnalyzers=false -m:1 -clp:ErrorsOnly`; içerik/input/tutorial mevcut regresyonları ve native ayarlar testleri PASS.
- [x] Fresh reviewer'a sadece bu işin dosya/karar kapsamını ve spec/plan/ledger'ı ver; önemli bulguları testle düzelt.
- [x] `docs/SettingsMenu.md` kullanım/ayar listesi/gerçek gamepad sınırı; sonuçları ve kalan maddi sınırlamaları bildir. Commit/push yapma.

Plan self-review: Spec'in dört sekmesi, kayıt/geri alma, tüketiciler, mevcut panel/font regressions ve Excel güncellemesi bu görevlerde kapsanıyor. Onay uygulamayı da kapsıyor; ek plan onayı beklenmeden yürütülür.
