using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChalkboardButton : MonoBehaviour
{
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        // Makine aktif değilse veya oyun durmuşsa çalışmasın
        if (
            ChalkboardManager.Instance == null
            || !ChalkboardManager.Instance.isMachineActive
            || (GameManager.Instance != null && GameManager.Instance.isGamePaused)
        )
            return;

        if (Input.GetMouseButtonDown(0))
        {
            // Fare neredeyse oraya ışın at
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 10f);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject == this.gameObject)
                {
                    // Yöneticiden sentezi başlat
                    ChalkboardManager.Instance.AttemptSynthesis();
                    break;
                }
            }
        }
    }
}
