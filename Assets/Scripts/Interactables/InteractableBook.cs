using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes; // BUNU EKLEDİK
using StarterAssets;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public partial class InteractableBook : MonoBehaviour, IInteractable, IForceExitable
{
    public enum BookInitialState
    {
        Closed,
        Open,
    }

    // --- EDİTÖRDE GÖRÜNMESİ GEREKENLER (AYARLAR) ---

    [Header("📖 Kitap Ayarları")]
    public Animator bookAnimator;
    public float pageFlipDuration = 0.8f;
    public int totalPages = 8;
    public bool allowLoop = false;

    // BURAYI EKLE:
    [Header("  Başlangıç Durumu")]
    [Tooltip("Kitap sahneye dizilirken ve oyun başlarken açık mı olsun?")]
    public bool startOpen = false;

    [Header("  Başlangıç Durumu")]
    [Tooltip("Kitap sahneye dizilirken ve oyun başlarken nasıl dursun?")]
    public BookInitialState initialState = BookInitialState.Closed;

    private DropdownList<int> GetPagePairs()
    {
        return new DropdownList<int>()
        {
            { "Sayfa 1 - 2", 0 },
            { "Sayfa 3 - 4", 2 },
            { "Sayfa 5 - 6", 4 },
            { "Sayfa 7 - 8", 6 },
            { "Sayfa 9 - 10", 8 },
            { "Sayfa 11 - 12", 10 },
            { "Sayfa 13 - 14", 12 },
            { "Sayfa 15 - 16", 14 },
        };
    }

    [ShowIf("initialState", BookInitialState.Open)]
    [Dropdown("GetPagePairs")]
    [Tooltip("Kitap açık başlarken hangi sayfalarda olsun?")]
    public int startPageIndex = 0;

    [Header("🎨 Görsel & Materyal")]
    [Tooltip("Kitabın olduğu Skinned Mesh Renderer (Tek parça model)")]
    public SkinnedMeshRenderer bookSkinnedMeshRenderer;
    public int bookMaterialIndex = 0;
    public Material bookPagesMaterial;

    [Space(10)]
    public GameObject pageFlipObject;
    public MeshRenderer pageFlipRenderer;
    public Material pageTurnMaterial;

    [Header("🔊 Ses Efektleri")]
    public AudioSource audioSource;
    public AudioClip bookOpenSound;
    public AudioClip bookCloseSound;
    public AudioClip[] pageFlipSounds;
    public AudioClip passwordFoundSound;

    [Header("🎥 Kamera & Pozisyon")]
    public Transform cameraTransform;
    public Vector3 viewPositionOffset = new Vector3(0, 0, 0.5f);
    public Vector3 viewRotationOffset = Vector3.zero;
    public float moveDuration = 0.5f;

    [Header("UI Bağlantıları")]
    public GameObject bookUI;
    public TextMeshProUGUI pageNumberText;

    [Header("Sentez Çalışma Alanı")]
    [Tooltip("Açıklamalar basılı kalır; yalnızca özel bloklar deftere sürüklenebilir.")]
    public List<BookWorkspacePage> synthesisPages = new List<BookWorkspacePage>();

    [Tooltip("Özel blokları ekran penceresi yerine gerçek kitap sayfalarında gösterir.")]
    public bool usePhysicalPageBlocks;
    [Tooltip("Notları kart penceresi yerine sayfanın UV yüzeyine basar.")]
    public bool printNotesOnPages;
    [Tooltip("Sayfalardaki parça bölgeleri; defter blokları bu bölgelerin gerçek görüntüsünden kırpılır.")]
    public BookClueData clueData;

    public int LeftPageIndex => pageIndexL;
    public int RightPageIndex => pageIndexR;

    [Header("Gizmos Settings")]
    [SerializeField]
    private Vector2 singlePageSize = new Vector2(0.16f, 0.32f);

    [SerializeField]
    private float gizmoYOffset = 0.005f;

    [SerializeField]
    private float animationDuration = 1f;

    [Header("📘 KİTAP KİMLİĞİ")]
    public PasswordData bookIdentity;
    public bool canContainPassword = true;

    // --- YENİ: OUTLINE & HIGHLIGHT AYARLARI ---
    [Header("✨ Vurgu (Highlight) Ayarları")]
    [Tooltip("HDRP Outline Scripti (Varsa Emission yerine bu çalışır)")]
    public HDRPOutlineController outlineController;

    [Tooltip("Vurgu Rengi (Sarı, Beyaz vb.)")]
    public Color highlightColor = new Color(1f, 0.8f, 0f);

    [Tooltip("HDRP Şiddeti (Eğer parlamıyorsa bunu 10, 50, 100 yap!)")]
    [Range(0f, 100f)]
    public float emissionIntensity = 10f;

    // --- Runtime Değişkenler ---
    private class RendererHighlightData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] highlightMaterials;
    }

    private List<RendererHighlightData> allRenderersData = new List<RendererHighlightData>();
    private readonly HashSet<Material> ownedMaterials = new HashSet<Material>();
    private Material[] highlightMaterials;
    private bool isFocused = false;

    // Diğer gizli değişkenler...
    [HideInInspector]
    public bool isOpen = false;

    [HideInInspector]
    public bool isAnimating = false;

    [HideInInspector]
    public int currentPage = 0;

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
    private MaterialPropertyBlock pagePropertyBlock;
    private UnityEngine.CharacterController playerController;
    private StarterAssets.CharacterController playerGameScript;
    private MonoBehaviour playerLookScript;
    private Animator playerAnimator;
    private float previousAnimatorSpeed;
    private bool previousLookInput;
    private Camera mainCamera;
    private BoxCollider interactionCollider;
    public Collider bookCollider;

    // --- COLLIDER FIX VARIABLES ---
    private Mesh originalMesh;
    private Mesh bakedMesh;

    // ------------------------------

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale; // BUNU EKLE
    private Transform originalParent;
    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int CloseTrigger = Animator.StringToHash("Close");
    private static readonly int IsOpenBool = Animator.StringToHash("IsOpen");
    private static readonly int PageNumber = Animator.StringToHash("PageNumber");

    private int pageIndexL;
    private int pageIndexR;
    private int currentSoundIndex = 0;

    // RAM Optimizasyonu: Child collider'lar için önbellek
    private Collider[] cachedChildColliders;

    private void Awake()
    {
        // Sayfa materyali kopyalama
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial = OwnMaterial(bookPagesMaterial);
            if (bookSkinnedMeshRenderer != null)
            {
                Material[] materials = bookSkinnedMeshRenderer.sharedMaterials;
                if (bookMaterialIndex < materials.Length)
                {
                    materials[bookMaterialIndex] = bookPagesMaterial;
                    bookSkinnedMeshRenderer.sharedMaterials = materials;
                }
            }
        }
        else if (bookSkinnedMeshRenderer != null)
        {
            // Fallback
            Material[] materials = bookSkinnedMeshRenderer.sharedMaterials;
            if (bookMaterialIndex < materials.Length && materials[bookMaterialIndex] != null)
            {
                bookPagesMaterial = OwnMaterial(materials[bookMaterialIndex]);
                materials[bookMaterialIndex] = bookPagesMaterial;
                bookSkinnedMeshRenderer.sharedMaterials = materials;
            }
        }

        if (pageTurnMaterial != null)
        {
            pageTurnMaterial = OwnMaterial(pageTurnMaterial);
            if (pageFlipRenderer != null)
                pageFlipRenderer.sharedMaterial = pageTurnMaterial;
        }
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged -= RefreshLocalizedPageTexture;
        GameLocalization.LanguageChanged += RefreshLocalizedPageTexture;
        RefreshLocalizedPageTexture();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshLocalizedPageTexture;
    }

    private Material OwnMaterial(Material source)
    {
        var material = new Material(source);
        ownedMaterials.Add(material);
        return material;
    }

    public void DisableEmission()
    {
        emissionIntensity = 0;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.materials)
            {
                ownedMaterials.Add(material);
                if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                if (material.HasProperty("_EmissiveIntensity")) material.SetFloat("_EmissiveIntensity", 0);
                material.DisableKeyword("_EMISSION");
            }
    }

    private void OnDestroy()
    {
        foreach (var material in ownedMaterials)
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        if (bakedMesh != null) { if (Application.isPlaying) Destroy(bakedMesh); else DestroyImmediate(bakedMesh); }
    }

    public void RefreshLocalizedPageTexture()
    {
        if (printNotesOnPages) return;
        Texture2D tex = clueData != null ? clueData.GetPageTexture()
            : bookIdentity != null ? bookIdentity.GetPageTexture() : null;
        if (tex == null) return;
        if (bookPagesMaterial != null && bookPagesMaterial.HasProperty("_PagesTex"))
            bookPagesMaterial.SetTexture("_PagesTex", tex);
        if (pageTurnMaterial != null && pageTurnMaterial.HasProperty("_PagesTex"))
            pageTurnMaterial.SetTexture("_PagesTex", tex);
    }

    private void Start()
    {
        pagePropertyBlock = new MaterialPropertyBlock();
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
        if (outlineController == null)
            outlineController = GetComponentInChildren<HDRPOutlineController>();

        // --- COLLIDER FIX ---
        if (bookCollider != null)
        {
            // Orijinal mesh'i sakla (kapalı hali)
            // Eğer MeshCollider'ın sharedMesh'i null ise (örn. BoxCollider kullanılıyorsa) bu adım atlanabilir,
            // ama kodun geri kalanında MeshCollider varsayılıyor.
            if (bookCollider is MeshCollider mc)
            {
                originalMesh = mc.sharedMesh;
                bakedMesh = new Mesh();
                bakedMesh.name = "BakedBookCollider";
            }
        }
        // --------------------

        // Highlight copies are prepared only when a book actually needs emission focus.
        FinishBookSetup();
    }

    private void PrepareHighlightMaterials()
    {
        if (allRenderersData.Count > 0) return;
        // --- 2. HIGHLIGHT (EMISSION) HAZIRLIĞI (GÜNCELLENDİ) ---
        // Kitabın altındaki TÜM Renderer'ları bul (Kapak, Sayfalar, vs.)
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            // Outline/Flip parçalarını veya particle'ları hariç tutmak istersen buraya if ekleyebilirsin
            if (r == pageFlipRenderer)
                continue; // Örn: Sayfa çevirme efektini parlatma

            RendererHighlightData data = new RendererHighlightData();
            data.renderer = r;
            data.originalMaterials = r.sharedMaterials;

            // Highlight materyallerini oluştur
            data.highlightMaterials = new Material[data.originalMaterials.Length];
            for (int i = 0; i < data.originalMaterials.Length; i++)
            {
                Material mat = OwnMaterial(data.originalMaterials[i]);
                mat.EnableKeyword("_EMISSION");

                // HDRP Emission Rengi Ayarı
                Color finalEmission = highlightColor * emissionIntensity;
                if (mat.HasProperty("_EmissiveColor"))
                    mat.SetColor("_EmissiveColor", finalEmission);
                else if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", finalEmission);

                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                data.highlightMaterials[i] = mat;
            }

            allRenderersData.Add(data);
        }
    }

    private void FinishBookSetup()
    {
        // --- DİĞER BAŞLANGIÇ KODLARI ---
        interactionCollider = GetComponent<BoxCollider>();
        if (interactionCollider == null)
            interactionCollider = gameObject.AddComponent<BoxCollider>();

        if (bookCollider == null)
            bookCollider = GetComponentInChildren<MeshCollider>();

        // RAM Optimizasyonu: Child collider'ları önbelleğe al
        cachedChildColliders = GetComponentsInChildren<Collider>();

        if (bookAnimator == null)
            bookAnimator = GetComponent<Animator>();
        // Kitabın mevcut durumunu Inspector'daki duruma eşitle
        // ESKİ: isOpen = startOpen;
        isOpen = (initialState == BookInitialState.Open); // YENİ

        if (bookAnimator != null)
        {
            bookAnimator.SetBool(IsOpenBool, isOpen);
            bookAnimator.SetInteger(PageNumber, currentPage);

            // Oyun başlar başlamaz animasyonun direkt o karede (açık/kapalı) başlaması için:
            bookAnimator.Update(0f);
        }

        playerController = Object.FindFirstObjectByType<UnityEngine.CharacterController>();
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
        if (usePhysicalPageBlocks && GetComponent<BookPageBlockSurface>() == null)
            gameObject.AddComponent<BookPageBlockSurface>();
    }

    // ==========================================
    // LEVEL DESIGN & EDITOR GÖRÜNÜMÜ İÇİN
    // ==========================================
#if UNITY_EDITOR
    [Button("Kitabı Editörde Ön İzle (Level Design)")]
    public void PreviewBookInEditor()
    {
        int pL = (startPageIndex % 2 == 0) ? startPageIndex : startPageIndex - 1;
        int pR = pL + 1;

        // 2. Sayfa Materyali (GÜNCELLENDİ)
        if (bookSkinnedMeshRenderer != null && bookPagesMaterial != null)
        {
            // YENİ: Editörde kitabın gerçek materyalini üstüne zorla giydiriyoruz ki sayfalar gözüksün!
            Material[] sharedMats = bookSkinnedMeshRenderer.sharedMaterials;
            if (bookMaterialIndex >= 0 && bookMaterialIndex < sharedMats.Length)
            {
                if (sharedMats[bookMaterialIndex] != bookPagesMaterial)
                {
                    sharedMats[bookMaterialIndex] = bookPagesMaterial;
                    bookSkinnedMeshRenderer.sharedMaterials = sharedMats;
                }
            }

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            bookSkinnedMeshRenderer.GetPropertyBlock(mpb, bookMaterialIndex);
            mpb.SetFloat("_PageIndexL", pL);
            mpb.SetFloat("_PageIndexR", pR);
            mpb.SetFloat("_PageCount", totalPages);
            bookSkinnedMeshRenderer.SetPropertyBlock(mpb, bookMaterialIndex);
            UnityEditor.EditorUtility.SetDirty(bookSkinnedMeshRenderer);
        }

        // 3. KİTABI FİZİKSEL OLARAK AÇ/KAPAT
        if (bookAnimator != null && bookAnimator.runtimeAnimatorController != null)
        {
            bool editorIsOpen = (initialState == BookInitialState.Open);

            bookAnimator.Rebind();
            bookAnimator.SetBool("IsOpen", editorIsOpen);

            string stateName = editorIsOpen ? "newBookOpened" : "newBookClose";
            bookAnimator.Play(stateName, 0, 1f);
            bookAnimator.Update(0f);
        }

        if (UnityEditor.SceneView.lastActiveSceneView != null)
        {
            UnityEditor.SceneView.lastActiveSceneView.Repaint();
        }

        Debug.Log(
            $"[Level Design] Kitap durumu: {(initialState == BookInitialState.Open ? "AÇIK" : "KAPALI")} | Sayfalar: {pL}-{pR} Yüklendi!"
        );
    }
#endif

    private void InitializePages()
    {
        // İstenen sayfa tek sayıysa (örn 3), sol sayfayı 2, sağ sayfayı 3 yap.
        pageIndexL = (startPageIndex % 2 == 0) ? startPageIndex : startPageIndex - 1;
        pageIndexR = pageIndexL + 1;

        RefreshLocalizedPageTexture();
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
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused) return;
        if (closeRequested && isOpen && !isAnimating)
        {
            closeRequested = false;
            ForceExit();
            return;
        }
        if (isOpen && !isAnimating)
        {
            if (
                GameManager.Instance != null
                && GameManager.Instance.activeInteraction == (IInteractable)this
            )
            {
                HandlePageInput(); // A ve D ile sayfa çevirme her zaman serbest

                if (isPasswordBook && !hasPasswordBeenFound && GameInput.Pressed("Interact"))
                    CheckForPasswordClick();

                if (GameInput.Pressed("Cancel"))
                {
                    StartCoroutine(CloseBook());
                }

                if (playerGameScript != null)
                    playerGameScript.ExternalStaminaRegen(Time.deltaTime);
            }
        }
    }

    public void OnFocus()
    {
        if (isOpen || isAnimating || isFocused)
            return;
        isFocused = true;

        // 1. ÖNCELİK: Outline Scripti (İstersen bunu tamamen silebilirsin)
        if (outlineController != null)
        {
            outlineController.ToggleOutline(true);
        }
        // 2. ÖNCELİK: Gelişmiş Emission Sistemi (Tüm parçalar parlar)
        else if (emissionIntensity > 0)
        {
            PrepareHighlightMaterials();
            foreach (var data in allRenderersData)
            {
                if (data.renderer != null)
                    data.renderer.sharedMaterials = data.highlightMaterials;
            }
        }
    }

    public void OnLoseFocus()
    {
        if (!isFocused)
            return;
        isFocused = false;

        // 1. ÖNCELİK: Outline Scripti
        if (outlineController != null)
        {
            outlineController.ToggleOutline(false);
        }
        // 2. ÖNCELİK: Gelişmiş Emission Sistemi
        else
        {
            foreach (var data in allRenderersData)
            {
                if (data.renderer != null)
                    data.renderer.sharedMaterials = data.originalMaterials;
            }
        }
    }

    public void Interact()
    {
        if (GameManager.Instance != null && (!GameManager.Instance.isGameStarted || GameManager.Instance.isGamePaused)) return;
        if (PlayerInteraction.Instance != null && !PlayerInteraction.Instance.CanInteractWithWorld) return;
        if (isAnimating)
            return;

        // Eğer kitap halihazırda açıksa ama bizim elimizde değilse alabilelim!
        if (
            isOpen
            && GameManager.Instance != null
            && GameManager.Instance.activeInteraction == (IInteractable)this
        )
            return;

        OnLoseFocus();
        StartCoroutine(OpenBook());
    }

    public string GetInteractionPrompt()
    {
        if (isAnimating)
            return "";
        string action = isOpen ? "[F] Kitabı Kapat" : "[Sol Tık] Kitabı Aç";
        var identity = GetComponent<PuzzleBookIdentity>();
        return identity != null && !string.IsNullOrWhiteSpace(identity.title)
            ? GameLocalization.Format("{0} · {1}\n{2}", identity.title, identity.location, action)
            : action;
    }

    private IEnumerator OpenBook()
    {
        bool alreadyOpen = isOpen;
        IsOpening = true;
        if (NotebookUI.Instance != null)
            NotebookUI.Instance.SetNotebookVisibility(false);
        isAnimating = true;
        isOpen = true;

        if (ControlsUIManager.Instance != null)
        {
            ControlsUIManager.Instance.ShowMachineUI(ControlsUIManager.MachineType.Book);
        }

        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;

        if (interactionCollider != null)
            interactionCollider.enabled = false;

        if (playerGameScript != null) playerGameScript.SetFrozen(true, true, false);
        // The view camera is attached to the animated head; pause its pose without disabling the Animator.
        if (playerAnimator != null)
        {
            previousAnimatorSpeed = playerAnimator.speed;
            playerAnimator.speed = 0;
        }
        if (playerLookScript is StarterAssetsInputs inputs)
        {
            previousLookInput = inputs.cursorInputForLook;
            inputs.look = Vector2.zero;
            inputs.cursorInputForLook = false;
        }

        GameManager.Instance.UpdateCursorState();

        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale; // BUNU EKLEs
        shelfPickup = !alreadyOpen && ShelfExit(out _);
        if (shelfPickup)
        {
            ShelfExit(out var exit);
            shelfExitLocalPosition = originalParent.InverseTransformPoint(exit);
            IsShelfPulling = true;
            yield return SlideShelf(transform.position, exit, transform.rotation);
        }
        transform.SetParent(cameraTransform, true);

        pageIndexL = (startPageIndex % 2 == 0) ? startPageIndex : startPageIndex - 1;
        pageIndexR = pageIndexL + 1;
        currentPage = startPageIndex;
        UpdateBookPagesMaterial();
        float duration = Mathf.Max(.3f, moveDuration + .32f);
        Vector3 startLocalPos = transform.localPosition;
        Quaternion startLocalRot = transform.localRotation;
        Quaternion targetLocalRot = Quaternion.Euler(viewRotationOffset);
        BeginCoverMotion(true, alreadyOpen);
        var presentation = usePhysicalPageBlocks ? GetComponent<BookPageBlockSurface>() : null;
        if (presentation != null)
        {
            // Build the input plane facing the reader, regardless of how the book lay on its shelf.
            transform.localPosition = viewPositionOffset;
            transform.localRotation = targetLocalRot;
            if (!alreadyOpen) UpdateCoverMotion(1);
            presentation.PreparePresentation();
            if (!alreadyOpen) UpdateCoverMotion(0);
            transform.localPosition = startLocalPos;
            transform.localRotation = startLocalRot;
        }
        float t = 0f;

        while (t < 1f)
        {
            if (GameManager.Instance != null && GameManager.Instance.isGamePaused) { yield return null; continue; }
            t = Mathf.Clamp01(t + Time.deltaTime / duration);
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
            float arc = Mathf.Sin(Mathf.PI * smoothT);
            transform.localPosition = Vector3.Lerp(startLocalPos, viewPositionOffset, smoothT) + Vector3.up * (.035f * arc);
            float rotationT = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.06f, .96f, t));
            transform.localRotation = Quaternion.Slerp(startLocalRot, targetLocalRot, rotationT)
                * Quaternion.Euler(-2f * arc, 0, 1.2f * arc);
            if (!alreadyOpen) UpdateCoverMotion(Mathf.InverseLerp(.12f, .92f, t));
            if (!alreadyOpen && !coverSoundPlayed && t >= .15f)
            { PlaySound(bookOpenSound); coverSoundPlayed = true; }
            yield return null;
        }
        transform.localPosition = viewPositionOffset;
        transform.localRotation = targetLocalRot;
        EndCoverMotion(true);
        // Kitap açıldığında belirlediğin sayfadan başlasın
        pageIndexL = (startPageIndex % 2 == 0) ? startPageIndex : startPageIndex - 1;
        pageIndexR = pageIndexL + 1;
        currentPage = startPageIndex;

        UpdateBookPagesMaterial();
        if (bookUI != null)
        {
            bookUI.SetActive(true);
            UpdatePageUI();
        }

        // --- COLLIDER FIX: Mesh'i Bake Et ve Engelleyenleri Kapat ---
        if (bookSkinnedMeshRenderer != null && bookCollider is MeshCollider mc)
        {
            if (bakedMesh == null)
            {
                bakedMesh = new Mesh();
                bakedMesh.name = "BakedBookCollider";
            }

            bookSkinnedMeshRenderer.BakeMesh(bakedMesh, true); // true = scale dahil et
            mc.sharedMesh = bakedMesh;

            // DİĞER COLLIDER'LARI KAPAT (Örn: Kapak veya Statik Mesh Collider raycast'i engellemesin)
            // RAM Optimizasyonu: Önbelleklenmiş collider'ları kullan
            foreach (var col in cachedChildColliders)
            {
                if (col != null && col != bookCollider && col != interactionCollider)
                    col.enabled = false;
            }
        }
        // ------------------------------------

        isAnimating = false;
        IsOpening = false;
        if (NotebookUI.Instance != null) NotebookUI.Instance.OpenBookWorkspace(this);
    }

    private IEnumerator CloseBook()
    {
        IsClosing = true;
        if (NotebookUI.Instance != null) NotebookUI.Instance.SetWorkspaceMode(false);
        // --- COLLIDER FIX: Mesh'i Geri Al ve Colliderları Aç ---
        // Kitap kapanırken eski (kapalı) mesh'e dön
        if (bookCollider is MeshCollider mc && originalMesh != null)
        {
            mc.sharedMesh = originalMesh;
        }

        // Diğer colliderları geri aç
        // RAM Optimizasyonu: Önbelleklenmiş collider'ları kullan
        foreach (var col in cachedChildColliders)
        {
            if (col != null && col != bookCollider && col != interactionCollider)
                col.enabled = true;
        }
        // ------------------------------------

        isAnimating = true;
        isOpen = false;

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.HideControls();
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        if (bookUI != null)
            bookUI.SetActive(false);

        // Release the player as soon as closing starts; the returning book can finish independently.
        if (playerLookScript is StarterAssetsInputs inputs)
        {
            inputs.look = Vector2.zero;
            inputs.cursorInputForLook = previousLookInput;
        }
        if (playerAnimator != null) playerAnimator.speed = previousAnimatorSpeed;
        if (playerGameScript != null) playerGameScript.SetFrozen(false, false, false);
        GameManager.Instance.UpdateCursorState();

        BeginCoverMotion(false, false);
        transform.SetParent(originalParent, true);

        Vector3 targetWorldPosition;
        Quaternion targetWorldRotation;
        if (originalParent != null)
        {
            targetWorldPosition = originalParent.TransformPoint(shelfPickup ? shelfExitLocalPosition : originalLocalPosition);
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
            if (GameManager.Instance != null && GameManager.Instance.isGamePaused) { yield return null; continue; }
            t = Mathf.Clamp01(t + Time.deltaTime / Mathf.Max(.3f, moveDuration + .22f));
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
            float arc = Mathf.Sin(Mathf.PI * smoothT);
            transform.position = Vector3.Lerp(startWorldPos, targetWorldPosition, smoothT) + Vector3.up * (.025f * arc);
            transform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRotation,
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, 1, t)));
            UpdateCoverMotion(Mathf.InverseLerp(0, .8f, t));
            if (!coverSoundPlayed && t >= .72f) { PlaySound(bookCloseSound); coverSoundPlayed = true; }
            yield return null;
        }
        EndCoverMotion(false);
        if (shelfPickup)
            yield return SlideShelf(transform.position, originalParent.TransformPoint(originalLocalPosition), targetWorldRotation);
        transform.SetParent(originalParent, true);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale; // BUNU EKLE

        if (interactionCollider != null)
            interactionCollider.enabled = true;
        currentPage = 0;
        isAnimating = false;
        IsClosing = false;
        PlayerInteraction.NotifyInteractionExit(gameObject);
    }

    // Oyuncu notu/kitabı kapattığında fırlatılacak statik event

    private void HandlePageInput()
    {
        if (GameInput.Pressed("NextPage"))
            NextPage();
        else if (GameInput.Pressed("PreviousPage"))
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

        bool soundPlayed = false;

        if (pageFlipObject != null)
            pageFlipObject.SetActive(true);

        if (pageTurnMaterial != null)
        {
            int turningPage = direction > 0 ? pageIndexR : pageIndexL - 1;
            if (allowLoop) turningPage = (turningPage + totalPages) % totalPages;
            pageTurnMaterial.SetFloat("_PageIndex", turningPage);
            if (printNotesOnPages) pageTurnMaterial.SetTexture("_PagesTex", Texture2D.whiteTexture);
            else if (bookPagesMaterial != null && bookPagesMaterial.HasProperty("_PagesTex"))
                pageTurnMaterial.SetTexture("_PagesTex", bookPagesMaterial.GetTexture("_PagesTex"));
            if (pageFlipRenderer != null) pageFlipRenderer.sharedMaterial = pageTurnMaterial;
        }

        var paper = GetComponent<BookPageBlockSurface>();
        bool physicalTurn = usePhysicalPageBlocks && paper != null && paper.BeginTurn(direction);
        if (physicalTurn)
        {
            // The next sheet is exposed as soon as the top one starts lifting.
            int next = direction > 0 ? pageIndexR + 2 : pageIndexL - 2;
            if (allowLoop) next = (next + totalPages) % totalPages;
            SetVisiblePage(direction > 0 ? "_PageIndexR" : "_PageIndexL", next);
        }

        float t = 0f;
        float flipSpeed = 1f / Mathf.Max(.1f, pageFlipDuration);
        bool indicesUpdated = false;

        while (t < 1f)
        {
            if (GameManager.Instance != null && GameManager.Instance.isGamePaused) { yield return null; continue; }
            t += Time.deltaTime * flipSpeed;
            t = Mathf.Clamp01(t);
            float v = Mathf.SmoothStep(0, 1, t);
            float flipAmount = (direction > 0) ? v : 1f - v;

            if (physicalTurn) paper.UpdateTurn(v);
            else if (pageTurnMaterial != null)
                pageTurnMaterial.SetFloat("_PageFlip", flipAmount);

            if (!soundPlayed && t >= .16f)
            {
                if (pageFlipSounds != null && pageFlipSounds.Length > 0)
                {
                    PlaySound(pageFlipSounds[currentSoundIndex % pageFlipSounds.Length]);
                    currentSoundIndex = (currentSoundIndex + 1) % pageFlipSounds.Length;
                }
                soundPlayed = true;
            }
            if (t >= 0.5f && !indicesUpdated)
            {
                if (!physicalTurn) { UpdatePageIndices(direction); UpdateBookPagesMaterial(); }
                indicesUpdated = true;
            }

            yield return null;
        }

        if (physicalTurn || !indicesUpdated)
        {
            UpdatePageIndices(direction);
            UpdateBookPagesMaterial();
        }

        if (pageFlipObject != null)
            pageFlipObject.SetActive(false);
        if (physicalTurn) paper.EndTurn();

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

    private void UpdateBookPagesMaterial()
    {
        // 1. Orijinal materyal referansını güncelle
        if (bookPagesMaterial != null)
        {
            bookPagesMaterial.SetFloat("_PageIndexL", pageIndexL);
            bookPagesMaterial.SetFloat("_PageIndexR", pageIndexR);
        }

        // 2. OPTİMİZASYON: Bellekte yeni materyal yaratmadan değerleri doğrudan GPU'ya ilet
        if (bookSkinnedMeshRenderer != null && pagePropertyBlock != null)
        {
            // Mevcut özellikleri bloğa çek
            bookSkinnedMeshRenderer.GetPropertyBlock(pagePropertyBlock, bookMaterialIndex);

            // Sadece değişen sayfa numaralarını bloğa yaz
            pagePropertyBlock.SetFloat("_PageIndexL", pageIndexL);
            pagePropertyBlock.SetFloat("_PageIndexR", pageIndexR);

            // Bloğu renderera geri ver (yeni materyal kopyası oluşmaz)
            bookSkinnedMeshRenderer.SetPropertyBlock(pagePropertyBlock, bookMaterialIndex);
        }
    }

    private void UpdatePageUI()
    {
        if (pageNumberText != null)
            pageNumberText.text = $"{pageIndexL}-{pageIndexR}";
        if (NotebookWorkspaceController.Instance != null)
            NotebookWorkspaceController.Instance.RefreshBook(this);
    }

    public bool IsOnPage(int targetPage)
    {
        return (pageIndexL == targetPage || pageIndexR == targetPage);
    }

    private void CheckForPasswordClick()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (pageIndexL != passwordPage && pageIndexR != passwordPage)
            return;

        if (bookCollider == null)
        {
            Debug.LogWarning("bookCollider is null!");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // BUILD FIX: Physics.Raycast kullan (Collider.Raycast build'de güvenilir değil)
        // Tüm collider'lara raycast at ve kitabın collider'ına isabet edeni bul
        RaycastHit hit;
        bool hitBook = false;

        // Önce doğrudan bookCollider'a raycast dene
        if (bookCollider.Raycast(ray, out hit, 100f))
        {
            hitBook = true;
        }
        // Fallback: Physics.Raycast ile tüm collider'lara at
        else if (Physics.Raycast(ray, out hit, 100f))
        {
            // Kitabın herhangi bir child collider'ına isabet etti mi kontrol et
            if (hit.collider == bookCollider || hit.collider.transform.IsChildOf(transform))
            {
                hitBook = true;
            }
        }

        if (hitBook)
        {
            Debug.Log($"Raycast Hit BOOK: {hit.collider.name}");

            Vector2 uv = hit.textureCoord;

            // BUILD FIX: Mesh "Read/Write Enabled" kapalıysa textureCoord (0,0) döner
            // Bu durumda dünya koordinatlarından UV hesapla
            if (uv == Vector2.zero)
            {
                Debug.Log("textureCoord is zero, calculating UV from world position...");
                uv = CalculateUVFromWorldPosition(hit.point);
            }

            Debug.Log($"Hit Book UV: {uv}");

            // Mesh UV'sinden hangi sayfaya tıklandığını belirle
            // Mesh UV: 0-0.5 = sol sayfa, 0.5-1 = sağ sayfa
            bool hitRightPage = uv.x > 0.5f;

            // Şu an görüntülenen sayfa indexleri
            int clickedPageIndex = hitRightPage ? pageIndexR : pageIndexL;

            bool isTargetPage = (clickedPageIndex == passwordPage);

            if (isTargetPage)
            {
                // Mesh UV'sini sayfa-local UV'ye çevir (0-1 aralığına normalize et)
                // Sol sayfa: uv.x 0-0.5 -> 0-1
                // Sağ sayfa: uv.x 0.5-1 -> 0-1
                float pageLocalU = hitRightPage ? (uv.x - 0.5f) * 2.0f : uv.x * 2.0f;
                Vector2 pageUV = new Vector2(pageLocalU, uv.y);

                Debug.Log($"Password Page: {passwordPage} | Clicked Page: {clickedPageIndex}");
                Debug.Log($"Calculated PageUV: {pageUV} | Hotspot: {passwordHotspotUV}");
                Debug.Log(
                    $"Hotspot Contains Check: x={pageUV.x} in [{passwordHotspotUV.xMin}-{passwordHotspotUV.xMax}], y={pageUV.y} in [{passwordHotspotUV.yMin}-{passwordHotspotUV.yMax}]"
                );

                if (passwordHotspotUV.Contains(pageUV))
                {
                    Debug.Log("PASSWORD FOUND!");
                    TriggerPasswordFind();
                }
                else
                {
                    Debug.Log("Click missed hotspot.");
                }
            }
            else
            {
                Debug.Log(
                    $"Wrong Page clicked. Target: {passwordPage}, Clicked: {clickedPageIndex}"
                );
            }
        }
        else
        {
            Debug.Log("Raycast did not hit bookCollider.");
        }
    }

    /// <summary>
    /// Mesh "Read/Write Enabled" kapalı olduğunda dünya koordinatlarından UV hesaplar.
    /// Bu, build'lerde textureCoord'un (0,0) dönmesi sorununu çözer.
    /// </summary>
    private Vector2 CalculateUVFromWorldPosition(Vector3 worldPoint)
    {
        // Dünya noktasını kitabın local koordinat sistemine çevir
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        // Kitabın bounds'unu al
        Bounds bounds;
        if (bookSkinnedMeshRenderer != null)
        {
            bounds = bookSkinnedMeshRenderer.localBounds;
        }
        else if (bookCollider != null)
        {
            // Collider bounds'unu local'e çevir
            bounds = bookCollider.bounds;
            bounds.center = transform.InverseTransformPoint(bounds.center);
            bounds.size = transform.InverseTransformVector(bounds.size);
            // Negatif değerleri düzelt
            bounds.size = new Vector3(
                Mathf.Abs(bounds.size.x),
                Mathf.Abs(bounds.size.y),
                Mathf.Abs(bounds.size.z)
            );
        }
        else
        {
            Debug.LogWarning("Cannot calculate UV: No renderer or collider found!");
            return Vector2.zero;
        }

        // Local noktayı 0-1 aralığına normalize et
        // X ekseni: Kitabın sol kenarından sağ kenarına (0 = sol, 1 = sağ)
        // Z ekseni: Kitabın alt kenarından üst kenarına (0 = alt, 1 = üst) - Y olarak kullanılacak
        float normalizedX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, localPoint.x);
        float normalizedY = Mathf.InverseLerp(bounds.min.z, bounds.max.z, localPoint.z);

        // Clamp to 0-1 range
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedY = Mathf.Clamp01(normalizedY);

        Debug.Log(
            $"UV Calculation: localPoint={localPoint}, bounds.min=({bounds.min.x}, {bounds.min.z}), bounds.max=({bounds.max.x}, {bounds.max.z}), result=({normalizedX}, {normalizedY})"
        );

        return new Vector2(normalizedX, normalizedY);
    }

    // private olan bu kelimeyi public yapıyoruz
    public void TriggerPasswordFind()
    {
        if (hasPasswordBeenFound || !isPasswordBook)
            return;
        hasPasswordBeenFound = true;
        PasswordManager.Instance.DiscoverClue(passwordID);
        PlaySound(passwordFoundSound);
        if (NotebookUI.Instance != null)
            NotebookUI.Instance.ShowPasswordNotification(passwordID);
        if (MegaphoneSystem.Instance != null)
            MegaphoneSystem.Instance.OnNotepadPickedUp();
        StartCoroutine(CloseBook());
    }

    public void AssignPassword(string newPasswordID, int locationIndex)
    {
        if (!canContainPassword || bookIdentity == null)
            return;
        if (locationIndex >= bookIdentity.possibleLocations.Count)
            return;
        isPasswordBook = true;
        passwordID = newPasswordID;
        var locEntry = bookIdentity.possibleLocations[locationIndex];
        passwordPage = locEntry.pageIndex;
        passwordHotspotUV = locEntry.hotspotUV;
        hasPasswordBeenFound = false;

        Debug.Log(
            $"[InteractableBook] Password Assigned: {passwordID} at Page {passwordPage}. Hotspot: {passwordHotspotUV}"
        );
    }

    // YENİ: Sembol Bulmacası (Makine Mantığı) için basitleştirilmiş şifre atama
    public void ClearPassword()
    {
        isPasswordBook = false;
        passwordID = "";
        hasPasswordBeenFound = false;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void ForceExit()
    {
        if (isOpen && isAnimating) { closeRequested = true; return; }
        if (isOpen && !isAnimating)
        {
            StartCoroutine(CloseBook());
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!isPasswordBook || passwordHotspotUV.width <= 0 || passwordHotspotUV.height <= 0)
            return;

        Gizmos.color = new Color(1.0f, 0f, 0f, 0.7f);

        // Mesh Renderer'ın local bounds'unu al
        Bounds localBounds;
        if (bookSkinnedMeshRenderer != null)
        {
            localBounds = bookSkinnedMeshRenderer.localBounds;
        }
        else if (bookCollider != null)
        {
            // Collider varsa onun bounds'unu kullan
            localBounds = bookCollider.bounds;
            // World bounds'u local'e çevir
            localBounds.center = transform.InverseTransformPoint(localBounds.center);
        }
        else
        {
            return;
        }

        // Kitap açıkken sol ve sağ sayfaların pozisyonlarını hesapla
        // Mesh UV: Sol sayfa = 0-0.5, Sağ sayfa = 0.5-1
        // passwordPage çift ise sol sayfa, tek ise sağ sayfa
        bool isRightPage = (passwordPage % 2) != 0;

        // Kitabın x ekseninde yarısı sol, yarısı sağ sayfa
        float halfWidth = localBounds.size.x / 2f;

        // Sayfa merkezi (local koordinatlarda)
        float pageCenterX = isRightPage
            ? localBounds.center.x + halfWidth / 2f // Sağ tarafa offset
            : localBounds.center.x - halfWidth / 2f; // Sol tarafa offset

        // Hotspot'un sayfa üzerindeki konumu
        // passwordHotspotUV: 0-1 arasında normalize edilmiş koordinatlar
        float hotspotLocalX =
            (passwordHotspotUV.x + passwordHotspotUV.width / 2f - 0.5f) * halfWidth;
        float hotspotLocalZ =
            (passwordHotspotUV.y + passwordHotspotUV.height / 2f - 0.5f) * localBounds.size.z;

        Vector3 hotspotCenter = new Vector3(
            pageCenterX + hotspotLocalX,
            localBounds.max.y + gizmoYOffset,
            localBounds.center.z + hotspotLocalZ
        );

        Vector3 hotspotSize = new Vector3(
            passwordHotspotUV.width * halfWidth,
            0.001f,
            passwordHotspotUV.height * localBounds.size.z
        );

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(hotspotCenter, hotspotSize);

        // Ek olarak dolu bir cube da çiz (daha görünür olması için)
        Gizmos.color = new Color(1.0f, 0f, 0f, 0.3f);
        Gizmos.DrawCube(hotspotCenter, hotspotSize);
    }
}
