using UnityEngine;

[RequireComponent(typeof(InteractableDoor))]
public class FinalDoorUnlocker : MonoBehaviour
{
    private InteractableDoor door;

    private void Start()
    {
        door = GetComponent<InteractableDoor>();

        // Başlangıçta kapıyı mutlaka kilitleyelim
        door.SetLocked(true);

        // PasswordManager'a abone ol
        if (PasswordManager.Instance != null)
        {
            PasswordManager.Instance.OnGameReadyToFinish += UnlockFinalDoor;
        }
    }

    private void OnDestroy()
    {
        // Abonelikten çık (Hata önlemek için)
        if (PasswordManager.Instance != null)
        {
            PasswordManager.Instance.OnGameReadyToFinish -= UnlockFinalDoor;
        }
    }

    private void UnlockFinalDoor()
    {
        Debug.Log("Final Kapısının Kilidi Açıldı!");
        door.SetLocked(false);
        // İstersen burada bir kilit açılma sesi ("Click") çalabilirsin.
    }
}
