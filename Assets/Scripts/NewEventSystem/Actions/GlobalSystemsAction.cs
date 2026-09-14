using UnityEngine;

public class GlobalSystemsAction : ActionBase
{
    public enum SystemState
    {
        Enable,
        Disable,
        DoNothing,
    }

    [Header("Global Sistemleri Yönet")]
    [Tooltip("DoNothing seçilirse o sisteme dokunmaz.")]
    public SystemState pressureSystem = SystemState.Enable;
    public SystemState breakerSystem = SystemState.Enable;
    public SystemState globalEnemyAttacks = SystemState.Enable;

    protected override void PerformAction()
    {
        // 1. Basınç Sistemi
        if (pressureSystem != SystemState.DoNothing && PressureSystemManager.Instance != null)
        {
            PressureSystemManager.Instance.isSystemActive = (pressureSystem == SystemState.Enable);
        }

        // 2. Şartel Sistemi
        if (breakerSystem != SystemState.DoNothing && BreakerBox.Instance != null)
        {
            BreakerBox.Instance.isSystemActive = (breakerSystem == SystemState.Enable);
        }

        // 3. Global Düşman Saldırıları
        // Zaten GlobalEnemyManager içinde yazdığın stopAllEnemies değişkenini kullanıyoruz.
        if (globalEnemyAttacks != SystemState.DoNothing && GlobalEnemyManager.Instance != null)
        {
            // Eğer sistemi aç (Enable) dediysek, stopAllEnemies = false olmalı.
            GlobalEnemyManager.Instance.stopAllEnemies = (
                globalEnemyAttacks == SystemState.Disable
            );
        }

        Debug.Log("[GlobalSystemsAction] Global sistemlerin durumu güncellendi.");
    }
}
