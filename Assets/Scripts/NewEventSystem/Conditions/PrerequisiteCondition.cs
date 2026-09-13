using System;
using UnityEngine;

public class PrerequisiteCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip("Bu olayın çalışması için önce hangi olayın bitmesi gerekiyor?")]
    public EventLogicController requiredEvent;

    private void Start()
    {
        if (requiredEvent != null)
        {
            // Gerekli olay tamamlandığında bizim durumumuz da değişeceği için dinliyoruz
            requiredEvent.OnEventTriggered += HandleRequiredEventTriggered;
        }
    }

    private void HandleRequiredEventTriggered()
    {
        // Önceki olay bitti, kendi ana merkezimize "Şartım sağlandı!" diyoruz
        OnConditionChanged?.Invoke();
    }

    public bool IsMet()
    {
        // Eğer bir olay atanmadıysa hata vermesin diye true dön, atandıysa bitip bitmediğine bak
        return requiredEvent == null || requiredEvent.HasTriggered;
    }

    private void OnDestroy()
    {
        if (requiredEvent != null)
        {
            requiredEvent.OnEventTriggered -= HandleRequiredEventTriggered;
        }
    }
}
