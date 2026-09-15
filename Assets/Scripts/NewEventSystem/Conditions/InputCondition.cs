using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public class InputCondition : MonoBehaviour, ICondition
{
    public enum InputMode
    {
        SinglePress, // Tek Sefer Basma (Örn: Tab'a 1 kere bas)
        MultiplePresses, // Üst Üste Basma (Örn: Feneri 20 kere sarj et)
        Combination, // Kombinasyon (Örn: Shift + W aynı anda bas)
    }

    public event Action OnConditionChanged;

    [Tooltip("Hangi tuş modunu kullanmak istiyorsun?")]
    public InputMode mode = InputMode.SinglePress;

    [HideIf("mode", InputMode.Combination)]
    [Tooltip("Dinlenecek ana tuş (Örn: Tab, Mouse1, LeftShift)")]
    public KeyCode targetKey = KeyCode.Tab;

    [ShowIf("mode", InputMode.MultiplePresses)]
    [Tooltip("Bu tuşa toplamda/üst üste kaç kere basılması gerekiyor?")]
    public int requiredPresses = 5;

    [ShowIf("mode", InputMode.Combination)]
    [Tooltip("Aynı anda basılması gereken tuşlar (Örn: LeftControl + F)")]
    public List<KeyCode> combinationKeys = new List<KeyCode>();

    private bool conditionMet = false;
    private int currentPresses = 0;

    public bool IsMet() => conditionMet;

    private void Update()
    {
        // Şart zaten sağlandıysa sistemi yorma
        if (conditionMet)
            return;

        switch (mode)
        {
            case InputMode.SinglePress:
                if (Input.GetKeyDown(targetKey))
                {
                    TriggerCondition();
                }
                break;

            case InputMode.MultiplePresses:
                if (Input.GetKeyDown(targetKey))
                {
                    currentPresses++;
                    if (currentPresses >= requiredPresses)
                    {
                        TriggerCondition();
                    }
                }
                break;

            case InputMode.Combination:
                if (combinationKeys.Count > 0 && CheckCombination())
                {
                    TriggerCondition();
                }
                break;
        }
    }

    private bool CheckCombination()
    {
        // Kombinasyondaki tüm tuşlara aynı anda basılıyor mu?
        foreach (KeyCode key in combinationKeys)
        {
            // GetKey kullanıyoruz çünkü biri basılı tutulurken diğerine basılması gerekir
            if (!Input.GetKey(key))
            {
                return false;
            }
        }
        return true;
    }

    private void TriggerCondition()
    {
        conditionMet = true;
        OnConditionChanged?.Invoke(); // EventLogicController'a "Şart sağlandı!" haberini gönderir
    }
}
