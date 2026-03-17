using UnityEngine;

public class SymbolOverlayManager : MonoBehaviour
{
    public static SymbolOverlayManager Instance { get; private set; }

    [Header("Görsel Ayarlar")]
    [Tooltip("Sahnede belirecek 3D Sembol Prefab'i")]
    public GameObject symbolPrefab;

    [Tooltip("4 farklı sembolün ID'sine göre atanacak materyaller")]
    public Material[] symbolMaterials;

    [Tooltip("Sembol sayfanın ne kadar üstünde süzülsün?")]
    public float hoverDistance = 0.15f;

    [Header("Hareket Ayarları")]
    public float moveSpeed = 0.02f;
    public float rotationSpeed = 10f;

    [Header("Çözüm Ayarları (Tolerans)")]
    [Tooltip("Sembolün hedefe ne kadar yaklaşması yeterli? (Ekran Uzayında)")]
    public float distanceTolerance = 0.1f;

    [Tooltip("Açısal olarak kaç derece sapma payı olsun?")]
    public float angleTolerance = 20f;
    public AudioClip successSound;
    public AudioClip errorSound;

    private GameObject activeSymbolInstance;
    private MeshRenderer symbolRenderer;
    private Camera mainCam;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        // --- ESC (PAUSE) MENÜSÜ ÇAKIŞMA ÖNLEYİCİ ---
        // Oyun duraklatıldıysa hiçbir işlemi yapma ve çık!
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused)
            return;
        // ------------------------------------------

        InteractableBook currentBook = GameManager.Instance.activeInteraction as InteractableBook;

        if (currentBook == null || !currentBook.isOpen)
        {
            if (
                PuzzleInventoryManager.Instance != null
                && PuzzleInventoryManager.Instance.isOverlayActive
            )
                ToggleSymbol(false, null);
            return;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (!PuzzleInventoryManager.Instance.isOverlayActive)
            {
                if (PuzzleInventoryManager.Instance.hasSymbol)
                    ToggleSymbol(true, currentBook);
                else
                    PlaySound(errorSound);
            }
            else
            {
                ToggleSymbol(false, currentBook);
            }
        }

        if (PuzzleInventoryManager.Instance.isOverlayActive && activeSymbolInstance != null)
        {
            HandleManipulation();
            CheckSuccess(currentBook);
        }
    }

    private void ToggleSymbol(bool state, InteractableBook book)
    {
        PuzzleInventoryManager.Instance.isOverlayActive = state;

        if (state)
        {
            if (activeSymbolInstance == null)
            {
                activeSymbolInstance = Instantiate(symbolPrefab);
                symbolRenderer = activeSymbolInstance.GetComponentInChildren<MeshRenderer>();
            }

            activeSymbolInstance.SetActive(true);

            int currentID = PuzzleInventoryManager.Instance.currentSymbolID;
            if (currentID >= 0 && currentID < symbolMaterials.Length)
            {
                symbolRenderer.material = symbolMaterials[currentID];
            }

            if (book != null)
            {
                Vector3 spawnLocalPos = new Vector3(
                    0,
                    0,
                    book.viewPositionOffset.z - hoverDistance
                );
                activeSymbolInstance.transform.position = mainCam.transform.TransformPoint(
                    spawnLocalPos
                );

                activeSymbolInstance.transform.rotation = mainCam.transform.rotation;
                activeSymbolInstance.transform.SetParent(book.transform, true);
            }
        }
        else
        {
            if (activeSymbolInstance != null)
                activeSymbolInstance.SetActive(false);
        }
    }

    private void HandleManipulation()
    {
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            activeSymbolInstance.transform.position += mainCam.transform.right * mouseX * moveSpeed;
            activeSymbolInstance.transform.position += mainCam.transform.up * mouseY * moveSpeed;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            activeSymbolInstance.transform.Rotate(0, 0, scroll * rotationSpeed, Space.Self);
        }
    }

    private void CheckSuccess(InteractableBook book)
    {
        if (
            !book.isSymbolTargetBook
            || book.requiredSymbolID != PuzzleInventoryManager.Instance.currentSymbolID
        )
            return;

        if (!book.IsOnPage(book.symbolPuzzlePage))
            return;

        if (book.targetSymbolAnchor == null)
            return;

        Vector3 localSymbolPos = mainCam.transform.InverseTransformPoint(
            activeSymbolInstance.transform.position
        );
        Vector3 localAnchorPos = mainCam.transform.InverseTransformPoint(
            book.targetSymbolAnchor.position
        );

        localSymbolPos.z = 0f;
        localAnchorPos.z = 0f;

        float distance = Vector3.Distance(localSymbolPos, localAnchorPos);

        float angle = Mathf.Abs(
            Mathf.DeltaAngle(
                activeSymbolInstance.transform.eulerAngles.z,
                book.targetSymbolAnchor.eulerAngles.z
            )
        );
#if UNITY_EDITOR
        float debugLength = 0.08f;

        // 1. Hedefin Eksenleri (Target Anchor)
        Debug.DrawRay(
            book.targetSymbolAnchor.position,
            book.targetSymbolAnchor.right * debugLength,
            Color.red
        );
        Debug.DrawRay(
            book.targetSymbolAnchor.position,
            book.targetSymbolAnchor.up * debugLength,
            Color.green
        );
        Debug.DrawRay(
            book.targetSymbolAnchor.position,
            book.targetSymbolAnchor.forward * debugLength,
            Color.blue
        );

        // 2. Sembolün Eksenleri (Active Symbol)
        Debug.DrawRay(
            activeSymbolInstance.transform.position,
            activeSymbolInstance.transform.right * debugLength,
            Color.red
        );
        Debug.DrawRay(
            activeSymbolInstance.transform.position,
            activeSymbolInstance.transform.up * debugLength,
            Color.green
        );
        Debug.DrawRay(
            activeSymbolInstance.transform.position,
            activeSymbolInstance.transform.forward * debugLength,
            Color.blue
        );

        // 3. Merkezleri bağlayan mesafe çizgisi
        Color lineColor = distance <= distanceTolerance ? Color.green : Color.red;
        Debug.DrawLine(
            activeSymbolInstance.transform.position,
            book.targetSymbolAnchor.position,
            lineColor
        );
#endif
        if (distance <= distanceTolerance && angle <= angleTolerance)
        {
            TriggerSuccess(book);
        }
    }

    private void TriggerSuccess(InteractableBook book)
    {
        PlaySound(successSound);

        // --- ÖNCEKİ MESAJDA KONUŞTUĞUMUZ DÜZELTME (SİLME VE ŞİFRE ALMA) ---
        if (activeSymbolInstance != null)
        {
            Destroy(activeSymbolInstance); // 3D objeyi tamamen dünyadan sil
            activeSymbolInstance = null;
        }

        PuzzleInventoryManager.Instance.RemoveSymbol(); // Envanteri boşalt

        // Şifreyi oyuncuya ver ve deftere eklet
        book.TriggerPasswordFind();
        // ------------------------------------------------------------------
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
