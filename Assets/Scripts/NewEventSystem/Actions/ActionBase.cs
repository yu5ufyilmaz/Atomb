using System.Collections;
using UnityEngine;

// Tüm action'ların miras alacağı ana sınıf
public abstract class ActionBase : MonoBehaviour, IAction
{
    [Header("Zamanlama (Delay)")]
    [Tooltip("Bu aksiyon şart sağlandıktan kaç saniye sonra çalışsın?")]
    public float delay = 0f;

    // EventLogicController hala burayı çağıracak.
    // Gecikme varsa bekleyecek, yoksa anında çalıştıracak.
    public virtual void Execute()
    {
        if (!isActiveAndEnabled) return;
        if (delay > 0)
            StartCoroutine(DelayRoutine());
        else
            PerformAction();
    }

    private IEnumerator DelayRoutine()
    {
        yield return new WaitForSeconds(delay);
        if (isActiveAndEnabled) PerformAction();
    }

    // Alt scriptler (DialogAction, DoorAction vb.) asıl kodlarını buraya yazacak
    protected abstract void PerformAction();
}
