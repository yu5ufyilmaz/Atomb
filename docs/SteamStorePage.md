# Senzora — Steam mağaza sayfası planı

**Tarih:** 1 Kasım 2026 · **Kaynak:** ProjectSettings (Senzora / Modiac / 0.0.6), docs/, en.json altyazıları, Assets/Sprites.

Steamworks'te sayfa "Edit Store Page" altında 10 sekmedir. Aşağıda her sekme sırayla; `[ ]` doldurulacak alan, altında yapıştırılacak metin. ✍️ işaretli yerler senin kararın.

---

## 0. Ön koşullar (sayfadan önce)

- [ ] Steamworks partner hesabı + 100 $ App Credit ödendi, vergi/banka formları tamam (Steam bunları onaylamadan sayfa incelemeye gönderilemez).
- [ ] Partner adı: **Modiac** (ProjectSettings `companyName`). Bu ad Developer/Publisher olarak görünecek.
- [ ] App oluşturuldu, App ID alındı. ✍️ App ID: ______
- [ ] Steam'in "Coming Soon" sayfası için zorunlu minimum: short description, about, 5 ekran görüntüsü, 1 trailer, tüm kapsüller, tag'ler, sistem gereksinimleri, yaş anketi.

---

## 1. Basic Info

- [ ] **App Name:** `Senzora`
- [ ] **Developer:** `Modiac` · **Publisher:** `Modiac`
- [ ] **Release Date:** ✍️ "Coming soon" ya da "To be announced". Tarih yazarsan Steam tarihi gösterir; emin değilsen "Coming soon" seç, sonra değiştirilebilir.
- [ ] **Early Access:** ✍️ Hayır (0.7 build tutorial + tek bölüm; Early Access seçersen ayrıca 6 soruluk EA formu var).
- [ ] **Supported platforms:** Windows (yalnız bu, Unity build hedefi Win64).
- [ ] **Controller support:** ✍️ Oyunda gamepad rebinding ve deadzone ayarı var (settings.gamepad/pad/deadzone). Tam akış gamepad ile test edildiyse "Full", değilse "Partial"; emin değilsen boş bırak.

---

## 2. Description

### Short description (max 300 karakter, EN)
```
Senzora is a first-person horror puzzle game set in a damp basement laboratory. Dig research out of forgotten books, build experiments on the chalkboard, feed codes to a Turing machine, keep the boilers from blowing, and never look at what hunts you.
```

### Short description (TR, Localized Description sekmesine)
```
Senzora, rutubetli bir bodrum laboratuvarında geçen birinci şahıs korku-bulmaca oyunudur. Unutulmuş kitaplardan araştırmayı çıkar, kara tahtada deneyler kur, kodları Turing makinesine gir, kazanları patlatma ve seni avlayana asla bakma.
```

### About the game (EN, Steam BBCode)
```
[h2]Leaving the university was a mistake, Arthur.[/h2]
You buried yourself in a damp basement laboratory to finish the research that cost you everything. Tonight the lights go out, the pipes groan, and a familiar voice inside the walls tells you your notes are scattered through the dusty books on the shelves. Five validated sequences will open the exit. Something else is already inside.

[h2]Research, don't guess[/h2]
[list]
[*][b]Read the books.[/b] Shelves hold catalogues, experiment logs and observations. Drag targets, part diagrams and rules straight off the page into your notebook.
[*][b]Build on the chalkboard.[/b] Lay out parts, draw the bonds, measure the result and compare the trace with the target record. Any layout that produces the same result is accepted.
[*][b]Feed the Turing machine.[/b] Turn a measurement card into a code, pull the lever, and watch the lock on the door react.
[*][b]Keep the lab alive.[/b] Flip the breaker, bleed the boiler valve before the gauge hits red, and tune the oscilloscope.
[/list]

[h2]Survive the night[/h2]
Footsteps in the corridor. A locker to hide in. A presence you must not look at: turn away, hold still, and resist as it drags your head back toward it. Hesitate and you'll be caught.

[h2]Features[/h2]
[list]
[*]Single-player first-person horror with research-driven puzzles
[*]A notebook that remembers everything you found, across every question
[*]Physical chalkboard experiments with real measurement feedback
[*]Pressure, power and stealth systems that interrupt your work
[*]Optional step-by-step tutorial, or jump straight in
[*]English and Turkish interface and subtitles
[/list]
```

### About the game (TR)
```
[h2]Üniversiteden ayrılmak hataydı, Arthur.[/h2]
Sana her şeye mal olan araştırmayı bitirmek için kendini rutubetli bir bodrum laboratuvarına gömdün. Bu gece ışıklar sönüyor, borular inliyor ve duvarların içindeki tanıdık bir ses notlarının raflardaki tozlu kitaplara dağıtıldığını söylüyor. Çıkışı beş doğrulanmış dizi açacak. Başka bir şey ise çoktan içeride.

[h2]Tahmin etme, araştır[/h2]
[list]
[*][b]Kitapları oku.[/b] Raflarda kataloglar, deney kayıtları ve gözlemler var. Hedefleri, parça çizimlerini ve kuralları sayfadan doğrudan defterine sürükle.
[*][b]Kara tahtada kur.[/b] Parçaları yerleştir, bağları çiz, sonucu ölç ve izi hedef kayıtla karşılaştır. Aynı sonucu üreten her düzen kabul edilir.
[*][b]Turing makinesini besle.[/b] Ölçüm kartını koda çevir, kolu çek ve kapıdaki kilidin tepkisini izle.
[*][b]Laboratuvarı ayakta tut.[/b] Sigortayı kaldır, gösterge kırmızıya vurmadan kazan vanasını aç, osiloskobu ayarla.
[/list]

[h2]Geceyi atlat[/h2]
Koridorda ayak sesleri. Saklanacak bir dolap. Bakmaman gereken bir varlık: arkanı dön, kıpırdama ve başını kendine çekerken diren. Tereddüt edersen yakalanırsın.

[h2]Özellikler[/h2]
[list]
[*]Araştırmaya dayalı bulmacalarla tek oyunculu birinci şahıs korku
[*]Bulduğun her şeyi her soru için hatırlayan defter
[*]Gerçek ölçüm geri bildirimli fiziksel kara tahta deneyleri
[*]İşini bölen basınç, elektrik ve gizlilik sistemleri
[*]İsteğe bağlı adım adım öğretici ya da doğrudan başla
[*]Türkçe ve İngilizce arayüz ve altyazı
[/list]
```

- [ ] **Mature Content Description:** ✍️ Jumpscare, sürekli tehdit, ölüm ekranı var; kan/şiddet görseli yok gibi. Öneri: `Senzora contains jumpscares, a persistent stalking threat and death sequences. No gore, nudity or substance use.` Steam'in yaş anketindeki cevaplarla tutarlı olmalı.
- [ ] **Legal line:** `© 2026 Modiac. All rights reserved.` (Caveat fontu SIL OFL; sayfada belirtmek zorunlu değil, krediler içinde yeter.)

---

## 3. Localization (Supported Languages tablosu)

| Dil | Interface | Full Audio | Subtitles |
|---|---|---|---|
| English | ✔ | ✍️ | ✔ |
| Turkish | ✔ | ✍️ | ✔ |

✍️ Projede Voice mixer grubu ve tutorial sesi var; seslendirme hangi dilde kayıtlı? Sadece tek dilde varsa o dilde Full Audio işaretle, diğerinde boş bırak. Sayfanın Türkçe çevirisini Localized Description sekmesine gir (yukarıdaki TR metinler).

---

## 4. Graphical Assets (hepsi zorunlu, PNG/JPG, metin yalnız logo)

| Varlık | Boyut | Elimizde | Yapılacak |
|---|---|---|---|
| Header capsule | 920×430 | logo 3599×937 var | Karanlık lab karesi + logo |
| Small capsule | 462×174 | – | Header'dan kırp |
| Main capsule | 1232×706 | – | Header'ın geniş hali |
| Vertical capsule | 748×896 | – | Dikey kompozisyon, logo üstte |
| Library capsule | 600×900 | – | Vertical'ın kırpılmış hali |
| Library hero | 3840×1240 | – | Yazısız geniş sahne (raflar/koridor) |
| Library logo | 1280×720, şeffaf PNG | `senzora_new_logo.png` | Siyah zemini şeffaf yap |
| Page background | 1438×810 | – | Hero'dan kırp, koyu tut |
| Community icon | 184×184 JPG | `SenzoraIcon.jpg` 1600×1600 | Küçült |
| Client icon | 32×32 ICO | `SenzoraIcon.jpg` | Dönüştür |

Kurallar: kapsüllerde logo dışında yazı yok (fiyat, "yeni", ödül vs. yasak); logo kapsülün en az %50'sinde okunur olmalı. Mevcut logo (siyah fırça zemin, beyaz harf, kırmızı atom) kapsül için uygun; zemin siyah olduğu için koyu sahnelere doğrudan oturur.

Kaynak sahne önerileri: kütüphane koridoru (ScreenShots/Image Sequence_001), kara tahta üstünde formül, Turing makinesi, LEES'in ekran kenarındaki elleri.

---

## 5. Screenshots (en az 5, 1920×1080, 16:9, oyun içi görüntü)

Mevcut `Assets/Sprites/ScreenShots/*.jpg` 1920×1080 ama çoğu çok karanlık (osiloskop karesi neredeyse siyah); Steam küçük karede hiçbir şey görünmez. Yeniden çek, parlaklık ayarını geçici yükselt, HUD'ı kapat.

Önerilen 8 kare (ilk 4 en önemlisi, sayfada önce o görünür):
1. Kara tahta üstünde kurulu deney + parçalar ve bağlar
2. Defter açık, kitaptan not sürüklenirken
3. Turing makinesi, ölçüm kartı yuvada, yeşil kol
4. LEES mücadelesi: ekran kenarında eller, baskı göstergesi
5. El fenerinde kütüphane koridoru
6. Kazan dairesi, kırmızıya yaklaşan basınç göstergesi
7. Osiloskop dalga eşleme
8. Dolapta saklanırken kapı aralığından görünüş

---

## 6. Trailer (en az 1, zorunlu)

- Format: MP4 (H.264) veya MOV, 1920×1080, 30/60 fps, 5 GB altı; 1080p için ayrıca 16:9 kapak karesi (1920×1080 PNG) istiyor.
- Elimizde: `Assets/Videos/ScreenRecords/Movie_017..025.mp4` (ham kayıt), `cutscenefinalavideo.mov` (açılış sinematiği).
- Öneri: 60–90 sn. Yapı: açılış sinematiğinden 5 sn → "Leaving the university was a mistake, Arthur." altyazısı → kitaptan deftere not → tahtada deney → Turing kolu → ışıkların sönmesi → LEES'e sırt dönme ve mücadele → yakalanma → logo. Trailer içinde metin serbest, kapsülde yasak.
- ✍️ Kurgu kim yapacak? Sahneler yeniden çekilecekse Play'de köprüyle 1080p kayıt alınabilir.

---

## 7. Tags (20'ye kadar; ilk 5 en ağırlıklı)

Sırayla:
```
Psychological Horror, Puzzle, First-Person, Horror, Stealth,
Survival Horror, Atmospheric, Dark, Mystery, Singleplayer,
Investigation, Story Rich, Indie, 3D, Realistic,
Science, Exploration, Walking Simulator, Lovecraftian, Difficult
```
"Lovecraftian" ve "Difficult" ✍️ sana kalmış; yanlış tag Steam'in yanlış kitleye göstermesine yol açar.

- [ ] **Genre:** Indie, Adventure (birincil: Adventure)
- [ ] **Features (Categories):** Single-player. ✍️ Steam Achievements / Cloud yoksa işaretleme; sonradan eklenebilir.

---

## 8. System Requirements (Windows)

Projede ölçülmüş değer yok; Unity 6 URP, DoF ve post-process kullanan tipik 3D korku için başlangıç önerisi. ✍️ Düşük GPU'da test edip düzelt.

```
MINIMUM
OS: Windows 10 64-bit
Processor: Intel Core i5-4460 / AMD Ryzen 3 1200
Memory: 8 GB RAM
Graphics: NVIDIA GTX 960 / AMD RX 560, 4 GB
DirectX: Version 11
Storage: 6 GB available space   (build klasörünün gerçek boyutunu yaz)

RECOMMENDED
OS: Windows 10/11 64-bit
Processor: Intel Core i5-8400 / AMD Ryzen 5 2600
Memory: 16 GB RAM
Graphics: NVIDIA GTX 1660 / AMD RX 580, 6 GB
DirectX: Version 12
Storage: 6 GB available space
```

---

## 9. Age Rating / Content Survey

Steam'in zorunlu anketi. Oyun içeriğine göre cevaplar:
- Violence: "Infrequent/mild" (yakalanma, jumpscare, ölüm ekranı; görsel şiddet yok)
- Fear/horror themes: Yes
- Blood/gore: No · Nudity: No · Drugs/alcohol: No · Gambling: No · Strong language: No
- ✍️ Almanya USK / Brezilya / Kore puanlaması ayrı istenir; başta boş bırakılabilir ama o ülkelerde sayfa görünmeyebilir.

---

## 10. Links & Support

- [ ] **Support e-mail:** ✍️ (modiacgames@gmail.com kullanılacaksa yaz)
- [ ] **Website:** ✍️ yoksa boş
- [ ] **Social:** ✍️ X / Discord / YouTube varsa
- [ ] **Privacy policy URL:** Oyun dışarıya veri göndermiyorsa gerekmez (SaveManager yerel JSON).

---

## 11. Gönderim

1. Her sekmede "Save" → sayfa üstünde "Publish" altında **"Review Store Page"**: eksik alanlar kırmızı listelenir.
2. "Submit for review": Valve 2–5 iş günü. Reddedilirse en sık neden: kapsülde yazı, karanlık/okunmaz ekran görüntüsü, trailer eksik.
3. Onaydan sonra "Coming Soon" olarak yayınla; wishlist toplanmaya başlar. Oyun build'i ve fiyat bu aşamada gerekmez.

---

## Durum (1 Kasım 2026, App ID 4318840)

Steamworks'e girildi ve kaydedildi: Basic Info (Windows, min/rec gereksinim, EN+TR arayüz+altyazı), Description (EN+TR kısa/uzun açıklama, legal, Single-player, Genre Adventure+Indie), 20 tag (Tag Wizard'dan yayınlandı), Content Survey yayınlandı (psikolojik korku + kansız şiddet; AI: Evet, mağaza metni ve kod yardımı beyanı EN+TR; Brezilya 14+, Almanya Horror).

Support Info: modiacgames@gmail.com girildi. Çıkış tarihi: tahmini 15 Mart 2027, müşteri "Coming Soon" görür (tarih yer tutucu, istediğin zaman değiştir). Controller: "Mouse and keyboard only" (gamepad test edilince wizard'dan yükselt). Boş kalan: Full Audio dili.

**Son aşama bilerek yapılmadı:** "Review Store Page" / "Submit for review" gönderilmedi; önce sen kontrol edeceksin. Instagram linki Steam'in sosyal medya listesinde yok (yalnız Discord/YouTube/Facebook/X/Twitch).

Yer tutucu görseller `outputs/steam-assets/` içinde hazır (README'de hangi alana yükleneceği yazıyor): 10 kapsül/kütüphane/ikon dosyası + 5 ekran görüntüsü (Steam'in 5 şartı karşılanıyor; kitap ve tahta kareleri Unity'den çekildi). Yükleme tarayıcıdan elle yapılacak. Eksik: LEES karesi (isteğe bağlı), trailer.

## Sıra önerisi

1. Hesap/ödeme/App ID (sen)
2. Metinleri yapıştır: Basic Info, Description, Localization, Tags, Requirements, Age anketi (hepsi yukarıda)
3. Ekran görüntülerini yeniden çek (Play'de parlaklık yüksek, HUD kapalı)
4. Kapsülleri üret (logo + 2–3 sahne karesi; 10 boyut)
5. Trailer kurgusu
6. Review'a gönder
