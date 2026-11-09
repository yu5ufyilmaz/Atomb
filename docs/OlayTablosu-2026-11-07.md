# Olay Tablosu ve Oranlama — 2026-11-07

**Güncelleme 2026-11-09:** Playtest "Lees, Guderian, elektrik kesintisi ve vana çok sık geliyor, Guderian çok uzun geziniyor" dedi; aşağıdaki Inspector değerleri `scn_labratory.unity` ve `scn_LaboratorySynthesis.unity` sahnelerinde değişti. Bu belgedeki tüm tablo, formül, olasılık ve "10 dk" tahminleri YENİ değerlerle yeniden hesaplandı. Kod varsayılanları değişmedi.

| Parametre | Eski → Yeni |
|---|---|
| Guderian `baseSearchDuration` | 20 → 10 |
| Guderian `timePerLight` | 10 → 4 (ofiste 3 ışık: 20–50 s → 10–22 s) |
| Guderian `minTimeBetweenAttacks` | 180 → 240 |
| Guderian `baseSpawnChance` | %40 → %30 (zar 30 → 35 → 40 …) |
| Lees `spawnCooldownAfterDespawn` | 90 → 150 |
| Lees `chanceIncreasePerSecond` | 0,5 → 0,3 |
| Şalter `checkInterval` | 100 → 150 |
| Basınç `pressureIncreaseRate` | 0,3 → 0,2 / s |
| Basınç `warningThreshold` | 80 → 85 (uyarı 425 s, ölüm 500 s, aradaki 75 s) |

Ek: basınç 85'i her yukarı geçişinde kafa-içi düşünce altyazısı gösterilir (tek seferlik megafon anonsuna ek olarak).

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
| Başlangıç şansı `baseSpawnChance` | %30 | 15 |
| Her başarısız zarda artış `chanceIncreaseStep` | +5 puan | 5 |
| Guderian kendi cooldown'u `minTimeBetweenAttacks` | 240 | 30 |
| Arama süresi `baseSearchDuration + timePerLight × açık ışık` | 10 + 4/ışık (ofiste 3 ışık → 10–22 s) | aynı |
| Adım sesi aralığı × 5 adım | 0,8 × 5 = 4 s | aynı |
| Kapı kırma: kapalı / kilitli | 2 / 5 s | aynı |
| Yürüme hızı | 1,75 | 2,5 |

Zar mekaniği: odaya girince sayaç -5'e çekilir (`SetCurrentRoom`), ilk zar 65. saniyede, sonra her 60 s. Başarısız zarlar şansı 30 → 35 → 40 … diye artırır; şans yalnız spawn olunca 30'a döner. Odadan çıkıp girmek sayacı sıfırlar ama şansı sıfırlamaz. Cooldown (240 s) sürerken sayaç hiç işlemez.

| Ofiste kesintisiz süre | Guderian gelmiş olma olasılığı |
|---|---|
| 65 s | %30 |
| 125 s | %55 |
| 185 s | %73 |
| 245 s | %85 |
| 305 s | %93 |

Ortalama ilk geliş: odaya girdikten ~167 s sonra. Saldırı süresi ~25–37 s (adım 4 + kırma 2–5 + arama 10–22 + çıkış). Tam döngü ≈ 240 cooldown + 167 bekleme + ~30 saldırı ≈ 437 s.

**10 dk'da (hep ofiste):** ortalama ~1,4, en fazla 2 Guderian saldırısı.

### Pusu (ceza) — RoomManager.ambushTimeout

Kural: oyuncu ofis dışında + ofis kapısı açık + Guderian `Hidden` + global bekçi izin veriyor → 15 s sonra Guderian kapının iç tarafına yerleşir; oyuncu odaya girince jumpscare. Zar yok, kesin tetiklenir. **Guderian'ın kendi 240 s cooldown'u pusuya bakılmaz**; yalnız global 30 s huzur onu geciktirir. Kapıyı açık bırakma alışkanlığı olan oyuncu için sınırsız.

## 3. Lees (tehlikeli odalar)

Sahne: `isDangerous = 1` olan odalar: `ParticleAccelerator_Room`, `Office_Room`, `Chemistry_Lab`, `Valve_Room`. Koridor, `Fuse_Room`, `Personel_Space` güvenli.

| Parametre | Sahne | Kod varsayılanı |
|---|---|---|
| Kontrol aralığı `spawnCheckInterval` (ilk kontrol sahne açılışından 5 s sonra, `InvokeRepeating`) | 20 | 5 |
| Başlangıç şansı `baseSpawnChance` | %5 | 10 |
| Saniye başına artış `chanceIncreasePerSecond` | +0,3 puan | 2 |
| Şans tavanı | %90 (kodda sabit) | — |
| Despawn sonrası cooldown `spawnCooldownAfterDespawn` | 150 | 20 |
| Görmezden gelme ölümü `maxIgnoranceTime` | 90 | 30 |
| Bakma ölümü `maxReactionTime` | 2 | 3 |
| Direnç yoksa hayatta kalma bekleme `survivalWaitTime` | 10 | 15 |
| Kıpırdama toleransı `movementGraceTime` | 1 | 0,5 |
| Direnç (`LeesResistanceController.tuning`): yaklaşma + direnme | 2,8 + 16 s | — |
| Direnç pratik modu | 2,8 + 8 s | — |

Zar mekaniği: `timeSpentInCurrentRoom` yalnız tehlikeli odada ve Lees gizliyken işler, **oda değişince sıfırlanmaz, yalnız spawn olunca sıfırlanır**. Yani dört tehlikeli oda arasında dolaşmak sayacı biriktirir. Şans = 5 + 0,3 × birikmiş saniye. Görüş açısı dışında spawn noktası yoksa o zar boşa gider ("Spawn iptal"), şans artmaya devam eder.

| Tehlikeli odada birikmiş süre | Lees gelmiş olma olasılığı |
|---|---|
| 20 s | %11 |
| 40 s | %26 |
| 60 s | %43 |
| 80 s | %60 |
| 100 s | %74 |
| 120 s | %85 |
| 140 s | %92 |
| 160 s | %96 |
| 180 s | %98 |
| 200 s | %99 |

Ortalama ilk geliş: ~83 s tehlikeli-oda süresi. Karşılaşma ~20 s (direnç 2,8 + 16; direnç yoksa 10 s bekleme). Tam döngü ≈ 150 cooldown + 83 bekleme + 20 karşılaşma ≈ 253 s.

**10 dk'da (hep tehlikeli odalarda):** ortalama ~2,4, şanslı zarlarla en fazla ~3,2 Lees karşılaşması. Buna zar olaylarının (bölüm 6) zorla çağırdığı Lees'ler dahil değil.

## 4. Şalter (BreakerBox) ve Adam

| Parametre | Sahne | Kod varsayılanı |
|---|---|---|
| Kontrol aralığı `checkInterval` | 150 | 180 |
| Tam yükte temel risk `maxRiskAtFullLoad` | %25,6 | 80 |
| Her stabil döngüde ek risk `cycleRiskMultiplier` | +1 puan | aynı |
| Riske katılan ışık sayısı | 8 (9 ControllableLight'tan 1'i koridor, `contributesToRisk = 0`) | — |
| Adam: karanlıkta ilk uyarı / ikinci uyarı / öldürme | 30 / +15 / +5 = 50 s | 15 / 5 / 3 = 23 s |

Trip şansı her 150 s'de = (açık riskli ışık / 8) × 0,256 + 0,01 × stabil döngü sayısı. Şalter atınca sayaç durur; oyuncu `Fuse_Room`'da kolu kaldırana kadar bekler. Tutorial sürerken ve sistem kapalıyken sayaç sıfırda tutulur.

| Açık riskli ışık | Ortalama atma süresi | 10 dk içinde atma olasılığı (4 kontrol) |
|---|---|---|
| 8/8 | 537 s (~9 dk) | %72 |
| 6/8 | 674 s | %60 |
| 4/8 | 890 s | %46 |
| 2/8 | 1262 s | %28 |
| 1/8 | 1558 s | %18 |
| 0/8 | hiç atmaz (döngü sayacı da sıfırlanır) | %0 |

**10 dk'da:** ışıklar hep açıksa ~1,0 şalter atması (600 / (537 + kolu kaldırma payı ~40 s)); oyuncu her seferinde sigorta odasına gider. Şalter atıkken oyuncu koridor dışı bir odada 50 s kalırsa Adam öldürür. Koridorda sayaç **durur ama sıfırlanmaz**; yalnız ışıklı odaya girmek sıfırlar. Şalter atıkken hiçbir oda ışıklı sayılmadığından tek sıfırlama yolu kolu kaldırmaktır.

## 5. Basınç ve vana odası

| Parametre | Sahne | Kod varsayılanı |
|---|---|---|
| Artış hızı `pressureIncreaseRate` | 0,2 / s | 2,0 |
| Uyarı eşiği `warningThreshold` (HUD kırmızı yazı + megafon; bar rengi 70'ten kırmızı; eşik her yukarı geçildiğinde kafa-içi düşünce altyazısı) | 85 | 90 |
| Vana azaltma `pressureDecreaseRate × dt × dönüş` | en fazla ~30 / s → sıfırlama 3–4 s | 15 |
| Ölüm | 100 → patlama, 3 s sonra ölüm ekranı | — |

| Sıfırdan başlayarak | Süre |
|---|---|
| Uyarı (85) | 425 s (7 dk 5 s) |
| Ölüm (100) | 500 s (8 dk 20 s) |
| Uyarıdan ölüme kalan | 75 s |

Basınç 85'i her yukarı geçtiğinde (vana ile düşürülüp tekrar çıkınca da) kafa-içi düşünce altyazısı gösterilir; megafon anonsu ise yalnız ilk geçişte, bir kez çalar.

**10 dk'da:** oyuncu her 8 dk 20 s'de bir vana odasına gitmek zorunda → en az 1 vana ziyareti (ölüm sınırında oynarsa 1,2); ikinci zorunlu ziyaret 10 dk'nın dışına düşer. Uyarıyı görünce gitmek için 75 s var; vana odası tehlikeli oda olduğundan içeride Lees gelebilir. Tutorial sürerken basınç artmaz, vana yine çalışır.

## 6. Zar olayları (WeightedRandomTrigger)

| Obje | Ne zaman | Mod | Sonuçlar |
|---|---|---|---|
| `RedBook_Trigger` | Kırmızı kitapla etkileşim (ön koşul var), 1 kez | Oyun başında zar atılır, sonuç sabit | Light %23 · Guderian saldırısı %12 · Boş %27 · Kapı kilidi %9 · Lees spawn %30 |
| `PasswordsChances` | Her doğru şifre girişi (`OnAnyCorrect`) + makineyle etkileşim | Her seferinde zar | Light %18 · Guderian %9 · Boş %21 · Kapı kilidi %7 · Lees spawn %23 · Lees saldırısı %23 |
| `ChemLabTrigger` | Kimya laboratuvarına her giriş (ön koşul var) | Her seferinde zar | Boş %34 · Dummy spawn %11 · Lees saldırısı %16 · Işıklar kapanır %23 · Silah sesi %15 |

Not: Zar sonucu Lees/Guderian çağırsa bile global bekçi (bölüm 1) meşgulse veya cooldown sürüyorsa saldırı sessizce düşer (`CanAttack` false). Dolayısıyla "Lees spawn" ağırlığı pratikte yazılandan düşük gerçekleşir.

Ayrıca sahnede 21 `EventLogicController` var; bunlar senaryo akışı (tek seferlik, koşul bazlı), periyodik değil.

## 7. Tek seferlik anonslar (MegaphoneSystem)

Oyun başı (3 s gecikme), ilk defter, ilk yanlış şifre, tutorial çözüldü, ilk basınç uyarısı (85), ilk şalter atması, final şifre. Her biri yalnız bir kez çalar. Basınç uyarısının megafon anonsu tek seferliktir; buna karşılık 85 eşiği her yukarı geçildiğinde ayrıca kafa-içi düşünce altyazısı gösterilir (tekrarlanabilir).

## 8. 10 dakikalık özet (oyuncu olayın koşulunu sağlayan yerde kalırsa)

| Olay | İlk geliş (ortalama) | Aralık / döngü | 10 dk'da beklenen | 10 dk'da üst sınır |
|---|---|---|---|---|
| Lees | ~83 s tehlikeli-oda süresi | ~253 s | ~2,4 | ~3,2 |
| Guderian (ofis) | ~167 s ofis süresi | ~437 s | ~1,4 | 2 |
| Guderian pusu | kapı açık bırakılınca 15 s | sınırsız, sadece global 30 s | davranışa bağlı | her kapı açık bırakmada 1 |
| Şalter (8 ışık açık) | ~537 s | ~537 s | ~1,0 | 4 kontrol (%72 en az 1) |
| Şalter (4 ışık açık) | ~890 s | — | ~0,6 | %46 en az 1 |
| Adam ölümü | şalter + 50 s karanlık oda | — | şalter başına 1 risk | — |
| Vana ziyareti zorunluluğu | 425 s uyarı / 500 s ölüm | 500 s | 1,2 | 2 |
| Zar olayı (şifre) | her doğru şifrede | — | şifre sayısı kadar | — |
| Zar olayı (kimya) | her girişte | — | giriş sayısı kadar | — |

Tüm düşman saldırıları tek kuyrukta olduğundan 10 dakikada toplam saldırı (Lees + Guderian + pusu + zar) pratikte 4–5'i geçmez; her saldırıdan sonra 30 s kesin huzur vardır.

## 9. Dikkat çeken etkileşimler (değişiklik kararı için)

1. Pusu, Guderian'ın 240 s cooldown'unu atlar; sıradan saldırıdan hemen sonra kapı açık bırakılırsa 45 s içinde tekrar Guderian gelebilir (RoomManager.cs:84-94, GuderianAI.cs:233-240).
2. Lees sayacı oda değişince sıfırlanmaz; vana odasına giden oyuncu ofiste biriktirdiği şansla gider (LeesEnemyAI.cs:222-224).
3. Adam sayacı koridorda durur ama sıfırlanmaz; şalter atıkken koridora kaçmak ölümü yalnız erteler (AdamAI.cs:118-121).
4. Basınç ölümü (500 s) ile Guderian döngüsü (437 s) hâlâ aynı bantta; basınç uyarısı (425 s) tam bir Guderian saldırısına denk gelebilir, ama eskiye göre (333 s ölüm / 370 s döngü) basınç döngüsü Guderian'dan uzun olduğundan oyuncunun vana ziyareti arasında bir Guderian saldırısı yaşama olasılığı yüksek. Basınç uyarısıyla ölüm arası 75 s.
5. Şalter kontrolü oyuncunun nerede olduğuna bakmaz; sigorta odası güvenli oda olduğundan karanlıkta vana odasına gitmek tek zorunlu risk.
6. Zar olaylarının Lees/Guderian sonuçları bekçi meşgulse sessizce kaybolur; oyuncu "bir şey olmadı" sanır.
