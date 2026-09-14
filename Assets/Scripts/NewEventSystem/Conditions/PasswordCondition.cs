using System;
using NaughtyAttributes;
using UnityEngine;

public class PasswordCondition : MonoBehaviour, ICondition
{
    public enum PasswordConditionMode
    {
        OnAnyCorrect, // Herhangi bir şifre doğru girildiğinde tetiklenir
        OnAnyIncorrect, // Herhangi bir şifre yanlış girildiğinde tetiklenir
        OnIncorrectLimitReached, // Şifre peş peşe X kez yanlış girildiğinde tetiklenir
    }

    public event Action OnConditionChanged;

    [Tooltip("Hangi durumda bu olay (event) tetiklenecek?")]
    public PasswordConditionMode mode = PasswordConditionMode.OnAnyCorrect;

    [ShowIf("mode", PasswordConditionMode.OnIncorrectLimitReached)]
    [Tooltip("Eğer Limit modu seçildiyse, oyuncu kaçıncı hatasında tetiklensin? (Örn: 5)")]
    public int incorrectLimit = 5;

    private bool conditionMet = false;
    private int currentFails = 0;

    public bool IsMet() => conditionMet;

    private void Start()
    {
        // PasswordManager'daki eventlere abone oluyoruz
        if (PasswordManager.Instance != null)
        {
            PasswordManager.Instance.OnPasswordSuccess += HandleSuccess;
            PasswordManager.Instance.OnPasswordFailed += HandleFail;
        }
    }

    private void HandleSuccess(string passwordID)
    {
        if (conditionMet)
            return;

        // Eğer oyuncu doğru şifre girdiğinde hata sayacını sıfırlamak istersen bu satırı açabilirsin:
        // currentFails = 0;

        if (mode == PasswordConditionMode.OnAnyCorrect)
        {
            TriggerCondition();
        }
    }

    private void HandleFail(string attemptedPassword)
    {
        if (conditionMet)
            return;

        if (mode == PasswordConditionMode.OnAnyIncorrect)
        {
            TriggerCondition();
        }
        else if (mode == PasswordConditionMode.OnIncorrectLimitReached)
        {
            currentFails++;
            Debug.Log(
                $"[PasswordCondition] Hatalı Şifre Denemesi: {currentFails}/{incorrectLimit}"
            );

            if (currentFails >= incorrectLimit)
            {
                TriggerCondition();
            }
        }
    }

    private void TriggerCondition()
    {
        conditionMet = true;
        OnConditionChanged?.Invoke(); // EventLogicController'a "Şart sağlandı!" der
    }

    private void OnDestroy()
    {
        // Script silinirse hata almamak için abonelikleri iptal et
        if (PasswordManager.Instance != null)
        {
            PasswordManager.Instance.OnPasswordSuccess -= HandleSuccess;
            PasswordManager.Instance.OnPasswordFailed -= HandleFail;
        }
    }
}
