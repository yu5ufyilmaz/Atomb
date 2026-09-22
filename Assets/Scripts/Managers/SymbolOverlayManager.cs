using UnityEngine;

public class SymbolOverlayManager : MonoBehaviour
{
    public static SymbolOverlayManager Instance { get; private set; }

    [Header("Serbest İnceleme (Inspect) Ayarları")]
    public float inspectDistance = 0.5f;
    public float freeRotationSpeed = 10f;
    public AudioClip errorSound;

    private GameObject activeSymbolInstance;
    private Camera mainCam;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused)
            return;

        // ÇAKIŞMAYI ÖNLEYEN KRİTİK SATIR: Eğer oyuncu bir Kitap okuyorsa burası 'T' tuşunu yoksayar!
        if (
            GameManager.Instance != null
            && GameManager.Instance.activeInteraction is InteractableBook
        )
            return;

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (!PuzzleInventoryManager.Instance.isOverlayActive)
            {
                if (PuzzleInventoryManager.Instance.HasActiveItem())
                    ToggleFreeInspect(true);
                else if (errorSound != null)
                    GetComponent<AudioSource>()?.PlayOneShot(errorSound);
            }
            else
            {
                ToggleFreeInspect(false);
            }
        }

        if (PuzzleInventoryManager.Instance.isOverlayActive && activeSymbolInstance != null)
        {
            HandleFreeInspectManipulation();
        }
    }

    private void ToggleFreeInspect(bool state)
    {
        PuzzleInventoryManager.Instance.isOverlayActive = state;
        if (PlayerInteraction.Instance != null)
            PlayerInteraction.Instance.ToggleCrosshair(!state);

        // --- KAMERA DÖNME BUG'I ÇÖZÜMÜ ---
        // Oyuncunun fare ile kamerayı döndürmesini sağlayan girdiyi aç/kapat
        var inputs = Object.FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.cursorInputForLook = !state;

            // EKSİK OLAN KRİTİK SATIR BURASI: Kamera kilitlendiğinde ivmeyi sıfırla!
            if (state)
                inputs.look = Vector2.zero;
        }
        // ----------------------------------

        if (state)
        {
            PuzzleItemSO currentItem = PuzzleInventoryManager.Instance.activeItem;
            if (activeSymbolInstance != null)
                Destroy(activeSymbolInstance);

            activeSymbolInstance = Instantiate(currentItem.overlayPrefab, mainCam.transform);
            activeSymbolInstance.transform.localPosition = new Vector3(0, 0, inspectDistance);
            activeSymbolInstance.transform.localRotation = Quaternion.Euler(
                currentItem.defaultRotationOffset
            );
            activeSymbolInstance.SetActive(true);

            var player = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
            if (player != null)
                player.SetFrozen(true, true, false);
        }
        else
        {
            if (activeSymbolInstance != null)
                Destroy(activeSymbolInstance);

            var player = Object.FindFirstObjectByType<StarterAssets.CharacterController>();
            if (player != null)
                player.SetFrozen(false, false, false);
        }
    }

    private void HandleFreeInspectManipulation()
    {
        if (Input.GetMouseButton(0))
        {
            float rotX = Input.GetAxis("Mouse X") * freeRotationSpeed;
            float rotY = Input.GetAxis("Mouse Y") * freeRotationSpeed;
            activeSymbolInstance.transform.Rotate(mainCam.transform.up, -rotX, Space.World);
            activeSymbolInstance.transform.Rotate(mainCam.transform.right, rotY, Space.World);
        }
    }
}
