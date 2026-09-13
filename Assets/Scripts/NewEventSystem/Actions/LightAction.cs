using UnityEngine;

public class LightAction : MonoBehaviour, IAction
{
    public enum LightOperation
    {
        TurnOn,
        TurnOff,
        Toggle,
    }

    [Tooltip("Etkilenecek ControllableLight (Şalter) objeleri")]
    public ControllableLight[] targetSwitches;

    [Tooltip("Bu eylem tetiklendiğinde şalterlere ne olacak?")]
    public LightOperation operation = LightOperation.TurnOn;

    public void Execute()
    {
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
