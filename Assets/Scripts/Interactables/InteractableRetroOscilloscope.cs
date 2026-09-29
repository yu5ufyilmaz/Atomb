using System.Collections;
using Cinemachine;
using StarterAssets;
using TMPro;
using UnityEngine;

public class InteractableRetroOscilloscope : MonoBehaviour, IInteractable, IForceExitable
{
    [Header("Player Control")]
    [SerializeField]
    private UnityEngine.CharacterController playerController;
    private CinemachineVirtualCamera interactVCam;

    [SerializeField]
    private GameObject _playerFollowCamera;

    [SerializeField]
    private MonoBehaviour playerLookScript;

    [SerializeField]
    private Animator playerAnimator;
    public MonoBehaviour playerMovementScript;

    [Header("KAMERA AYARLARI")]
    public Transform fixedCameraTransform;
    public Transform interactionStandPoint;

    [SerializeField]
    private string interactAnimTrigger = "InspectScope";

    [Header("Makine Parçaları & Ses")]
    [SerializeField]
    private Transform controlKnob;

    [SerializeField]
    private TextMeshPro screenText;

    [SerializeField]
    private GameObject overheatSmoke;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip wheelClickSound;

    [SerializeField]
    private AudioClip successSound;

    [SerializeField]
    private AudioClip failSound;

    [SerializeField]
    private AudioClip overheatSound;

    [Header("Retro Oyun Ayarları")]
    public float screenRadius = 0.5f; // <--- DÜŞÜRDÜK (Ekrana sığması için, gerekirse Inspector'dan artır)
    public float paddleWidth = 45f;
    public float paddleSpeed = 150f;
    public float ballSpeed = 1.5f; // <--- Hızını ekrana göre kıstık
    public float speedIncreasePerScore = 0.3f;
    public int lives = 3;
    public int scoreToWin = 5;
    private float currentBallSpeed;

    // Mini Oyun Durumları (Herkese Açık)
    public float paddleAngle = 0f;
    public Vector2 ballPosition;
    public Vector2 targetPosition;

    private Vector2 ballVelocity;
    private int currentScore = 0;
    private string assignedPassword = "";

    // Sistem Durumları
    public bool GameActive { get; private set; } = false; // <--- YENİ: Sol tık bekleme durumu
    private bool isBallMoving = false; //
    public bool IsSolved { get; private set; } = false;
    public bool IsBroken { get; private set; } = false;

    private bool isInteracting = false;
    private bool inMachineMode = false;
    private bool isExiting = false;
    private float currentCooldown = 0f;
    public float cooldownDuration = 10f;

    void Start()
    {
        if (playerController == null)
            playerController = Object.FindFirstObjectByType<UnityEngine.CharacterController>();

        if (playerController != null)
        {
            playerLookScript =
                playerController.GetComponent<StarterAssetsInputs>() as MonoBehaviour;
            playerAnimator = playerController.GetComponent<Animator>();
            if (playerMovementScript == null)
                playerMovementScript =
                    playerController.GetComponent<StarterAssets.CharacterController>();
        }

        if (fixedCameraTransform != null)
        {
            interactVCam = fixedCameraTransform.GetComponentInChildren<CinemachineVirtualCamera>();
            if (interactVCam == null)
            {
                GameObject vcamObj = new GameObject("RetroOscilloscope_VCam");
                vcamObj.transform.parent = fixedCameraTransform;
                vcamObj.transform.localPosition = Vector3.zero;
                vcamObj.transform.localRotation = Quaternion.identity;
                interactVCam = vcamObj.AddComponent<CinemachineVirtualCamera>();
                interactVCam.Priority = 0;
                interactVCam.m_Lens.FieldOfView = 90f;
            }
        }
    }

    public void Interact()
    {
        if (isInteracting || isExiting || IsSolved)
            return;

        if (IsBroken)
        {
            if (audioSource)
                audioSource.PlayOneShot(overheatSound);
            return;
        }

        StartCoroutine(MoveToInteractionPoint());
    }

    private IEnumerator MoveToInteractionPoint()
    {
        isInteracting = true;
        inMachineMode = false;

        StarterAssets.CharacterController saController =
            playerMovementScript as StarterAssets.CharacterController;
        if (saController != null)
            saController.SetFrozen(true, lockCameraInput: true, restrictRotation: false);

        if (playerLookScript)
            playerLookScript.enabled = false;
        if (playerMovementScript)
            playerMovementScript.enabled = false;
        if (playerController)
            playerController.enabled = false;

        if (interactionStandPoint != null)
        {
            float duration = 0.5f;
            float t = 0f;
            Vector3 startPos = playerController.transform.position;
            Quaternion startRot = playerController.transform.rotation;

            if (playerAnimator)
            {
                playerAnimator.SetFloat("Speed", 0f);
                playerAnimator.SetFloat("MotionSpeed", 0f);
            }

            while (t < duration)
            {
                t += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t / duration);
                playerController.transform.position = Vector3.Lerp(
                    startPos,
                    interactionStandPoint.position,
                    smoothT
                );
                playerController.transform.rotation = Quaternion.Slerp(
                    startRot,
                    interactionStandPoint.rotation,
                    smoothT
                );
                yield return null;
            }
        }

        StartCoroutine(EnterMachineView());
    }

    private IEnumerator EnterMachineView()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = this;
        if (playerAnimator)
            playerAnimator.SetTrigger(interactAnimTrigger);

        if (interactVCam)
        {
            interactVCam.Priority = 102;
            _playerFollowCamera.SetActive(false);
        }

        yield return new WaitForSeconds(1.5f);
        inMachineMode = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.ShowMachineUI(ControlsUIManager.MachineType.Oscilloscope);

        PlayerInteraction playerInt = Object.FindFirstObjectByType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(false);

        // YENİ: Başlangıç bekleme mesajı
        GameActive = false;
        if (screenText)
        {
            screenText.color = Color.white;
            screenText.text = "SYSTEM READY\n[LEFT CLICK] TO START";
        }
    }

    private IEnumerator ExitMachineView()
    {
        if (isExiting)
            yield break;
        isExiting = true;
        inMachineMode = false;
        GameActive = false; // Çıkarken oyunu dondur

        if (interactVCam)
        {
            interactVCam.Priority = 0;
            if (_playerFollowCamera != null)
                _playerFollowCamera.SetActive(true);
        }

        yield return new WaitForSeconds(1.5f);

        if (playerController)
            playerController.enabled = true;
        if (playerMovementScript)
            playerMovementScript.enabled = true;

        StarterAssets.CharacterController saController =
            playerMovementScript as StarterAssets.CharacterController;
        if (saController != null)
            saController.SetFrozen(false, lockCameraInput: false, restrictRotation: false);

        if (playerLookScript)
            playerLookScript.enabled = true;

        PlayerInteraction playerInt = Object.FindFirstObjectByType<PlayerInteraction>();
        if (playerInt != null)
            playerInt.ToggleCrosshair(true);

        if (ControlsUIManager.Instance != null)
            ControlsUIManager.Instance.HideControls();
        if (GameManager.Instance != null)
            GameManager.Instance.activeInteraction = null;

        isInteracting = false;
        isExiting = false;
        PlayerInteraction.NotifyInteractionExit(gameObject);
    }

    private void Update()
    {
        if (IsBroken)
        {
            currentCooldown -= Time.deltaTime;
            if (screenText != null)
                screenText.text = $"SYSTEM FAILURE\nREBOOTING: {currentCooldown:F1}s";
            if (currentCooldown <= 0)
            {
                IsBroken = false;
                if (overheatSmoke)
                    overheatSmoke.SetActive(false);

                GameActive = false;
                if (screenText)
                {
                    screenText.color = Color.white;
                    screenText.text = "SYSTEM READY\n[LEFT CLICK] TO RESTART";
                }
            }
            return;
        }

        if (!inMachineMode || isExiting)
            return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            StartCoroutine(ExitMachineView());
            return;
        }

        if (IsSolved)
            return;

        if (!GameActive)
        {
            if (Input.GetMouseButtonDown(0))
            {
                GameActive = true;
                ResetMiniGame();
            }
            return;
        }

        // --- GÜNCELLENEN KISIM: İLK FIRLATMA (SERVE) MANTIĞI ---
        if (!isBallMoving)
        {
            HandlePaddleInput();

            if (Input.GetMouseButtonDown(0))
            {
                // Sen tıkladığın an, topu raketin olduğu yöne doğru fırlat!
                // Tam dümdüz olmasın diye +-10 derece rastgele kavis katıyoruz.
                float serveAngle = paddleAngle + Random.Range(-10f, 10f);
                float serveAngleRad = serveAngle * Mathf.Deg2Rad;
                ballVelocity = new Vector2(
                    Mathf.Cos(serveAngleRad),
                    Mathf.Sin(serveAngleRad)
                ).normalized;

                isBallMoving = true; // Top hareket etmeye başlasın
            }
            return;
        }

        HandleMiniGameLogic();
    }

    private void HandlePaddleInput()
    {
        float rotationInput = 0f;
        if (Input.GetKey(KeyCode.A))
            rotationInput = 1f;
        if (Input.GetKey(KeyCode.D))
            rotationInput = -1f;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.1f)
            rotationInput = 1f;
        if (scroll < -0.1f)
            rotationInput = -1f;

        if (rotationInput != 0)
        {
            paddleAngle += rotationInput * paddleSpeed * Time.deltaTime;
            if (paddleAngle >= 360f)
                paddleAngle -= 360f;
            if (paddleAngle < 0f)
                paddleAngle += 360f;

            if (controlKnob)
                controlKnob.Rotate(0, rotationInput * 2f, 0, Space.Self);
        }
    }

    private void HandleMiniGameLogic()
    {
        HandlePaddleInput();

        ballPosition += ballVelocity * currentBallSpeed * Time.deltaTime;

        if (Vector2.Distance(ballPosition, targetPosition) < (screenRadius * 0.15f))
        {
            currentScore++;
            currentBallSpeed += speedIncreasePerScore;

            if (audioSource && successSound)
                audioSource.PlayOneShot(successSound);

            if (currentScore >= scoreToWin)
            {
                StartCoroutine(SuccessSequence());
                return;
            }
            SpawnNewTarget();
        }

        // --- GÜNCELLENEN KISIM: KUSURSUZ SEKME MATEMATİĞİ ---
        if (ballPosition.magnitude >= screenRadius)
        {
            float ballAngle = Mathf.Atan2(ballPosition.y, ballPosition.x) * Mathf.Rad2Deg;
            if (ballAngle < 0)
                ballAngle += 360f;

            float angleDifference = Mathf.Abs(Mathf.DeltaAngle(ballAngle, paddleAngle));

            if (angleDifference <= paddleWidth / 2f)
            {
                // Topu doğrudan merkeze doğru (180 derece tersine) çeviriyoruz
                float baseReturnAngle = ballAngle + 180f;

                // Raketin neresine çarptı? (-1 sol uç, 0 tam orta, +1 sağ uç)
                float signedDifference = Mathf.DeltaAngle(paddleAngle, ballAngle);
                float hitFactor = signedDifference / (paddleWidth / 2f);

                // Sapma (Spin) ekle. Kenarlara çarptıkça maksimum 45 dereceye kadar kavis alır
                float deflection = hitFactor * 45f;

                // Yeni fırlama açısını kesin olarak belirle (Eski hızı tamamen eziyoruz, böylece yavaşlama olmaz)
                float finalAngle = (baseReturnAngle + deflection) * Mathf.Deg2Rad;
                ballVelocity = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle)).normalized;

                // Topu sertçe içeri iterek kenara "yapışma" (multiple-collision) bug'ını engelliyoruz
                ballPosition = ballPosition.normalized * (screenRadius - 0.05f);

                if (audioSource && wheelClickSound)
                    audioSource.PlayOneShot(wheelClickSound);
            }
            else
            {
                LoseLife();
            }
        }

        if (screenText)
        {
            screenText.text = $"LIVES: {lives} | SCORE: {currentScore}/{scoreToWin}";
        }
    }

    private void LoseLife()
    {
        lives--;
        if (audioSource && failSound)
            audioSource.PlayOneShot(failSound);

        if (lives <= 0)
        {
            GameActive = false; // Yenilince oyunu durdur
            isBallMoving = false;
            StartCoroutine(TriggerBreakdown());
        }
        else
        {
            ResetBall(); // Topu merkeze al ve durdur
            if (screenText)
            {
                screenText.text =
                    $"LIVES: {lives} | SCORE: {currentScore}/{scoreToWin}\n[LEFT CLICK] TO SERVE";
            }
        }
    }

    private IEnumerator TriggerBreakdown()
    {
        IsBroken = true;
        currentCooldown = cooldownDuration;

        if (audioSource && overheatSound)
            audioSource.PlayOneShot(overheatSound);
        if (overheatSmoke)
            overheatSmoke.SetActive(true);
        if (screenText)
            screenText.text = "CRITICAL ERROR!";

        // YENİ: Oyuncuyu dışarı fırlatan (ExitMachineView) kodunu sildik!
        // Artık kamerada kalıp sürenin dolmasını bekleyecek.
        yield return null;
    }

    private IEnumerator SuccessSequence()
    {
        IsSolved = true;
        GameActive = false; // Çözülünce oyunu dondur

        if (audioSource && successSound)
            audioSource.PlayOneShot(successSound);

        if (screenText)
        {
            screenText.color = Color.green;
            screenText.text = $"STABLE\nKEY: {assignedPassword}";
        }

        if (PasswordManager.Instance != null)
            PasswordManager.Instance.DiscoverClue(assignedPassword);

        yield return new WaitForSeconds(3.0f);
        StartCoroutine(ExitMachineView());
    }

    private void ResetMiniGame()
    {
        lives = 3;
        currentScore = 0;
        currentBallSpeed = ballSpeed; // Hızı sıfırla
        paddleAngle = 0f;
        if (screenText)
            screenText.color = Color.white;

        ResetBall();
        SpawnNewTarget();

        // DÜZELTME: isBallMoving = true; SATIRINI TAMAMEN SİLDİK!
        // Artık sistem topun hızını (0,0)'da bırakıp senin Update içinde sol tıkla fırlatmanı bekleyecek.
    }

    private void ResetBall()
    {
        ballPosition = Vector2.zero;
        isBallMoving = false; // Yeni top fırlatılmayı beklesin
        // ballVelocity atamasını sildik, çünkü artık Update içinde tıklayınca yapılıyor.
    }

    private void SpawnNewTarget()
    {
        float randAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randRadius = Random.Range(screenRadius * 0.2f, screenRadius * 0.8f);
        targetPosition = new Vector2(
            Mathf.Cos(randAngle) * randRadius,
            Mathf.Sin(randAngle) * randRadius
        );
    }

    public void AssignPassword(string pw)
    {
        assignedPassword = pw;
    }

    public void ForceExit()
    {
        if (inMachineMode)
            StartCoroutine(ExitMachineView());
    }

    public void OnFocus() { }

    public void OnLoseFocus() { }

    public string GetInteractionPrompt() =>
        IsSolved
            ? "Sinyal Stabil"
            : (IsBroken ? "Sistem Soğuyor..." : (isInteracting ? "" : "[Sol Tık] Osiloskop"));
}
