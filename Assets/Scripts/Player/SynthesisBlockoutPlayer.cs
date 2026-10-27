using UnityEngine;

// Yalnız mekanik deneme sahnesinin basit oyuncusu; ana oyuncu prefabını kullanmaz.
[RequireComponent(typeof(UnityEngine.CharacterController))]
public class SynthesisBlockoutPlayer : MonoBehaviour
{
    [Header("Test Oyuncusu")]
    public Camera viewCamera;
    public float speed = 3f;
    public float sensitivity = 2f;
    public InteractableBook book;
    public InteractableChalkboard board;
    public Transform bookView;
    public Transform boardView;
    public InteractableBook[] researchBooks;
    public InteractableTuringMachine trialTerminal;
    public Transform terminalView;
    private UnityEngine.CharacterController body;
    private float pitch;
    private IForceExitable session;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private float returnPitch;
    private IInteractable focused;

    private void Awake() { body = GetComponent<UnityEngine.CharacterController>(); }

    private void Update()
    {
        if (GameManager.Instance == null || viewCamera == null) return;
        var active = GameManager.Instance.activeInteraction;
        if (session != null)
        {
            if (active != null || (session is InteractableBook reading && reading.isAnimating))
            {
                if (Input.GetKeyDown(KeyCode.Escape)) session.ForceExit();
                return;
            }
            body.enabled = false;
            transform.SetPositionAndRotation(returnPosition, returnRotation);
            pitch = returnPitch;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            body.enabled = true;
            session = null;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (active != null || GameManager.Instance.isGamePaused) return;
        if (NotebookWorkspaceController.Instance != null && NotebookWorkspaceController.Instance.IsStandaloneOpen)
        {
            focused?.OnLoseFocus(); focused = null;
            return;
        }
        if (Input.GetKeyDown(KeyCode.Alpha1)) { Begin(book, bookView); return; }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { Begin(board, boardView); return; }
        if (Input.GetKeyDown(KeyCode.Alpha6) && trialTerminal != null && trialTerminal.gameObject.activeInHierarchy)
        { Begin(trialTerminal, terminalView); return; }
        if (researchBooks != null)
            for (int i = 1; i < researchBooks.Length && i < 4; i++)
                if (Input.GetKeyDown(KeyCode.Alpha3 + i - 1)) { Begin(researchBooks[i], null); return; }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            return;
        }
        transform.Rotate(0, Input.GetAxis("Mouse X") * sensitivity, 0);
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, -75, 75);
        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        Vector3 move = Vector3.ClampMagnitude(transform.right * Input.GetAxisRaw("Horizontal")
            + transform.forward * Input.GetAxisRaw("Vertical"), 1f);
        if (body.enabled) body.Move((move * speed + Vector3.down * 5f) * Time.deltaTime);
        IInteractable next = null;
        if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, 4f))
            next = hit.collider.GetComponentInParent<IInteractable>();
        if (next != focused)
        {
            focused?.OnLoseFocus(); focused = next; focused?.OnFocus();
        }
        if (focused != null && Input.GetMouseButtonDown(0))
            Begin(focused, focused is InteractableChalkboard ? boardView : focused == trialTerminal ? terminalView : null);
    }

    private void Begin(IInteractable target, Transform pose)
    {
        if (target == null || !(target is IForceExitable exit)) return;
        returnPosition = transform.position; returnRotation = transform.rotation; returnPitch = pitch;
        focused?.OnLoseFocus(); focused = null;
        if (pose != null)
        {
            body.enabled = false;
            transform.position = pose.position - Vector3.up * viewCamera.transform.localPosition.y;
            transform.rotation = Quaternion.Euler(0, pose.eulerAngles.y, 0);
            pitch = Mathf.DeltaAngle(0, pose.eulerAngles.x);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            body.enabled = true;
        }
        session = exit;
        target.Interact();
    }

    private void OnGUI()
    {
        string controls = researchBooks != null && researchBooks.Length == 4
            ? "ARAŞTIRMA DENEMELERİ / KİTAP → DEFTER → TAHTA\n1: Sorular | 2: Tahta | 3: Parçalar | 4: İlkeler | 5: Deney kayıtları\nWASD / fare: yürü ve bak | Sol tık: etkileş | F / Esc: çık"
            : "SENTEZ BLOCKOUT / SADECE ETKİLEŞİM TESTİ\nWASD: yürü | Fare: bak | Sol tık: etkileş\n1: kitap | 2: tahta | F: etkileşimden çık | Esc: çık";
        var research = ResearchPuzzleTrial.Active;
        if (research != null) controls += "\nAraştırma sonuçları: " + research.Completed.Count + " / " + research.Content.Quests.Count
            + (SignalExperiment.Active != null ? "  |  6: Turing deneme kopyası" : "  |  Hedef değişince tahta çalışman saklanır.");
        GUI.Box(new Rect(12, 12, 680, research != null ? 96 : 76), controls);
        if (session == null)
        {
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 12, 30, 30), "+");
            if (focused != null) GUI.Box(new Rect(Screen.width / 2f - 170, Screen.height - 65, 340, 30), focused.GetInteractionPrompt());
        }
    }
}
