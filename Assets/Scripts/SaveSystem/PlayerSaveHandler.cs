using UnityEngine;

public class PlayerSaveHandler : MonoBehaviour, ISaveable
{
    private StarterAssets.CharacterController playerController;

    private void Awake()
    {
        playerController = GetComponent<StarterAssets.CharacterController>();
    }

    public void LoadData(GameData data)
    {
        // Eğer kayıt verisi boşsa (0,0,0) işlem yapma
        if (data.playerPosition == Vector3.zero) return;

        Debug.Log("Oyuncu ışınlanıyor, Kollar ve Kamera senkronize ediliyor...");

        // 1. ADIM: Tüm oyuncu objesini (Kamera ve kollar dahil) tamamen kapat!
        // Bu işlem kameranın ve animasyonların "yumuşak geçiş" yapmasını engeller.
        gameObject.SetActive(false);

        // 2. ADIM: Güvenlik için Controller'ı da kapat
        if (playerController != null) playerController.enabled = false;

        // 3. ADIM: Yeni konuma ve rotasyona taşı
        transform.position = data.playerPosition;
        transform.rotation = data.playerRotation;

        // 4. ADIM: Fiziği zorla senkronize et
        Physics.SyncTransforms();

        // 5. ADIM: Controller'ı aç
        if (playerController != null) playerController.enabled = true;

        // 6. ADIM: Oyuncu objesini yeni yerinde tekrar uyandır!
        // Kamera ve kollar bu noktada kök objenin yeni yerinde kusursuz olarak doğar.
        gameObject.SetActive(true);
    }

    public void SaveData(ref GameData data)
    {
        data.playerPosition = transform.position;
        data.playerRotation = transform.rotation;
    }
}