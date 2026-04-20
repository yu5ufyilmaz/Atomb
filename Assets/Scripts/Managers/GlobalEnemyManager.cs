using UnityEngine;

public class GlobalEnemyManager : MonoBehaviour, ISaveable
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

    public void LoadData(GameData data)
    {
        // Eğer oyuncu tam saldırı altındayken oyunu kaydettiyse,
        // geri döndüğünde anında haksızca ölmesin diye saldırıyı iptal edip,
        // ona 10 saniyelik bir "nefes alma" süresi (grace period) tanıyoruz.
        if (data.wasAttackInProgress)
        {
            this.isAttackInProgress = false;
            this.currentGlobalCooldown = 10f; // 10 saniye haksızlık payı (istediğin gibi değiştirebilirsin)
            Debug.Log(
                "GlobalEnemyManager: Kayıt yüklendi. Oyuncu saldırı altındaydı, 10 saniye nefes alma süresi verildi."
            );
        }
        else
        {
            // Eğer huzurlu bir andaysa, kalan süreyi aynen devam ettir
            this.isAttackInProgress = false;
            this.currentGlobalCooldown = data.remainingGlobalCooldown;
            Debug.Log(
                $"GlobalEnemyManager: Kayıt yüklendi. Kalan huzur süresi: {this.currentGlobalCooldown} saniye."
            );
        }
    }

    public void SaveData(ref GameData data)
    {
        // Şu anki saldırı durumunu ve kalan süreyi kaydet
        data.wasAttackInProgress = this.isAttackInProgress;
        data.remainingGlobalCooldown = this.currentGlobalCooldown;
    }
}
