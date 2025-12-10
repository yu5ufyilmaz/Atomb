using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance;

    [Header("Referanslar")]
    [SerializeField]
    private StarterAssetsInputs playerInput;

    [SerializeField]
    private StarterAssets.CharacterController playerController;

    [SerializeField]
    private Animator playerAnimator;

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Transform headBone;

    [Header("Jumpscare Ayarları")]
    [SerializeField]
    private float scareDuration = 2.5f;

    [SerializeField]
    private Vector3 eyeOffset = new Vector3(0, 0.1f, 0.15f);

    // Animasyon ID'leri
    private int _animIDPanicRight;
    private int _animIDPanicLeft;
    private int _animIDPanicBack;

    // YENİ: Hız parametrelerini sıfırlamak için
    private int _animIDSpeed;
    private int _animIDMotionSpeed;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // ID'leri bir kez cache'liyoruz
        _animIDPanicRight = Animator.StringToHash("PanicTurnRight");
        _animIDPanicLeft = Animator.StringToHash("PanicTurnLeft");
        _animIDPanicBack = Animator.StringToHash("PanicTurnBack");

        // StarterAssets animatör parametreleri
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            if (!playerInput)
                playerInput = player.GetComponent<StarterAssetsInputs>();
            if (!playerController)
                playerController = player.GetComponent<StarterAssets.CharacterController>();
            if (!playerAnimator)
                playerAnimator = player.GetComponent<Animator>();

            if (headBone == null)
            {
                Transform head = RecursiveFindChild(player.transform, "Head");
                if (head == null)
                    head = RecursiveFindChild(player.transform, "mixamorig:Head");
                if (head != null)
                    headBone = head;
            }
        }
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    public void StartJumpscare(Transform enemy, bool playTurnAnim = true)
    {
        StartCoroutine(BoneLockRoutine(enemy, playTurnAnim));
    }

    private IEnumerator BoneLockRoutine(Transform enemy, bool playTurnAnim)
    {
        // 1. KONTROLLERİ KAPAT
        if (playerInput)
        {
            playerInput.cursorInputForLook = false;
            playerInput.move = Vector2.zero;
            playerInput.look = Vector2.zero;
            playerInput.enabled = false;
        }
        if (playerController)
            playerController.enabled = false;

        // --- DÜZELTME BAŞLANGICI ---
        // Karakterin yürüme animasyonunu ZORLA durdur.
        // Aksi takdirde script kapansa bile Animator son hızı hatırlar ve karakter olduğu yerde yürür.
        if (playerAnimator != null)
        {
            playerAnimator.SetFloat(_animIDSpeed, 0f);
            playerAnimator.SetFloat(_animIDMotionSpeed, 0f);
        }
        // --- DÜZELTME BİTİŞİ ---

        // 2. CINEMACHINE KAPAT & KAFAYA MONTE ET
        if (mainCamera != null)
        {
            var brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain)
                brain.enabled = false;

            if (headBone != null)
            {
                mainCamera.transform.SetParent(headBone);
                mainCamera.transform.localPosition = eyeOffset;
                // Kameranın rotasyonunu sıfırla ki tam kafanın baktığı yere baksın
                mainCamera.transform.localRotation = Quaternion.identity;
            }
        }

        // 3. ANİMASYON (Sadece İstenirse Oynar - Örn: Arkadan yakalanınca)
        if (playTurnAnim && playerAnimator != null)
        {
            // Yön Hesapla
            Vector3 dirToEnemy = (enemy.position - playerController.transform.position).normalized;
            float angle = Vector3.SignedAngle(
                playerController.transform.forward,
                dirToEnemy,
                Vector3.up
            );

            // Arkadaysa (135+)
            if (Mathf.Abs(angle) > 135f)
                playerAnimator.SetTrigger(_animIDPanicBack);
            // Sağdaysa
            else if (angle > 0)
                playerAnimator.SetTrigger(_animIDPanicRight);
            // Soldaysa
            else
                playerAnimator.SetTrigger(_animIDPanicLeft);
        }

        // 4. BEKLE
        yield return new WaitForSeconds(scareDuration);

        // 5. BİTİŞ (Sahneyi Yenile)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private Transform RecursiveFindChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(childName))
                return child;
            Transform found = RecursiveFindChild(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
