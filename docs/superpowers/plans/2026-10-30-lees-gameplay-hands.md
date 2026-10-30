# LEES Gameplay Hands Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for inline execution.

**Goal:** Sessizleştirilen sert LEES mücadelesini gerçek sahnelere, görünür LEES elleriyle bağla.
**Architecture:** Mevcut controller ve AI arasında küçük bağlantı; iki bake edilmiş el mesh'i; tek editör kurulum/denetim komutu.
**Spec:** docs/superpowers/specs/2026-10-30-lees-gameplay-hands-design.md
**Tech Stack:** Mevcut Unity HDRP, Cinemachine, Input System, TMP; yeni paket yok.

- [x] Bağlantı ve eller için RED kontrolünü çalıştır.
- [x] Controller ses/pause/eller ve LeesEnemyAI başlatma/başarı/ölüm/iptal akışlarını uygula.
- [x] Modelden el meshlerini bake et; test rig'i ve iki ana sahneye kurulumu kaydet.
- [x] Model/Unity kontrolleri, el renderı ve bağımsız kod incelemesi; bulguları düzelt.

Doğrulama: 372 model / 209 atölye / 68 ana sahne assertion PASS. Başlangıç RED kontrolü canlı AI bağlantısının eksikliğini yakaladı; el ölçeği kontrolü ilk bake'in .01 rig nedeniyle ikinci kez küçülmesini yakaladı. `BakeMesh(..., true)` ile doğru dünya ölçeği elde edildi. İlk el pozu renderdan düzeltilip iki kenarda gerçek LEES elleri doğrulandı.

Bağımsız incelemenin iki bulgusu giderildi: eski AI movement/look alanları PlayerInteraction'a işaret ettiği için gerçek StarterAssets.CharacterController türle çözülüp askıya alındı; kalp sesi mevcut efekt mikserine bağlandı. Ek olay dinleyicileri yeniden kurulumda korunur. Kullanıcı sert zorluk ayarları değişmedi. Ana sahnede tam insan oynama testi iddia edilmiyor; izole preview callback ve kamera/kontrol kontrolleri çalıştırıldı.

Risk odakları: Cinemachine ile kamera sahipliği; hareket scripti kapalıyken hareket niyetinin kaybolmaması; PauseManager ESC çakışması; ellerin raycast/render cache'e girmemesi; aynı anda eski survivalWaitTime ile bedava kurtuluş olmaması; ölürken eski kameralara doğru geri dönüş.
