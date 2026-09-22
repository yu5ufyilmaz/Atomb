using UnityEngine;
using UnityEngine.Events; // UnityEvent kullanmak için şart

public class CustomMethodAction : ActionBase
{
    [Header("Özel Metot Tetikleyici")]
    [Tooltip(
        "Buraya istediğin objeyi sürükleyip, içindeki herhangi bir public metodu (fonksiyonu) seçebilirsin."
    )]
    public UnityEvent customMethodsToCall;

    protected override void PerformAction()
    {
        // Eğer Inspector'dan bir metot atandıysa onu çalıştır
        if (customMethodsToCall != null)
        {
            customMethodsToCall.Invoke();
            Debug.Log("[CustomMethodAction] Belirtilen özel metot(lar) başarıyla tetiklendi.");
        }
        else
        {
            Debug.LogWarning("[CustomMethodAction] Tetiklenecek bir metot atanmamış!");
        }
    }
}
