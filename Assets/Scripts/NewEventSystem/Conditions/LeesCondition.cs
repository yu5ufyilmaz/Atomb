using System;
using UnityEngine;

public class LeesCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip("Lees hangi duruma geçtiğinde tetiklensin?")]
    public LeesEnemyAI.LeesState targetState; // Eğer enum adı farklıysa burayı kendi koduna göre düzelt

    private bool conditionMet = false;
    private PrerequisiteCondition prereq; // EKLENDİ: Ön koşul kontrolcüsü

    public bool IsMet() => conditionMet;

    private void Start()
    {
        // EKLENDİ: Aynı objede bir PrerequisiteCondition varsa onu bul
        prereq = GetComponent<PrerequisiteCondition>();
    }

    private void Update()
    {
        // Şart çoktan sağlandıysa veya Lees sahnede yoksa işlem yapma
        if (conditionMet || LeesEnemyAI.Instance == null)
            return;

        // EKLENDİ: Eğer bu eventin bir "Ön Koşulu" (Örn: Oyuncunun arkaya dönmesi) varsa
        // ve o koşul henüz gerçekleşmediyse, Lees'in durumunu HİÇ KONTROL ETME! Bekle.
        if (prereq != null && !prereq.IsMet())
            return;

        // Lees beklenen duruma geçti mi?
        if (LeesEnemyAI.Instance.currentState == targetState)
        {
            conditionMet = true;
            OnConditionChanged?.Invoke(); // EventLogicController'a haber ver
        }
    }
}
