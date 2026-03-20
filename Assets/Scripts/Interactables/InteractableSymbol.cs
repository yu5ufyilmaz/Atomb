using System.Collections;
using StarterAssets; // Oyuncu kontrollerini dondurmak için gerekli
using UnityEngine;

public class InteractableSymbol : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Sembol Kimliği")]
    [Tooltip("Bu sembol hangi ID'ye sahip? (0, 1, 2, 3)")]
    public int symbolID;

    [Header("Etkileşim Ayarları")]
    public string promptText = "Sembolü İncele";
    public AudioClip pickupSound;

    [Header("Sembol Hikayesi / İpucu")]
    [TextArea(3, 10)]
    public string symbolLore =
        "Bu sembol kadim bir ritüeli temsil ediyor...\nŞifresi şu kitapta olabilir...";

    [Header("İnceleme (Inspect) Ayarları")]
    public float inspectDistance = 0.5f; // Obje kameranın ne kadar önünde duracak?
    public float rotationSpeed = 10f; // Fare ile çevirme hassasiyeti
    public float moveDuration = 0.3f; // Ekrana gelme animasyon hızı

    // --- Gizli Değişkenler ---
    private bool isInspecting = false;
    private bool isAnimating = false;
    private Camera mainCamera;

    // Oyuncu dondurma referansları
    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;
    private MonoBehaviour playerLookScript;
    private Animator playerAnimator; // YENİ EKLENEN: Animasyon dondurucu

    private void Start()
    {
        mainCamera = Camera.main;

        // Oyuncu scriptlerini bul
        playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerController != null)
        {
            playerGameScript = playerController.GetComponent<StarterAssets.CharacterController>();
            playerLookScript =
                playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>(); // YENİ EKLENEN
        }
    }

    public void Interact()
    {
        if (isInspecting || isAnimating)
            return;
        StartCoroutine(StartInspectMode());
    }

    private IEnumerator StartInspectMode()
    {
        isAnimating = true;
        isInspecting = true;

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        // --- OYUNCUYU TAMAMEN DONDUR (Animator Dahil) ---
        if (playerController != null)
            playerController.enabled = false;
        if (playerGameScript != null)
            playerGameScript.enabled = false;
        if (playerLookScript != null)
            playerLookScript.enabled = false;
        if (playerAnimator != null)
            playerAnimator.enabled = false; // YENİ EKLENEN

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 targetPos =
            mainCamera.transform.position + mainCamera.transform.forward * inspectDistance;
        Quaternion targetRot = mainCamera.transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.SetParent(mainCamera.transform, true);
        isAnimating = false;
    }

    private void Update()
    {
        if (isInspecting && !isAnimating)
        {
            if (Input.GetMouseButton(0))
            {
                float rotX = Input.GetAxis("Mouse X") * rotationSpeed;
                float rotY = Input.GetAxis("Mouse Y") * rotationSpeed;

                transform.Rotate(mainCamera.transform.up, -rotX, Space.World);
                transform.Rotate(mainCamera.transform.right, rotY, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.F))
            {
                CollectAndExit();
            }
        }
    }

    private void CollectAndExit()
    {
        isInspecting = false;

        if (PuzzleInventoryManager.Instance != null)
        {
            PuzzleInventoryManager.Instance.PickupSymbol(symbolID);

            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, mainCamera.transform.position);

            if (NotebookUI.Instance != null)
                NotebookUI.Instance.AddLorePage(symbolLore);

            Debug.Log($"[Oyun Dünyası] Oyuncu {symbolID} ID'li sembolü inceledi ve cebine attı!");
        }

        if (
            GameManager.Instance != null
            && GameManager.Instance.activeInteraction == (IForceExitable)this
        )
            GameManager.Instance.activeInteraction = null;

        // --- OYUNCU KONTROLLERİNİ VE ANİMASYONUNU GERİ AÇ ---
        if (playerController != null)
            playerController.enabled = true;
        if (playerGameScript != null)
            playerGameScript.enabled = true;
        if (playerAnimator != null)
            playerAnimator.enabled = true; // YENİ EKLENEN

        if (playerLookScript != null)
        {
            playerLookScript.enabled = true;
            if (playerLookScript is StarterAssetsInputs inputs)
            {
                inputs.cursorInputForLook = true;
            }
            else
            {
                var inputsComp = playerLookScript.GetComponent<StarterAssetsInputs>();
                if (inputsComp != null)
                    inputsComp.cursorInputForLook = true;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Destroy(gameObject);
    }

    public void ForceExit()
    {
        if (isInspecting && !isAnimating)
        {
            CollectAndExit();
        }
    }

    public string GetInteractionPrompt()
    {
        if (isInspecting)
            return "İncele: Sol Tık Basılı Tut | Almak İçin: [T]";

        return promptText;
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }
}
