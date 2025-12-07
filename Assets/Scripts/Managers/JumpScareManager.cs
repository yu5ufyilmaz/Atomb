using System.Collections;
using Cinemachine; // Unity 6 ise Unity.Cinemachine olabilir
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance;

    [Header("Referanslar")]
    [Tooltip("StarterAssetsInputs scripti")]
    [SerializeField]
    private StarterAssetsInputs playerInput;

    [SerializeField]
    private StarterAssets.CharacterController playerController;

    [SerializeField]
    private Animator playerAnimator;

    [Header("Kamera Ayarları")]
    [Tooltip("Sahnedeki Main Camera")]
    [SerializeField]
    private Camera mainCamera;

    [Tooltip("Karakterin İskeletindeki KAFA (Head) Kemiği")]
    [SerializeField]
    private Transform headBone;

    [Header("Jumpscare Ayarları")]
    [Tooltip("Siyah ekran öncesi bekleme süresi")]
    [SerializeField]
    private float scareDuration = 2.5f;

    [Tooltip("Kamerayı kafaya bağlayınca yapılacak ince ayar (Göz hizası)")]
    [SerializeField]
    private Vector3 eyeOffset = new Vector3(0, 0.1f, 0.15f);

    // Animator Parametreleri
    private int _animIDPanicRight;
    private int _animIDPanicLeft;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        _animIDPanicRight = Animator.StringToHash("PanicTurnRight");
        _animIDPanicLeft = Animator.StringToHash("PanicTurnLeft");
    }

    private void Start()
    {
        // Otomatik Bulma
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            if (!playerInput)
                playerInput = player.GetComponent<StarterAssetsInputs>();
            if (!playerController)
                playerController = player.GetComponent<StarterAssets.CharacterController>();
            if (!playerAnimator)
                playerAnimator = player.GetComponent<Animator>();

            // Eğer headBone atanmadıysa, otomatik bulmaya çalış (İsimle)
            if (headBone == null)
            {
                // Mixamo veya standart riglerde genelde bu isimlerdedir
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

    public void StartDirectionalJumpscare(Transform enemy, bool turnRight)
    {
        StartCoroutine(BoneLockRoutine(enemy, turnRight));
    }

    private IEnumerator BoneLockRoutine(Transform enemy, bool turnRight)
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

        // 2. CINEMACHINE'İ SUSTUR (Kamerayı serbest bırak)
        if (mainCamera != null)
        {
            var brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain)
                brain.enabled = false;
        }

        // 3. KAMERAYI KAFAYA MONTE ET (Parenting)
        if (mainCamera != null && headBone != null)
        {
            mainCamera.transform.SetParent(headBone);

            // Pozisyonu kafanın tam ortasına (gözlere) getir
            mainCamera.transform.localPosition = eyeOffset;

            // Rotasyonu kafanın baktığı yöne eşitle
            mainCamera.transform.localRotation = Quaternion.identity;

            // Eğer kafa yamuk duruyorsa (bazı riglerde olur), buraya manuel düzeltme gerekebilir:
            // mainCamera.transform.localRotation = Quaternion.Euler(0, 90, -90); // Örnek
        }

        // 4. ANİMASYONU BAŞLAT (Artık kamera kafa nereye giderse oraya gidecek)
        if (playerAnimator)
        {
            if (turnRight)
                playerAnimator.SetTrigger(_animIDPanicRight);
            else
                playerAnimator.SetTrigger(_animIDPanicLeft);
        }

        // 5. BEKLE
        yield return new WaitForSeconds(scareDuration);

        // 6. BİTİŞ
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Kemikleri isme göre bulmak için yardımcı fonksiyon
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
