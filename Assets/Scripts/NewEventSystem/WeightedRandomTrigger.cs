using System;
using System.Collections.Generic;
using UnityEngine;

public class WeightedRandomTrigger : MonoBehaviour
{
    public enum RandomMode
    {
        SelectOnceAtStart, // Oyun başında 1 kere seçilir, oyun boyu o geçerli olur
        RollOnEveryTrigger, // Şart her sağlandığında yeniden zar atılır
    }

    [System.Serializable]
    public class WeightedOutcome
    {
        public string outcomeName = "Yeni İhtimal";

        [Tooltip("Bu ihtimalin ağırlığı (Örn: 10, 50, 100)")]
        [Range(0f, 1000f)]
        public float weight = 10f;

        [Tooltip("Boş bırakırsan 'Hiçbir Şey Olmama (Empty)' ihtimali olur.")]
        public GameObject actionContainer;
    }

    [Header("Zar Sistemi Ayarları")]
    [Tooltip("Zar ne zaman atılsın?")]
    public RandomMode mode = RandomMode.SelectOnceAtStart;

    [Tooltip("Şartlar sağlandığında bu olay sadece 1 kez mi tetiklensin?")]
    public bool triggerOnlyOnce = true;
    public bool requireAllConditions = true;

    [Header("İhtimaller (Sonuçlar)")]
    [Tooltip("Farklı senaryoları ve ağırlıklarını buraya ekle.")]
    public List<WeightedOutcome> outcomes = new List<WeightedOutcome>();

    // Gizli Değişkenler
    private ICondition[] conditions;
    public bool HasTriggered { get; private set; } = false;
    private WeightedOutcome preSelectedOutcome;

    private void Start()
    {
        // Ana obje üzerindeki tüm şartları bul ve abone ol
        conditions = GetComponents<ICondition>();
        foreach (var cond in conditions)
        {
            cond.OnConditionChanged += EvaluateConditions;
        }

        // Eğer mod "Tek Seferlik" ise, zarı oyun başında at ve diğer objeleri kapat
        if (mode == RandomMode.SelectOnceAtStart)
        {
            preSelectedOutcome = RollDice();
            DisableUnusedContainers(preSelectedOutcome);
        }
    }

    private void EvaluateConditions()
    {
        if (HasTriggered && triggerOnlyOnce)
            return;

        if (CheckConditions())
        {
            HasTriggered = true;
            ExecuteOutcome();
        }
    }

    private bool CheckConditions()
    {
        if (conditions.Length == 0)
            return false;

        foreach (var cond in conditions)
        {
            bool met = cond.IsMet();
            if (requireAllConditions && !met)
                return false;
            if (!requireAllConditions && met)
                return true;
        }
        return requireAllConditions;
    }

    private void ExecuteOutcome()
    {
        WeightedOutcome winner = null;

        if (mode == RandomMode.SelectOnceAtStart)
        {
            winner = preSelectedOutcome; // Oyun başı atılan zarı kullan
        }
        else if (mode == RandomMode.RollOnEveryTrigger)
        {
            winner = RollDice(); // Şu an canlı olarak zar at
        }

        if (winner != null && winner.actionContainer != null)
        {
            // Seçilen alt objedeki tüm Action'ları bul ve Ateşle!
            IAction[] actions = winner.actionContainer.GetComponents<IAction>();
            foreach (var action in actions)
            {
                action.Execute();
            }
            Debug.Log($"[WeightedRandom] Zar atıldı! Seçilen Sonuç: {winner.outcomeName}");
        }
        else
        {
            // actionContainer boşsa -> EMPTY durumu.
            Debug.Log(
                $"[WeightedRandom] Zar atıldı! Seçilen Sonuç: {(winner != null ? winner.outcomeName : "Tamamen Boş")} (EMPTY)"
            );
        }
    }

    private WeightedOutcome RollDice()
    {
        if (outcomes.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (var outcome in outcomes)
        {
            totalWeight += outcome.weight;
        }

        // 0 ile Toplam Ağırlık arasında rastgele bir sayı tut
        float randomVal = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        foreach (var outcome in outcomes)
        {
            cumulativeWeight += outcome.weight;
            if (randomVal <= cumulativeWeight)
            {
                return outcome;
            }
        }
        return outcomes[outcomes.Count - 1]; // Emniyet kilidi
    }

    private void DisableUnusedContainers(WeightedOutcome winner)
    {
        // Sadece oyun başı atılan zarda çalışır. Seçilmeyen alt objeleri (eğer atanmışsa) kapatır.
        foreach (var outcome in outcomes)
        {
            if (outcome.actionContainer != null)
            {
                outcome.actionContainer.SetActive(outcome == winner);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var cond in conditions)
        {
            if (cond != null)
                cond.OnConditionChanged -= EvaluateConditions;
        }
    }
}
