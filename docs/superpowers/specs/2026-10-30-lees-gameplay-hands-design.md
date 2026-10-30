# LEES ana oyun bağlantısı ve eller

Kullanıcı sert mücadeleyi oynayıp onayladı. Yeni istek: tekrarlanan çınlama/korku seslerini çıkar; sistemi ana sahneye bağla; ekran kenarlarında LEES'in elleri görünsün.

Tercih: mevcut LeesEnemyAI'nin görme/arkasını dönme koşulu bağımsız mücadeleyi başlatır. Aynı kamera kullanılır; ikinci kamera/AudioListener eklenmez. CinemachineBrain, hareket/bakış ve etkileşim scriptlerinin enabled durumları mücadele boyunca saklanıp geri yüklenir. Oyun PauseManager'ı ESC'nin sahibi olmaya devam eder. Mücadelede hareket niyeti mevcut grace süresinden uzun sürerse ölüm uygulanır. Başarı DespawnLees'e, yenilgi mevcut ölüm/timeline akışına gider. İptal, test modu ve scene unload kamera/elleri temizler.

Çınlama döngüsü ve her yön değişimindeki keman/jumpscare mini oyundan çıkarılır; kalp atışı kalır. Gerçek ölümdeki jumpscare sesleri korunur. Entegre LEES fark edilme sesi de aynı çınlamayı yeniden başlatmaz.

Eller: yeni resim veya geometrik taklit yerine mevcut LEES modelinin el/önkol mesh'i ve dokuları kullanılır. Editör sırasında mesh çıkarılır, hafif kavrama pozu verilir ve iki küçük mesh asseti saklanır. Oyun sırasında yalnız iki mesh hareket eder. Kamera kenarlarından yaklaşırken belirir, çekilen tarafta daha fazla bastırır; bitiş/iptalde kaybolur. Pozisyon/ölçek Inspector'dan ayarlanabilir. Kaynak model ve animasyonlar değiştirilmez.

Hedefler: scn_LaboratorySynthesis (çalışma sahnesi), scn_labratory (Build Settings giriş sahnesi), mevcut scn_LeesResistanceWorkshop (hızlı görsel test). scn_labratoryfast ve giriş/Build Settings değiştirilmez. Mevcut kullanıcı değişiklikleri korunur; yalnız ilgili sahne nesneleri eklenir.

Kabul: aktif sahne bağlantıları ve mevcut ölüm olayları doğru; başarı/yenilgi bir kez; pause/focus/cancel/disable güvenli; sessiz ses politikası; iki gerçek el mesh'i görünür; kamera ve kontrol geri döner; sertleştirilmiş 372 model kontrolü korunur; Unity derlemesi ve izole preview/entegrasyon kontrolleri geçer.
