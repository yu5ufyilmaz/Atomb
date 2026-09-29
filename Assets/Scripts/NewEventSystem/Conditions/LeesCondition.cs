using System;
using UnityEngine;

public class LeesCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip("Lees hangi duruma geçtiğinde tetiklensin? (Genelde 'Active' olmalıdır)")]
    public LeesEnemyAI.LeesState targetState = LeesEnemyAI.LeesState.Active;

    [Header("Şart Ayarları")]
    [Tooltip(
        "True ise, Lees'in sadece belirtilen duruma geçmesi yetmez, aynı zamanda oyuncuyu fark etmiş olması da gerekir."
    )]
    public bool triggerOnSpotted = true;

    private bool conditionMet = false;
    private PrerequisiteCondition prereq;

    // Konsolu spamlememek için zamanlayıcı
    private float debugTimer = 0f;

    public bool IsMet() => conditionMet;

    private void Start()
    {
        prereq = GetComponent<PrerequisiteCondition>();
    }

    private void Update()
    {
        // 1. Zaten sağlandıysa veya Lees sahnede yoksa çık
        if (conditionMet || LeesEnemyAI.Instance == null)
            return;

        // 2. Ön koşul varsa ve tamamlanmadıysa GİREMEZ
        if (prereq != null && !prereq.IsMet())
        {
            if (Time.time > debugTimer)
            {
                Debug.LogWarning(
                    $"[LeesCondition - {gameObject.name}] Bekliyor: Ön koşul (PrerequisiteCondition) henüz tamamlanmamış!"
                );
                debugTimer = Time.time + 1f;
            }
            return;
        }

        // 3. Mevcut State Inspector'da seçilen state ile uyuşuyor mu?
        bool isStateMatched = (LeesEnemyAI.Instance.currentState == targetState);

        // 4. Fark edilme kontrolü açık mı? Açıksa fark edilmiş mi?
        bool isSpottedMatched = triggerOnSpotted ? LeesEnemyAI.Instance.HasBeenSpotted : true;

        // Her iki şart da sağlanıyorsa tetikle
        if (isStateMatched && isSpottedMatched)
        {
            Debug.Log(
                $"[LeesCondition - {gameObject.name}] ŞART BAŞARIYLA SAĞLANDI! State: {targetState}, Spotted: {LeesEnemyAI.Instance.HasBeenSpotted}"
            );
            conditionMet = true;
            OnConditionChanged?.Invoke();
        }
    }
}
