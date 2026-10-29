using System;
using UnityEngine;

public class PrerequisiteCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip("Bu olayın çalışması için önce hangi olayın bitmesi gerekiyor?")]
    public EventLogicController requiredEvent;
    [Tooltip("Kapatılırsa, belirtilen event henüz tetiklenmemiş olmalı.")]
    public bool requireTriggered = true;

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
        return requiredEvent == null || requiredEvent.HasTriggered == requireTriggered;
    }

    private void OnDestroy()
    {
        if (requiredEvent != null)
        {
            requiredEvent.OnEventTriggered -= HandleRequiredEventTriggered;
        }
    }
}
