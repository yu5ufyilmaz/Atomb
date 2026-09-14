using System.Collections;
using UnityEngine;

public class DoorAction : ActionBase
{
    public enum DoorOperation
    {
        Open,
        Close,
        Lock,
        Unlock,
        CreepySlam,
        CreepyLockRattle,
    }

    [Tooltip("Etkilenmesini istediğin kapılar (InteractableDoor scripti olan objeler)")]
    public InteractableDoor[] targetDoors;

    [Tooltip("Kapılara ne olacak?")]
    public DoorOperation operation = DoorOperation.Open;

    [Header("Paranormal Ayarlar")]
    [Tooltip("Creepy eylemler seçildiyse kaç kere tekrar etsin? (Örn: 3)")]
    public int repeatCount = 3;

    [Tooltip(
        "Aç/Kapa arası bekleme süresi (Animasyonun bitmesini beklemeden 0.3 veya 0.4 saniye yaparsan sert çarpma hissi verir)"
    )]
    public float delayBetweenSteps = 0.4f;

    protected override void PerformAction()
    {
        if (targetDoors == null || targetDoors.Length == 0)
            return;

        foreach (var door in targetDoors)
        {
            if (door == null)
                continue;

            switch (operation)
            {
                case DoorOperation.Open:
                    door.SetLocked(false);
                    door.SetOpen(true);
                    break;
                case DoorOperation.Close:
                    door.SetOpen(false);
                    break;
                case DoorOperation.Lock:
                    // Senin koduna göre: Kilitlendiğinde kapı açıksa otomatik kapanır[cite: 4]
                    door.SetLocked(true);
                    break;
                case DoorOperation.Unlock:
                    door.SetLocked(false);
                    break;
                case DoorOperation.CreepySlam:
                    StartCoroutine(CreepySlamRoutine(door));
                    break;
                case DoorOperation.CreepyLockRattle:
                    StartCoroutine(CreepyLockRattleRoutine(door));
                    break;
            }
        }
    }

    private IEnumerator CreepySlamRoutine(InteractableDoor door)
    {
        door.SetLocked(false);

        for (int i = 0; i < repeatCount; i++)
        {
            door.SetOpen(true);
            yield return new WaitForSeconds(delayBetweenSteps);
            door.SetOpen(false);
            yield return new WaitForSeconds(delayBetweenSteps);
        }
    }

    private IEnumerator CreepyLockRattleRoutine(InteractableDoor door)
    {
        if (door.isOpen)
            door.SetOpen(false);

        for (int i = 0; i < repeatCount; i++)
        {
            door.SetLocked(true);
            yield return new WaitForSeconds(delayBetweenSteps);
            door.SetLocked(false);
            yield return new WaitForSeconds(delayBetweenSteps);
        }

        // Panik yaratmak için en son kapıyı kilitli bırakıyoruz
        door.SetLocked(true);
    }
}
