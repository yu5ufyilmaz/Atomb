using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering; // Post-Processing için (Volume kullanıyorsan)
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

    [Header("Jumpscare Hissi (The Juice)")]
    [Tooltip("Sarsıntının şiddeti")]
    [SerializeField]
    private float shakeIntensity = 0.5f;

    [Tooltip("Sarsıntının hızı")]
    [SerializeField]
    private float shakeFrequency = 20f;

    [Tooltip("Kameranın eğilme açısı (Dutch Angle). Örn: 15 derece.")]
    [SerializeField]
    private float tiltAngle = 10f;

    [Tooltip("Jumpscare anında gidilecek FOV değeri (Daha düşük = Daha yakın/Klostrofobik)")]
    [SerializeField]
    private float targetFOV = 40f;

    [Header("Ayarlar")]
    [SerializeField]
    private float defaultScareDuration = 2.5f;

    [SerializeField]
    private Vector3 eyeOffset = new Vector3(0, 0.1f, 0.15f);

    // Animasyon ID'leri
    private int _animIDPanicRight;
    private int _animIDPanicLeft;
    private int _animIDPanicBack;
    private int _animIDSpeed;
    private int _animIDMotionSpeed;

    private float originalFOV;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        _animIDPanicRight = Animator.StringToHash("PanicTurnRight");
        _animIDPanicLeft = Animator.StringToHash("PanicTurnLeft");
        _animIDPanicBack = Animator.StringToHash("PanicTurnBack");
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

        if (mainCamera != null)
            originalFOV = mainCamera.fieldOfView;
    }

    public void StartJumpscare(Transform enemy, bool playTurnAnim = true, float customDuration = 0f)
    {
        float duration = customDuration > 0 ? customDuration : defaultScareDuration;
        StartCoroutine(JumpscareRoutine(enemy, playTurnAnim, duration));
    }

    private IEnumerator JumpscareRoutine(Transform enemy, bool playTurnAnim, float duration)
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

        // Karakteri durdur
        if (playerAnimator != null)
        {
            playerAnimator.SetFloat(_animIDSpeed, 0f);
            playerAnimator.SetFloat(_animIDMotionSpeed, 0f);
        }

        // 2. CINEMACHINE KAPAT & KAFAYA MONTE ET
        if (mainCamera != null)
        {
            var brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain)
                brain.enabled = false;

            if (headBone != null)
            {
                mainCamera.transform.SetParent(headBone);
                // Pozisyonu sıfırla ama aşağıda Shake ile değiştireceğiz
                mainCamera.transform.localPosition = eyeOffset;
                // Önce düşmana dümdüz baktır
                mainCamera.transform.LookAt(enemy.position + Vector3.up * 1.5f); // Göz hizasına bakması için +1.5f ekledim
            }
        }

        // 3. ANİMASYON (Karakterin tepkisi)
        if (playTurnAnim && playerAnimator != null)
        {
            Vector3 dirToEnemy = (enemy.position - playerController.transform.position).normalized;
            float angle = Vector3.SignedAngle(
                playerController.transform.forward,
                dirToEnemy,
                Vector3.up
            );

            if (Mathf.Abs(angle) > 135f)
                playerAnimator.SetTrigger(_animIDPanicBack);
            else if (angle > 0)
                playerAnimator.SetTrigger(_animIDPanicRight);
            else
                playerAnimator.SetTrigger(_animIDPanicLeft);
        }

        // 4. KAOS DÖNGÜSÜ (Shake, Tilt, Zoom)
        float timer = 0f;

        // Rastgele bir eğilme yönü seç (Sağa mı sola mı yatacak?)
        float randomTilt = Random.Range(-1f, 1f) > 0 ? tiltAngle : -tiltAngle;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            // A) Shake (Titreme)
            // Perlin Noise kullanarak daha "doğal" bir titreme yapıyoruz
            float xShake =
                (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * shakeIntensity;
            float yShake =
                (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * shakeIntensity;

            if (headBone != null)
            {
                // Kamerayı kafa kemiğine göre ofsetliyoruz + titreme ekliyoruz
                mainCamera.transform.localPosition = eyeOffset + new Vector3(xShake, yShake, 0);
            }

            // B) Look At (Düşmanın gözüne kilitli kal ama titreyerek)
            // Düşmanın kafa hizasını tahmin ediyoruz (Enemy pos + 1.6m)
            Vector3 targetLookPos = enemy.position + (Vector3.up * 1.5f);
            mainCamera.transform.LookAt(targetLookPos);

            // C) Tilt (Eğilme) ve Zoom (FOV) - Lerp ile yumuşak geçiş
            // Zamanla hedef FOV'a ve hedef Tilt açısına git
            float t = timer / 0.5f; // İlk 0.5 saniyede bu etki otursun
            mainCamera.fieldOfView = Mathf.Lerp(originalFOV, targetFOV, t);

            // LookAt rotasyonu sıfırladığı için, Tilt'i Z eksenine sonradan ekliyoruz
            Vector3 currentEuler = mainCamera.transform.localEulerAngles;
            // Z eksenini yumuşakça hedef açıya götür
            float currentZ = Mathf.LerpAngle(0, randomTilt, t);
            mainCamera.transform.localRotation = Quaternion.Euler(
                currentEuler.x,
                currentEuler.y,
                currentZ
            );

            yield return null;
        }

        // 5. SONUÇ
        if (DeathUIManager.Instance != null)
        {
            DeathUIManager.Instance.ShowDeathScreen();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
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
