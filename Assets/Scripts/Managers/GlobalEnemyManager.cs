using UnityEngine;

public class GlobalEnemyManager : MonoBehaviour
{
    public static GlobalEnemyManager Instance;

    [Header("Durum")]
    public bool isAttackInProgress = false; // Şu an sahada düşman var mı?

    [Header("Huzur Ayarları (Global Cooldown)")]
    [Tooltip(
        "Bir saldırı bittikten sonra diğerinin başlaması için geçmesi gereken ZORUNLU huzur süresi."
    )]
    public float postAttackCooldown = 30f; // Örn: 30 Saniye boyunca kimse gelemez

    // Editörün okuması için public (ama inspector'da gizli)
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
        // Huzur Süresi Geri Sayımı
        if (currentGlobalCooldown > 0)
        {
            currentGlobalCooldown -= Time.deltaTime;
        }
    }

    // Düşmanlar saldırmadan önce buraya sorar
    public bool CanAttack()
    {
        // 1. Şu an başka saldırı yoksa
        // 2. VE Huzur süresi (Global Cooldown) bittiyse
        return !isAttackInProgress && currentGlobalCooldown <= 0;
    }

    // Saldırı başlayınca kilitler
    public void RegisterAttackStart()
    {
        isAttackInProgress = true;
        currentGlobalCooldown = 0f; // Saldırı başladı, cooldown'ı iptal et
        Debug.Log("GLOBAL: Saldırı başladı, sıra kilitlendi.");
    }

    // Düşman gidince kilidi açar ve HUZUR SÜRESİNİ başlatır
    public void RegisterAttackEnd()
    {
        isAttackInProgress = false;

        // KRİTİK NOKTA: Saldırı bitti, sayacı başlat!
        currentGlobalCooldown = postAttackCooldown;
        Debug.Log(
            $"GLOBAL: Tehdit geçti. {postAttackCooldown} saniye huzur modu (Global Cooldown)."
        );
    }
}
