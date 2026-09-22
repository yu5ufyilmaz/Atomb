using UnityEngine;

public class PhantomSpawnAction : ActionBase
{
    [Header("Spawn Ayarları")]
    [Tooltip("Sahnede belirecek zararsız model (Prefab). Üzerinde sadece Animator olması yeterli.")]
    public GameObject phantomPrefab;

    [Tooltip("Nerede belirecek? (Sahnedeki boş bir Transform/GameObject)")]
    public Transform spawnPoint;

    [Header("Animasyon ve Davranış")]
    [Tooltip(
        "Oynatılacak animasyonun State adı (Animator penceresindeki kutunun tam adı, örn: 'Walk' veya 'Jumpscare')"
    )]
    public string animationStateName = "Idle";

    [Tooltip("Belirdiğinde oyuncuya doğru baksın mı?")]
    public bool lookAtPlayer = true;

    [Tooltip("Kaç saniye sonra kaybolsun? (0 yaparsanız sahnede kalıcı olur)")]
    public float destroyAfterSeconds = 3f;

    [Header("Ses (Opsiyonel)")]
    [Tooltip("Belirdiğinde o noktada çalacak ses (Fısıltı, ayak sesi, anlık çığlık vs.)")]
    public AudioClip spawnSound;

    [Range(0f, 1f)]
    public float volume = 1f;

    protected override void PerformAction()
    {
        if (phantomPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[PhantomSpawnAction] Prefab veya Spawn Point atanmamış!");
            return;
        }

        // 1. Modeli Sahnede Yarat
        GameObject phantom = Instantiate(phantomPrefab, spawnPoint.position, spawnPoint.rotation);

        // 2. Oyuncuya Döndür (Opsiyonel)
        if (lookAtPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // Sadece Y ekseninde dönmesini sağla (eğilip bükülmesin)
                Vector3 direction = (
                    player.transform.position - phantom.transform.position
                ).normalized;
                direction.y = 0;
                if (direction != Vector3.zero)
                {
                    phantom.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        // 3. Animasyonu Oynat (Trigger yerine doğrudan Play kullanıyoruz, böylece ok çekmeye gerek kalmaz)
        if (!string.IsNullOrEmpty(animationStateName))
        {
            Animator anim = phantom.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Play(animationStateName);
            }
            else
            {
                Debug.LogWarning(
                    $"[PhantomSpawnAction] {phantom.name} üzerinde Animator bulunamadı!"
                );
            }
        }

        // 4. Belirdiği Yerde Sesi Çal
        if (spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(spawnSound, spawnPoint.position, volume);
        }

        // 5. Görevi Bitince Kendini Yok Et
        if (destroyAfterSeconds > 0)
        {
            Destroy(phantom, destroyAfterSeconds);
        }
    }
}
