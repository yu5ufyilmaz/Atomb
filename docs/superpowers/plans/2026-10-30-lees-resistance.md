# LEES Resistance Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ayrı bir sahnede fareyle LEES'in baş çekişine direnme mücadelesi.
**Architecture:** Saf C# model; kameraya bağlanan mücadele bileşeni; TMP/uGUI sunumu; bağımsız demo rig'i; deterministik sahne kurulum ve kontrol menüleri.
**Tech Stack:** Unity 6000.3.10f1, mevcut HDRP, Input System, TMP.
**Spec:** docs/superpowers/specs/2026-10-30-lees-resistance-design.md

## Global Constraints
- Ana sahne, LeesEnemyAI, gerçek kayıtlar, ayarlar ve Build Settings değiştirilmez.
- Mevcut checkout ve yeni dosyalar kullanılır; kullanıcı değişikliklerine dokunulmaz.
- Yeni paket eklenmez. Ham mouse delta kullanılır; kamera kontrolü paylaşılmaz.

## Tasks
- [x] 1. Model için çalıştırılabilir davranış kontrollerini yaz, RED gözle, modeli uygula: sağ/sol karşılık, idle/yanlış yön yenilgisi, doğru karşılık kurtuluş, FPS bağımsızlığı, sıfır zaman, reset ve bitmiş durum.
- [x] 2. Kamera ve HUD bileşenleri: baş açısı, haber verilen yön değişimi, çevresel baskı, ses, FOV; pause/cancel/disable ve script durumlarını geri yükleme.
- [x] 3. Ayrı scn_LeesResistanceWorkshop sahnesi, gerçek LEES görseli ve koridor; başlama/arkaya dönme/deneme akışı; Senzora menüsü, prefab, Türkçe rehber.
- [x] 4. Unity derlemesi ve editör kontrolleri, kamera/UI render önizlemesi; kontrollerin sonucunu rehbere kaydet.

## Uygulama kaydı
- Test-first model stub'ı davranış kontrolünde RED verdi; uygulama sonrasında 152 assertion PASS.
- Açık Unity projesi kilidi nedeniyle ikinci batch editörü yerine mevcut editörün yerel MCP bağlantısı kullanıldı. Açık ana sahne kapatılmadı.
- Son Unity kontrolü: 199 assertion PASS; iki 1600×900 kamera/HUD renderı incelendi.
- Bağımsız kod incelemesindeki dört bulgu giderildi: ilk açılış, kontrolün sahneye sınırlandırılması, iptalde HUD temizliği, Volume bileşenlerinin asset olarak saklanması.
- Görsel incelemede projenin varsayılan parlak HDRI ortamı saptandı; yalnız workshop profilinde sky kapatılarak düzeltildi. Mevcut HDRP varsayılanları değiştirilmedi.
- Ana LeesEnemyAI dosyası değişmedi. Ana oyun entegrasyonu sonraki kapsamdır. İnsanla zorluk/his testi henüz yapılmadı.
- Kullanıcı ilk sürümü oynayıp çok zayıf buldu; sertleştirme uygulandı. RED: idle kaybı 8,51 sn ile yeni 2,25 sn sınırını aştı. GREEN: 372 model assertion / 207 Unity assertion; yeni idle kaybı 1,23 sn. Açık sahne ve prefab 54 derece/sn çekiş, 52 derece sınır ve 0,25 sn uyarı ile kaydedildi. Süreler korundu.

## Review Focus
- Yalnız bekleme rastgele yön değişimleriyle kurtuluş sağlamamalı.
- Fare girişi FPS'e bağlı kuvvete dönüşmemeli; tek kare warp otomatik başarı sağlamamalı.
- Pause/odak kaybı ilerleme veya ücretsiz fare girdisi üretmemeli.
- İptal/disable kamera, cursor ve daha önce kapalı scriptleri değiştirmemeli.
- Çalışma sahnesi gerçek GameManager / save / düşman singleton'ları olmadan açılmalı.
