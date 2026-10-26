# Test Kitaplarında Kapak ve Sırt Adları Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for implementation. Steps use checkbox syntax for tracking.

**Goal:** Dört araştırma kitabını kapalıyken ve rafta sırtından okunabilen adlarla ayırt etmek.

**Architecture:** Mevcut test kurulumu, yalnız sahnedeki kitap örneklerinin hareketli kapak ve sırt kemiklerine TMP başlıkları ekler. Ortak kitap prefabı ve materyal assetleri değişmez. İsimler mevcut içerikle aynı kalır.

**Tech Stack:** Unity 6000.3.10f1; mevcut TextMeshPro ve kitap rig'i.

**Spec:** Kullanıcının yalnız test sahnesinde kitap kapaklarına/sırtlarına ad ekleme isteği. Yeni puzzle için yalnız fikir hazırlanır; puzzle bu planın uygulama kapsamı değildir.

## Constraints

- Yalnız scn_SynthesisBlockout sahnesindeki dört kitap örneği.
- Ön kapak Page.L kemiğini, sırt Binding kemiğini izlemeli.
- Kullanıcının mevcut değişiklikleri korunur; commit veya prefab apply yapılmaz.
- Okunur ve yüzeye oturan yazılar; yeni collider veya etkileşim kontrolü yok.
- Bu kozmetik değişiklik için ek test sistemi kurulmaz; gerçek Unity görüntüsü ve aç/kapat akışı kontrol edilir.

## Task

- [x] Kitap prefabı, kapak/sırt kemikleri ve skinned mesh yüzeylerini incele.
- [x] İdempotent sahne başlık kurulumunu ekle ve mevcut kurulumdan çağır.
- [x] Yalnız test sahnesine uygula ve kaydet.
- [x] Unity derlemesini, kapak/sırtın yakın görüntüsünü ve aç/kapat akışını kontrol et.
- [x] İki parçalı isteğin diğer kısmı için uygulanmamış puzzle önerisini sun.

Unity 6000.3.10f1: sekiz başlık taşıyıcısı (dört kapak + dört sırt); tekrar kurulum bunları yeniden kullanır. Atlas kapak/sırt görüntüsü alındı, gerçek Play etkileşimiyle kitap açılıp kapatıldı. Son konsol hata sayısı 0. Küçük FBX kemik birimlerinde TMP otomatik boyut yuvarlamasını önlemek için yazı rect/font ölçüleri ölçeklendi. Yeni puzzle ve Turing makinesi değiştirilmedi.
