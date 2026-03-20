using StarterAssets; // Oyuncu dondurmak için gerekli
using UnityEngine;

public class SymbolOverlayManager : MonoBehaviour
{
    public static SymbolOverlayManager Instance { get; private set; }

    [Header("Görsel Ayarlar")]
    [Tooltip("Sahnede belirecek 3D Sembol Prefab'i")]
    public GameObject symbolPrefab;

    [Tooltip("4 farklı sembolün ID'sine göre atanacak materyaller")]
    public Material[] symbolMaterials;
    public float hoverDistance = 0.15f;

    [Header("Bulmaca (Kitap) Hareket Ayarları")]
    public float moveSpeed = 0.02f;
    public float puzzleRotationSpeed = 10f; // Scroll ile dönme hızı

    [Header("Serbest İnceleme (Inspect) Ayarları")]
    public float inspectDistance = 0.5f; // Yürürken kameraya uzaklığı
    public float freeRotationSpeed = 10f; // Fare ile 3D dönme hızı

    [Header("Çözüm Ayarları (Tolerans)")]
    public float distanceTolerance = 0.1f;
    public float angleTolerance = 20f;
    public AudioClip successSound;
    public AudioClip errorSound;

    private GameObject activeSymbolInstance;
    private MeshRenderer symbolRenderer;
    private Camera mainCam;
    private AudioSource audioSource;

    // Oyuncu dondurma referansları
    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;
    private MonoBehaviour playerLookScript;
    private Animator playerAnimator;

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

        // Oyuncu referanslarını bul
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
        // ESC Menüsü vb. açıksa işlem yapma
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused)
            return;

        InteractableBook currentBook = GameManager.Instance.activeInteraction as InteractableBook;

        // --- T TUŞU İLE AÇ/KAPA KONTROLÜ ---
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (!PuzzleInventoryManager.Instance.isOverlayActive)
            {
                // Envanterde sembol varsa aç
                if (PuzzleInventoryManager.Instance.hasSymbol)
                    ToggleSymbol(true, currentBook);
                else
                    PlaySound(errorSound); // Cepte sembol yok!
            }
            else
            {
                // Zaten ekrandaysa kapat (cebe at)
                ToggleSymbol(false, currentBook);
            }
        }

        // --- EKRANDA SEMBOL VARSA HAREKETLERİ İŞLE ---
        if (PuzzleInventoryManager.Instance.isOverlayActive && activeSymbolInstance != null)
        {
            if (currentBook != null && currentBook.isOpen)
            {
                // 1. KİTAP MODU (Bulmaca Çözümü)
                HandlePuzzleManipulation();
                CheckSuccess(currentBook);
            }
            else
            {
                // 2. SERBEST İNCELEME MODU (Yürürken T'ye basıldıysa)
                HandleFreeInspectManipulation();
            }
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

            // Sembolün materyalini ayarla
            int currentID = PuzzleInventoryManager.Instance.currentSymbolID;
            if (currentID >= 0 && currentID < symbolMaterials.Length)
            {
                symbolRenderer.material = symbolMaterials[currentID];
            }

            if (book != null && book.isOpen)
            {
                // KİTAP MODU: Kitabın üstünde spawnla
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
            else
            {
                // SERBEST İNCELEME MODU: Kameranın önünde spawnla ve oyuncuyu dondur
                FreezePlayer(true);
                activeSymbolInstance.transform.position =
                    mainCam.transform.position + mainCam.transform.forward * inspectDistance;
                activeSymbolInstance.transform.rotation = mainCam.transform.rotation; // Yüzü kameraya baksın
                activeSymbolInstance.transform.SetParent(mainCam.transform, true);
            }
        }
        else
        {
            // Sembolü ekrandan gizle
            if (activeSymbolInstance != null)
                activeSymbolInstance.SetActive(false);

            // Eğer kitap açık değilse (yani serbest yürüyüşte incelemeyi kapattıysak) oyuncuyu tekrar çöz
            if (book == null || !book.isOpen)
            {
                FreezePlayer(false);
            }
        }
    }

    // --- KİTAP ESNASINDAKİ 2D HAREKETLER ---
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
            activeSymbolInstance.transform.Rotate(0, 0, scroll * puzzleRotationSpeed, Space.Self);
        }
    }

    // --- YÜRÜRKEN EKRANA GELEN 3D İNCELEME HAREKETLERİ ---
    private void HandleFreeInspectManipulation()
    {
        // Alternatif olarak F ile de çıkabilme
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleSymbol(false, null);
            return;
        }

        // Fare ile 3D objeyi çevirme
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X") * freeRotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * freeRotationSpeed;

            activeSymbolInstance.transform.Rotate(mainCam.transform.up, -mouseX, Space.World);
            activeSymbolInstance.transform.Rotate(mainCam.transform.right, mouseY, Space.World);
        }
    }

    // --- OYUNCU HAREKETLERİNİ VE ANİMASYONUNU DONDUR/ÇÖZ ---
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
                // Çözüldüğünde fareyi merkeze kilitle
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

    // --- BAŞARI KONTROLÜ (Değişmedi) ---
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

        PuzzleInventoryManager.Instance.RemoveSymbol(); // Envanteri boşalt
        book.TriggerPasswordFind(); // Şifreyi deftere yaz
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
