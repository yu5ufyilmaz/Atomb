using UnityEngine;

public class HidingSpotControlAction : ActionBase
{
    [Tooltip("Kontrol edilecek saklanma alanı")]
    public InteractableHidingSpot hidingSpot;

    [Tooltip("True: Oyuncu çıkamaz. False: Çıkabilir.")]
    public bool lockExit = true;

    protected override void PerformAction()
    {
        if (hidingSpot != null)
        {
            hidingSpot.canExit = !lockExit;
        }
    }
}
