using System.Collections;
using System.Text;
using Cinemachine;
using UnityEngine;

// IForceExitable EKLENDİ
public class InteractableTuringMachine : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Player Control")]
    [SerializeField]
    private UnityEngine.CharacterController playerController;

    [SerializeField]
    private MonoBehaviour playerLookScript;

    [SerializeField]
    private Animator playerAnimator;

    [Header("Head Cam & Animation")]
    [SerializeField]
    private string interactAnimTrigger = "SitDown";

    [SerializeField]
    private float animationDuration = 1.5f;

    [SerializeField]
    private Transform cameraViewTarget;

    [SerializeField]
    private Vector3 headOffset = new Vector3(0, 0.1f, 0.15f);

    private Transform mainCamera;
    private CinemachineBrain cinemachineBrain;
    private Transform headBone;
    private Transform cinemachineTarget;

    [Header("Makine Bileşenleri")]
    [SerializeField]
    private Transform[] wordWheelModels;

    [SerializeField]
    private Transform symbolWheelModel;

    [SerializeField]
    private Transform[] numberWheelModels;

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
    private string[] symbolChars = { ">=", "+", "-", "/", "√", "%", "<=", "=", "<", ">", ".", "," };

    [SerializeField]
    private string numberChars = "0123456789";

    [SerializeField]
    private Vector3 rotationAxis = Vector3.up;

    [SerializeField]
    private float rotationSpeed = 10f;

    [Header("Göstergeler & Ses")]
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

    private bool isInteracting = false;
    private int currentGroup = 0;
    private int currentWordIndex = 0;
    private int currentNumberIndex = 0;

    private int[] wordWheelIndices;
    private int symbolWheelIndex = 0;
    private int[] numberWheelIndices;

    private Quaternion[] wordWheelTargets;
    private Quaternion symbolWheelTarget;
    private Quaternion[] numberWheelTargets;
    private Quaternion[] wordWheelInitialRots;
    private Quaternion symbolWheelInitialRot;
    private Quaternion[] numberWheelInitialRots;

    private void Start()
    {
        if (playerController == null)
            playerController = FindObjectOfType<UnityEngine.CharacterController>();

        if (playerController != null)
        {
            playerLookScript =
                playerController.GetComponent("StarterAssetsInputs") as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>();
            if (playerAnimator)
                headBone = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                headBone = playerController.transform;

            Transform camRoot = playerController.transform.Find("PlayerCameraRoot");
            if (camRoot != null)
                cinemachineTarget = camRoot;
            else
                cinemachineTarget = headBone;
        }

        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }

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

        for (int i = 0; i < wordWheelModels.Length; i++)
        {
            if (wordWheelModels[i])
            {
                wordWheelInitialRots[i] = wordWheelModels[i].localRotation;
                wordWheelTargets[i] = wordWheelModels[i].localRotation;
            }
        }
        for (int i = 0; i < numberWheelModels.Length; i++)
        {
            if (numberWheelModels[i])
            {
                numberWheelInitialRots[i] = numberWheelModels[i].localRotation;
                numberWheelTargets[i] = numberWheelModels[i].localRotation;
            }
        }
        if (symbolWheelModel)
        {
            symbolWheelInitialRot = symbolWheelModel.localRotation;
            symbolWheelTarget = symbolWheelModel.localRotation;
        }
        ClearAllHighlights();
        UpdateIndicators(0);
    }

    public void Interact()
    {
        if (isInteracting)
            return;
        StartCoroutine(EnterMachineView());
    }

    public string GetInteractionPrompt() => isInteracting ? "" : "[Sol Tık] Turing Makinesi";

    private IEnumerator EnterMachineView()
    {
        isInteracting = true;

        // --- GM KAYIT ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;
        // ----------------

        if (playerController)
            playerController.enabled = false;
        if (playerLookScript)
            playerLookScript.enabled = false;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        PlaySound(accessSound);

        if (cinemachineBrain)
            cinemachineBrain.enabled = false;

        if (headBone != null)
        {
            mainCamera.SetParent(headBone);
            float t = 0f;
            Vector3 startPos = mainCamera.localPosition;
            Quaternion startRot = mainCamera.localRotation;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float s = t / 0.5f;
                mainCamera.localPosition = Vector3.Lerp(startPos, headOffset, s);
                mainCamera.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, s);
                yield return null;
            }
        }

        yield return new WaitForSeconds(animationDuration - 0.5f);

        if (cameraViewTarget != null)
        {
            mainCamera.SetParent(null);
            Vector3 startDockPos = mainCamera.position;
            Quaternion startDockRot = mainCamera.rotation;
            float dockTime = 0.8f;
            float t = 0f;
            while (t < dockTime)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / dockTime);
                mainCamera.position = Vector3.Lerp(startDockPos, cameraViewTarget.position, s);
                mainCamera.rotation = Quaternion.Slerp(startDockRot, cameraViewTarget.rotation, s);
                yield return null;
            }
            mainCamera.position = cameraViewTarget.position;
            mainCamera.rotation = cameraViewTarget.rotation;
        }

        TogglePlayerModel(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (PasswordManager.Instance != null)
            UpdateIndicators(PasswordManager.Instance.GetValidatedPasswordCount());
        UpdateActiveWheelHighlight();
    }

    private IEnumerator ExitMachineView()
    {
        isInteracting = false;

        // --- GM KAYIT SİL ---
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;
        // --------------------

        PlaySound(exitSound);
        ClearAllHighlights();

        TogglePlayerModel(true);

        if (cinemachineTarget != null)
        {
            mainCamera.SetParent(null);
            Vector3 startPos = mainCamera.position;
            Quaternion startRot = mainCamera.rotation;
            float undockTime = 0.6f;
            float t = 0f;
            while (t < undockTime)
            {
                t += Time.deltaTime;
                float s = Mathf.SmoothStep(0f, 1f, t / undockTime);
                mainCamera.position = Vector3.Lerp(startPos, cinemachineTarget.position, s);
                mainCamera.rotation = Quaternion.Slerp(startRot, cinemachineTarget.rotation, s);
                yield return null;
            }
        }

        if (playerController != null)
        {
            Vector3 camForward = mainCamera.forward;
            camForward.y = 0;
            if (camForward != Vector3.zero)
                playerController.transform.rotation = Quaternion.LookRotation(camForward);
        }

        if (cinemachineBrain)
            cinemachineBrain.enabled = true;
        if (playerController)
            playerController.enabled = true;
        if (playerLookScript)
            playerLookScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isInteracting)
            return;
        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

        // ... (Tuş kontrolleri aynı) ...
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
        if (Input.GetKeyDown(KeyCode.Return))
            CheckPassword();

        AnimateWheels();
    }

    // ... (Diğer fonksiyonlar: TogglePlayerModel, AnimateWheels, vb. AYNEN KALSIN) ...

    private void TogglePlayerModel(bool show)
    {
        if (!playerController)
            return;
        Renderer[] renderers = playerController.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = show;
    }

    private void AnimateWheels()
    {
        float s = Time.deltaTime * rotationSpeed;
        for (int i = 0; i < wordWheelModels.Length; i++)
            if (wordWheelModels[i])
                wordWheelModels[i].localRotation = Quaternion.Slerp(
                    wordWheelModels[i].localRotation,
                    wordWheelTargets[i],
                    s
                );
        for (int i = 0; i < numberWheelModels.Length; i++)
            if (numberWheelModels[i])
                numberWheelModels[i].localRotation = Quaternion.Slerp(
                    numberWheelModels[i].localRotation,
                    numberWheelTargets[i],
                    s
                );
        if (symbolWheelModel)
            symbolWheelModel.localRotation = Quaternion.Slerp(
                symbolWheelModel.localRotation,
                symbolWheelTarget,
                s
            );
    }

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
                wordWheelIndices[currentWordIndex] =
                    (wordWheelIndices[currentWordIndex] + d + cW) % cW;
                wordWheelTargets[currentWordIndex] =
                    wordWheelInitialRots[currentWordIndex]
                    * Quaternion.AngleAxis(
                        (360f / cW) * wordWheelIndices[currentWordIndex],
                        rotationAxis
                    );
                break;
            case 1:
                int cS = symbolChars.Length;
                symbolWheelIndex = (symbolWheelIndex + d + cS) % cS;
                symbolWheelTarget =
                    symbolWheelInitialRot
                    * Quaternion.AngleAxis((360f / cS) * symbolWheelIndex, rotationAxis);
                break;
            case 2:
                int cN = numberChars.Length;
                numberWheelIndices[currentNumberIndex] =
                    (numberWheelIndices[currentNumberIndex] + d + cN) % cN;
                numberWheelTargets[currentNumberIndex] =
                    numberWheelInitialRots[currentNumberIndex]
                    * Quaternion.AngleAxis(
                        (360f / cN) * numberWheelIndices[currentNumberIndex],
                        rotationAxis
                    );
                break;
        }
    }

    private void CheckPassword()
    {
        if (!PasswordManager.Instance)
            return;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < wordWheelIndices.Length; i++)
            sb.Append(wordChars[wordWheelIndices[i]]);
        string pw =
            $"{sb.ToString().TrimEnd('-')}_{symbolChars[symbolWheelIndex]}_{numberChars[numberWheelIndices[0]]}{numberChars[numberWheelIndices[1]]}{numberChars[numberWheelIndices[2]]}";
        if (PasswordManager.Instance.ValidatePassword(pw))
        {
            PlaySound(successSound);
            UpdateIndicators(PasswordManager.Instance.GetValidatedPasswordCount());
        }
        else
            PlaySound(failSound);
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

    private void PlaySound(AudioClip c)
    {
        if (audioSource && c)
            audioSource.PlayOneShot(c);
    }

    // --- IFORCEEXITABLE ---
    public void ForceExit()
    {
        if (isInteracting)
            StartCoroutine(ExitMachineView());
    }
}
