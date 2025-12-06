using UnityEngine;

public class GlobalEnemyManager : MonoBehaviour
{
    public static GlobalEnemyManager Instance;
    public bool isAttackInProgress = false; // Şu an sahada düşman var mı?

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool CanAttack() => !isAttackInProgress;

    public void RegisterAttackStart()
    {
        isAttackInProgress = true;
        Debug.Log("GLOBAL: Saldırı başladı, sıra kilitlendi.");
    }

    public void RegisterAttackEnd()
    {
        isAttackInProgress = false;
        Debug.Log("GLOBAL: Ortalık sakin, kilit açıldı.");
    }
}