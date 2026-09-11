using UnityEngine;

public class DesktopMenuInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    public LayerMask menuLayer; // Sadece menü objelerini algılaması için

    // Oyunu başlatma sekansını yönetecek referans
    public InGameMenuController menuController;

    void Update()
    {
        // Eğer oyun çoktan başladıysa bu tıklama kodunu çalıştırma
        if (GameManager.Instance.isGameStarted)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, menuLayer))
            {
                if (hit.collider.gameObject.name == "StartGameObj")
                {
                    menuController.PlayStartSequence();
                }
                else if (hit.collider.gameObject.name == "QuitObj")
                {
                    QuitGameFromDesk();
                }
            }
        }
    }

    public void QuitGameFromDesk()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Debug.Log("Masadan Oyundan Çıkıldı!");
        Application.Quit();
    }
}
