using System.Collections;
using System.Text;
using Cinemachine;
using StarterAssets; // Karakter scriptlerini garanti bulmak için
using UnityEngine;

public class InteractableTuringMachine : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Player Control")]
    [SerializeField]
    private UnityEngine.CharacterController playerController;

    [SerializeField]
    private MonoBehaviour playerLookScript;

    [SerializeField]
    private Animator playerAnimator;

    [Header("⚠️ ÖNEMLİ: Script Referansları")]
    public MonoBehaviour playerMovementScript;
    public PlayerInteraction playerInteractionScript;

    [Header("🎥 KAMERA (YENİ SİSTEM)")]
    [Tooltip("Kameranın gidip duracağı nokta (Boş bir GameObject oluşturup buraya sürükle)")]
    public Transform fixedCameraTransform;
    private CinemachineVirtualCamera interactVCam; // Kod otomatik oluşturacak

    [Header("📍 Etkileşim Pozisyonu")]
    public Transform interactionStandPoint;
    public float autoWalkSpeed = 2.0f;
    public float autoRotateSpeed = 5.0f;

    [SerializeField]
    private string interactAnimTrigger = "InteractIdle"; // Oturma veya bekleme animasyonu

    [Header("Makine Bileşenleri")]
    [SerializeField]
    private Transform[] wordWheelModels;

    [SerializeField]
    private Transform symbolWheelModel;

    [SerializeField]
    private Transform[] numberWheelModels;

    [SerializeField]
    private Transform[] wordLeverModels;

    [SerializeField]
    private Transform symbolLeverModel;

    [SerializeField]
    private Transform[] numberLeverModels;

    [Header("Ayarlar")]
    [SerializeField]
    private Vector3 leverRotationAxis = Vector3.right;

    [SerializeField]
    private Vector3 rotationAxis = Vector3.up;

    [SerializeField]
    private float rotationSpeed = 10f;

    [Header("Highlights")]
    [SerializeField]
    private GameObject[] wordWheelHighlights;

    [SerializeField]
    private GameObject symbolWheelHighlight;

    [SerializeField]
    private GameObject[] numberWheelHighlights;

    [Header("Data")]
    [SerializeField]
    private string wordChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ-";

    [SerializeField]
    private string numberChars = "0123456789";
    private string[] symbolChars = { "+", "-", "/", "√", "%", "<=", "=", "<", ">", ".", ",", ">=" };
    private float wordStepAngle,
        symbolStepAngle,
        numberStepAngle;

    public bool useForwardLetterOrder = true;
    public bool invertWordLeverRotation = false;
    public bool isSymbolOrderReversed = true;
    public int symbolVisualOffset = 0;

    [Header("Ses & Görsel")]
    [SerializeField]
    private Renderer[] indicatorRenderers;

    [SerializeField]
    private Material redMaterial;

    [SerializeField]
    private Material greenMaterial;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip wheelClickSound;

    [SerializeField]
    private AudioClip successSound;

    [SerializeField]
    private AudioClip failSound;

    [SerializeField]
    private AudioClip accessSound;

    [SerializeField]
    private AudioClip exitSound;

    // Durum Değişkenleri
    private bool isInteracting = false;
    private bool inMachineMode = false;
    private bool isExiting = false;

    private int currentGroup = 0;
    private int currentWordIndex = 0;
    private int currentNumberIndex = 0;
    private int[] wordWheelIndices;
    private int symbolVisualStep = 0;
    private int[] numberWheelIndices;

    // Hedef Rotasyonlar
    private Quaternion[] wordWheelTargets,
        numberWheelTargets;
    private Quaternion symbolWheelTarget;
    private Quaternion[] wordWheelInitialRots,
        numberWheelInitialRots;
    private Quaternion symbolWheelInitialRot;
    private Quaternion[] wordLeverTargets,
        numberLeverTargets;
    private Quaternion symbolLeverTarget;
    private Quaternion[] wordLeverInitialRots,
        numberLeverInitialRots;
    private Quaternion symbolLeverInitialRot;

    private void Start()
    {
        // 1. Karakter Referanslarını Bul
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();

        if (playerController != null)
        {
            GameObject p = playerController.gameObject;
            playerLookScript = p.GetComponent<StarterAssetsInputs>() as MonoBehaviour;
            playerAnimator = p.GetComponent<Animator>();

            // Movement script'i garanti bul
            if (playerMovementScript == null)
                playerMovementScript = p.GetComponent<StarterAssets.CharacterController>();
            if (playerMovementScript == null)
                playerMovementScript = p.GetComponent("ThirdPersonController") as MonoBehaviour;

            if (playerInteractionScript == null)
                playerInteractionScript = FindObjectOfType<PlayerInteraction>();
        }

        // 2. Sanal Kamera (VCam) Oluşturma/Bulma
        if (fixedCameraTransform != null)
        {
            interactVCam = fixedCameraTransform.GetComponentInChildren<CinemachineVirtualCamera>();
            if (interactVCam == null)
            {
                // Eğer yoksa kodla oluşturuyoruz
                GameObject vcamObj = new GameObject("Turing_Interact_VCam");
                vcamObj.transform.parent = fixedCameraTransform;
                vcamObj.transform.localPosition = Vector3.zero;
                vcamObj.transform.localRotation = Quaternion.identity;
                interactVCam = vcamObj.AddComponent<CinemachineVirtualCamera>();
                interactVCam.Priority = 0; // Başlangıçta pasif
            }
        }
        else
        {
            Debug.LogError("TuringMachine: FIXED CAMERA TRANSFORM EKSİK! Lütfen atayın.");
        }

        // 3. Makine Ayarları
        if (PasswordManager.Instance != null && PasswordManager.Instance.symbols.Length > 0)
            symbolChars = PasswordManager.Instance.symbols;

        wordStepAngle = 360f / wordChars.Length;
        symbolStepAngle = 360f / symbolChars.Length;
        numberStepAngle = 360f / numberChars.Length;

        InitializeWheels();
    }

    private void InitializeWheels()
    {
        wordWheelIndices = new int[wordWheelModels.Length];
        numberWheelIndices = new int[numberWheelModels.Length];

        wordWheelTargets = new Quaternion[wordWheelModels.Length];
        numberWheelTargets = new Quaternion[numberWheelModels.Length];
        wordWheelInitialRots = new Quaternion[wordWheelModels.Length];
        numberWheelInitialRots = new Quaternion[numberWheelModels.Length];

        wordLeverTargets = new Quaternion[wordWheelModels.Length];
        numberLeverTargets = new Quaternion[numberWheelModels.Length];
        wordLeverInitialRots = new Quaternion[wordWheelModels.Length];
        numberLeverInitialRots = new Quaternion[numberWheelModels.Length];

        for (int i = 0; i < wordWheelModels.Length; i++)
        {
            if (wordWheelModels[i])
            {
                wordWheelInitialRots[i] = wordWheelModels[i].localRotation;
                wordWheelTargets[i] = wordWheelModels[i].localRotation;
            }
            if (i < wordLeverModels.Length && wordLeverModels[i])
            {
                wordLeverInitialRots[i] = wordLeverModels[i].localRotation;
                wordLeverTargets[i] = wordLeverModels[i].localRotation;
            }
        }
        for (int i = 0; i < numberWheelModels.Length; i++)
        {
            if (numberWheelModels[i])
            {
                numberWheelInitialRots[i] = numberWheelModels[i].localRotation;
                numberWheelTargets[i] = numberWheelModels[i].localRotation;
            }
            if (i < numberLeverModels.Length && numberLeverModels[i])
            {
                numberLeverInitialRots[i] = numberLeverModels[i].localRotation;
                numberLeverTargets[i] = numberLeverModels[i].localRotation;
            }
        }
        if (symbolWheelModel)
        {
            symbolWheelInitialRot = symbolWheelModel.localRotation;
            symbolWheelTarget = symbolWheelModel.localRotation;
        }
        if (symbolLeverModel)
        {
            symbolLeverInitialRot = symbolLeverModel.localRotation;
            symbolLeverTarget = symbolLeverModel.localRotation;
        }

        ClearAllHighlights();
        UpdateIndicators(0);
    }

    public void Interact()
    {
        if (isInteracting || inMachineMode || isExiting)
            return;
        StartCoroutine(MoveToInteractionPoint());
    }

    // --- ADIM 1: YÜRÜME ---
    private IEnumerator MoveToInteractionPoint()
    {
        isInteracting = true;
        inMachineMode = false;

        if (playerLookScript)
            playerLookScript.enabled = false;
        if (playerMovementScript)
            playerMovementScript.enabled = false;
        if (playerController)
            playerController.enabled = true;

        if (interactionStandPoint != null)
        {
            float timer = 0f;
            int animIDSpeed = Animator.StringToHash("Speed");
            int animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            int animIDGrounded = Animator.StringToHash("Grounded");

            while (timer < 4.0f)
            {
                timer += Time.deltaTime;
                Vector3 targetPos = interactionStandPoint.position;
                Vector3 playerPos = playerController.transform.position;
                playerPos.y = targetPos.y;

                if (Vector3.Distance(playerPos, targetPos) < 0.15f)
                    break;

                Vector3 dir = (targetPos - playerPos).normalized;
                if (dir != Vector3.zero)
                {
                    playerController.transform.rotation = Quaternion.Slerp(
                        playerController.transform.rotation,
                        Quaternion.LookRotation(dir),
                        Time.deltaTime * autoRotateSpeed
                    );
                }

                float speed = autoWalkSpeed;
                if (playerAnimator)
                {
                    playerAnimator.SetBool(animIDGrounded, true);
                    playerAnimator.SetFloat(animIDSpeed, speed);
                    playerAnimator.SetFloat(animIDMotionSpeed, 1f);
                }

                playerController.Move(dir * speed * Time.deltaTime + Vector3.down); // Yerçekimi ekle
                yield return null;
            }
        }

        // Son Oturtma (Snap)
        if (interactionStandPoint != null)
        {
            float t = 0f;
            Quaternion startRot = playerController.transform.rotation;
            Vector3 startPos = playerController.transform.position;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                playerController.transform.position = Vector3.Lerp(
                    startPos,
                    interactionStandPoint.position,
                    t / 0.5f
                );
                playerController.transform.rotation = Quaternion.Slerp(
                    startRot,
                    interactionStandPoint.rotation,
                    t / 0.5f
                );
                if (playerAnimator)
                    playerAnimator.SetFloat("Speed", 0);
                yield return null;
            }
        }

        StartCoroutine(EnterMachineView());
    }

    // --- ADIM 2: YUMUŞAK GEÇİŞ (GİRİŞ) ---
    private IEnumerator EnterMachineView()
    {
        if (GameManager.Instance)
            GameManager.Instance.activeInteraction = this;

        // Kontrolleri dondur
        if (playerController)
            playerController.enabled = false;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        PlaySound(accessSound);

        // VCAM AKTİF ET (Blend otomatik başlar)
        if (interactVCam)
            interactVCam.Priority = 100; // Ana kameradan yüksek olsun

        // Blend süresini bekle (Varsayılan 1.5 - 2 saniye idealdir)
        yield return new WaitForSeconds(1.5f);

        inMachineMode = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (playerInteractionScript)
            playerInteractionScript.ToggleCrosshair(false);
        if (ControlsUIManager.Instance)
            ControlsUIManager.Instance.ShowControls("W/S: Grup | A/D: Çevir | Q/E: Harf | F: Kalk");
        if (PasswordManager.Instance)
            UpdateIndicators(PasswordManager.Instance.GetValidatedPasswordCount());

        UpdateActiveWheelHighlight();
    }

    // --- ADIM 3: YUMUŞAK GEÇİŞ (ÇIKIŞ) ---
    private IEnumerator ExitMachineView()
    {
        if (isExiting)
            yield break;
        isExiting = true;
        inMachineMode = false;

        PlaySound(exitSound);
        ClearAllHighlights();

        // VCAM PASİF ET (Unity otomatik olarak karakterin arkasına süzer)
        if (interactVCam)
            interactVCam.Priority = 0;

        // Blend süresini bekle
        yield return new WaitForSeconds(1.5f);

        // Kontrolleri Aç
        if (playerController)
            playerController.enabled = true;
        if (playerLookScript)
            playerLookScript.enabled = true;
        if (playerMovementScript)
            playerMovementScript.enabled = true;

        if (playerInteractionScript)
            playerInteractionScript.ToggleCrosshair(true);
        if (ControlsUIManager.Instance)
            ControlsUIManager.Instance.HideControls();
        if (GameManager.Instance)
            GameManager.Instance.activeInteraction = null;

        isInteracting = false;
        isExiting = false;
    }

    private void Update()
    {
        if (!inMachineMode || isExiting)
            return;

        // ÇIKIŞ
        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

        // --- MAKİNE KONTROLLERİ ---
        if (Input.GetKeyDown(KeyCode.W))
        {
            currentGroup = (currentGroup - 1 + 3) % 3;
            UpdateActiveWheelHighlight();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            currentGroup = (currentGroup + 1) % 3;
            UpdateActiveWheelHighlight();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            HandleIndexChange(-1);
            UpdateActiveWheelHighlight();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            HandleIndexChange(1);
            UpdateActiveWheelHighlight();
        }

        float rotationInput = 0f;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            rotationInput = 1f;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            rotationInput = -1f;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.1f)
            rotationInput = 1f;
        if (scroll < -0.1f)
            rotationInput = -1f;

        if (rotationInput != 0)
            RotateActiveWheel((int)rotationInput);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            CheckPassword();

        AnimateWheels();
    }

    // ... Yardımcı Fonksiyonlar (Değişmedi) ...
    private void HandleIndexChange(int d)
    {
        switch (currentGroup)
        {
            case 0:
                currentWordIndex =
                    (currentWordIndex + d + wordWheelModels.Length) % wordWheelModels.Length;
                break;
            case 2:
                currentNumberIndex =
                    (currentNumberIndex + d + numberWheelModels.Length) % numberWheelModels.Length;
                break;
        }
    }

    private void RotateActiveWheel(int d)
    {
        PlaySound(wheelClickSound);
        switch (currentGroup)
        {
            case 0:
                int cW = wordChars.Length;
                int dir = useForwardLetterOrder ? d : -d;
                wordWheelIndices[currentWordIndex] =
                    (wordWheelIndices[currentWordIndex] + dir + cW) % cW;
                wordWheelTargets[currentWordIndex] =
                    wordWheelInitialRots[currentWordIndex]
                    * Quaternion.AngleAxis(
                        wordStepAngle * wordWheelIndices[currentWordIndex],
                        rotationAxis
                    );
                if (currentWordIndex < wordLeverModels.Length && wordLeverModels[currentWordIndex])
                {
                    float mult = invertWordLeverRotation ? -1f : 1f;
                    wordLeverTargets[currentWordIndex] =
                        wordLeverInitialRots[currentWordIndex]
                        * Quaternion.AngleAxis(
                            wordStepAngle * wordWheelIndices[currentWordIndex] * mult,
                            leverRotationAxis
                        );
                }
                break;
            case 1:
                int cS = symbolChars.Length;
                symbolVisualStep = (symbolVisualStep - d + cS) % cS;
                symbolWheelTarget =
                    symbolWheelInitialRot
                    * Quaternion.AngleAxis(symbolStepAngle * symbolVisualStep, rotationAxis);
                if (symbolLeverModel)
                    symbolLeverTarget =
                        symbolLeverInitialRot
                        * Quaternion.AngleAxis(
                            symbolStepAngle * symbolVisualStep,
                            leverRotationAxis
                        );
                break;
            case 2:
                int cN = numberChars.Length;
                numberWheelIndices[currentNumberIndex] =
                    (numberWheelIndices[currentNumberIndex] - d + cN) % cN;
                numberWheelTargets[currentNumberIndex] =
                    numberWheelInitialRots[currentNumberIndex]
                    * Quaternion.AngleAxis(
                        numberStepAngle * numberWheelIndices[currentNumberIndex],
                        rotationAxis
                    );
                if (
                    currentNumberIndex < numberLeverModels.Length
                    && numberLeverModels[currentNumberIndex]
                )
                    numberLeverTargets[currentNumberIndex] =
                        numberLeverInitialRots[currentNumberIndex]
                        * Quaternion.AngleAxis(
                            numberStepAngle * numberWheelIndices[currentNumberIndex],
                            leverRotationAxis
                        );
                break;
        }
    }

    private int GetCorrectSymbolIndex()
    {
        int total = symbolChars.Length;
        int idx = symbolVisualStep;
        if (isSymbolOrderReversed)
            idx = (total - (symbolVisualStep % total)) % total;
        return (idx + symbolVisualOffset + total) % total;
    }

    private void CheckPassword()
    {
        if (!PasswordManager.Instance)
            return;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < wordWheelIndices.Length; i++)
            sb.Append(wordChars[wordWheelIndices[i]]);
        string wp = sb.ToString();
        string sp = symbolChars[GetCorrectSymbolIndex()];
        string np =
            $"{numberChars[numberWheelIndices[0]]}{numberChars[numberWheelIndices[1]]}{numberChars[numberWheelIndices[2]]}";
        string pw = $"{wp}_{sp}_{np}";
        
        Debug.Log($"GİRİLEN ŞİFRE: '{pw}'");
        
        if (PasswordManager.Instance.ValidatePassword(pw))
        {
            PlaySound(successSound);
            UpdateIndicators(PasswordManager.Instance.GetValidatedPasswordCount());

            // --- EKLENEN KISIM: Megafon Sistemi ---
            if (MegaphoneSystem.Instance != null)
            {
                // Eğer bu ilk şifreyse Tutorial biter ("Aferin, sistemler açıldı" vs.)
                // Değilse sadece "Güzel, devam et" der.
                MegaphoneSystem.Instance.OnTutorialCompleted(); 
                MegaphoneSystem.Instance.OnCodeSubmitted();
            }
            // -------------------------------------
        }
        else
        {
            PlaySound(failSound);
        }
    }
    private void UpdateIndicators(int c)
    {
        if (indicatorRenderers == null)
            return;
        for (int i = 0; i < indicatorRenderers.Length; i++)
            if (indicatorRenderers[i])
                indicatorRenderers[i].material = (i < c) ? greenMaterial : redMaterial;
    }

    private void UpdateActiveWheelHighlight()
    {
        ClearAllHighlights();
        switch (currentGroup)
        {
            case 0:
                if (wordWheelHighlights[currentWordIndex])
                    wordWheelHighlights[currentWordIndex].SetActive(true);
                break;
            case 1:
                if (symbolWheelHighlight)
                    symbolWheelHighlight.SetActive(true);
                break;
            case 2:
                if (numberWheelHighlights[currentNumberIndex])
                    numberWheelHighlights[currentNumberIndex].SetActive(true);
                break;
        }
    }

    private void ClearAllHighlights()
    {
        if (wordWheelHighlights != null)
            foreach (var h in wordWheelHighlights)
                if (h)
                    h.SetActive(false);
        if (symbolWheelHighlight)
            symbolWheelHighlight.SetActive(false);
        if (numberWheelHighlights != null)
            foreach (var h in numberWheelHighlights)
                if (h)
                    h.SetActive(false);
    }

    private void AnimateWheels()
    {
        float s = Time.deltaTime * rotationSpeed;
        for (int i = 0; i < wordWheelModels.Length; i++)
        {
            if (wordWheelModels[i])
                wordWheelModels[i].localRotation = Quaternion.Slerp(
                    wordWheelModels[i].localRotation,
                    wordWheelTargets[i],
                    s
                );
            if (i < wordLeverModels.Length && wordLeverModels[i])
                wordLeverModels[i].localRotation = Quaternion.Slerp(
                    wordLeverModels[i].localRotation,
                    wordLeverTargets[i],
                    s
                );
        }
        for (int i = 0; i < numberWheelModels.Length; i++)
        {
            if (numberWheelModels[i])
                numberWheelModels[i].localRotation = Quaternion.Slerp(
                    numberWheelModels[i].localRotation,
                    numberWheelTargets[i],
                    s
                );
            if (i < numberLeverModels.Length && numberLeverModels[i])
                numberLeverModels[i].localRotation = Quaternion.Slerp(
                    numberLeverModels[i].localRotation,
                    numberLeverTargets[i],
                    s
                );
        }
        if (symbolWheelModel)
            symbolWheelModel.localRotation = Quaternion.Slerp(
                symbolWheelModel.localRotation,
                symbolWheelTarget,
                s
            );
        if (symbolLeverModel)
            symbolLeverModel.localRotation = Quaternion.Slerp(
                symbolLeverModel.localRotation,
                symbolLeverTarget,
                s
            );
    }

    private void PlaySound(AudioClip c)
    {
        if (audioSource && c)
            audioSource.PlayOneShot(c);
    }

    public void ForceExit()
    {
        if (isInteracting)
            StartCoroutine(ExitMachineView());
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    public string GetInteractionPrompt() => isInteracting ? "" : "[Sol Tık] Turing Makinesi";
}
