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

        if (Input.GetMouseButtonDown(0) && !FormulaNode.IsPointerOverUI())
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;
            // Fare neredeyse oraya ışın at
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 10f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject == this.gameObject)
                {
                    // Yöneticiden sentezi başlat
                    ChalkboardManager.Instance.AttemptSynthesis();
                    break;
                }
                if (hit.collider.GetComponentInParent<DraggableFormula>() != null)
                    break;
            }
        }
    }
}
