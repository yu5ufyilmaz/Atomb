using NaughtyAttributes;
using UnityEngine;

public class LightAction : ActionBase // ActionBase'den miras alıyor (Delay sistemi için)
{
    public enum LightOperation
    {
        TurnOn,
        TurnOff,
        Toggle,
        TripBreaker, // <--- YENİ EKLENEN SEÇENEK: Direkt ana şalteri attırır
    }

    [Tooltip("Bu eylem tetiklendiğinde ışıklara veya sisteme ne olacak?")]
    public LightOperation operation = LightOperation.TurnOn;

    [HideIf("operation", LightOperation.TripBreaker)]
    [Tooltip(
        "Etkilenecek ControllableLight (şalter) objeleri (TripBreaker seçiliyse burayı boş bırakabilirsiniz)"
    )]
    public ControllableLight[] targetSwitches;

    protected override void PerformAction()
    {
        // 1. EĞER ANA ŞALTERİ ATTIRMA SEÇİLDİYSE
        if (operation == LightOperation.TripBreaker)
        {
            if (BreakerBox.Instance != null)
            {
                BreakerBox.Instance.ForceTrip();
                Debug.Log("[LightAction] Tüm tesisin elektriği (Breaker) zorla kesildi!");
            }
            else
            {
                Debug.LogWarning("[LightAction] Sahnede BreakerBox bulunamadı!");
            }

            // Şalter atınca zaten tüm ışıklar kapanacağı için aşağıdaki ışık döngüsüne girmeye gerek yok
            return;
        }

        // 2. EĞER NORMAL IŞIK AÇMA/KAPAMA SEÇİLDİYSE
        if (targetSwitches == null || targetSwitches.Length == 0)
            return;

        foreach (ControllableLight switchObj in targetSwitches)
        {
            if (switchObj == null)
                continue;

            switch (operation)
            {
                case LightOperation.TurnOn:
                    switchObj.SetLightState(true);
                    break;
                case LightOperation.TurnOff:
                    switchObj.SetLightState(false);
                    break;
                case LightOperation.Toggle:
                    // Mevcut durumun tersini uygula
                    switchObj.SetLightState(!switchObj.IsOn);
                    break;
            }
        }

        Debug.Log(
            $"[LightAction] {operation} işlemi {targetSwitches.Length} şalter için başarıyla uygulandı."
        );
    }
}
