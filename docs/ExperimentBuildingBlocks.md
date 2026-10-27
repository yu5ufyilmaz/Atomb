# Çift Kayıt deney parçaları

Güncel içerik `Assets/Scripts/PuzzleSystems/Chalkboard/SignalTrial.cs` içinde oturum başına oluşturulur. Eski İkinci İz prototipi ve beşli parça envanteri kaldırıldı.

| Parça | Giriş / çıkış | Davranış |
|---|---|---|
| Atım Kaynağı | 0 / 1 | Tek bir uyarım başlatır. |
| Numune N-12 | 1 / 1 | Kısa tepki üretir. |
| Numune N-34 | 1 / 1 | Uzun tepki üretir. |
| Dağıtıcı | 1 / 2 | Gelen tepkiyi iki kola da iletir. |
| Geciktirici | 1 / 1 | İzi bir zaman aralığı sağa kaydırır; yüksekliği korur. |
| Sönümleyici | 1 / 1 | Yüksekliği yarıya indirir; zamanı korur. |
| Çift Kanallı Ölçer | 2 / 0 | Gelen izleri aynı kayıtta gösterir. |

Çıkış oku bir giriş halkasına bağlanır. İşlem parçaları tekrar kullanılabilir; her parçayı kullanmak gerekmez. Çift Kayıt görevi tek numune ister. Koşula uymayan ama çalışan bir düzenek ölçüm üretmeye devam eder; Turing sonucu onaylamaz.

Kitaplardaki parça atlası, ilkeler ve gözlemler deftere alınabilir. Hedef, tamamlanmış bir bağlantı şeması yerine üretilecek ölçüm kaydını gösterir. Karşılaştırmada izlerin yüksekliği, genişliği ve zaman aralığı önemlidir.
