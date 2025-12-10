using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;

public class InteractableBook : MonoBehaviour, IInteractable, IForceExitable
{
    // --- EDİTÖRDE GÖRÜNMESİ GEREKENLER (AYARLAR) ---

    [Header("📖 Kitap Ayarları")]
    [Tooltip("Kitabın animasyon bileşeni")]
    public Animator bookAnimator;

    [Tooltip("Sayfa çevirme süresi")]
    public float pageFlipDuration = 0.8f;

    [Tooltip("Toplam sayfa sayısı")]
    public int totalPages = 8;

    [Tooltip("Son sayfadan başa dönsün mü?")]
    public bool allowLoop = false;

    [Header("🎨 Görsel & Materyal")]
    public SkinnedMeshRenderer bookSkinnedMeshRenderer;
    public int bookMaterialIndex = 0;
    public Material bookPagesMaterial; // Runtime'da instance olacak

    [Space(10)]
    public GameObject pageFlipObject;
    public MeshRenderer pageFlipRenderer;
    public Material pageTurnMaterial; // Runtime'da instance olacak

    [Header("🔊 Ses Efektleri")]
    public AudioSource audioSource;
    public AudioClip bookOpenSound;
    public AudioClip bookCloseSound;
    public AudioClip pageFlipSound;
    public AudioClip passwordFoundSound;

    [Header("🎥 Kamera & Pozisyon")]
    public Transform cameraTransform;
    public Vector3 viewPositionOffset = new Vector3(0, 0, 0.5f);
    public Vector3 viewRotationOffset = Vector3.zero;
    public float moveDuration = 0.5f;

    [Header("UI Bağlantıları")]
    public GameObject bookUI;
    public TextMeshProUGUI pageNumberText;

    [Header("Gizmos Settings")]
    [SerializeField]
    private Vector2 singlePageSize = new Vector2(0.16f, 0.32f);

    [SerializeField]
    private float gizmoYOffset = 0.005f;

    [SerializeField]
    private float animationDuration = 1f;

    [Header("📘 KİTAP KİMLİĞİ")]
    [Tooltip("Bu kitabın türü ne? (Kırmızı Kitap Datası vb.)")]
    public PasswordData bookIdentity;

    [Tooltip("Bu kitaptan şifre çıkabilir mi? (Süs ise kapat)")]
    public bool canContainPassword = true;

    // --- EDİTÖRDE GİZLENECEK TEKNİK DEĞİŞKENLER (Runtime) ---
    // Bunlar Manager ve kod tarafından yönetilir.

    [HideInInspector]
    public bool isOpen = false;

    [HideInInspector]
    public bool isAnimating = false;

    [HideInInspector]
    public int currentPage = 0;

    // Password System (PasswordManager tarafından doldurulur)
    [HideInInspector]
    public bool isPasswordBook = false;

    [HideInInspector]
    public int passwordPage = -1;

    [HideInInspector]
    public string passwordID = "";

    [HideInInspector]
    public Rect passwordHotspotUV;

    [HideInInspector]
    public bool hasPasswordBeenFound = false;

    // Internal References
    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;
    private MonoBehaviour playerLookScript;
    private Animator playerAnimator;
    private Camera mainCamera;
    private BoxCollider interactionCollider;

    // ...

    [Header("Raycast Ayarları")]
    [Tooltip("Sayfaların olduğu Mesh Collider buraya atanmalı!")]
    [SerializeField]
    private Collider bookCollider; // SerializeField yaptık

    // ...

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Transform originalParent;

    // Animator Hashleri
    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");
    private static readonly int PageNumber = Animator.StringToHash("PageNumber");

    private int pageIndexL;
    private int pageIndexR;

    private void Awake()
    {
        // Materyal kopyalarını oluştur (Instance) ki diğer kitaplar etkilenmesin
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial = new Material(bookPagesMaterial);
            if (bookSkinnedMeshRenderer != null)
            {
                Material[] materials = bookSkinnedMeshRenderer.materials;
                if (bookMaterialIndex < materials.Length)
                {
                    materials[bookMaterialIndex] = bookPagesMaterial;
                    bookSkinnedMeshRenderer.materials = materials;
                }
            }
        }
        else if (bookSkinnedMeshRenderer != null)
        {
            // Eğer slot boşsa renderer'dan alıp kopyala
            Material[] materials = bookSkinnedMeshRenderer.sharedMaterials;
            if (bookMaterialIndex < materials.Length && materials[bookMaterialIndex] != null)
            {
                bookPagesMaterial = new Material(materials[bookMaterialIndex]);
                materials[bookMaterialIndex] = bookPagesMaterial;
                bookSkinnedMeshRenderer.materials = materials;
            }
        }

        if (pageTurnMaterial != null)
        {
            pageTurnMaterial = new Material(pageTurnMaterial);
            if (pageFlipRenderer != null)
                pageFlipRenderer.material = pageTurnMaterial;
        }
        else if (pageFlipRenderer != null)
        {
            pageTurnMaterial = new Material(pageFlipRenderer.sharedMaterial);
            pageFlipRenderer.material = pageTurnMaterial;
        }
    }

    private void Start()
    {
        // Etkileşim için BoxCollider'ı al veya oluştur (Bu ana objede kalmalı)
        interactionCollider = GetComponent<BoxCollider>();
        if (interactionCollider == null)
            interactionCollider = gameObject.AddComponent<BoxCollider>();

        // --- GÜNCELLEME: Alt Nesneleri Tara ---
        if (bookCollider == null)
        {
            // 1. Öncelik: Kendisinde veya ALT NESNELERİNDE MeshCollider ara
            bookCollider = GetComponentInChildren<MeshCollider>();

            // 2. Öncelik: Eğer MeshCollider yoksa, Alt nesnelerde Trigger OLMAYAN herhangi bir collider ara
            if (bookCollider == null)
            {
                // Tüm çocuklardaki colliderları getir
                Collider[] cols = GetComponentsInChildren<Collider>();
                foreach (var c in cols)
                {
                    // Etkileşim kutusu olmayan ve Trigger olmayan ilk collider'ı al
                    if (!c.isTrigger && c != interactionCollider)
                    {
                        bookCollider = c;
                        break;
                    }
                }
            }
        }

        if (bookCollider == null)
            Debug.LogError(
                $"{gameObject.name}: Raycast için uygun bir Collider (MeshCollider) ne kendisinde ne de alt nesnelerinde bulunamadı!",
                this
            );

        // --- Diğer Başlangıç Ayarları (Aynı Kalıyor) ---
        if (bookAnimator == null)
            bookAnimator = GetComponent<Animator>();

        if (bookAnimator != null)
        {
            bookAnimator.SetBool(IsOpenBool, isOpen);
            bookAnimator.SetInteger(PageNumber, currentPage);
        }

        playerController = FindObjectOfType<UnityEngine.CharacterController>();
        if (playerController != null)
        {
            playerGameScript = playerController.GetComponent<StarterAssets.CharacterController>();
            playerLookScript =
                playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>();
        }

        mainCamera = Camera.main;
        if (cameraTransform == null && mainCamera != null)
            cameraTransform = mainCamera.transform;

        if (bookUI != null)
            bookUI.SetActive(false);

        if (pageFlipObject != null)
            pageFlipObject.SetActive(false);

        InitializePages();
    }

    private void InitializePages()
    {
        pageIndexL = 0;
        pageIndexR = 1;
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial.SetFloat("_PageCount", totalPages);
            bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
            bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
        }
        if (pageTurnMaterial != null)
            pageTurnMaterial.SetFloat("_PageCount", totalPages);
    }

    private void Update()
    {
        if (isOpen && !isAnimating)
        {
            HandlePageInput();

            // Sadece şifreli kitapsa ve şifre henüz bulunmadıysa tıkla
            if (isPasswordBook && !hasPasswordBeenFound && Input.GetMouseButtonDown(0))
                CheckForPasswordClick();
        }

        if (isOpen && Input.GetKeyDown(KeyCode.F))
        {
            if (!isAnimating)
                StartCoroutine(CloseBook());
        }
    }

    private void CheckForPasswordClick()
    {
        // Sadece şifrenin olduğu sayfalar açıksa işlem yap
        if (pageIndexL != passwordPage && pageIndexR != passwordPage)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // ÖNEMLİ: Vurulan obje, bizim sayfaların olduğu Mesh Collider mı?
            // (Etkileşim kutusuna (BoxCollider) tıklarsan UV (0,0) döner ve çalışmaz)
            if (hit.collider == bookCollider)
            {
                Vector2 uv = hit.textureCoord;

                // Sol sayfa mı Sağ sayfa mı? (Standart kitap UV'sinde X > 0.5 sağ sayfadır)
                bool hitRightPage = uv.x > 0.5f;

                // Tıklanan taraf ile şifrenin olduğu taraf eşleşiyor mu?
                bool isTargetPage =
                    (hitRightPage && pageIndexR == passwordPage)
                    || (!hitRightPage && pageIndexL == passwordPage);

                if (isTargetPage)
                {
                    // Global UV'yi, Sayfa İçi Lokal UV'ye (0-1) çevir
                    // Sol sayfa (0-0.5) -> 0-1'e map edilir
                    // Sağ sayfa (0.5-1) -> 0-1'e map edilir
                    float pageLocalU = hitRightPage ? (uv.x - 0.5f) * 2.0f : uv.x * 2.0f;
                    float pageLocalV = uv.y;
                    Vector2 pageUV = new Vector2(pageLocalU, pageLocalV);

                    // Editörde çizdiğimiz kutunun içine denk geldi mi?
                    if (passwordHotspotUV.Contains(pageUV))
                    {
                        TriggerPasswordFind();
                    }
                }
            }
        }
    }

    private void TriggerPasswordFind()
    {
        if (hasPasswordBeenFound || !isPasswordBook)
            return;

        hasPasswordBeenFound = true;

        // Şifreyi Manager'a bildir
        PasswordManager.Instance.DiscoverClue(passwordID);

        PlaySound(passwordFoundSound);

        // Bildirim UI
        if (NotebookUI.Instance != null)
            NotebookUI.Instance.ShowPasswordNotification(passwordID);

        StartCoroutine(CloseBook());
    }

    public void Interact()
    {
        if (isAnimating || isOpen)
            return;
        StartCoroutine(OpenBook());
    }

    public string GetInteractionPrompt()
    {
        if (isAnimating)
            return "";
        return isOpen ? "[F] Kitabı Kapat" : "[Sol Tık] Kitabı Aç";
    }

    private IEnumerator OpenBook()
    {
        isAnimating = true;
        isOpen = true;

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        // Oyuncu kontrollerini kapat
        if (interactionCollider != null)
            interactionCollider.enabled = false;
        if (playerGameScript != null)
            playerGameScript.enabled = false;
        if (playerController != null)
            playerController.enabled = false;
        if (playerLookScript != null)
            playerLookScript.enabled = false;
        if (playerAnimator != null)
            playerAnimator.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Kameraya ebeveynle
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        transform.SetParent(cameraTransform, true);

        float t = 0f;
        Vector3 startLocalPos = transform.localPosition;
        Quaternion startLocalRot = transform.localRotation;
        Quaternion targetLocalRot = Quaternion.Euler(viewRotationOffset);

        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
            transform.localPosition = Vector3.Lerp(startLocalPos, viewPositionOffset, smoothT);
            transform.localRotation = Quaternion.Slerp(startLocalRot, targetLocalRot, smoothT);
            yield return null;
        }

        if (bookAnimator != null)
        {
            bookAnimator.SetTrigger(OpenTrigger);
            bookAnimator.SetBool(IsOpenBool, true);
        }
        PlaySound(bookOpenSound);

        yield return new WaitForSeconds(animationDuration);

        // Sayfa numaralarını sıfırla/başlat
        pageIndexL = 0;
        pageIndexR = 1;
        currentPage = 1;
        UpdateBookPagesMaterial();

        if (bookUI != null)
        {
            bookUI.SetActive(true);
            UpdatePageUI();
        }
        isAnimating = false;
    }

    private IEnumerator CloseBook()
    {
        isAnimating = true;
        isOpen = false;

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;

        if (bookUI != null)
            bookUI.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (bookAnimator != null)
        {
            bookAnimator.SetTrigger(CloseTrigger);
            bookAnimator.SetBool(IsOpenBool, false);
        }
        PlaySound(bookCloseSound);

        yield return new WaitForSeconds(animationDuration);

        // Eski yerine dön
        Vector3 targetWorldPosition;
        Quaternion targetWorldRotation;
        if (originalParent != null)
        {
            targetWorldPosition = originalParent.TransformPoint(originalLocalPosition);
            targetWorldRotation = originalParent.rotation * originalLocalRotation;
        }
        else
        {
            targetWorldPosition = originalLocalPosition;
            targetWorldRotation = originalLocalRotation;
        }

        float t = 0f;
        Vector3 startWorldPos = transform.position;
        Quaternion startWorldRot = transform.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
            transform.position = Vector3.Lerp(startWorldPos, targetWorldPosition, smoothT);
            transform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRotation, smoothT);
            yield return null;
        }
        transform.SetParent(originalParent, true);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;

        // Kontrolleri aç
        if (playerAnimator != null)
            playerAnimator.enabled = true;
        if (playerController != null)
            playerController.enabled = true;
        if (playerGameScript != null)
            playerGameScript.enabled = true;
        if (playerLookScript != null)
            playerLookScript.enabled = true;
        if (interactionCollider != null)
            interactionCollider.enabled = true;

        currentPage = 0;
        isAnimating = false;
    }

    private void HandlePageInput()
    {
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            NextPage();
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            PreviousPage();
    }

    public void NextPage()
    {
        if (isAnimating)
            return;
        if (!allowLoop && pageIndexR >= totalPages - 1)
            return;
        StartCoroutine(PageFlip(1));
    }

    public void PreviousPage()
    {
        if (isAnimating)
            return;
        if (!allowLoop && pageIndexL <= 0)
            return;
        StartCoroutine(PageFlip(-1));
    }

    private IEnumerator PageFlip(int direction)
    {
        if (direction == 0)
            yield break;
        isAnimating = true;

        if (pageFlipObject != null)
            pageFlipObject.SetActive(true);

        if (pageTurnMaterial != null)
        {
            if (direction > 0)
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexR);
            else
                pageTurnMaterial.SetFloat("_PageIndex", pageIndexL - 1);
        }

        float t = 0f;
        float flipSpeed = 1f / pageFlipDuration;
        bool indicesUpdated = false;

        while (t < 1f)
        {
            t += Time.deltaTime * flipSpeed;
            t = Mathf.Clamp01(t);

            // Sayfa çevirme eğrisi
            float v = t * t * t * (t * (t * 6f - 15f) + 10f);
            float flipAmount = (direction > 0) ? v : 1f - v;

            if (pageTurnMaterial != null)
                pageTurnMaterial.SetFloat("_PageFlip", flipAmount);

            // Sayfanın yarısında indexleri güncelle
            if (t >= 0.5f && !indicesUpdated)
            {
                UpdatePageIndices(direction);
                UpdateBookPagesMaterial();
                indicesUpdated = true;
            }
            yield return null;
        }

        if (!indicesUpdated)
        {
            UpdatePageIndices(direction);
            UpdateBookPagesMaterial();
        }

        if (pageFlipObject != null)
            pageFlipObject.SetActive(false);
        PlaySound(pageFlipSound);

        currentPage = direction > 0 ? currentPage + 1 : currentPage - 1;
        UpdatePageUI();

        isAnimating = false;
    }

    private void UpdatePageIndices(int direction)
    {
        if (direction > 0)
        {
            pageIndexL += 2;
            pageIndexR += 2;
        }
        else
        {
            pageIndexL -= 2;
            pageIndexR -= 2;
        }

        if (allowLoop)
        {
            pageIndexL = (pageIndexL + totalPages) % totalPages;
            pageIndexR = (pageIndexR + totalPages) % totalPages;
        }
    }

    // --- ÖNEMLİ: PasswordManager tarafından çağrılan fonksiyon ---
    public void AssignPassword(string newPasswordID, int locationIndex)
    {
        if (!canContainPassword || bookIdentity == null)
            return;
        if (locationIndex >= bookIdentity.possibleLocations.Count)
            return;

        isPasswordBook = true;
        passwordID = newPasswordID;

        // Kendi datamdan, bana söylenen indexi çekiyorum
        var locEntry = bookIdentity.possibleLocations[locationIndex];

        passwordPage = locEntry.pageIndex;
        passwordHotspotUV = locEntry.hotspotUV;
        hasPasswordBeenFound = false;

        Debug.Log($"{gameObject.name} şifrelendi. ID: {passwordID} | Yer: {locEntry.note}");
    }

    public void ClearPassword()
    {
        isPasswordBook = false;
        passwordID = "";
        hasPasswordBeenFound = false;
    }

    private void UpdateBookPagesMaterial()
    {
        if (bookPagesMaterial == null)
            return;
        bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
        bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
    }

    private void UpdatePageUI()
    {
        if (pageNumberText != null)
            pageNumberText.text = $"{pageIndexL}-{pageIndexR}";
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    // --- IForceExitable Implementation ---
    public void ForceExit()
    {
        if (isOpen && !isAnimating)
            StartCoroutine(CloseBook());
    }

    // --- GIZMOS (Editörde Kırmızı Kutu Görmek İçin) ---
    private void OnDrawGizmosSelected()
    {
        if (!isPasswordBook || passwordHotspotUV == null)
            return;

        Gizmos.color = new Color(1.0f, 0f, 0f, 0.7f);
        Gizmos.matrix = transform.localToWorldMatrix;

        // Sayfa konumunu tahmin et (Sol mu Sağ mı?)
        bool isRightPage = (passwordPage % 2) != 0;
        float pageCenterX = isRightPage ? (-singlePageSize.x / 2.0f) : (singlePageSize.x / 2.0f);
        Vector3 pageCenterLocalPos = new Vector3(pageCenterX, 0, 0);

        // UV'den Lokale çevir
        float hotspotCenter_UV_X = passwordHotspotUV.x + (passwordHotspotUV.width / 2.0f);
        float hotspotCenter_UV_Y = passwordHotspotUV.y + (passwordHotspotUV.height / 2.0f);

        float hotspotOffsetX_UV = hotspotCenter_UV_X - 0.5f;
        float hotspotOffsetY_UV = hotspotCenter_UV_Y - 0.5f;

        float hotspotOffsetX_Local = hotspotOffsetX_UV * singlePageSize.x;
        float hotspotOffsetZ_Local = hotspotOffsetY_UV * singlePageSize.y;

        Vector3 hotspotCenter =
            pageCenterLocalPos
            + new Vector3(hotspotOffsetX_Local, gizmoYOffset, hotspotOffsetZ_Local);

        float hotspotWidth_Local = passwordHotspotUV.width * singlePageSize.x;
        float hotspotHeight_Local = passwordHotspotUV.height * singlePageSize.y;
        Vector3 hotspotSize = new Vector3(hotspotWidth_Local, 0.001f, hotspotHeight_Local);

        Gizmos.DrawWireCube(hotspotCenter, hotspotSize);
    }
}
