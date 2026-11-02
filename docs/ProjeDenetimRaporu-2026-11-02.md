# Senzora — Proje Denetim Raporu (kod, sahne, ışık, texture, model, ses, build)

Tarih: 2 Kasım 2026 · Branch: SNZ-005 · Unity 6000.3.10f1 · HDRP 17.3 · Hedef: Windows/Steam · Değişiklik yapılmadı, yalnız inceleme.

## 1. Özet

**Oyun ne yapıyor:** Tek büyük laboratuvar sahnesinde geçen birinci şahıs korku/bulmaca oyunu. Oyuncu 47–53 kitabı okur, defter–kara tahta–Turing makinesi döngüsüyle araştırma yapar; üç düşman (Guderian, Lees, Adam) baskı kurar. Tüm oyun tek sahnede (`scn_LaboratorySynthesis`); ölünce sahne baştan yüklenir. Varsayılan yük: tek oyuncu, tek PC, orta sınıf Steam donanımı.

**Genel sağlık:** Oyun mantığı çalışıyor ve "Senzora/Doğrulama" test menüleriyle korunuyor; Ekim'deki kitap optimizasyonu ve sahne temizliği doğru yöndeydi. Ancak iki şey acil:

1. **Build 0.7 oyuncuya kırık gidiyor (kod):** kayıt sistemi build sahnesinde kapalı (Continue hiç açılmıyor, kapatınca her şey kaybolur), Adam ve Guderian ölümleri soft-lock yapıyor, "osm" tuş dizisi oyunu bitiriyor, final sonrası credits yerine açılış ekranı geliyor, Guderian araması ses yığınına dönüyor. Beşi de 1–10 satırlık düzeltme (bkz. 4.3 K1–K4, 4.6 F1).
2. **Performans sorunu kodda değil, içerik ve ayar katmanında:** proje "Standard shader'dan HDRP'ye dönüştürülmüş, hiç ayar yapılmamış 4K asset paketi" gibi davranıyor. Takılmaların ve uzun yüklemenin büyük kısmı buradan geliyor ve çoğu **birkaç saatlik toplu ayar değişikliğiyle, kod yazmadan** çözülüyor.

**Takılmanın ("kasma") en olası beş nedeni, sırayla:**
1. **35 lamba prefab'ı her kare gerçek zamanlı gölge çiziyor** (Shadow Update "Every Frame", caster modu "Everything", 1024², occlusion culling yok → duvar arkasındaki lambalar da çiziliyor) ve hepsi volumetric fog'a ×5 katkı veriyor. (R1, R2)
2. **≈ 2,3 GB texture tek seferde VRAM'de** (hepsi 2048², mip streaming yok). Bunun ≈ 683 MB'ı HDRP'nin hiç okumadığı eski Standard shader slotlarından (`_MetallicGlossMap`, `_OcclusionMap`), ≈ 220 MB'ı görünmez parallax için height map. (A1–A3)
3. **82 materyalde Pixel Displacement açık** (genlik 0,4 mm → görünmez), ortamın her pikselinde ray-march. (A3)
4. **Tek sahne, tek seferde yükleme:** 73 ses klibi Decompress On Load (müzik dahil ≈ 110 MB ham PCM, intro'da senkron decode), 92 Animator "Always Animate", dinamik TMP atlasları (ilk defter/kitap açılışı 50–200 ms), shader variant koleksiyonu yok ("ilk kez" takılmaları). (B1/F3, R6, U1, C4)
5. **Kod tarafı küçük ama sürekli:** HUD barları her kare canvas rebuild, bakılan nesnenin ipucu her kare yeniden lokalize, GameInput string araması, tahta/tutorial LINQ çöpü → GC dilimleri. (H1–H6)

**Build süresi / geliştirme hızı:** HDRP yanında URP + PostProcessing v2 kurulu, HDRP debug varyant stripping kapalı, 9 gereksiz paket, kaynak texture'ların %64'ü 4096–8192 (Library 12 GB), repo 11 GB LFS'siz, asmdef yok. (C1, C2, A5, D1, D3)

**Vazgeçilmesi önerilenler (maliyet > fayda):** git geçmişini LFS'e taşımak; `SciencInstute.fbx`'i yeniden modellemek/LOD üretmek; URP'ye geçmek; 53 kitabı şimdi prefab'a çevirmek; tek sahneyi additive sahnelere bölmek; IL2CPP'ye ölçmeden geçmek; asmdef bölmesini temizlikten önce yapmak; olay sistemini ve Excel build kapısını baştan yazmak; Unity Test Framework'e taşımak.

**Önerilen sıra (özet):** (0) Oyuncu hatalarını kapat [1 saat] → (1) Materyal temizliği + displacement kapatma + texture streaming + ses import [1 gün] → (2) Lamba gölge ayarları + occlusion bake + yeniden bake + volume ayarları [1 gün] → (3) Build'de ölç [yarım gün] → (4) Animator culling, TMP statik atlas, kod sıcak yolları [1–2 gün] → (5) Paket/URP temizliği, hijyen [fırsat buldukça]. Ayrıntı 6. bölümde.

## 2. Nasıl inceledik, kanıtın sınırı

- **Ölçüm, tahmin değil.** Assets klasörü (6,1 GB), 1101 texture/112 model/83 ses dosyasının `.meta` import ayarları, 6,7 MB'lık ana sahne dosyası, prefab'lar, materyaller, HDRP/Quality/Player ayarları ve 45 bin satırlık C# kodu script'lerle tarandı. "Build'e ne giriyor" sorusu, build sahnelerinden başlayıp GUID referanslarını izleyen bir bağımlılık taramasıyla hesaplandı (Unity'nin kendi build raporu `Library/LastBuild.buildreport` ikili olduğu için okunamadı; sayılar kaynak dosya boyutudur, sıkıştırılmış build boyutu değil).
- Kod tarafı altı paralel inceleme ile okundu: oyun/AI/etkileşim sıcak yolları, mimari ve doğruluk, sahne-ışık-render, asset/import/build, UI-TMP-defter, fizik-AI-ses. Her bulgu dosya:satır ya da ayar değeriyle kanıtlanmaya çalışıldı; kanıtı olmayan bulgular rapora alınmadı.
- **Ölçülemeyen:** Gerçek build'de FPS, GPU süresi ve bellek ölçümü yapılmadı (Profiler/Frame Debugger yakalaması yok). Bu yüzden "takılma" nedenleri, mekanizması bilinen ve sayısı ölçülen etkenlere göre sıralandı; kesin pay dağılımı ancak 7. bölümdeki ölçüm planı ile çıkar.
- Önceki çalışmalar kullanıldı: `docs/BookPerformanceAudit.md` (sayfa çevirme 600 ms → 1 ms), `docs/LaboratorySynthesisAudit-2026-10-30.md` ve `LaboratorySynthesisCleanup-2026-10-30.md` (174 ölü obje, 5 ölü script temizliği), `graphify-out/GRAPH_REPORT.md` (kod grafiği).

## 3. Proje profili (ölçülen)

| Konu | Değer |
|---|---|
| Unity / pipeline | 6000.3.10f1, **HDRP 17.3** aktif; URP 17.3 ve PostProcessing v2 paketleri de kurulu (kullanılmıyor) |
| Build sahneleri | `scn_PressAnyKeyMenu` → `scn_LaboratorySynthesis` (tüm oyun tek sahne) → `scn_CreditsMenu` |
| Ana sahne | 2877 GameObject, 905 MonoBehaviour (121 farklı script), 755 MeshRenderer, 122 SkinnedMeshRenderer, 95 Animator, 43 ışık, 7 reflection probe, 105 AudioSource, 6 Canvas, 66 MeshCollider, 218 BoxCollider; 0 LODGroup, 0 occlusion verisi |
| Prefab kullanımı | Sahnede yalnız 40 prefab örneği (35'i lamba). 53 kitap prefab değil, açılmış kopya (970 obje = sahnenin üçte biri) |
| Kod | 250 kendi script'i, 45.314 satır; 59 editör script'i (çoğu "Senzora/Doğrulama" menüleri); asmdef yok; 23 `static Instance` singleton |
| Texture | 1101 dosya (5,4 GB kaynak). Build'e 676'sı giriyor; hepsinin üst sınırı 2048; kaynakların 390'ı 4096², 42'si 8192². **Tahmini VRAM ≈ 2,3 GB** (mip streaming yok) |
| Materyal | Build'de 226; 138'i HDRP Lit. **174 texture (≈ 683 MB VRAM, 910 MB disk) sadece eski Standard shader slotları (`_MetallicGlossMap`, `_OcclusionMap`) yüzünden build'e giriyor** |
| Model | 112 FBX (158 MB), 63'ü build'de; hepsinde mesh compression kapalı, blend shape/animasyon/kamera/ışık import açık; `SciencInstute.fbx` 35 MB tek bina |
| Ses | 83 klip, 73'ü build'de; **hepsi Decompress On Load + Vorbis %100** (3 dk'lık müzik dahil) |
| Video | 376 MB; build'de 40 MB "WhatsApp Video…mp4" + 12 MB .mov; 323 MB ekran kaydı Assets içinde (import ediliyor, build'e girmiyor) |
| Fontlar | 78 .ttf (75'i kullanılmayan DMSans varyantı); TMP ana fontu dinamik atlas (1024², 4,3 MB) |
| Repo | `.git` 11 GB, LFS yok; Library 12 GB |
| Paketler | Kullanılmayan: URP, universal-config, postprocessing, visualscripting, animation.rigging, recorder, package-validation-suite (preview), multiplayer.center, collab-proxy, feature.development, test-framework.performance |
| Player | Mono (IL2CPP değil), stripping Low, incremental GC açık, engine code stripping açık, tüm log tipleri "full stack trace" |
| Varsayılan kalite | "High Fidelity" (HDRP, gölge atlası 8192, 32-bit) |


## 4. Bulgular

Her bulgu: Nerede / Bu ne / Sorun / Çözüm (en küçük çalışan değişiklik) / Atlarsak. Önem: Kesin düzelt > Düzeltilmeli > İyi olur. Çaba: S saatler, M 1–2 gün, L daha uzun. Kod tarafında ★ = bu oturumda ayrıca doğrulandı.

## 4.1 Render, ışık ve gölge (GPU)

Not: Bu bölümün ışık/HDRP ajanı oturum limitine takılıp bitiremedi; bulgular doğrudan sahne/prefab/ayar dosyalarından okunan değerlere dayanır (satır numaraları `tr -d '\r'` ile normalize edilmiş dosyalara göredir).

### R1. 35 lamba prefab'ı: her kare gölge haritası, "Everything" caster modu, volumetrik ×5 — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Düzeltme (2 Kasım, ölçüm sonrası):** Bu bulgu prefab dosyasına bakılarak yazıldı ve sahnede geçerli değil. Sahnedeki 35 örnek prefab'ı eziyor: gölgeler zaten önbellekli (`m_ShadowUpdateMode: 1`), çözünürlük en düşük seviyede (256), contact shadow kapalı. Lambalar ucuz; aşağıdaki öneriler uygulanmayacak. Her kare gölge çizen yalnız 3 sahne spotu ve fener var; spotlara 20 m gölge mesafesi verildi.
- **Nerede:** `Assets/Models/lambdeneme.prefab` (sahnede 35 örnek): Light `m_Type: 0` (spot), `m_Lightmapping: 1` (Mixed), `m_Shadows.m_Type: 1` (açık), `m_LightShadowCasterMode: 2` (Everything), `m_Range: 6`, `m_SpotAngle: 120`, `m_Intensity: 1591` lm; HDAdditionalLightData `m_ShadowUpdateMode: 0` (Every Frame), `m_ShadowResolution.m_Level: 2` (=1024², tier tablosu 256/512/1024/2048), `m_VolumetricDimmer: 5`, `m_ShadowFadeDistance: 10000`, IES cookie atanmış. Ayrıca 4 sahne ışığı daha gölgeli (2 realtime spot, 2 mixed spot).
- **Bu ne:** Mixed + Shadowmask modunda statik geometrinin gölgesi zaten lightmap/shadowmask'ta; "Everything" modu statikleri de her kare gölge haritasına yeniden çizdiriyor. Occlusion culling olmadığı için kamera frustum'una giren her lamba (duvar arkasındakiler dahil) gölge güncelliyor.
- **Sorun:** Koridor+odaların görüldüğü açılarda 10–20 gölgeli spot aynı anda aktif → her kare 10–20 × (755 renderer'ın frustum içindeki kısmı) gölge geçişi; High Fidelity'de 8192² 32-bit atlas (256 MB). Volumetrik fog'a 35 ışığın ×5 katkısı fog pass'ini de şişiriyor. HDRP'de kare süresinin en büyük tek kalemi büyük olasılıkla bu.
- **Çözüm (prefab'ta tek değişiklik, 35 örneğe yayılır):** Shadow Update Mode → **On Enable** (statik lambalar; anahtarla açılıp kapananlar için On Demand + `HDAdditionalLightData.RequestShadowMapRendering()`), Light Shadow Caster Mode → **Non-Lightmapped Only**, çözünürlük 512 (yalnız oyuncunun yakından baktığı 4–6 lamba 1024), Volumetric Dimmer 5 → 1; uzak lambalarda Shadow Fade Distance 15–20 m. Sonra bu sahne için yeniden bake (R3).
- **Atlarsak:** Gölge maliyeti ve fog maliyeti kare başına sabit kalır; düşük/orta kartlarda 30 FPS altı.

### R2. Occlusion culling yok: kapalı mekân oyununda duvar arkası odalar çiziliyor — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** `scn_LaboratorySynthesis.unity` `OcclusionCullingSettings.m_OcclusionCullingData: {fileID: 0}`; kamera prefab'ında `m_OcclusionCulling: 1` (hazır); sahnede 158 obje Static, 2714 değil.
- **Sorun:** Koridordan bakınca kütüphane, kimya ve diğer odaların 755 renderer'ı + 122 skinned mesh'i frustum içindeyse çiziliyor; gölge pass'leri de aynı geometriyi tekrar çiziyor.
- **Çözüm:** Bina kabuğu ve büyük mobilyaları Static (Occluder+Occludee) işaretle; Window > Rendering > Occlusion Culling > Bake (smallestOccluder 0,5, smallestHole 0,05 mevcut değerler uygun). 2 saatlik iş, kod yok.
- **Atlarsak:** R1'in kazancının yarısı boşa gider; GPU her odayı her zaman çizer.

### R3. Ana sahne başka sahnenin lightmap/probe bake'ini kullanıyor — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `scn_LaboratorySynthesis.unity` `m_LightingDataAsset` → `Assets/Scenes/scn_labratory/LightingData.asset`; `m_LightingSettings` → `scnl_labratory/Osman_GameScene_Settings.lighting`; 7 reflection probe `m_BakedTexture` → `scn_labratory/ReflectionProbe-0…6.exr`; 56 light probe. Kendi lighting klasörü yok.
- **Bu ne:** Sahne `scn_labratory`'den kopyalanmış (Ctrl+D), bake verisi referansla gelmiş. Ekim temizliğinde 174 obje silindi/taşındı; bake o zamandan beri alınmadı.
- **Sorun:** Kopyadan sonra eklenen/taşınan her statik obje lightmapsiz (gerçek zamanlı/probe ile aydınlanır, R1'in "Everything" modu bunu gizliyor). `scn_labratory.unity` ve klasörü silinirse ana sahnenin ışığı/yansımaları gider. Shadowmask yoksa "Non-Lightmapped Only" da çalışmaz.
- **Çözüm:** Yeni bake (GPU lightmapper, mevcut ayarlar: 1024 atlas, 400 örnek yeterli; 10–20 dk), bake'i sahnenin kendi klasörüne kaydet; sonra eski sahne/klasörleri sil.
- **Atlarsak:** R1 tam kazanç vermez; eski sahneler silinemez.

### R4. Volumetric fog "High" kalite + 35 volumetrik ışık; Motion Blur, DoF, Contact Shadows, Film Grain, Lens Distortion, Chromatic Aberration hepsi açık — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `Assets/Scenes/scnl_labratory/Global Volume Profile.asset`: Fog `enableVolumetricFog: 1`, `quality: 2`, `denoisingMode: 1`; MotionBlur (4 override), DepthOfField (18 override, `Managers/DoFManager.cs` her kare odak mesafesi güncelliyor), ScreenSpaceAmbientOcclusion, ContactShadows, Bloom, Vignette, FilmGrain, LensDistortion, ChromaticAberration. HDRP asset: SSS açık, decal atlası 4096², lowres transparent açık, lit shader mode "Both".
- **Sorun:** Korku atmosferi için fog ve DoF haklı; ama volumetrik "High" (64 dilim, denoise) + 35 ışık × dimmer 5 pahalı; Motion Blur ve Contact Shadows'un bu oyunda görünür katkısı az, GPU'da 1080p'de ~0,5–1 ms her biri. Ayarlar menüsünde zaten Motion Blur/Film Grain/DoF kapatma var; varsayılanları açık.
- **Çözüm:** Fog quality Medium, volumetrik ışık dimmer'ları 1 (R1), Motion Blur varsayılan kapalı, Contact Shadows kapalı; HDRP asset'te Lit Shader Mode → Deferred Only (varyant sayısı ve build süresi yarıya), kullanılmıyorsa Subsurface Scattering ve Decal kapalı. DoF ve Bloom kalsın.
- **Atlarsak:** Orta kartlarda birkaç ms; atmosfer için "pahalı ama bilinçli" diye bırakılabilir — önce ölç.

### R5. Kamerada anti-aliasing kapalı, near clip 0,01 m — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `Assets/StarterAssets/ThirdPersonController/Prefabs/MainCamera.prefab` HDAdditionalCameraData `antialiasing: 0`; sahne override'ları: `near clip plane: 0.01`, `far clip plane: 500`, `field of view: 85`.
- **Sorun:** HDRP'de AA yok → ince kenarlar (kitap sırtları, kablolar) titrer; near 0,01/far 500 derinlik hassasiyetini düşürür (gölge aknesi, z-fighting). Near değeri muhtemelen defter/el yakın planı için.
- **Çözüm:** Antialiasing → TAA (kalite Medium) ya da SMAA; near clip 0,03–0,05 (defterle test et).
- **Atlarsak:** Görsel kalite; performansa etkisi yok.

### R6. 92 Animator "Always Animate", 50'si kapalı kitap — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** sahne: 92/95 Animator `m_CullingMode: 0`; 50 × `Animations/NewBookAnim/newBookAnim.controller`; kapak hareketi zaten `InteractableBook.Motion.cs:56-90` ile elle sürülüyor (Animator yalnız idle pozu basıyor).
- **Sorun:** Her kare 50 state machine + 15 kemik yazımı, kamera görmese de (≈ 1 ms/kare, Mono'da daha çok); Lees despawn'dayken 9 skinned mesh'li rig animasyona devam ediyor.
- **Çözüm:** Kitap Animator'larında Culling Mode = Cull Completely (toplu seçim) ya da kurulum bitince `bookAnimator.enabled = false`; `LeesEnemyAI.ShowModel(false)`'ta Animator'ı kapat.
- **Atlarsak:** ~1 ms/kare ölü ağırlık.

### R7. URP/PPv2 kalıntıları, kullanılmayan shader graph'lar, Resources'taki shader — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** bkz. C1; `Assets/Shaders/New Shader Graph 1.shadergraph`, `VertexTry.shadergraph`, `Assets/Scripts/Shader/*.shader`, `Assets/Scripts/Outline/OutlineShader.shadergraph` (referans kontrolü: GUID'leri hiçbir build sahnesinde/materyalinde geçmiyorsa sil), `Assets/Resources/TuringCrtPatch.shader` (Resources'ta olduğu için her zaman build'de; `Shader.Find` ile yükleniyorsa kalsın).
- **Çözüm:** GUID taraması sonrası sil; C4 ile birlikte shader variant koleksiyonu üret.
- **Atlarsak:** Build süresi ve kafa karışıklığı.

### Render değerlendirmesi
En büyük üç GPU kazancı sırayla: lamba gölgelerinin cache'lenmesi + caster modu (R1), occlusion bake (R2) ve volumetrik/post ayarlarının bir kademe indirilmesi (R4). Bunların hiçbiri kod istemez; hepsi tek prefab + sahne ayarı + bake. Ölçüm olmadan bilinmeyen: hangi odada hangi kalem baskın (Frame Debugger ile 10 dakikada görülür). Korku görünümü için fog, DoF, bloom, vignette ve film grain bilinçli olarak tutulabilir; "pahalı ama değer" sınıfı bunlar. HDRP'nin kendisi bu oyun için ağır bir seçim ama **URP'ye geçişten vazgeç**: shader graph'lar, volume profilleri, IES ışıklar ve 138 Lit materyal yeniden ayarlanır, kazanç belirsiz; yukarıdaki ayarlarla HDRP'de hedef donanıma yetişilebilir.

## 4.2 Texture, materyal ve VRAM

### A1. Eski Standard shader slotları 174 texture'ı boşuna build'e sokuyor — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** HDRP Lit materyalleri, örn. `Assets/Materials/ChemicalLaboratory/M_ChemistryCupboard1/M_ChemistryCupboard1.mat` (`_MetallicGlossMap` → `…_Metallic.png`, `_OcclusionMap` → `…_AO.png`; `_MainTex`/`_BumpMap`/`_ParallaxMap` da dolu ama bunlar aynı texture'ın kopya referansı). Tam liste: 92 `_MetallicGlossMap`, 85 `_OcclusionMap`.
- **Bu ne:** Materyaller Standard/URP'den HDRP Lit'e çevrilmiş; HDRP Lit bu slotları hiç okumuyor (HDRP metallic+AO+smoothness'ı `_MaskMap` içinde taşır ve 96 materyalde MaskMap zaten dolu). Unity, shader'ın kullanmadığı slotlardaki texture referanslarını materyalde saklar ve **build'e dahil eder**.
- **Sorun:** 174 texture ≈ 683 MB VRAM + 910 MB kaynak disk, sahne yüklenince belleğe alınıyor, hiçbir pikselde kullanılmıyor. Yükleme süresi, bellek baskısı ve build boyutuna doğrudan etki.
- **Çözüm:** Tek bir editör script'i ile tüm materyallerde shader'da olmayan `m_TexEnvs` kayıtlarını sil (Unity'nin "unused properties" temizliği; `SerializedObject` üzerinden 20 satır). Sonra `_Metallic.png`/`_AO.png` dosyalarını projeden silmek isteğe bağlı (MaskMap zaten var).
- **Atlarsak:** ≈ 0,7 GB VRAM ve ≈ 1 GB build boyutu boşa gider; 4–6 GB kartlarda bu tek başına fark yaratır.

### A2. Build'deki 676 texture'ın tamamı 2048², mip streaming kapalı — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** Tüm texture `.meta`'ları: `maxTextureSize: 2048`, `streamingMipmaps: 0`, platform override yok; `ProjectSettings/QualitySettings.asset` texture streaming kapalı.
- **Bu ne:** Her materyal Albedo+Normal+Mask(+Height) 2048² DXT ile ≈ 16–21 MB; 138 Lit materyal + 47 kitap kapağı + spritelar ≈ 2,3 GB.
- **Sorun:** Sahne tek parça olduğu için hepsi aynı anda VRAM'de. HDRP'nin kendi tamponları (1080p'de ≈ 0,8–1,2 GB, gölge atlası 8192² 32-bit tek başına 256 MB) eklenince 4 GB kartlarda sistem belleğine taşma → sürekli takılma; 6–8 GB'ta bile yükleme uzun.
- **Çözüm:** (1) Quality'de **Texture Streaming**'i aç (bütçe 1024–1536 MB) ve tüm texture'larda `streamingMipmaps` = 1 (toplu, editör script'i ile). (2) Metallic/Mask/Height/AO gibi ikincil haritaları 1024'e indir; yalnız kahraman yüzeyler (kitap kapakları, tahta, yakından bakılan masalar) 2048 kalsın. (3) Kaynak PNG'leri 2048'e küçült (bkz. A5).
- **Atlarsak:** Düşük/orta kartlarda takılma ve 20–40 saniyelik sahne yüklemeleri sürer.

### A3. 82 materyalde Pixel Displacement (parallax occlusion) açık, genlik 0,4 mm: görünmez efekt için her piksel ray-march — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** 82 HDRP Lit materyal: `_DisplacementMode: 2` (Pixel), `_PPDMinSamples: 5`, `_PPDMaxSamples: 15`, `_HeightPoMAmplitude: 0.04` (cm), `_HeightMap` dolu. Örn. `M_ChemistryCupboard1.mat`, `M_Cage.mat`, `M_ChemistryDesk1.mat`, `M_MegafoneAndCamera.mat` (muhtemelen Standard→HDRP dönüştürücüsü `_ParallaxMap` görünce otomatik açtı).
- **Bu ne:** Pixel displacement, her pikselde height map üzerinde 5–15 adımlık ışın yürütür (ek texture okumaları + döngü), sonra normal gölgelendirme devam eder. Genlik 0,04 cm olduğu için sonuç gözle ayırt edilemez.
- **Sorun:** Ortamın büyük kısmını kaplayan 82 materyalde, 1080p'de ekranın çoğu pikseli için fazladan iş; ayrıca 83 height map (2048², ≈ 2,7 MB'tan toplam ≈ 220 MB VRAM) sırf bu yüzden build'de.
- **Çözüm:** Toplu olarak `Displacement Mode = None` (A1'deki materyal temizleme script'ine tek satır). Gerçekten kabartma istenen 2–3 kahraman yüzeyde (tahta, taş duvar) genliği 0,5–1 cm yapıp açık bırak.
- **Atlarsak:** Her karede ölçülebilir GPU süresi (ortam kaplamasına göre 1080p'de yaklaşık 0,5–1,5 ms) ve ≈ 220 MB VRAM boşa gider.

### A4. 47 kitap kapağı için 192 MB VRAM (her kitap 4096² kapak+normal+mask) — Önem: Düzeltilmeli — Çaba: M — Güven: yüksek
- **Nerede:** `Assets/Materials/KitapDışTexturelar/Kitap1…7/`, `Models/KitapDışTexturelar` (154 MB kaynak), `Kitap_Cover N.mat`.
- **Bu ne:** Aynı kitap modeline 7 ayrı kapak seti, hepsi 4096 kaynaktan 2048'e.
- **Sorun:** Kapaklar ekranda 20–40 cm'lik nesne; 1024² fazlasıyla yeter. 192 MB → ≈ 50 MB.
- **Çözüm:** Kapak albedo 1024, normal/mask 512; ya da 7 kapağı tek 2048² atlasta topla.
- **Atlarsak:** Orta ölçekli VRAM israfı; tek başına kritik değil.

### A5. Kaynak texture'ların %64'ü 4096–8192, bu import süresini ve repoyu şişiriyor — Önem: Düzeltilmeli — Çaba: M — Güven: yüksek
- **Nerede:** 390 dosya 4096², 42 dosya 8192² (örn. `Models/Characters/Lees/Textures/leeslow_final_Lees_Normal.png` 58 MB, 8192²); hepsi build'de 2048'e küçülüyor.
- **Sorun:** Build'e etkisi yok ama Library 12 GB, tam reimport saatler, git 11 GB; her `.meta` değişikliği 4096² PNG'yi yeniden kodluyor.
- **Çözüm:** Kaynakları 2048'e küçültüp (veya 2048 üstündekileri Assets dışına taşıyıp) commit'le. Tek seferlik toplu iş (ImageMagick/Python ile 1 saat).
- **Atlarsak:** Build değişmez; geliştirme hızı ve repo boyutu acı çekmeye devam eder. "Vazgeçilebilir" sınıfındadır; A1–A2 yapıldıktan sonra karar verilir.

### A6. UI sprite'ları atlas'sız (64 sprite, 0 SpriteAtlas) — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `Assets/Sprites/UI`, `Sprites/TuringMachine` (13 MB VRAM), `Resources/ControlIcons`.
- **Sorun:** Her sprite ayrı draw call/texture bind; Turing makinesi sembolleri gibi küçük sprite'lar tek tek 2048 limitiyle yaşıyor.
- **Çözüm:** `UI` ve `TuringMachine` için iki SpriteAtlas (2048², mipmaps kapalı).
- **Atlarsak:** UI çizim maliyeti küçük kalır; 3D sahnenin yanında belirleyici değil.

### A7. 65 texture'da mipmap kapalı, 63 NPOT, 4 sıkıştırmasız — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `asset_audit_out2.txt` texture bölümü (ör. sprite olarak işaretlenmemiş UI görselleri, `Sprites/paper.png`).
- **Sorun:** 3D'de kullanılan mipmapsiz texture uzaktan "kıvılcım" yapar ve cache'i yorar; NPOT sprite'lar sıkıştırılamaz (RGBA32 = 4 byte/px).
- **Çözüm:** 3D'de kullanılanlara mipmap aç; UI olanları Sprite tipine alıp atlasa koy.
- **Atlarsak:** Küçük görsel/bellek kaybı.

## 4.3 Kod: doğruluk ve mimari (oyuncunun göreceği hatalar)

Bu bölümdeki bulgular kod okumasına dayanır; ★ işaretliler bu oturumda ayrıca dosya/sahne üzerinde doğrulandı.

### K1 ★ Build sahnesinde kalıcı kayıt kapalı (`sessionOnly: 1`) — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** `scn_LaboratorySynthesis.unity:84868` (`sessionOnly: 1`), `SaveSystem/SaveManager.cs:58,69,76,112,142`, `Editor/LaboratorySynthesisSetup.cs:85`, `Editor/LaboratorySynthesisChecks.cs:380,445`.
- **Bu ne:** `sessionOnly` dosya okuma/yazmayı tümüyle kapatan geliştirici bayrağı; sahne "araştırma kopyası" olarak üretilirken 1'e çekilmiş ve doğrulama menüsü bunu doğru durum sayıyor. Bu sahne artık build'deki tek oyun sahnesi.
- **Sorun:** `HasSaveFile()` hep false → ana menüde Continue hiç açılmaz; `SaveGame()` sessizce döner. "Build ready Senzora_0_7" bu halde çıktı: oyunu kapatan oyuncu her şeyi kaybediyor.
- **Çözüm:** Sahnede `sessionOnly: 0`; Setup/Checks'teki iki satırı sil (Play testleri çalışma zamanında zaten `sessionOnly = true` yapıyor, korunur). Kayıtsız oyun bilinçli bir tasarımsa Continue düğmesini ve SaveSystem'i sil; ölü düğme gönderme.
- **Atlarsak:** Her kapatma = baştan başlama; Steam'de ilk şikayet.

### K2 ★ Ölüm kapısı iki kez çağrılıyor: Adam ölümleri ve Guderian'ın timeline'sız jumpscare'leri soft-lock — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** `Managers/JumpScareManager.cs:127` (`if (!GameManager.TryBeginDeath()) return;`), `EnemyAI/AdamAI.cs:182`, `EnemyAI/GuderianAI.cs:736`, `Managers/GameManager.cs:39-59`; sahne `timelineInFront: {fileID: 0}` (Guderian).
- **Bu ne:** `BeginEnding` yalnız ilk çağrıda true döner. Adam ve Guderian önce `TryBeginDeath()` çağırıp sonra `StartJumpscare`'e giriyor; oradaki ikinci kapı (commit e99c106 "Bazı optimizasyonlar", build 0.7'de) false dönüp çıkıyor.
- **Sorun:** Oyuncu dondurulmuş, girdi kapalı, jumpscare ve ölüm ekranı gelmiyor → karanlıkta kilitli. Lees'in timeline'ı atanmış, etkilenmiyor.
- **Çözüm:** `StartJumpscare` içindeki `TryBeginDeath()` kapısını sil, yerine sınıf içi `bool running` (koroutin zaten çalışıyorsa çık).
- **Atlarsak:** Adam ve Guderian (InFront/Ambush) ölümleri her seferinde soft-lock.

### K3 ★ Gizli bitiş kodu "osm" sürüm build'inde aktif — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `Managers/GameManager.cs:29,104-137` (`Input.inputString` her kare; "o","s","m" sırası → `TriggerFinalEnding()`).
- **Sorun:** S geri yürüme tuşu; pause/ayar ekranında da çalışıyor. `DebugMenuManager` `#if UNITY_EDITOR || DEVELOPMENT_BUILD` ile korunmuş, bu değil.
- **Çözüm:** `HandleCheatCode` ve kuyruğu sil; DebugMenu'de "Trigger End Game" zaten var.
- **Atlarsak:** Kazara final; speedrun exploit'i.

### K4 ★ Final sinematiğinden sonra credits yerine açılış ekranı yükleniyor — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** sahne `:151203` `creditsSceneName: scn_PressAnyKeyMenu` (kod varsayılanı `scn_CreditsScene`, sahnede ezilmiş); `Interactables/EndGameButton.cs:19,89`. `scn_CreditsMenu` build'de ama hiçbir yerden yüklenmiyor.
- **Çözüm:** Alanı `scn_CreditsMenu` yap (ya da credits sahnesini build'den çıkar).
- **Atlarsak:** Ölü sahne + bozuk son.

### K5. Kayıt: ölüm ekranında çıkışta kayıt, atomik olmayan yazma, kalıcı `SaveBlocked` — Önem: Kesin düzelt (K1 açılınca) — Çaba: S — Güven: orta
- **Nerede:** `SaveManager.cs:131,139-156,104-113`, `Managers/PressureSystemManager.cs:124-141,315-319`, `SettingsData.cs:86-94` (`SettingsStore.Write` zaten temp+Replace ile atomik).
- **Sorun:** (a) Ölüm ekranında `isGameStarted` hâlâ true → Alt+F4 → `savedPressure=100` yazılır → Continue → ilk Update `TriggerGameOver()` → kırılamayan ölüm döngüsü. (b) `File.WriteAllText` doğrudan dosyaya; yarım JSON → `LoadGame` fırlatır → `SaveBlocked=true` → oturum boyunca kayıt yok. (c) Oyun içi kayıt noktası yok (yalnız quit/pause).
- **Çözüm:** Quit/pause kaydına `&& !GameManager.Instance.IsGameplayOver`; `SettingsStore.Write`'ı string alacak şekilde genelleştirip `SaveManager:131`'den çağır; `PasswordManager.ValidatePassword` başarı dalında tek `SaveGame()` kayıt noktası.
- **Atlarsak:** Ölüm döngüsü ve kaybolan oturumlar.

### K6. ISaveable kaydı Awake/OnEnable sırasına bağlı; ControllableLight hiç kaydolmuyor — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `SaveSystem/PlayerSaveHandler.cs:12-26`, `Managers/RoomManager.cs:58-71`, `PasswordManager.cs:73-87`, `PressureSystemManager.cs:80-93`, `GlobalEnemyManager.cs:28-41` (yalnız OnEnable, `Instance==null` ise sessizce atlar), `ChalkboardManager.cs:90-99` ve `UI/NotebookUI.cs:77-92` (çift kayıt — tuzağa daha önce düşülmüş), `Interactables/ControllableLight.cs:178-215` (ISaveable ama Register yok); sahne sırasında SaveManager 2024. nesne, 8 RoomManager/Pressure/GlobalEnemy ondan önce.
- **Sorun:** Işıkları kapat → çık → devam et → ışıklar sahne varsayılanına döner (bugün). Awake sırasına göre basınç/düşman cooldown/oda anonsları kaydedilmeyebilir.
- **Çözüm:** Kayıt defterini sil: Save/Load anında `FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>()`; 7 sınıftaki Register/Unregister bloklarını sil (~60 satır eksilir). Ya da en küçük: `SaveManager`'a `[DefaultExecutionOrder(-100)]` + ControllableLight'a Register.
- **Atlarsak:** Rastgele "kayıt beni başa attı" raporları.

### K7. Continue'da rastgele şifreler temizlenmiyor — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `Managers/PasswordManager.cs:89-92,143-174,181-218`, `Interactables/InteractableBook.cs:1111-1135`.
- **Sorun:** Start her yüklemede yeni kitaplara şifre atar; LoadData kayıttakileri geri yükler ama yenileri temizlemez → hiçbir yere gitmeyen sahte şifre sayfaları (K1 açılınca görünür olur).
- **Çözüm:** `LoadData`'da geri yüklemeden önce `foreach (var b in allBooksInLevel) b?.ClearPassword();`.

### K8. Yedi etkileşim ipucu çeviri tablosunda yok, "[Sol Tık]" sabit — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `EndGameButton.cs:34`, `BreakerBox.cs:203`, `RemotePowerLever.cs:107`, `InteractableHidingSpot.cs:138`, `InteractableMassSpectrometer.cs:594`, `InteractableDoor.cs:104`; tüketici `PlayerInteraction.cs:330`.
- **Sorun:** İngilizce oynayan oyuncu bu altı nesnede Türkçe yazı görür; tuş değişince ipucu değişmez.
- **Çözüm:** Metinler sekmesine 7 satır ekle, aktar (metin kuralı: Excel üzerinden); literal'i `[{input:Interact}] …` yaz.

### K9. Çeviri rehberi build kapısıyla çelişiyor — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `docs/LocalizationGuide.md` §2/§5 ("en.json'a ekleyin") ↔ `Editor/ContentWorkbook/ContentWorkbookProject.cs:388-418` (hash damgası; Import tabloları Excel'den yeniden yazar → elle eklenen çeviri silinir). Yaşandı (bellek notu).
- **Çözüm:** Rehberi "Metinler sekmesine ekle → Senzora > İçerik Tablosu > Oyuna Aktar" olarak yeniden yaz. Kapının kendisi doğru, **kaldırma**.

### K10. Şifreler ve tam stack trace Player.log'a yazılıyor — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `PasswordManager.cs:173` (`[Oyun Şifresi - RASTGELE KİTAP] …: {bookPass}`), `ProjectSettings.asset m_StackTraceTypes` hepsi full, 120 runtime `Debug.Log`.
- **Çözüm:** 173. satırı sil; Stack Trace → Log: None/ScriptOnly; runtime logları `[Conditional("UNITY_EDITOR")]` sarmalayıcıya al.

### K11. Ölü kod / yinelenen yardımcılar / dosya adı hijyeni — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `GameManager.cs:30` (`inputBuffer`), `:19-25,71,212-223` (`RefreshReferences` + Awake'te 4 boşa Find; tek çağıran editör), `GlobalSubtitleManager.cs:181-188`, `GameLocalization.cs:58-69` (SettingsSave.json'u ikinci kez parse), `NewEventSystem/Conditions/InputCondition.cs` ve `LookCondition.cs` (yalnız build dışı sahnelerde), `UI/SettingsUIManager.cs:28-37` + `SettingsManager.cs:143` (eski slider adaptörleri), `GuderianAI.cs:157-168` (erişilemeyen blok), `AdamAI.cs:189` (null kontrolünden önce deref), `InteractableMassSpectrometer.cs:2` (kullanılmayan `using System.Reflection`), `Actions/CustomMethodAction,.cs`, `NewEventSystem - Shortcut.lnk`, `NaughtyAttributes/Scripts/Test/` (oyuna derleniyor).
- **Çözüm:** Sil / yeniden adlandır. Eski sahneler silinince InputCondition/LookCondition/adaptörler de gider.

### Mimari değerlendirme (özet)
İyi olan: ayarlar katmanı atomik yazıyor ve eski kayıtları taşıyor; `Resources.Load`/JSON/dosya sınırları korumalı; statik `Instance`'lar `OnDestroy`'da temizleniyor, statik event abonelikleri dengeli, domain-reload-kapalı sıfırlamaları var; olay sistemi (ICondition push + `IsMet()` pull) küçük ve okunabilir; "Senzora/Doğrulama" harness'ı gerçek Play akışlarını sınadığı için değerli. En pahalı üç yapısal karar: (1) tek Assembly-CSharp içinde 45k satır ve 125-kenarlı god sınıflar (InteractableBook, NotebookWorkspaceController); (2) sahneye gömülü ana menü + sahneye gömülü yönetici bayrakları + ölümde tam sahne reload (K1 ve K4 doğrudan bunun sonucu: geliştirici aracının sahneye yazdığı değer build'e gidiyor); (3) Excel'i metin/altyazı/tuş/DialogAction için tek kaynak yapan kriptografik build kapısı (doğru, ama dört düzenleme yolunu birbirine bağlıyor; K9). **Olduğu gibi bırak:** 272 satıra sıkıştırılmış `SettingsMenuView`, NotebookWorkspaceController'ın 8 partial dosyası, ölümde tam sahne reload (durumu doğru sıfırlıyor; async/additive aylık iş), karma eski/yeni girdi (`activeInputHandler=2`), dil için index/code/enum üçlüsü, Unity Test Framework'e taşıma, asmdef bölme (god sınıflar bölünmeden anlamlı sınır çizilemez).

## 4.4 Kod: CPU sıcak yolları (gameplay, AI, etkileşim)

Ajanın genel tespiti: okunan script yollarında kare başı sabit iş toplamı küçük (HUD canvas ~0,5 ms, 50 kitap Animator'ı ~1 ms, prompt/GameInput/format çöpleri ~0,1–0,2 ms); bunlar taban yüksekliğidir, "kasma" değil. Script'ten gelebilecek takılmalar: GC (kare başı 1–6 KB çöp üreten yollar), kitap aç/kapa anındaki BakeMesh + MeshCollider cook (tek sefer, birkaç ms), tek seferlik senkronlar (TuringCrtInk ReadPixels, ilk TMP atlası, ölümde senkron `LoadScene`). Asıl kare süresi render tarafında (4.1).

### H1. HUD barları her kare 132 elemanlı overlay Canvas'ı yeniden batch'letiyor — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `Player/CharacterController.cs:600-607`, `Player/DynomaFlashLight.cs:63,126-134`, `Managers/PressureSystemManager.cs:148,190-197`; sahne `UI/Canvas` (ScreenSpaceOverlay, 132 RectTransform).
- **Sorun:** Her Update `Image.fillAmount` yazımı → Image dirty → kök canvas'ın 127 grafiği yeniden batch (≈0,3–0,8 ms/kare, fener açıkken garanti).
- **Çözüm:** `PROGRESSBARS` nesnesine bir `Canvas` bileşeni ekle (nested canvas) + fillAmount'ı yalnız |fark|>0,005 iken yaz.

### H2. Bakılan nesnenin ipucu metni her kare yeniden formatlanıp çevriliyor — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `Player/PlayerInteraction.cs:182,324-331`, `Interactables/InteractableBook.cs:550-559`, `Localization/GameLocalization.cs:97-112`, `LocalizedText.cs:88-194`, `InputHintText.cs:10`.
- **Sorun:** Kare başı 4 GetComponent, 3 Regex.Replace, string.Format, sözlük aramaları, ~0,5–1 KB çöp (kitaba bakmak en sık durum).
- **Çözüm:** Son prompt string'ini sakla; yalnız değişince `GameLocalization.Set`; kitapta `PuzzleBookIdentity`'yi Start'ta cache'le.

### H3. `GameInput.Pressed/Held` her çağrıda string birleştirip `FindAction` ile kültür-duyarlı arıyor — Önem: İyi olur — Çaba: S — Güven: orta
- **Nerede:** `Player/GameInput.cs:72-76`; çağıranlar: DynomaFlashLight:74, PlayerInteraction:336, NotebookUI:118-131, NotebookWorkspaceController:172-173, Turing makinesinde 8–10/kare (`InteractableTuringMachine.cs:456-504`), tahtada parça başına ~6 (`DraggableFormula.cs:189-224`).
- **Çözüm:** `GameInput.Action` içinde `Dictionary<string, InputAction>` cache; `Reset/ReloadBindings`'te temizle (3 satır).

### H4. ChalkboardTutorial.Update her kare LINQ ile defteri tarıyor — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `PuzzleSystems/Chalkboard/ChalkboardTutorial.cs:124-159` (`HasCollectedCards`, `HasStoredReading` her kare; `UpdateGuide` zaten 10 Hz).
- **Çözüm:** Mevcut `nextGuidePoll` zamanlayıcısını Update'in tamamına uygula.

### H5. Kara tahta scriptleri her kare hiyerarşi sorgusu ve ayırma yapıyor — Önem: İyi olur — Çaba: M — Güven: orta
- **Nerede:** `ChalkboardFreehandDrawing.cs:43` (`GetComponentsInChildren<DraggableFormula>` her kare), `DraggableFormula.cs:28-30,124-185`, `FormulaNode.cs:79-94,256-257,327-347` (bağ çizgisi değişmese de her kare yeniden yazılıyor).
- **Çözüm:** Düğüm dizisini Initialize'da cache'le; parça listesini ChalkboardManager'dan al; `DrawConnection`'ı yalnız konum/bağ değişince çalıştır.

### H6. Değer değişmese de her kare string/format ayırmaları — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `AdamAI.cs:74-79` (debug alanı), `PressureSystemManager.cs:201` (`$"{currentPressure:F0}%"`), `InteractableMassSpectrometer.cs:339,437,442`, `InteractableRetroOscilloscope.cs:268,419`, `FormulaNode.cs:46-47`.
- **Çözüm:** Son yazılan değeri sakla, yalnız değişince formatla.

### H7. WaveformGenerator kimse bakmasa da her kare 100 nokta + 200 Random — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `Interactables/WaveformGenerator.cs:40-79`; `InteractableOscilloscope.cs:344-347` (`inMachineMode` bayrağı hazır).
- **Çözüm:** `OnBecameVisible/Invisible` ya da yalnız `inMachineMode` iken enable.

### H8. TuringCrtInk: metin değişiminde CPU raster + 2×2048² Blit + mip üretimi, ilk açılışta ReadPixels — Önem: İyi olur — Çaba: M — Güven: orta
- **Nerede:** `Interactables/TuringCrtInk.cs:107-203`, `InteractableTuringMachine.MeasurementReader.cs:146-153,254-255`.
- **Sorun:** İlk okuyucu açılışında tek seferlik GPU stall; okuma sırasında saniyede 1–4 kez ~1 ms GPU; +~42 MB VRAM.
- **Çözüm:** Kabul edilebilir; istenirse `Graphics.CopyTexture` ile yalnız şerit bölgesi, `SetReaderDisplay`'i saniye değişince çağır.

### H9. Düşman AI'da korumasız singleton/Tag erişimi ve erişilemeyen blok — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `GuderianAI.cs:153-168` (153 döndüğü için 158-168 hiç çalışmaz; 158 null korumasız), `AdamAI.cs:189`.
- **Çözüm:** 157-168'i sil; Adam'da null kontrolünü `.transform`'dan önce yap.

### Düzeltmeye değmeyenler (ajan okuyup eledi)
DoFManager'ın 5 ışını, PlayerFootwork'ün ≤5 ışın + IK'si, 53 `InteractableBook.Update` / `BookPageBlockSurface.LateUpdate` erken çıkışları (toplam <0,1 ms), 25 DialogAction'ın her kare `ConditionsAreMet`'i (~5 µs), SanityGlitchAction/LookAction'ın Start'taki Find'ları (tek sefer), tahtadaki maskesiz RaycastAll'lar (yalnız tıklamada). Aranıp bulunmayanlar: kare başı Debug.Log/FindObjectsByType döngüsü yok; `.material` sızıntısı yok; statik event += / -= dengesi tam; sahne yeniden yüklemede sızıntı bulunmadı; Player.log'da 0 NRE.

## 4.5 UI, TextMeshPro, defter ve kitap sayfaları

Ajanın özeti: defter/kitap sistemi Ekim düzeltmesinden sonra iyi kurulmuş (UV üçgenleri ve barycentrik bağlar önbellekte, dönüşte yalnız hareket eden sayfa güncelleniyor, kapalı kitabın Canvas'ı devre dışı, LayoutGroup/ContentSizeFitter yok, InputBindingIcon olay güdümlü, `SettingsMenuView.Update` kare başına tarama yapmıyor — facts dosyasındaki "her kare LINQ" iddiası yanlıştı). Kalan gerçek takılma kaynakları: build'de boş başlayan dinamik font atlasları, sayfa/kart ilk yapımındaki iş yığını (AutoSize, hiyerarşi taramaları, dinamik LocalizedText, Color32 boxing, Mono ilk JIT), serbest çizimde O(n²).

### U1. Dinamik TMP fontları: build'de her glif ilk kullanımda rasterize ediliyor — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** `Assets/Sprites/UI/MainMenu/Font/static/DMSans-Medium SDF.asset:170,179` (Dynamic, Clear On Build 1, 0 glif, 4049 kerning), `Assets/Fonts/Caveat/Caveat-Regular SDF.asset:61,70` (Dynamic, 0 glif), `Assets/TextMesh Pro/Fonts/SpecialElite-Regular SDF.asset:216,225` (Dynamic; 102 kitap sırt/kapak etiketi), `LiberationSans SDF.asset` (Dynamic, 2 atlas dokusu, Clear 0, 259 glif), `TMP Settings` `m_GetFontFeaturesAtRuntime: 1`.
- **Sorun:** İlk defter/kitap kartı/sahne girişindeki 102 etiket → her yeni glif için FreeType raster + SDF + 1024² atlas `Apply` + kerning sorgusu. `docs/BookPerformanceAudit.md`'deki 52–233 ms "ilk font hazırlığı" takılmasının ana kaynağı; build'de editörden kötü.
- **Çözüm:** Font Asset Creator ile DM Sans Medium, Caveat, Special Elite ve LiberationSans'ı **Static** atlasa çevir (karakter listesi GameContent.xlsx + UI metinleri, Türkçe tam küme + semboller ≈120 glif, 1024²), yalnız Fallback dynamic kalsın. Sıfır kodlu tembel seçenek: oyunu editörde bir kez baştan sona gezip bu 4 asset'te Clear Dynamic Data On Build'i kapatmak.
- **Atlarsak:** İlk defter/kitap açılışı 50–200 ms donar; yeni harf/sembol her seferinde küçük takılma.

### U2. Serbest kalem çizgisi her noktada baştan üretiliyor (O(n²)) — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `UI/NotebookFreehandDrawer.cs:39` (`PointsPerGraphic = 4000`), `:145-149`, `:294-317`; `NotebookWorkspaceController.cs:242,495-507` (`Changed` → `RefreshBookmarks` string birleştirme).
- **Çözüm:** `PointsPerGraphic` → 256; `Changed`'ı yalnız `EndStroke/Erase`'de tetikle.

### U3. Sabit içerikli metinlerde AutoSize açık — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `UI/BookPageBlockSurface.cs:398,405,482`, `NotebookWorkspaceController.Appearance.cs:204`, `NotebookDraggableBlock.cs:169,201`, `ChalkboardDocuments.cs:232`.
- **Sorun:** AutoSize sığana kadar 4–10 yerleşim geçişi; sayfa başına ~20–25 metin → her yeni sayfa/kart 100–250 geçiş, hep aynı sonuç.
- **Çözüm:** Kart tipi başına sabit punto + Overflow/Ellipsis; `BookPageBlockSurface.cs:403-408` autosize zorlama döngüsünü kaldır.

### U4. Basılı mürekkep vertex anahtarı `Color32` ile boxing yapıyor — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `UI/BookPagePrintedGraphic.cs:21,69,107-109` (`Dictionary<(Vector3, Vector4, Color32), int>`).
- **Çözüm:** Anahtarı `(Vector3, Vector4, int)` yap (renk 4 byte → int).

### U5. Kitap kartı kurulumunda bileşen taraması ve AddComponent yığını — Önem: Düzeltilmeli — Çaba: M — Güven: orta
- **Nerede:** `BookPageBlockSurface.cs:379-436` (kart başına 6× GetComponentsInChildren, her Graphic'e AddComponent), `NotebookDraggableBlock.cs:73-275`, `NotebookWorkspaceController.Appearance.cs:174-239`, `GameLocalization.cs:100-112` (her `Set` → AddComponent<LocalizedText>).
- **Çözüm:** Referansları alan olarak tut; döngüleri tek `GetComponentsInChildren<Graphic>`'e indir; yükleme ekranı arkasında bir kitapta `PreparePresentation()` + defteri Open/Close ile ısıt (JIT + atlas).

### U6. Defter her sekme/sayfa/bırakmada bütün kartları yıkıp yeniden kuruyor — Önem: İyi olur — Çaba: M — Güven: orta
- **Nerede:** `NotebookWorkspaceController.cs:289-318,394-399`, `.Information.cs:71-74`, `.Sections.cs:81-158`.
- **Çözüm:** Küçük adım: `ShowNotebookView`'da yalnız görünür panelin Refresh'i; büyük adım: kart havuzu.

### U7. Kara tahta ve belge panosu kare başı işleri — Önem: İyi olur — Çaba: S — Güven: orta
- **Nerede:** `ChalkboardFreehandDrawing.cs:43-45` (H5 ile aynı), `ChalkboardDocuments.cs:198-204` + `.Research.cs:224-240` (ipler her kare `sizeDelta` → canvas dirty).
- **Çözüm:** `UpdateResearchThreads`'i LateUpdate'ten çıkarıp drag/drop olaylarına bağla.

### U8. 127 gereksiz raycastTarget, 102 kitap etiketinde `isTextObjectScaleStatic` kapalı, iki SettingsMenu örneği, kontrol ikonları merkez çapalı — Önem: İyi olur — Çaba: S — Güven: orta
- **Nerede:** sahne `UI/Canvas` (78 Image + 49 TMP raycastTarget=1, 10 Button); `Editor/ResearchTrialBookTitles.cs:199-201,271-272` (etiket üretici); 2× `Resources/Settings/SettingsMenu.prefab` örneği (`SettingsMenuView.cs:43` Awake'te ~150 nesne kuruyor); `UI/Canvas/ControlsPanel/*/Icon*` merkez çapa ±800 px (16:10/4:3/21:9'da kayık — bellek notundaki bozukluk), `ControlUIManager.cs:196-200`.
- **Çözüm:** raycastTarget'ları kapat (`Panel()/Paper()` yardımcılarında varsayılan false); üreticide `isTextObjectScaleStatic = true`; `EnsureBuilt`'i ilk OnEnable'a al, fazla prefab örneğini sil; ControlsPanel satırlarını kenar çapasına al.

## 4.6 Fizik, AI, ses, cutscene

Ajanın özeti: fizik bu oyunda yalnız sorgu motoru (0 FixedUpdate, 6 kinematik Rigidbody, ~350 statik collider) — ucuz, dokunma; kitap MeshCollider'ları kapalıyken boş (`m_Mesh: 0`), açılınca BakeMesh ile dolduruluyor (doğru). AI CPU açısından sorunsuz; sorunlar davranışsal. Ses CPU bedeli ihmal edilebilir; sorunlar yükleme tipi, 3D mesafe ve Guderian yığını.

### F1 ★ Guderian arama sesi: 47,7 s stereo loop üstüne aynı klip her devriye noktasında PlayOneShot — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** `EnemyAI/GuderianAI.cs:449` (loop fade-in), `:468-477` (`PlayOneShot(searchHumSound)` — `rummageSounds` hiç kullanılmıyor); klip `Sounds/Sound Fx/Guderian/478862__ranne_madsen__tiroir-fouille.wav` (47,7 s, stereo, 8,4 MB PCM).
- **Sorun:** 50 s'lik aramada her 3–6 s yeni 47 s'lik kopya → 8–12 üst üste klip, her biri 32 gerçek voice'tan birini yer; ExitSequence yalnız loop'u keser, kopyalar 47 s daha çalar; stereo klip 3D kaynakta konumlanamaz.
- **Çözüm:** 470'teki `PlayOneShot(searchHumSound)` yerine `rummageSounds[Random.Range(...)]` kısa klip (ya da satırı sil); ExitSequence'te `audioSource.Stop()`; klipte Force To Mono.
- **Atlarsak:** Guderian set-piece'i her oyunda bozuk ses yığınıyla biter, başka SFX'ler sanal voice'a düşer.

### F2. Trigger hacimleri etkileşim ve Lees görüş ışınlarını kesiyor — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `Player/PlayerInteraction.cs:147` (Raycast, trigger parametresi yok; `DynamicsManager m_QueriesHitTriggers: 1` ★), `EnemyAI/LeesEnemyAI.cs:481-488` (`QueryTriggerInteraction.Collide`), `:814`; sahnede 11 trigger BoxCollider Default katmanında (EVENTS/*, Tutorial trigger'ları, Corridor_SafeZone, ReactionZone).
- **Sorun:** Event kutusunun dışından içindeki nesneye bakınca ilk hit trigger → prompt yok, tıklanamaz; Lees için kamera-Lees arasındaki kutu → "görünmüyor" → haksız ölüm.
- **Çözüm:** Üç raycast'e `QueryTriggerInteraction.Ignore` (Lees collider'ı trigger değil). Alternatif: 11 kutuyu Ignore Raycast katmanına al.

### F3. Üç uzun stereo klip Decompress On Load + senkron decode (bkz. B1) — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `Senzora_Music_mixdown.wav` (187 s, PlayOnAwake loop), `850444__…ambience-music-loop.wav` (96 s, 48 kHz/24-bit, intro timeline'da Play → cutscene ilk karesinde decode), `478862…tiroir-fouille.wav`; 83 klibin 83'ü `loadInBackground: 0`.
- **Çözüm:** Bu 3 klip Streaming + Load In Background; ambiyansı 44,1 kHz/16-bit'e indir (27,6 MB → ~9 MB).

### F4. 3D kaynaklar: MaxDistance 500, logaritmik rolloff, Doppler açık — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** 105 AudioSource: `rolloffMode: 0`, `MaxDistance: 500`, `DopplerLevel: 1`; 13 3D kaynakta minDistance 2,4–41,6 (Lees_Dummy 41,6); `AudioManager Doppler Factor: 1`.
- **Sorun:** Laboratuvar ~40–60 m; kapı/Guderian/Lees sesleri odalar arası neredeyse aynı güçte → "nereden geliyor" ipucu kaybolur; sprint'te pitch kayması.
- **Çözüm:** Project Settings → Audio → Doppler Factor 0; 13 3D kaynakta minDistance 1–2 m, MaxDistance 20–25 m, Linear/Custom rolloff.

### F5. CharacterController step offset ölçekle 0,94 m'ye çıkıyor — Önem: Düzeltilmeli — Çaba: S — Güven: orta (sahnede doğrulanmadı)
- **Nerede:** sahne `Senekas_Player` CharacterController (`m_StepOffset 0.25`, `m_SkinWidth 0.02`, Transform ölçeği 3,75 → dünya 0,94 m / 7,5 cm); `CharacterController.cs:114`.
- **Sorun:** 0,9 m'ye kadar her engele "basamak" gibi tırmanır (masa 0,75 m) → trigger/NavMesh alanı dışına çıkma, sequence-break.
- **Çözüm:** `m_StepOffset` 0.08, `m_SkinWidth` 0.006; Physics Debugger'da kapsülü bir kez doğrula.

### F6. NavMesh eski sahne klasöründen, yalnız bina kabuğu bake'li — Önem: İyi olur — Çaba: M — Güven: orta
- **Nerede:** `ENVIRONMENT/SciencInstute` NavMeshSurface → `Assets/Scenes/scn_labratory/NavMesh-SciencInstute.asset` (22 Eyl; `collectObjects: Children`, `useGeometry: RenderMeshes`); `GuderianAI.cs:536-567` (warp başarısızsa transform ile duvardan yürür).
- **Çözüm:** Canlı sahnede Bake, veriyi sahnenin klasörüne kaydet (R3 ile aynı temizlik), `collectObjects: All` + mobilyalara NavMeshModifier.

### F7. Megaphone sistemi kapalı: oyunda hiç seslendirme çalmıyor, 14 MB monolog build'de — Önem: İyi olur (karar) — Çaba: S — Güven: orta
- **Nerede:** sahne `MANAGERS/MegaphoneManager` inaktif (ajan tespiti; bu oturumda nesne adıyla doğrulanamadı); `RoomManager.cs:125` → `DialogueEvent.Play` sessiz; `MainSubtitles.asset` 99 girişte `voiceClip` 0; 36 DialogAction'da klip 0.
- **Çözüm:** Karar ver: ya aç (4 odada speaker konumları hazır) ya da MegaphoneSystem + `Sounds/Monologs/*` + RoomManager alanlarını sil.

### F8. VideoPlayer'da Prepare yok; 4 player tek RenderTexture; 40 MB klip ölü — Önem: İyi olur — Çaba: S — Güven: orta
- **Nerede:** `Managers/PlayVideoOnReactivate.cs:11-15`; final odası `DM_FinalRoomCurtain` (cutscenefinalavideo.mov), `DM_FinalRoomCurtain (1)` (disabled, WhatsApp mp4 40 MB), `Screen`, `SM_FinalroomCamera` — aynı RT'ye yazıyor.
- **Çözüm:** `armed` adımında `Prepare()`; ölü iki player'ı ve referansları sil (40 MB build'den düşer).

### F9. PhantomSpawnAction 0,5 s'lik korkutma için skinned karakter Instantiate/Destroy — Önem: İyi olur — Çaba: S — Güven: orta
- **Nerede:** `Actions/PhantomSpawnAction.cs:40,86` (5 örnek, `Lees_Dummy.prefab`); sahnede zaten 2 inaktif Lees_Dummy; `SettingsManager.PlayEffectAtPoint` her ses için yeni GameObject.
- **Çözüm:** Sahnedeki inaktif dummy'yi SetActive ile kullan; tek yeniden kullanılan AudioSource.

### F10. Ölü/etkisiz bileşenler ve kopyalar — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `Senekas_Player/BasicRigidBodyPush` (`canPush 0`), klipsiz PlayOnAwake AudioSource ×63 (53 kitap dahil), `OsiloskopTennis/Stable Hum` + `Static Noise` loop kaynaklarına hiçbir script dokunmuyor (17 MB klip build'de), inaktif düşman kopyaları (`ENEMIES/Guderian_Enemy/Guderian_Enemy`, `Adam_Enemy/Guderian_Enemy (1)`, `Senekas_Player/Guderian_Dummy` — 18 skinned renderer), intro timeline'ın iki sahibi (`IntroCutsceneManager.cs:13-38` ↔ `InGameMenuController.cs:122-140`).
- **Çözüm:** Sil; kitap prefab'ında PlayOnAwake kapat; kontrol kilidini tek yere topla.

## 4.7 Model, ses, video, font: import ayarları ve yükleme

### B1. Tüm sesler "Decompress On Load" + Vorbis %100, müzik ve ambiyans dahil — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** Build'deki 73 klibin tamamı: `loadType: 0`, `compressionFormat: 1`, `quality: 1`. Örnek: `Assets/Sounds/Senzora_Music_mixdown.wav` (31,5 MB, 187 s, stereo), `850444__evanboyerman__uneasy-horror-ambience-music-loop.wav` (26 MB), `Sound Fx/Oscilloscope/Oscilloscope_Tone.wav` (50 s), `Ion_Beam_Loop.wav` (49 s), 13 monolog (13–27 s).
- **Bu ne:** Decompress On Load klibi sahne açılırken çözüp ham PCM olarak RAM'de tutar (stereo 44,1 kHz ≈ 10 MB/dk/kanal). Müzik tek başına ≈ 32 MB, ambiyans ≈ 28 MB, osiloskop tonları ≈ 17 MB; toplam ≈ 110 MB ham ses RAM'de ve sahne yüklemesinde ≈ 1–2 s çözme süresi. Kalite %100 Vorbis dosya boyutunu %70'e göre ~2 kat şişirir, kulakla fark edilmez.
- **Çözüm:** Kategoriye göre tek seferlik toplu ayar (AssetPostprocessor ya da seçip Inspector'dan): müzik/ambiyans/uzun loop → **Streaming**; monolog/diyalog → **Compressed In Memory**, kalite 0,6–0,7; <2 s SFX → Decompress On Load kalsın; 3D konumlu SFX'lere **Force To Mono**.
- **Atlarsak:** ≈ 100 MB fazla RAM ve sahne yüklemesinde ekstra çözme süresi; "yüklemede donma" hissine katkı.

### B2. 63 FBX'in hepsinde mesh compression kapalı, blend shape/animasyon/kamera/ışık import açık, statik eşyalar humanoid/generic rig — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** Tüm model `.meta`'ları (`meshCompression: 0`, `importBlendShapes: 1`, `importAnimation: 1`, `importCameras: 1`, `importLights: 1`, `animationType: 2/3`). Örn. `Assets/Models/NewWorkingTable/ramiDesk/desk.fbx`, `Models/TuringMachine.fbx`, `Models/SciencInstute.fbx` (35 MB, generic rig).
- **Sorun:** Statik eşyalara rig+Avatar üretiliyor (import süresi, gereksiz Animator/Avatar verisi), FBX içindeki kamera/ışıklar sahneye sızabiliyor, build'de mesh verisi sıkıştırılmıyor. Tek tek bakıldığında küçük; toplamda 60+ dosya.
- **Çözüm:** Statik prop'lar için `animationType: None`, `importAnimation: 0`, `importBlendShapes: 0`, `importCameras/Lights: 0`; mesh compression "Medium" (kapı/kitap gibi yakın nesnelerde "Low"). Bir AssetPostprocessor ile klasör bazında uygulanır.
- **Atlarsak:** Ölçülebilir FPS kaybı yok; build 10–30 MB ve import süresi fazla. Düşük öncelik ama ucuz.

### B3. `SciencInstute.fbx`: 35 MB tek bina, 130 alt mesh, LOD yok, 6 non-convex MeshCollider — Önem: Düzeltilmeli — Çaba: L — Güven: orta
- **Nerede:** `Assets/Models/SciencInstute.fbx`; sahnede 130 MeshFilter bu dosyadan; `Models/Revisyons/*` ek revizyon parçaları.
- **Sorun:** Bina alt meshlere bölünmüş olduğu için frustum culling çalışıyor, ama occlusion verisi olmadığı için duvar arkasındaki odalar da çiziliyor (bkz. render bölümü). MeshCollider'lar görsel mesh üzerinden; yürünmeyen yüzeyler de fizik hesabında.
- **Çözüm:** Modeli yeniden yapmak yerine: occlusion culling bake (ucuz, büyük kazanç) + collider'ları basitleştirilmiş ayrı mesh/box'lara taşı. LOD üretimi bu oyunda (kapalı mekân, kısa mesafe) gerekli değil.
- **Atlarsak:** Occlusion olmadan GPU boşa odalar çizer. "Modeli baştan yap" kısmından **vazgeçilebilir**.

### B4. Build'de 40 MB "WhatsApp Video 2026-01-02….mp4" ve 12 MB .mov; Assets içinde 323 MB ekran kaydı — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `Assets/Videos/WhatsApp Video 2026-01-02 at 00.05.15.mp4` (sahnedeki VideoPlayer'dan referanslı), `Assets/Videos/cutscenefinalavideo.mov`, `Assets/Videos/ScreenRecords/Movie_017…026.mp4`.
- **Sorun:** Ekran kayıtları build'e girmiyor ama her import'ta VideoClip importer tarafından işleniyor ve 323 MB git'e giriyor. `.mov` ve telefon kaydı mp4'ü Unity'nin H.264 yoluna bağımlı; "transcode" açıksa import dakikalar sürer.
- **Çözüm:** `ScreenRecords` klasörünü Assets dışına (örn. `outputs/`) taşı; cutscene videolarını tek codec'e (H.264 1080p, transcode kapalı, uygun bitrate) getir ve dosya adlarını düzelt.
- **Atlarsak:** Repo ve import süresi şişer; build'e etkisi 50 MB ile sınırlı.

### B5. TMP ana fontu dinamik atlas; Türkçe karakterler ilk görüldüğünde rasterize ediliyor — Önem: Düzeltilmeli — Çaba: S — Güven: orta
- **Nerede:** `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` (`atlasPopulationMode: 1`, 1024², 4,3 MB), `Assets/Fonts/Caveat/Caveat-Regular SDF.asset` (dinamik, atlas 0×0), `TMP Settings.asset` (`m_GetFontFeaturesAtRuntime: 1`). Önceki ölçüm: ilk font/mesh hazırlığı 52–233 ms (`docs/BookPerformanceAudit.md`).
- **Bu ne:** Dinamik atlas, oyun içinde ilk kez görülen her glifi CPU'da SDF'e çevirip atlasa yazar; editörde biriken glifler "Clear Dynamic Data On Build" kapalıysa build'e de gider.
- **Sorun:** Her yeni metin (kitap sayfası, altyazı) ilk gösterimde ms'lik takılma; kitap açılışlarında hissedilen "ilk açılış takılması"nın parçası.
- **Çözüm:** Metinler Excel'de olduğu için karakter kümesi bilinir: ana fontları **Static** atlas (Türkçe+İngilizce tam küme) olarak bake'le, yalnız fallback dinamik kalsın ve "Clear Dynamic Data On Build" aç.
- **Atlarsak:** Küçük ama tekrar eden takılmalar; özellikle ilk 5 dakikada.

### B6. 75 kullanılmayan DMSans .ttf ve kullanılmayan 415 texture/50 model import ediliyor — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `Assets/Sprites/UI/MainMenu/Font/static/` (72 ttf kullanılmıyor), `Assets/Sprites/UI/MainMenu/ReferansGorsel.png` (23 MB) ve `IdarelikBackground.png` (23 MB), `Assets/StarterAssets/ThirdPersonController/` (84 MB demo; yalnız MainCamera prefab'ı, Armature.fbx ve 12 adım sesi kullanımda), `Assets/Models/NewWorkingTable/*` ham Sketchfab paketleri (400 MB'ın büyük kısmı kullanılmıyor), `Assets/Screenshots/` (40 QA görüntüsü, 24 MB).
- **Sorun:** Build'e girmiyorlar; ama Library'yi, import süresini ve repoyu büyütüyor, "ne kullanılıyor" sorusunu bulanıklaştırıyor.
- **Çözüm:** Kullanım kanıtı olmayanları (GUID'leri hiçbir sahne/prefab/materyalde geçmeyen) silmek ya da Assets dışına taşımak. `asset_audit_out2.txt` içindeki "NOT-REACHED" listesi hazır.
- **Atlarsak:** Sadece geliştirme konforu.

## 4.8 Paketler ve proje ayarları

### C1. HDRP yanında URP + PostProcessing v2 kurulu, HDRP "debug variant stripping" kapalı — Önem: Kesin düzelt — Çaba: S — Güven: yüksek
- **Nerede:** `Packages/manifest.json` (`com.unity.render-pipelines.universal`, `universal-config`, `com.unity.postprocessing`), `Assets/UniversalRenderPipelineGlobalSettings.asset`, `Assets/StarterAssets/Environment/RenderPipelineProfiles/StarterAssetsURPAsset.asset`; `Assets/Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset` → `m_StripDebugVariants: 0`, `m_ExportShaderVariants: 1`.
- **Sorun:** URP paketi derlenip import ediliyor (domain reload, Library), URP global settings asset'i dependency taramasında build'e giriyor; HDRP debug görüntüleme varyantları build'de derleniyor → **build süresi** ve shader belleği artıyor; "export shader variants" her build'de log dosyası yazıyor.
- **Çözüm:** URP, universal-config, postprocessing paketlerini kaldır; `Assets/UniversalRenderPipelineGlobalSettings.asset` ve StarterAssets URP asset'ini sil; HDRP Global Settings'te **Strip Debug Variants = açık**, Export Shader Variants = kapalı.
- **Atlarsak:** Her build dakikalarca uzun; editörde gereksiz derleme.

### C2. Kullanılmayan 9 paket ve ~12 modül — Önem: Düzeltilmeli — Çaba: S — Güven: yüksek
- **Nerede:** `Packages/manifest.json`: visualscripting (kodda 2 `using Unity.VisualScripting`, ScriptMachine yok → muhtemelen IDE'nin otomatik eklediği using), animation.rigging (0 kullanım), recorder, package-validation-suite (preview), multiplayer.center, collab-proxy, feature.development, test-framework.performance, searcher. Modüller: cloth, terrain, terrainphysics, tilemap, vehicles, wind, xr, vr, androidjni, unityanalytics, unitywebrequest*, adaptiveperformance.
- **Sorun:** Her paket domain reload ve derleme süresine, bazıları (visualscripting, rigging) build'e DLL ve shader ekler.
- **Çözüm:** İki `using` satırını silip paketleri Package Manager'dan kaldır; gereksiz modülleri "Built-in packages" listesinden kapat. 30 dakika.
- **Atlarsak:** Yalnız geliştirme ve build süresi.

### C3. Proje varsayılan kalitesi "High Fidelity", oyun içi varsayılan "Balanced": editör ile build farklı davranıyor — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `ProjectSettings/QualitySettings.asset` `m_CurrentQuality: 0` (High Fidelity: gölge atlası 8192², 32 bit, `colorBufferFormat: 48`); buna karşılık `Assets/Scripts/Managers/SettingsData.cs:34` `qualityIndex = 1` ve `SettingsManager.ApplyAllSettings()` oyun başında `QualitySettings.SetQualityLevel(1)` → oyuncu aslında **Balanced** görüyor (doğru seçim).
- **Sorun:** Editörde Play ve ilk kareler High Fidelity ile çalışır; editördeki ölçümler build'i temsil etmez; 8192² 32-bit gölge atlası editörde 256 MB VRAM tutar. Oyuncu için sorun yok.
- **Çözüm:** Proje varsayılanını da Balanced yap (tek tık). High Fidelity ayar menüsünde kalsın; atlasını 4096'ya indir.
- **Atlarsak:** Yalnız editör/ölçüm tutarsızlığı.

### C4. Shader variant collection yok → ilk kullanımda shader derleme takılması — Önem: Düzeltilmeli — Çaba: M — Güven: orta
- **Nerede:** Projede `.shadervariants` dosyası yok; `GraphicsSettings.m_PreloadedShaders: []`.
- **Bu ne:** HDRP Lit + volumetrik + decal + SSS kombinasyonlarında ilk kez görülen varyant oyun içinde derlenir (DX11'de onlarca ms).
- **Sorun:** Oyunda ilk kez bir odaya girince, ilk kitap açılınca, ilk jumpscare'de tek karelik büyük takılmalar.
- **Çözüm:** Editörde oyunu baştan sona oynayıp (mevcut `Senzora/Doğrulama` Play testleri bunu zaten yapıyor) Graphics Settings → "Save to asset" ile ShaderVariantCollection üret, `m_PreloadedShaders`'a ekle; açılış ekranında `WarmUp()`.
- **Atlarsak:** "İlk kez" takılmaları kalır.

### C5. Mono + Low stripping + tüm log tipleri full stack trace — Önem: İyi olur — Çaba: M — Güven: orta
- **Nerede:** `ProjectSettings/ProjectSettings.asset`: `scriptingBackend` Standalone için ayarsız (Mono), `managedStrippingLevel` varsayılan (Low), `m_StackTraceTypes: 01…` (tüm log tipleri full).
- **Sorun:** Mono, C# ağırlıklı yollarda (kitap mürekkep projeksiyonu, 68 koşul tarama) IL2CPP'den yavaş; full stack trace her `Debug.Log` çağrısında (runtime'da 120 çağrı noktası) yığın yakalar. IL2CPP build süresini uzatır (+5–10 dk) ve C++ toolchain ister.
- **Çözüm:** Önce `Debug.Log`'ları build'de kapat (bir `Conditional("UNITY_EDITOR")` sarmalayıcı ya da `Debug.unityLogger.logEnabled=false`), stack trace'i "ScriptOnly"a indir. IL2CPP'yi yalnız Steam sürüm build'inde dene; ölçüp karar ver.
- **Atlarsak:** CPU'da küçük kayıp; log çağrıları çöp üretir.

## 4.9 Repo ve proje hijyeni

### D1. `.git` 11 GB, LFS yok, 6,1 GB ikili dosya doğrudan git'te — Önem: Düzeltilmeli — Çaba: L (tam) / S (ileriye dönük) — Güven: yüksek
- **Nerede:** `git count-objects`: pack 9,16 GB + 933 MB çöp; `.gitattributes` yalnız satır sonu kuralı; en büyük blob 58 MB (Lees normal map). 6,7 MB sahne son 60 commit'in 8'inde yeniden yazılmış.
- **Sorun:** Clone/fetch/checkout dakikalar; GitHub 100 MB dosya sınırına yakın dosyalar var; iki worktree (Atomb / Atomb-Osman) arasında senkron maliyeti.
- **Çözüm (ucuz):** Bugünden itibaren LFS: `*.png *.jpg *.tga *.psd *.exr *.fbx *.wav *.mp3 *.ogg *.mp4 *.mov` için `git lfs track`; `git gc --prune` ile 933 MB çöpü temizle; ScreenRecords/Screenshots'ı repo dışına al. **Geçmişi yeniden yazmaktan (git lfs migrate) vazgeç**: iki branch, iki worktree ve GitHub Desktop akışı için risk > fayda.
- **Atlarsak:** Büyümeye devam eder; işlev kaybı yok.

### D2. Assets içinde çöp: kurtarma sahneleri, kısayol dosyası, virgüllü dosya adı, kopya sahneler — Önem: İyi olur — Çaba: S — Güven: yüksek
- **Nerede:** `Assets/_Recovery/0.unity`, `0 (1).unity`; `Assets/Scripts/NewEventSystem - Shortcut.lnk`; `Assets/Scripts/NewEventSystem/Actions/CustomMethodAction,.cs`; `Assets/Scenes/MainMenu.unity` ≈ `scn_MainMenu.unity`, `OutdoorsScene.unity` = `scn_OutdoorsScene.unity`, `scn_PuzzleWorkshop.unity` ≈ `scn_SynthesisBlockout.unity`; `scn_labratory.unity` (6,1 MB) ve `scn_labratoryfast.unity` (5,5 MB) eski kopyalar (yalnız `scn_labratory/` lighting klasörü canlı bake verisi olarak kullanılıyor — bkz. render bölümü); `Assets/Models/lambdeneme.prefab`, `DenemeLamb.mat`, `New Material.mat`; `Assets/Shaders/New Shader Graph 1.*`, `VertexTry.*`; `Assets/NaughtyAttributes/Scripts/Test/` (oyun assembly'sine derlenen ~30 test MonoBehaviour'u); `Assets/Materials/pexels-resourceboy-18541807.jpg`.
- **Sorun:** Karışıklık ve yanlış dosyada çalışma riski (iki MainMenu). Test MonoBehaviour'ları build'e giriyor.
- **Çözüm:** Silmeden önce GUID araması (bu raporun listesi hazır); `scn_labratory.unity` silinirse lighting klasörü ve `LightingData.asset` **korunmalı** (ana sahne ona bağlı) ya da önce ana sahne için yeniden bake alınmalı.
- **Atlarsak:** Zarar yok, kafa karışıklığı sürer.

### D3. Kendi kodu için asmdef yok; 59 editör "doğrulama" script'i aynı derlemede — Önem: İyi olur — Çaba: M — Güven: yüksek
- **Nerede:** `Assets/Scripts` (tek Assembly-CSharp, 45 bin satır) + `Assets/Scripts/Editor` (Assembly-CSharp-Editor); yalnız NaughtyAttributes asmdef'li.
- **Sorun:** Her C# değişikliğinde tüm oyun kodu ve tüm editör araçları yeniden derlenir (Unity'nin bildirdiği 30–60 s domain reload döngüsü); editör araçları oyun koduna sıkı bağlı.
- **Çözüm:** Şimdilik dokunma. Temizlik sonrası, kod yerleşince 3–4 asmdef (Core, Gameplay, UI, Editor) eklenir. Önce temizlik, sonra bölme.
- **Atlarsak:** Yalnız iterasyon hızı.

## 5. Öncelik matrisi

Etiketler: **S** = saatler, **M** = 1–2 gün, **L** = daha uzun. "Kazanç" tahminidir; ölçümle doğrulanır (bkz. 7).

### Kesinlikle düzelt (oyuncu görür ya da en büyük kazanç)
| # | Ne | Çaba | Kazanç |
|---|---|---|---|
| K1 | Build sahnesinde `sessionOnly` → 0, Setup/Checks satırlarını sil | S | Kayıt çalışır |
| K2 | `StartJumpscare` içindeki ikinci `TryBeginDeath` kapısını kaldır | S | Adam/Guderian ölümleri soft-lock yapmaz |
| K5 | Ölüm ekranında kaydetme, atomik yazma, kayıt noktası | S | Ölüm döngüsü/bozuk kayıt kalmaz |
| F1 | Guderian `PlayOneShot(searchHumSound)` → kısa rummage klip | S | Ses yığını biter |
| A1 | Materyallerde eski shader slotlarını temizle (script) | S | −683 MB VRAM, −0,9 GB disk |
| A3 | 82 materyalde Displacement Mode → None | S | GPU −0,5–1,5 ms, −220 MB VRAM |
| A2 | Texture streaming + ikincil haritalar 1024 | S | −0,5–1 GB VRAM, kısa yükleme |
| R1 | Lamba prefab'ı: cached shadows, Non-Lightmapped Only, 512, dimmer 1 | S | En büyük GPU kazancı |
| R2 | Occlusion culling bake | S | Duvar arkası çizim biter |
| B1/F3 | Ses import: müzik/ambiyans Streaming, kalite 0,7, mono | S | −100 MB RAM, intro hitch yok |
| U1 | TMP fontları statik atlas | S | İlk açılış 50–200 ms takılması biter |
| C1 | URP/PPv2 kaldır, Strip Debug Variants aç | S | Build süresi, shader belleği |

### Düzeltilmeli (belirgin kazanç, düşük risk)
R3 yeniden bake + eski sahne klasörlerinden kurtulma · R4 volumetrik fog Medium, Motion Blur/Contact Shadows kapalı, Lit mode Deferred Only · R5 TAA + near clip · R6 Animator culling · K3 "osm" kodu sil · K4 credits sahne adı · K6 ISaveable kaydı · K7 Continue'da şifre temizliği · K8 7 ipucu çevirisi · K9 çeviri rehberi · H1 HUD nested canvas · H2 prompt cache · U2 freehand 256 nokta · U3 AutoSize kapat · U4 Color32 anahtarı · U5 kart kurulum taramaları + ısıtma · F2 raycast'lerde trigger yoksay · F4 3D ses mesafe/Doppler · F5 step offset · A4 kitap kapakları 1024 · B2 model import ayarları · B4 videolar · C4 shader variant collection · C2 paket/modül temizliği · A5 kaynak texture'ları 2048'e küçült · D1 LFS ileriye dönük + gc.

### İyi olur (küçük, fırsat buldukça)
A6 sprite atlas · A7 mipmap/NPOT · B5 (U1 ile aynı) · B6 kullanılmayan asset'ler · C3 proje varsayılan kalitesi · C5 Debug.Log/stack trace/IL2CPP denemesi · R7 kalıntı shader'lar · H3–H9 · U6–U8 · F6–F10 · K10–K11 · D2 çöp dosyalar · D3 asmdef (en son).

### Vazgeç / şimdi yapma (maliyet > fayda)
| Ne | Neden |
|---|---|
| Git geçmişini LFS'e taşımak (`git lfs migrate`) | İki branch + iki worktree + GitHub Desktop akışı; geçmiş yeniden yazımı herkesin clone'unu bozar. LFS'i yalnız yeni dosyalar için aç. |
| `SciencInstute.fbx`'i yeniden modellemek / LOD | Kapalı mekân, kısa mesafe; occlusion bake aynı kazancı kod/model değişmeden verir. |
| URP'ye geçiş | 138 Lit materyal, shader graph'lar, IES ışıklar, volume profilleri yeniden kurulur; HDRP ayarlarla hedefe yetişir. |
| 53 kitabı prefab'a çevirmek | Sahne boyutu ve toplu düzenleme için güzel ama davranış değişmeden önce yapılırsa iki kez yapılır; Etap 5. |
| Tek sahneyi additive sahnelere bölmek | Kayıt, olay sistemi, testler etkilenir; aylık iş. Texture streaming + occlusion aynı belleği/GPU'yu büyük ölçüde kurtarır. |
| IL2CPP'ye ölçmeden geçmek | Build +5–10 dk, toolchain; önce Mono'da GC/JIT kaynaklarını kapat, sonra ölçüp karar ver. |
| asmdef bölmesi, Unity Test Framework'e taşıma | God sınıflar bölünmeden sınır çizilemez; mevcut Doğrulama menüleri işi görüyor. |
| Olay sistemini / Excel build kapısını yeniden yazmak | Her ikisi doğru çalışıyor; sorun dokümantasyon ve 1–2 yardımcı menü (K9, ContentWorkbook Kontroller yenileme). |
| Debug `Doğrulama` script'lerini silmek | Değerli; yalnız sahneye yazdıkları bayraklar (K1) ayıklanmalı. |

## 6. Yol haritası

Her etap tek başına build alınıp doğrulanabilir; sıralama "en büyük kazanç / en düşük risk" önce olacak şekilde. Süreler tek kişi için kaba tahmin.

### Etap 0 — Ölçüm tabanı (2 saat, kod değişikliği yok)
- Mevcut build ile aynı rotada (başlangıç → kütüphane → kimya → final odası; bir kitap açma; bir jumpscare) **Profiler (CPU+GPU+Memory) ve Frame Debugger** yakalaması al; `Window > Analysis > Profiler` "Development Build + Autoconnect" ile. Kaydet: `ProfilerCaptures/` (klasör var, boş).
- Memory Profiler paketiyle bir snapshot (texture/audio/mesh payları). Bu rapordaki VRAM tahminlerini doğrular.
- Sonraki her etabın sonunda aynı rota tekrarlanır; "önce/sonra" olmadan hiçbir etap "bitti" sayılmaz.

### Etap 0.5 — Oyuncunun göreceği hatalar (1 saat, küçük kod)
K1 `sessionOnly` → 0 + Setup/Checks satırları; K2 `StartJumpscare` kapısı; K5 ölüm ekranında kayıt yok + atomik yazma; F1 Guderian PlayOneShot; K3 "osm"; K4 credits sahne adı. Doğrulama: `Senzora/Doğrulama` Play testleri + bir Adam ölümü + kapat/aç → Continue.

### Etap 1 — İçerik ayarları, kod yok (1 gün) · beklenen: VRAM −1,1 GB, yükleme süresi belirgin kısalma, GPU −%10–20
1. A1: Materyallerde shader'da olmayan texture slotlarını temizle (editör script'i, tüm `.mat`). Commit.
2. A3: 82 materyalde Displacement Mode → None (aynı script). Commit.
3. A2: Quality'de Texture Streaming aç (bütçe ~1,2 GB), tüm texture'larda streaming mipmaps aç; ikincil haritaları (Mask/Height/Metallic kalanlar) 1024'e indir. Commit.
4. B1: Ses import ayarları kategoriye göre (Streaming / Compressed In Memory / kalite 0,7 / mono). Commit.
5. C3: Varsayılan kalite Balanced. Commit.
Doğrulama: Memory snapshot (texture toplamı ≈ 2,3 GB → ≈ 1,2 GB), gözle materyal kontrolü (metalik/pürüzlülük MaskMap'ten geldiği için görsel değişmemeli; displacement kapanınca fark görünmemeli).

### Etap 2 — Işık, gölge, görünürlük (1 gün) · beklenen: GPU'da en büyük kazanç, kare süresi tutarlılığı
1. Lamba prefab'ı: Shadow Update Mode → **On Enable** (statik lambalar; kapı/anahtarla açılıp kapananlar On Demand + `RequestShadowMapRendering()`), Light Shadow Caster Mode → **Non-Lightmapped Only**, gölge çözünürlüğü 512 (yakın plan lambalar 1024), gerekiyorsa gölgeyi yalnız oyuncuya yakın 6–8 lambada bırak; Volumetric dimmer ×5 → 1. Tek prefab değişikliği 35 örneğe yayılır.
2. `scn_LaboratorySynthesis` için **lightmap + shadowmask + reflection probe yeniden bake** (şu an başka sahnenin bake verisi kullanılıyor) ve 53 kitap/hareketli nesneler dışındaki eşyaları Static işaretle (Contribute GI + Occluder/Occludee).
3. **Occlusion Culling bake** (Window > Rendering > Occlusion Culling; odalar/kapılar için smallestOccluder 0,5 m uygun). Kamera'da occlusion zaten açık.
4. Global Volume: volumetric fog quality → Medium (ya da bütçe sliderı), Motion Blur kapalı/azaltılmış, Contact Shadows yalnız gerekiyorsa, SSAO Medium. Kamera: TAA aç (antialiasing şu an **kapalı**), near clip 0,01 → 0,05 (z-fighting/gölge akne riski).
5. C1: URP/PPv2 paketlerini kaldır, Strip Debug Variants aç. C4: Shader variant collection üret ve preload et.
Doğrulama: Frame Debugger'da gölge pass sayısı (35 → birkaç), GPU süresi, Stats penceresinde batch/tri sayısı duvar arkasında düşmeli.

### Etap 3 — Sahne ve kod sıcak yolları (1–2 gün) · beklenen: CPU kare süresi −2–5 ms, "ilk kez" takılmaları
1. 92 Animator → Culling Mode "Cull Completely" (kitaplar kapalıyken Animator'ı devre dışı bırak), kitap `DM_Book` MeshCollider → Box/Capsule (animasyonlu skinned mesh üzerinde MeshCollider zaten güncellenmiyor).
2. Koşul/olay MonoBehaviour'larının Update'lerini merkezi tek tarayıcıya ya da olay tabanlıya çevirmek yerine: koşulu sağlanmış/pasif olanların `enabled=false` yapılması (ucuz), `Camera.main`/`FindFirstObjectByType` sonuçlarının Awake'te önbelleklenmesi, `RaycastAll`'ın layer mask'li `Raycast`'e çevrilmesi (4.3'teki madde listesi).
3. TMP: ana fontlar statik atlas, fallback dinamik + Clear Dynamic Data On Build.
4. `Debug.Log` runtime çağrılarını `[Conditional]` sarmalayıcıya al; stack trace → ScriptOnly.
Doğrulama: Profiler CPU'da `Animator.Update`, `BehaviourUpdate`, `TMP` işaretçileri önce/sonra.

### Etap 4 — Hijyen ve geliştirme hızı (fırsat buldukça)
- C2 paket/modül temizliği, B4/B6/D2 çöp ve kopya dosyalar (GUID kanıtıyla), D1 LFS'i ileriye dönük aç + `git gc`, A5 kaynak texture'ları 2048'e küçült, B2 model import ayarları (AssetPostprocessor), D3 asmdef (en son).

### Etap 5 — Yapısal işler (sonraya, bilinçli karar)
- 53 kitabı prefab varyantına çevirmek (toplu düzenleme ve sahne boyutu için); tek sahneyi oda bazlı additive sahnelere bölmek (yükleme süresi ve VRAM için en köklü çözüm, ama kayıt sistemi/olay sistemi/testler etkilenir). Bunlar ancak Etap 1–3 ölçümleri yetmezse gündeme gelmeli.

## 7. Ölçüm planı (neyi, neyle)

| Soru | Araç | Neye bakılır |
|---|---|---|
| GPU'da ne pahalı? | Frame Debugger (build'e bağlı) + Profiler GPU modülü | ShadowMaps pass sayısı/süresi, Volumetric Lighting, SSAO, POM'lu opaque pass süresi, DoF/MotionBlur post pass'leri |
| CPU'da ne pahalı? | Profiler CPU (Timeline) | `BehaviourUpdate` (koşullar), `Animator.Update`, `Canvas.BuildBatch`/`SendWillRenderCanvases`, `Physics.Raycast`, GC.Alloc sütunu |
| Bellek | Memory Profiler snapshot | Texture2D toplamı ve en büyük 50; AudioClip toplamı; Mesh; RenderTexture |
| Yükleme | Profiler "Loading" + sahne yükleme süresi logu | `Resources.UnloadUnusedAssets`, texture upload, audio decode |
| İlk-kez takılmaları | Profiler'da tek kare 50 ms+ olanlar | `Shader.CreateGPUProgram`, `TMP_FontAsset.TryAddCharacters`, `Instantiate` |
Mevcut araçlar: `Senzora/Book Motion/Optimization Live Frames (Play)` ve `Optimization Live Main Thread Breakdown (Play)` menüleri (editör içi kare ölçümü), `outputs/laboratory-cleanup-2026-10-30/unity_bridge.py` ile Play sürme. Build'de ölçüm için Development Build + Autoconnect Profiler yeterli; yeni araç gerekmez.

## 8. Bakılmayanlar ve bilinmeyenler
- Gerçek build FPS/GPU/bellek ölçümü yapılmadı; VRAM değerleri import ayarlarından hesaplanmış tahmindir (DXT boyutu + mip %33), sıkıştırılmış build boyutu hesaplanmadı.
- `Library/LastBuild.buildreport` (ikili) okunmadı; Unity'nin kendi "Build Report" çıktısı Editor.log'da yoktu (log döndüğü için).
- Kodun tamamı değil, belirlenen sıcak yollar ve mimari düğümler okundu; `Assets/Scripts/Editor` altındaki 59 araçtan yalnız örnekler incelendi.
- NaughtyAttributes, StarterAssets ve paket kodları incelenmedi.
- Lightmap'lerin bu sahneye gerçekten uygulanıp uygulanmadığı (başka sahnenin LightingData'sı) editörde Lighting penceresinden doğrulanmalı; sahne YAML'ından anlaşılmıyor.
- Ses miksajı, animasyon kalitesi, oyun tasarımı ve içerik doğruluğu bu raporun kapsamı dışında.
