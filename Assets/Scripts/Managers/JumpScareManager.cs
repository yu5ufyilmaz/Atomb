using System.Collections;
using Cinemachine;
using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering; // <-- Volume için gerekli
using UnityEngine.Rendering.HighDefinition; // <-- HDRP efektleri için gerekli
using UnityEngine.SceneManagement;

// TİPLERİ BURADA TANIMLIYORUZ
public enum JumpscareStyle
{
    Direct, // Klasik: Anında veya çok hızlı yüze bakma (Adam, Guderian Kapı)
    SmartDisplacement, // Lees Özel: Sağ/Sol kontrolü ve yavaş dönüş (Halüsinasyon)
    ForcedBehind, // Senaryo B: Zorla arkaya çevirme (Kapıdan kaçarken)
}

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

    [Header("Volume (Post-Process)")]
    [Tooltip("Sahnendeki Global Volume'ü buraya sürükle")]
    [SerializeField]
    private Volume globalVolume;

    // HDRP Efektleri
    private Vignette m_Vignette;
    private ChromaticAberration m_Aberration;
    private LensDistortion m_LensDistortion;
    private FilmGrain m_FilmGrain;
    private ColorAdjustments m_ColorAdjustments;

    [Header("Jumpscare Hissi (The Juice)")]
    [Tooltip("Sarsıntının şiddeti")]
    [SerializeField]
    private float shakeIntensity = 0.5f;

    [Tooltip("Sarsıntının hızı")]
    [SerializeField]
    private float shakeFrequency = 20f;

    [Tooltip("Kameranın eğilme açısı (Dutch Angle)")]
    [SerializeField]
    private float tiltAngle = 10f;

    [Tooltip("Jumpscare anında gidilecek FOV değeri")]
    [SerializeField]
    private float targetFOV = 40f;

    [Header("Dönüş Ayarları")]
    [Tooltip("Lees tarzı yavaş dönüş hızı")]
    [SerializeField]
    private float slowTurnSpeed = 3.5f;

    [Tooltip("Adam/Guderian tarzı hızlı dönüş hızı")]
    [SerializeField]
    private float fastTurnSpeed = 15.0f;

    [SerializeField]
    private LayerMask obstacleLayers;

    [Tooltip("Duvar kontrolü için ışın mesafesi")]
    [SerializeField]
    private float obstacleCheckDistance = 1.0f;

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
        // 1. Oyuncu Referanslarını Bul
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

        // 2. Volume Referanslarını Al (Yoksa Bul)
        if (globalVolume == null)
            globalVolume = FindObjectOfType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out m_Vignette);
            globalVolume.profile.TryGet(out m_Aberration);
            globalVolume.profile.TryGet(out m_LensDistortion);
            globalVolume.profile.TryGet(out m_FilmGrain);
            globalVolume.profile.TryGet(out m_ColorAdjustments);
        }
    }

    public void StartJumpscare(
        Transform enemy,
        bool playTurnAnim = true,
        float customDuration = 0f,
        JumpscareStyle style = JumpscareStyle.Direct
    )
    {
        float duration = customDuration > 0 ? customDuration : defaultScareDuration; // defaultScareDuration tanımlı olmalı
        StartCoroutine(JumpscareRoutine(enemy, playTurnAnim, duration, style));
    }

    private IEnumerator JumpscareRoutine(
        Transform enemy,
        bool playTurnAnim,
        float duration,
        JumpscareStyle style
    )
    {
        // 1. KONTROLLERİ VE KAMERAYI KAPAT
        if (playerInput)
        {
            playerInput.cursorInputForLook = false;
            playerInput.move = Vector2.zero;
            playerInput.enabled = false;
        }
        if (playerController)
            playerController.enabled = false;
        if (mainCamera != null)
        {
            var brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain)
                brain.enabled = false;
        }

        // Kamerayı kafaya sabitle
        if (headBone != null)
        {
            mainCamera.transform.SetParent(headBone);
            mainCamera.transform.localPosition = eyeOffset;
        }

        // 2. POZİSYON VE HEDEF BELİRLEME
        Vector3 targetPos = enemy.position;
        float currentTurnSpeed = fastTurnSpeed;

        switch (style)
        {
            case JumpscareStyle.Direct:
                currentTurnSpeed = fastTurnSpeed;
                break;

            case JumpscareStyle.SmartDisplacement:
                targetPos = GetSmartJumpscarePosition(playerController.transform, 1.2f);
                targetPos.y = playerController.transform.position.y;
                enemy.position = targetPos;
                enemy.LookAt(playerController.transform.position);
                currentTurnSpeed = slowTurnSpeed;
                break;

            case JumpscareStyle.ForcedBehind:
                Vector3 backDir = -playerController.transform.forward;
                targetPos = playerController.transform.position + (backDir * 1.2f);
                targetPos.y = playerController.transform.position.y;
                enemy.position = targetPos;
                enemy.LookAt(playerController.transform.position);
                currentTurnSpeed = slowTurnSpeed;
                break;
        }

        // 3. ANİMASYON TETİKLEME
        if (playTurnAnim && playerAnimator != null)
        {
            Vector3 dirToTarget = (enemy.position - playerController.transform.position).normalized;
            float angle = Vector3.SignedAngle(
                playerController.transform.forward,
                dirToTarget,
                Vector3.up
            );

            if (Mathf.Abs(angle) > 135f)
                playerAnimator.SetTrigger(_animIDPanicBack);
            else if (angle > 0)
                playerAnimator.SetTrigger(_animIDPanicRight);
            else
                playerAnimator.SetTrigger(_animIDPanicLeft);
        }

        // 4. DÖNGÜ (DÜZELTİLMİŞ KISIM)
        float timer = 0f;

        // Kamera dönüşü başlamadan önce beklenecek süre (saniye)
        // Animasyonun biraz ilerlemesine izin verir.
        float rotationDelay = 0.35f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;

            // Düşmanın kafasına bakmak için hedef rotasyon
            Vector3 enemyHeadPos = enemy.position + (Vector3.up * 1.6f);
            Quaternion targetRot = Quaternion.LookRotation(
                enemyHeadPos - mainCamera.transform.position
            );

            // GECİKMELİ DÖNÜŞ KONTROLÜ
            if (timer > rotationDelay)
            {
                // Belirlenen süre geçtiyse kamerayı düşmana çevir
                mainCamera.transform.rotation = Quaternion.Slerp(
                    mainCamera.transform.rotation,
                    targetRot,
                    Time.deltaTime * currentTurnSpeed
                );
            }
            // else: Süre dolmadıysa kamera karakterin kafasına bağlı olarak doğal açısında kalsın.

            // Titreşim (Shake) her zaman aktif olabilir
            float shake =
                (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * shakeIntensity;
            mainCamera.transform.Rotate(new Vector3(shake, shake * 0.5f, 0));

            ApplyJumpscareEffects(progress);

            yield return null;
        }

        // BİTİŞ
        if (DeathUIManager.Instance != null)
            DeathUIManager.Instance.ShowDeathScreen();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Bu fonksiyon, sağımız/solumuz dolu mu diye bakar ve düşmanı koymak için EN MÜSAİT yeri seçer.
    // YARDIMCI: Akıllı Pozisyon Bulucu (Lees için)
    private Vector3 GetSmartJumpscarePosition(Transform player, float distance)
    {
        Vector3 origin = player.position + Vector3.up * 1.5f;
        bool hitRight = Physics.Raycast(origin, player.right, 1.0f, obstacleLayers);
        bool hitLeft = Physics.Raycast(origin, -player.right, 1.0f, obstacleLayers);

        if (!hitRight)
            return player.position + (player.right * distance); // Sağ boşsa sağa
        else if (!hitLeft)
            return player.position + (-player.right * distance); // Sol boşsa sola
        else
            return player.position + (-player.forward * distance); // İkisi de doluysa arkaya
    }

    private void ApplyJumpscareEffects(float progress)
    {
        // Efektlerin şiddetini zamanla artır (Lerp)
        // progress: 0.0 (Başlangıç) -> 1.0 (Son)

        // 1. VIGNETTE (Kenar Kararması)
        if (m_Vignette != null)
        {
            // Normalden 0.65 şiddetine çıksın
            m_Vignette.intensity.Override(Mathf.Lerp(0f, 0.65f, progress));
            m_Vignette.smoothness.Override(Mathf.Lerp(0.2f, 1f, progress));
        }

        // 2. CHROMATIC ABERRATION (Renk Ayrışması)
        if (m_Aberration != null)
        {
            // Çok şiddetli bozulma (1.0 max)
            m_Aberration.intensity.Override(Mathf.Lerp(0f, 1f, progress));
        }

        // 3. LENS DISTORTION (Bükülme)
        if (m_LensDistortion != null)
        {
            // Hafif içe göçme etkisi (-0.4)
            m_LensDistortion.intensity.Override(Mathf.Lerp(0f, -0.4f, progress));
            m_LensDistortion.scale.Override(Mathf.Lerp(1f, 0.9f, progress));
        }

        // 4. FILM GRAIN (Kumlanma/Gürültü)
        if (m_FilmGrain != null)
        {
            m_FilmGrain.intensity.Override(Mathf.Lerp(0f, 1f, progress));
        }

        // 5. SATURATION (Renk Solması - Opsiyonel)
        if (m_ColorAdjustments != null)
        {
            // Renkler %50 azalsın
            m_ColorAdjustments.saturation.Override(Mathf.Lerp(0f, -50f, progress));
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
