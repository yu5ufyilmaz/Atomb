using UnityEngine;

public class GlobalEnemyManager : MonoBehaviour
{
    public static GlobalEnemyManager Instance;

    [Header("TEST MODU")]
    [Tooltip("Eğer işaretliyse hiçbir düşman spawn olmaz, sayaçlar ilerlemez.")]
    public bool stopAllEnemies = false;

    [Header("Durum")]
    public bool isAttackInProgress = false;

    [Header("Huzur Ayarları")]
    public float postAttackCooldown = 30f;

    [HideInInspector]
    public float currentGlobalCooldown = 0f;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Update()
    {
        // --- DÜZELTME: TEST MODU KONTROLÜ ---
        if (stopAllEnemies)
        {
            // Eğer test modundaysak, saldırı bayrağını indir.
            // Yoksa testi kapattığında sistem "Hala saldırı var" sanıp kilitli kalır.
            if (isAttackInProgress)
            {
                isAttackInProgress = false;
                Debug.Log("TEST MODU: Aktif saldırı iptal edildi.");
            }
            return;
        }
        // ------------------------------------

        if (currentGlobalCooldown > 0)
        {
            currentGlobalCooldown -= Time.deltaTime;
        }
    }

    public bool CanAttack()
    {
        if (stopAllEnemies)
            return false;
        return !isAttackInProgress && currentGlobalCooldown <= 0;
    }

    public void RegisterAttackStart()
    {
        isAttackInProgress = true;
        currentGlobalCooldown = 0f;
        Debug.Log("GLOBAL: Saldırı başladı.");
    }

    public void RegisterAttackEnd()
    {
        isAttackInProgress = false;
        currentGlobalCooldown = postAttackCooldown;
        Debug.Log($"GLOBAL: Tehdit geçti. {postAttackCooldown}s huzur.");
    }
}
