using StarterAssets;
using UnityEngine;

public class SymbolOverlayManager : MonoBehaviour
{
    public static SymbolOverlayManager Instance { get; private set; }

    [Header("Görsel Ayarlar")]
    [Tooltip("Envanterdeki ID'lere karşılık gelen Sembol Prefab'leri (Sırasıyla 0, 1, 2, 3...)")]
    public GameObject[] symbolPrefabs;
    public float hoverDistance = 0.15f;

    [Header("Bulmaca (Kitap) Hareket Ayarları")]
    public float moveSpeed = 0.02f;
    public float puzzleRotationSpeed = 10f;

    [Tooltip(
        "Scroll tekerleğiyle çevirirken modelin hangi eksende döneceğini belirler. Z için (0,0,1), X için (1,0,0), Y için (0,1,0). Tersine dönmesi için eksi değer girin örn: (0,0,-1)"
    )]
    public Vector3 scrollRotationAxis = new Vector3(0, 0, 1); // YENİ EKLENEN EKSEN AYARI

    [Header("Serbest İnceleme (Inspect) Ayarları")]
    public float inspectDistance = 0.5f;
    public float freeRotationSpeed = 10f;
    public Vector3 spawnScaleOffset = Vector3.zero;

    [Tooltip(
        "Sembol ekrana ilk geldiğinde ters veya yan duruyorsa buradaki X,Y,Z değerleriyle oynayarak düzeltebilirsin (Örn: X:90)"
    )]
    public Vector3 spawnRotationOffset = Vector3.zero;

    [Header("Çözüm Ayarları (Tolerans)")]
    public float distanceTolerance = 0.1f;
    public float angleTolerance = 20f;
    public AudioClip successSound;
    public AudioClip errorSound;

    private GameObject activeSymbolInstance;
    private Camera mainCam;
    private AudioSource audioSource;

    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;
    private MonoBehaviour playerLookScript;
    private Animator playerAnimator;

    private int spawnedSymbolID = -1;

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

        playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerController != null)
        {
            playerGameScript = playerController.GetComponent<StarterAssets.CharacterController>();
            playerLookScript =
                playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>();
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused)
            return;

        InteractableBook currentBook = GameManager.Instance.activeInteraction as InteractableBook;

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
            if (currentBook != null && currentBook.isOpen)
            {
                HandlePuzzleManipulation();
                CheckSuccess(currentBook);
            }
            else
            {
                HandleFreeInspectManipulation();
            }
        }
    }

    private void ToggleSymbol(bool state, InteractableBook book)
    {
        PuzzleInventoryManager.Instance.isOverlayActive = state;
        if (PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.ToggleCrosshair(!state);
        }

        if (state)
        {
            int currentID = PuzzleInventoryManager.Instance.currentSymbolID;

            if (activeSymbolInstance != null && spawnedSymbolID != currentID)
            {
                Destroy(activeSymbolInstance);
                activeSymbolInstance = null;
            }

            if (activeSymbolInstance == null)
            {
                if (
                    currentID >= 0
                    && currentID < symbolPrefabs.Length
                    && symbolPrefabs[currentID] != null
                )
                {
                    activeSymbolInstance = Instantiate(symbolPrefabs[currentID]);
                    spawnedSymbolID = currentID;
                }
                else
                {
                    Debug.LogError(
                        $"[SymbolOverlayManager] {currentID} ID'li sembol prefab'i bulunamadı! Inspector'ı kontrol et."
                    );
                    return;
                }
            }

            activeSymbolInstance.SetActive(true);

            if (book != null && book.isOpen)
            {
                Vector3 spawnLocalPos = new Vector3(
                    0,
                    0,
                    book.viewPositionOffset.z - hoverDistance
                );
                activeSymbolInstance.transform.position = mainCam.transform.TransformPoint(
                    spawnLocalPos
                );

                // Kitap için de ofsetli rotasyon uygula
                activeSymbolInstance.transform.rotation =
                    mainCam.transform.rotation * Quaternion.Euler(spawnRotationOffset);
                activeSymbolInstance.transform.SetParent(book.transform, true);
            }
            else
            {
                FreezePlayer(true);
                activeSymbolInstance.transform.position =
                    mainCam.transform.position + mainCam.transform.forward * inspectDistance;

                // Serbest inceleme için ofsetli rotasyon uygula
                activeSymbolInstance.transform.rotation =
                    mainCam.transform.rotation * Quaternion.Euler(spawnRotationOffset);
                activeSymbolInstance.transform.SetParent(mainCam.transform, true);
                activeSymbolInstance.transform.localScale = spawnScaleOffset; // Ölçek ofseti sadece serbest inceleme için uygulanır
            }
        }
        else
        {
            if (activeSymbolInstance != null)
                activeSymbolInstance.SetActive(false);

            if (book == null || !book.isOpen)
            {
                FreezePlayer(false);
            }
        }
    }

    private void HandlePuzzleManipulation()
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
            // YENİ SİSTEM: Artık senin belirlediğin scrollRotationAxis kullanılıyor
            activeSymbolInstance.transform.Rotate(
                scrollRotationAxis * (scroll * puzzleRotationSpeed),
                Space.Self
            );
        }
    }

    private void HandleFreeInspectManipulation()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleSymbol(false, null);
            return;
        }

        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X") * freeRotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * freeRotationSpeed;

            activeSymbolInstance.transform.Rotate(mainCam.transform.up, -mouseX, Space.World);
            activeSymbolInstance.transform.Rotate(mainCam.transform.right, mouseY, Space.World);
        }
    }

    private void FreezePlayer(bool freeze)
    {
        if (playerController != null)
            playerController.enabled = !freeze;
        if (playerGameScript != null)
            playerGameScript.enabled = !freeze;
        if (playerAnimator != null)
            playerAnimator.enabled = !freeze;

        if (playerLookScript != null)
        {
            playerLookScript.enabled = !freeze;
            if (!freeze)
            {
                if (playerLookScript is StarterAssetsInputs inputs)
                    inputs.cursorInputForLook = true;
                else
                {
                    var inputsComp = playerLookScript.GetComponent<StarterAssetsInputs>();
                    if (inputsComp != null)
                        inputsComp.cursorInputForLook = true;
                }
            }
        }

        Cursor.lockState = freeze ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = freeze;
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

        if (distance <= distanceTolerance && angle <= angleTolerance)
        {
            TriggerSuccess(book);
        }
    }

    private void TriggerSuccess(InteractableBook book)
    {
        PlaySound(successSound);

        if (activeSymbolInstance != null)
        {
            Destroy(activeSymbolInstance);
            activeSymbolInstance = null;
        }

        spawnedSymbolID = -1;
        PuzzleInventoryManager.Instance.RemoveSymbol();
        book.TriggerPasswordFind();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
