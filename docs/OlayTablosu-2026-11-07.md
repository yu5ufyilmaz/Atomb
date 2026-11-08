# Olay Tablosu ve Oranlama — 2026-11-07

Kaynak: `scn_labratory.unity` sahnesindeki Inspector değerleri (kod varsayılanları farklıysa ayrı sütunda). Tüm süreler saniye. "10 dk" hesapları, oyuncunun o olayın koşulunu sağlayan yerde kesintisiz kaldığını varsayar; gerçek oyunda oyuncu odalar arasında dolaştığı için sayılar üst sınıra yakındır.

## 1. Kapı bekçisi: GlobalEnemyManager

| Kural | Değer |
|---|---|
| Aynı anda yalnız bir saldırı (Lees, Guderian, pusu) | `isAttackInProgress` |
| Her saldırı bittikten sonra herkes için huzur süresi | 30 s (`postAttackCooldown`) |
| Oyun başlamadan (`isGameStarted`) hiçbir sayaç işlemez | — |
| Tutorial bitene kadar `stopAllEnemies = true`; basınç, şalter ve düşmanlar aynı tutorial adımında birlikte açılır | TutorialManager.cs:119-121 |

## 2. Guderian (yalnız Office_Room)

Sahne: yalnız `Office_Room` için `canGuderianSpawn = 1`. Diğer odalarda Guderian zarı hiç atılmaz.

| Parametre | Sahne | Kod varsayılanı |
|---|---|---|
| Kontrol aralığı `checkInterval` | 60 | 5 |
| Başlangıç şansı `baseSpawnChance` | %40 | 15 |
| Her başarısız zarda artış `chanceIncreaseStep` | +5 puan | 5 |
| Guderian kendi cooldown'u `minTimeBetweenAttacks` | 180 | 30 |
| Arama süresi `baseSearchDuration + timePerLight × açık ışık` | 20 + 10/ışık (ofiste 3 ışık → 20–50 s) | aynı |
| Adım sesi aralığı × 5 adım | 0,8 × 5 = 4 s | aynı |
| Kapı kırma: kapalı / kilitli | 2 / 5 s | aynı |
| Yürüme hızı | 1,75 | 2,5 |

Zar mekaniği: odaya girince sayaç -5'e çekilir (`SetCurrentRoom`), ilk zar 65. saniyede, sonra her 60 s. Başarısız zarlar şansı 40 → 45 → 50 … diye artırır; şans yalnız spawn olunca 40'a döner. Odadan çıkıp girmek sayacı sıfırlar ama şansı sıfırlamaz. Cooldown (180 s) sürerken sayaç hiç işlemez.

| Ofiste kesintisiz süre | Guderian gelmiş olma olasılığı |
|---|---|
| 65 s | %40 |
| 125 s | %67 |
| 185 s | %84 |
| 245 s | %93 |
| 305 s | %97 |

Ortalama ilk geliş: odaya girdikten ~138 s sonra. Saldırı süresi ~35–65 s (adım 4 + kırma 2–5 + arama 20–50 + çıkış). Tam döngü ≈ 180 cooldown + 138 bekleme + ~50 saldırı ≈ 370 s.

**10 dk'da (hep ofiste):** ortalama ~1,6, en fazla 2 Guderian saldırısı.

### Pusu (ceza) — RoomManager.ambushTimeout

Kural: oyuncu ofis dışında + ofis kapısı açık + Guderian `Hidden` + global bekçi izin veriyor → 15 s sonra Guderian kapının iç tarafına yerleşir; oyuncu odaya girince jumpscare. Zar yok, kesin tetiklenir. **Guderian'ın kendi 180 s cooldown'u pusuya bakılmaz**; yalnız global 30 s huzur onu geciktirir. Kapıyı açık bırakma alışkanlığı olan oyuncu için sınırsız.

## 3. Lees (tehlikeli odalar)

Sahne: `isDangerous = 1` olan odalar: `ParticleAccelerator_Room`, `Office_Room`, `Chemistry_Lab`, `Valve_Room`. Koridor, `Fuse_Room`, `Personel_Space` güvenli.

| Parametre | Sahne | Kod varsayılanı |
|---|---|---|
| Kontrol aralığı `spawnCheckInterval` (ilk kontrol sahne açılışından 5 s sonra, `InvokeRepeating`) | 20 | 5 |
| Başlangıç şansı `baseSpawnChance` | %5 | 10 |
| Saniye başına artış `chanceIncreasePerSecond` | +0,5 puan | 2 |
| Şans tavanı | %90 (kodda sabit) | — |
| Despawn sonrası cooldown `spawnCooldownAfterDespawn` | 90 | 20 |
| Görmezden gelme ölümü `maxIgnoranceTime` | 90 | 30 |
| Bakma ölümü `maxReactionTime` | 2 | 3 |
| Direnç yoksa hayatta kalma bekleme `survivalWaitTime` | 10 | 15 |
| Kıpırdama toleransı `movementGraceTime` | 1 | 0,5 |
| Direnç (`LeesResistanceController.tuning`): yaklaşma + direnme | 2,8 + 16 s | — |
| Direnç pratik modu | 2,8 + 8 s | — |

Zar mekaniği: `timeSpentInCurrentRoom` yalnız tehlikeli odada ve Lees gizliyken işler, **oda değişince sıfırlanmaz, yalnız spawn olunca sıfırlanır**. Yani dört tehlikeli oda arasında dolaşmak sayacı biriktirir. Şans = 5 + 0,5 × birikmiş saniye. Görüş açısı dışında spawn noktası yoksa o zar boşa gider ("Spawn iptal"), şans artmaya devam eder.

| Tehlikeli odada birikmiş süre | Lees gelmiş olma olasılığı |
|---|---|
| 20 s | %15 |
| 40 s | %36 |
| 60 s | %59 |
| 80 s | %77 |
| 100 s | %90 |
| 120 s | %96 |
| 140 s | %99 |

Ortalama ilk geliş: ~66 s tehlikeli-oda süresi. Karşılaşma ~20 s (direnç 2,8 + 16; direnç yoksa 10 s bekleme). Tam döngü ≈ 90 cooldown + 66 bekleme + 20 karşılaşma ≈ 176 s.

**10 dk'da (hep tehlikeli odalarda):** ortalama ~3,4, şanslı zarlarla en fazla ~4,5 Lees karşılaşması. Buna zar olaylarının (bölüm 6) zorla çağırdığı Lees'ler dahil değil.

## 4. Şalter (BreakerBox) ve Adam

| Parametre | Sahne | Kod varsayılanı |
|---|---|---|
| Kontrol aralığı `checkInterval` | 100 | 180 |
| Tam yükte temel risk `maxRiskAtFullLoad` | %25,6 | 80 |
| Her stabil döngüde ek risk `cycleRiskMultiplier` | +1 puan | aynı |
| Riske katılan ışık sayısı | 8 (9 ControllableLight'tan 1'i koridor, `contributesToRisk = 0`) | — |
| Adam: karanlıkta ilk uyarı / ikinci uyarı / öldürme | 30 / +15 / +5 = 50 s | 15 / 5 / 3 = 23 s |

Trip şansı her 100 s'de = (açık riskli ışık / 8) × 0,256 + 0,01 × stabil döngü sayısı. Şalter atınca sayaç durur; oyuncu `Fuse_Room`'da kolu kaldırana kadar bekler. Tutorial sürerken ve sistem kapalıyken sayaç sıfırda tutulur.

| Açık riskli ışık | Ortalama atma süresi | 10 dk içinde atma olasılığı |
|---|---|---|
| 8/8 | 358 s (~6 dk) | %86 |
| 6/8 | 449 s | %77 |
| 4/8 | 594 s | %63 |
| 2/8 | 842 s | %43 |
| 1/8 | 1039 s | %30 |
| 0/8 | hiç atmaz (döngü sayacı da sıfırlanır) | %0 |

**10 dk'da:** ışıklar hep açıksa ~1,5 şalter atması; oyuncu her seferinde sigorta odasına gider. Şalter atıkken oyuncu koridor dışı bir odada 50 s kalırsa Adam öldürür. Koridorda sayaç **durur ama sıfırlanmaz**; yalnız ışıklı odaya girmek sıfırlar. Şalter atıkken hiçbir oda ışıklı sayılmadığından tek sıfırlama yolu kolu kaldırmaktır.

## 5. Basınç ve vana odası

| Parametre | Sahne | Kod varsayılanı |
|---|---|---|
| Artış hızı `pressureIncreaseRate` | 0,3 / s | 2,0 |
| Uyarı eşiği `warningThreshold` (HUD kırmızı yazı + megafon; bar rengi 70'ten kırmızı) | 80 | 90 |
| Vana azaltma `pressureDecreaseRate × dt × dönüş` | en fazla ~30 / s → sıfırlama 3–4 s | 15 |
| Ölüm | 100 → patlama, 3 s sonra ölüm ekranı | — |

| Sıfırdan başlayarak | Süre |
|---|---|
| Uyarı (80) | 267 s (4 dk 27 s) |
| Ölüm (100) | 333 s (5 dk 33 s) |
| Uyarıdan ölüme kalan | 67 s |

**10 dk'da:** oyuncu her 5,5 dk'da bir vana odasına gitmek zorunda → en az 2 vana ziyareti (ölüm sınırında oynarsa 1,8). Uyarıyı görünce gitmek için 67 s var; vana odası tehlikeli oda olduğundan içeride Lees gelebilir. Tutorial sürerken basınç artmaz, vana yine çalışır.

## 6. Zar olayları (WeightedRandomTrigger)

| Obje | Ne zaman | Mod | Sonuçlar |
|---|---|---|---|
| `RedBook_Trigger` | Kırmızı kitapla etkileşim (ön koşul var), 1 kez | Oyun başında zar atılır, sonuç sabit | Light %23 · Guderian saldırısı %12 · Boş %27 · Kapı kilidi %9 · Lees spawn %30 |
| `PasswordsChances` | Her doğru şifre girişi (`OnAnyCorrect`) + makineyle etkileşim | Her seferinde zar | Light %18 · Guderian %9 · Boş %21 · Kapı kilidi %7 · Lees spawn %23 · Lees saldırısı %23 |
| `ChemLabTrigger` | Kimya laboratuvarına her giriş (ön koşul var) | Her seferinde zar | Boş %34 · Dummy spawn %11 · Lees saldırısı %16 · Işıklar kapanır %23 · Silah sesi %15 |

Not: Zar sonucu Lees/Guderian çağırsa bile global bekçi (bölüm 1) meşgulse veya cooldown sürüyorsa saldırı sessizce düşer (`CanAttack` false). Dolayısıyla "Lees spawn" ağırlığı pratikte yazılandan düşük gerçekleşir.

Ayrıca sahnede 21 `EventLogicController` var; bunlar senaryo akışı (tek seferlik, koşul bazlı), periyodik değil.

## 7. Tek seferlik anonslar (MegaphoneSystem)

Oyun başı (3 s gecikme), ilk defter, ilk yanlış şifre, tutorial çözüldü, ilk basınç uyarısı (80), ilk şalter atması, final şifre. Her biri yalnız bir kez çalar.

## 8. 10 dakikalık özet (oyuncu olayın koşulunu sağlayan yerde kalırsa)

| Olay | İlk geliş (ortalama) | Aralık / döngü | 10 dk'da beklenen | 10 dk'da üst sınır |
|---|---|---|---|---|
| Lees | ~66 s tehlikeli-oda süresi | ~176 s | ~3,4 | ~4,5 |
| Guderian (ofis) | ~138 s ofis süresi | ~370 s | ~1,6 | 2 |
| Guderian pusu | kapı açık bırakılınca 15 s | sınırsız, sadece global 30 s | davranışa bağlı | her kapı açık bırakmada 1 |
| Şalter (8 ışık açık) | ~358 s | ~358 s | ~1,5 | 6 kontrol (%86 en az 1) |
| Şalter (4 ışık açık) | ~594 s | — | ~0,8 | %63 en az 1 |
| Adam ölümü | şalter + 50 s karanlık oda | — | şalter başına 1 risk | — |
| Vana ziyareti zorunluluğu | 267 s uyarı / 333 s ölüm | 333 s | 1,8 | 2 |
| Zar olayı (şifre) | her doğru şifrede | — | şifre sayısı kadar | — |
| Zar olayı (kimya) | her girişte | — | giriş sayısı kadar | — |

Tüm düşman saldırıları tek kuyrukta olduğundan 10 dakikada toplam saldırı (Lees + Guderian + pusu + zar) pratikte 5–6'yı geçmez; her saldırıdan sonra 30 s kesin huzur vardır.

## 9. Dikkat çeken etkileşimler (değişiklik kararı için)

1. Pusu, Guderian'ın 180 s cooldown'unu atlar; sıradan saldırıdan hemen sonra kapı açık bırakılırsa 45 s içinde tekrar Guderian gelebilir (RoomManager.cs:84-94, GuderianAI.cs:233-240).
2. Lees sayacı oda değişince sıfırlanmaz; vana odasına giden oyuncu ofiste biriktirdiği şansla gider (LeesEnemyAI.cs:222-224).
3. Adam sayacı koridorda durur ama sıfırlanmaz; şalter atıkken koridora kaçmak ölümü yalnız erteler (AdamAI.cs:118-121).
4. Basınç ölümü (333 s) ile Guderian döngüsü (370 s) yakın; ikisi aynı anda zorlayabilir. Basınç uyarısıyla ölüm arası yalnız 67 s.
5. Şalter kontrolü oyuncunun nerede olduğuna bakmaz; sigorta odası güvenli oda olduğundan karanlıkta vana odasına gitmek tek zorunlu risk.
6. Zar olaylarının Lees/Guderian sonuçları bekçi meşgulse sessizce kaybolur; oyuncu "bir şey olmadı" sanır.
