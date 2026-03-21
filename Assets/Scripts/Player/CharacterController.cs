using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.UI;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(UnityEngine.CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class CharacterController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        // --- STAMINA AYARLARI (YENİ EKLENEN KISIM) ---
        [Header("Stamina System")]
        [Tooltip("Maksimum Stamina")]
        public float maxStamina = 100f;

        [Tooltip("Koşarken saniyede ne kadar azalsın?")]
        public float staminaDrainRate = 15f;

        [Tooltip("Dururken/Yürürken saniyede ne kadar dolsun?")]
        public float staminaRegenRate = 10f;

        [Tooltip(
            "Stamina tamamen biterse, tekrar koşabilmek için dolması gereken oran (0.75 = %75)"
        )]
        [Range(0f, 1f)]
        public float exhaustionThreshold = 0.75f;

        [Header("Stamina UI")]
        public Image staminaCircularImage;

        // Private Stamina Değişkenleri
        private float currentStamina;
        private bool isExhausted = false; // Yorgunluk cezası durumu

        // --- [YENİ] SARHOŞLUK SİSTEMİ DEĞİŞKENLERİ ---
        [Header("😵 Drunk Effect Settings")]
        [Range(0f, 1f)]
        public float drunkIntensity = 0f; // 0 = Normal, 1 = Tam Sarhoş

        [Header("Sway (Kamera Sallantısı)")]
        public float swaySpeed = 0.5f; // Ne kadar hızlı sallansın? (Düşük = Ağır sarhoş)
        public float swayAmountRoll = 10.0f; // Kafa yan yatma açısı (Z ekseni)
        public float swayAmountYaw = 10.0f; // Kafa sağa/sola bakma sapması (Y ekseni)

        [Header("Drift (Yürüme Kayması)")]
        public float driftSpeed = 0.3f; // Kayma yönünün değişme hızı
        public float driftForce = 1.5f; // Kaymanın şiddeti

        // Hesaplanan anlık değerler (Private)
        private float drunkTime;
        private float currentDrunkRoll;
        private float currentDrunkYaw;
        private Vector3 currentDrunkDrift;

        // ----------------------------------------------

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;

        [Range(0, 1)]
        public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip(
            "Time required to pass before being able to jump again. Set to 0f to instantly jump again"
        )]
        public float JumpTimeout = 0.50f;

        [Tooltip(
            "Time required to pass before entering the fall state. Useful for walking down stairs"
        )]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip(
            "If the character is grounded or not. Not part of the CharacterController built in grounded check"
        )]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip(
            "The radius of the grounded check. Should match the radius of the CharacterController"
        )]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip(
            "The follow target set in the Cinemachine Virtual Camera that the camera will follow"
        )]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip(
            "Additional degress to override the camera. Useful for fine tuning camera position when locked"
        )]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        // Mevcut ID'lerin yanına ekle
        private int _animIDVelocityX;
        private int _animIDVelocityZ;

        [Header("Head Bob System")]
        [Tooltip("Adım atma hızı (Adım sıklığı). 12-14 arası idealdir.")]
        public float BobFrequency = 12f;

        [Tooltip("Kafanın YUKARI-AŞAĞI oynama miktarı. (Tok his için düşük tut: 0.05)")]
        public float BobYAmplitude = 0.05f;

        [Tooltip("Kafanın SAĞA-SOLA oynama miktarı. (Doğallığı bu verir: 0.06)")]
        public float BobXAmplitude = 0.06f;

        private float _defaultYPos;
        private float _bobTimer;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private UnityEngine.CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private AudioSource _audioSource;
        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            // Karakterde AudioSource var mı diye bak, yoksa otomatik ekle
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f; // Sesi 3D (uzamsal) yapar
            }
            _controller = GetComponent<UnityEngine.CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError(
                "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it"
            );
#endif
            currentStamina = maxStamina;
            if (staminaCircularImage != null)
            {
                // Slider'da value vardı, Image'de fillAmount var (0 ile 1 arası)
                staminaCircularImage.fillAmount = currentStamina / maxStamina;
                staminaCircularImage.color = Color.white;
            }
            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            // Rastgelelik için seed belirle
            if (CinemachineCameraTarget != null)
                _defaultYPos = CinemachineCameraTarget.transform.localPosition.y;
            // CharacterController.cs içindeki Start() metodunun en alt kısmı
            if (GameManager.Instance != null && !GameManager.Instance.isGameStarted)
            {
                // Faz 1: Masada Oturuyoruz
                // freeze = true (Karakter yürüyemez)
                // lockCameraInput = true (Kamera TAMAMEN kilitli, fare ile etrafa bakamayız)
                // restrictRotation = false (Artık tam kilitli olduğumuz için boyun kısıtlamasına gerek yok)
                SetFrozen(true, true, false);
            }
        }

        private void Update()
        {
            JumpAndGravity();
            GroundedCheck();
            HandleStamina();
            CalculateDrunkEffects(); // <--- [YENİ] BURAYA EKLENDİ
            Move();
            HandleHeadBob(); // <-- YENİ: Bunu buraya ekle!
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");

            // --- YENİ EKLENECEK KISIM ---
            _animIDVelocityX = Animator.StringToHash("VelocityX");
            _animIDVelocityZ = Animator.StringToHash("VelocityZ");
            // ----------------------------
        }

        // --- YENİ EKLENEN FONKSİYON: Head Bob Merkezini Sıfırlama ---
        public void ResetHeadBobYPos(float newYPos)
        {
            _defaultYPos = newYPos;
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(
                transform.position.x,
                transform.position.y - GroundedOffset,
                transform.position.z
            );
            Grounded = Physics.CheckSphere(
                spherePosition,
                GroundedRadius,
                GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // 1. TAM KİLİT (Hiç hareket yok)
            if (_lockCamera)
                return;

            // Mouse/Gamepad giriş kontrolü
            if (_input.look.sqrMagnitude >= _threshold)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                // Sarhoşluk kontrolü
                float controlLag =
                    (drunkIntensity > 0.01f) ? Mathf.Lerp(1f, 0.5f, drunkIntensity) : 1f;

                // --- YENİ EKLENEN KISIM: Ayarlardan Hassasiyeti Çek ---
                float sensitivity = 1f;
                if (SettingsManager.Instance != null)
                {
                    sensitivity = SettingsManager.Instance.currentSettings.mouseSensitivity;
                }
                // -------------------------------------------------------

                // sensitivity çarpanını dönüş hızlarına ekliyoruz
                _cinemachineTargetYaw +=
                    _input.look.x * deltaTimeMultiplier * controlLag * sensitivity;
                _cinemachineTargetPitch +=
                    _input.look.y * deltaTimeMultiplier * controlLag * sensitivity;
            }

            // Pitch (Yukarı/Aşağı) Clamp (Eski kodun)
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Yaw (Sağ/Sol) Clamp (Eski kodun - Ama aşağıda modifiye edeceğiz)
            _cinemachineTargetYaw = ClampAngle(
                _cinemachineTargetYaw,
                float.MinValue,
                float.MaxValue
            );

            // --- 2. YENİ KISIM: KISITLI GÖRÜŞ (BOYUN HAREKETİ) ---
            if (_restrictRotation)
            {
                // Şu anki açı ile Merkez açı arasındaki farkı bul (Mathf.DeltaAngle 360 sarmalını doğru hesaplar)
                float angleDifference = Mathf.DeltaAngle(_centerYaw, _cinemachineTargetYaw);

                // Farkı limitler arasında tut (-50 ile +50 arası)
                angleDifference = Mathf.Clamp(angleDifference, -_yawLimit, _yawLimit);

                // Açıyı tekrar hesapla
                _cinemachineTargetYaw = _centerYaw + angleDifference;
            }
            // ----------------------------------------------------

            // Sarhoşluk Etkisi (Mevcut kodların)
            float addedYaw = (drunkIntensity > 0.01f) ? currentDrunkYaw : 0f;
            float addedRoll = (drunkIntensity > 0.01f) ? currentDrunkRoll : 0f;

            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                _cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw + addedYaw,
                0.0f + addedRoll
            );
        }

        private bool _isFrozen = false; // Hareket kilitli mi?
        private bool _lockCamera = false; // Kamera kilitli mi?

        private void Move()
        {
            if (_isFrozen)
            {
                _speed = 0f;
                _animationBlend = 0f;

                if (_hasAnimator)
                {
                    _animator.SetFloat(_animIDSpeed, 0f);
                    _animator.SetFloat(_animIDMotionSpeed, 0f);
                    _animator.SetFloat(_animIDVelocityX, 0f); // Kaymayı önleyen asıl kahramanlar
                    _animator.SetFloat(_animIDVelocityZ, 0f);
                }
                return; // Hareket hesaplamasını yapmadan fonksiyondan çık
            }
            // 1. Hız Hesaplama
            // 1. Hız Hesaplama
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // --- YENİ: BİTKİNLİK HİSSİYATI ---
            // Eğer karakter yorgunluktan tükenmişse (ceza modundaysa), normalden de yavaş yürüsün.
            if (isExhausted)
            {
                targetSpeed = MoveSpeed * 0.65f; // Normal yürüme hızının %65'ine düşer
            }

            if (_input.move == Vector2.zero)
                targetSpeed = 0.0f;

            if (drunkIntensity > 0.01f)
                targetSpeed *= Mathf.Lerp(1f, 0.6f, drunkIntensity);

            float currentHorizontalSpeed = new Vector3(
                _controller.velocity.x,
                0.0f,
                _controller.velocity.z
            ).magnitude;
            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (
                currentHorizontalSpeed < targetSpeed - speedOffset
                || currentHorizontalSpeed > targetSpeed + speedOffset
            )
            {
                _speed = Mathf.Lerp(
                    currentHorizontalSpeed,
                    targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate
                );
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(
                _animationBlend,
                targetSpeed,
                Time.deltaTime * SpeedChangeRate
            );
            if (_animationBlend < 0.01f)
                _animationBlend = 0f;

            // ROTASYON: Karakteri her zaman KAMERANIN baktığı yöne kilitle.
            _targetRotation = _mainCamera.transform.eulerAngles.y;

            // Sarhoşluk yalpalamasını rotasyona ekle
            float rotationWithDrunk = _targetRotation;
            if (drunkIntensity > 0.01f)
                rotationWithDrunk += currentDrunkYaw;

            // Karakterin gövdesini kamera açısına yumuşakça döndür
            float rotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                rotationWithDrunk,
                ref _rotationVelocity,
                RotationSmoothTime
            );
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

            // HAREKET YÖNÜ: Input'u (WASD) kamera açısına göre ayarla.
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // Input vektörünü, Kameranın Y açısı (TargetRotation) ile çarpıp dünya yönüne çeviriyoruz
            Vector3 targetDirection =
                Quaternion.Euler(0.0f, _targetRotation, 0.0f) * inputDirection;

            // Hareketi Uygula
            Vector3 movement =
                targetDirection.normalized * (_speed * Time.deltaTime)
                + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime;

            // Sarhoş kayması
            if (drunkIntensity > 0.01f)
            {
                movement += currentDrunkDrift * Time.deltaTime;
            }

            _controller.Move(movement);

            // Animasyon Güncelleme
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);

                // YÖNLÜ ANİMASYON
                Vector3 localVelocity = transform.InverseTransformDirection(_controller.velocity);
                _animator.SetFloat(_animIDVelocityX, localVelocity.x, 0.15f, Time.deltaTime);
                _animator.SetFloat(_animIDVelocityZ, localVelocity.z, 0.15f, Time.deltaTime);
            }
        }

        // --- YENİ FONKSİYON: Stamina Yönetimi ---
        private void HandleStamina()
        {
            // Oyuncu hareket ediyor mu? (Hareket etmiyorsa koşamaz)
            bool isMoving = _input.move != Vector2.zero;
            bool wantsToSprint = _input.sprint;

            // DURUM 1: Koşmaya Çalışıyor
            if (isMoving && wantsToSprint && !isExhausted)
            {
                if (currentStamina > 0)
                {
                    // Staminayı azalt
                    currentStamina -= staminaDrainRate * Time.deltaTime;

                    // Biterse yorgunluk moduna gir
                    if (currentStamina <= 0)
                    {
                        currentStamina = 0;
                        isExhausted = true; // CEZA BAŞLADI
                        _input.sprint = false; // Otomatik yürüme moduna al
                    }
                }
            }
            // DURUM 2: Dinleniyor (Yürüyor veya Duruyor)
            else
            {
                if (currentStamina < maxStamina)
                {
                    // Staminayı doldur
                    currentStamina += staminaRegenRate * Time.deltaTime;
                    if (currentStamina > maxStamina)
                        currentStamina = maxStamina;
                }

                // Yorgunluk Cezası Kontrolü
                if (isExhausted)
                {
                    // İstenen sınıra (%75) geldi mi?
                    if (currentStamina >= maxStamina * exhaustionThreshold)
                    {
                        isExhausted = false; // CEZA BİTTİ, tekrar koşabilir
                    }
                    else
                    {
                        // Hala cezalı, sprint girdisini engelle
                        _input.sprint = false;
                    }
                }
            }

            if (staminaCircularImage != null)
            {
                // Slider.value yerine fillAmount kullanıyoruz (0 ile 1 arası değer alır)
                staminaCircularImage.fillAmount = currentStamina / maxStamina;

                // Yorgunken bar Kırmızı, normalken Beyaz olsun
                staminaCircularImage.color = isExhausted ? Color.red : Color.white;
            }
        }

        // --- [YENİ] SARHOŞLUK HESAPLAMALARI ---
        private void CalculateDrunkEffects()
        {
            // --- DÜZELTME: KESİN SIFIRLAMA ---
            // Eğer sarhoşluk yoksa (veya çok azsa), tüm değerleri anında sıfırla.
            // Lerp kullanmıyoruz, direkt kesiyoruz ki "normal" hissetsin.
            if (drunkIntensity <= 0.01f)
            {
                currentDrunkRoll = 0f;
                currentDrunkYaw = 0f;
                currentDrunkDrift = Vector3.zero;
                drunkTime = 0f; // Zamanlayıcıyı da sıfırla
                return;
            }

            drunkTime += Time.deltaTime;

            // 1. Kamera Sallantısı
            float noiseRoll = (Mathf.PerlinNoise(drunkTime * swaySpeed, 0f) - 0.5f) * 2f;
            float noiseYaw = (Mathf.PerlinNoise(0f, drunkTime * swaySpeed) - 0.5f) * 2f;

            float targetRoll = noiseRoll * swayAmountRoll * drunkIntensity;
            float targetYaw = noiseYaw * swayAmountYaw * drunkIntensity;

            currentDrunkRoll = Mathf.Lerp(currentDrunkRoll, targetRoll, Time.deltaTime * 1.5f);
            currentDrunkYaw = Mathf.Lerp(currentDrunkYaw, targetYaw, Time.deltaTime * 1.5f);

            // 2. Yürüme Kayması
            float driftX = (Mathf.PerlinNoise(drunkTime * driftSpeed, 100f) - 0.5f) * 2f;
            float driftZ = (Mathf.PerlinNoise(drunkTime * driftSpeed, 200f) - 0.5f) * 2f;

            Vector3 targetDrift = new Vector3(driftX, 0, driftZ) * driftForce * drunkIntensity;
            currentDrunkDrift = Vector3.Lerp(currentDrunkDrift, targetDrift, Time.deltaTime * 0.5f);
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // Düşme zaman aşımını sıfırla
                _fallTimeoutDelta = FallTimeout;

                // Animatörü güncelle
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // Yerdeyken hızın sonsuza kadar düşmesini engelle
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // --- DEĞİŞİKLİK BURADA ---
                // Zıplama kodunu tamamen sildik/yorum satırına aldık.
                // Artık Space'e bassan da zıplamayacak.

                /* // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }
                */

                // Zıplama zaman aşımı
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // Havadaysak zıplama zaman aşımını sıfırla
                _jumpTimeoutDelta = JumpTimeout;

                // Düşme zaman aşımı
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // Düşme animasyonu
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // Havadaysak zıplama girdisini iptal et
                _input.jump = false;
            }

            // Yerçekimini uygula (Terminal hıza kadar)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private bool _restrictRotation = false; // Kısıtlı görüş açık mı?
        private float _centerYaw; // Kilitlendiğimizde baktığımız merkez açı
        private float _yawLimit = 50f; // Sağa/Sola kaç derece dönebiliriz?

        public void SetFrozen(
            bool freeze,
            bool lockCameraInput = false,
            bool restrictRotation = false
        )
        {
            _isFrozen = freeze;
            _lockCamera = lockCameraInput;
            _restrictRotation = restrictRotation;

            if (freeze)
            {
                // Inputları sıfırla
                if (_input != null)
                {
                    _input.move = Vector2.zero;
                    _input.sprint = false;
                    _input.jump = false;
                }

                _speed = 0f;
                _animationBlend = 0f;

                // Eğer kısıtlı görüş istiyorsak, şu an nereye bakıyorsak orayı "MERKEZ" kabul et
                if (restrictRotation)
                {
                    _centerYaw = _cinemachineTargetYaw;
                }
            }
        }

        private void HandleHeadBob()
        {
            if (!Grounded)
                return;

            // Hız kontrolü (Yatay hız)
            float speed = new Vector3(_controller.velocity.x, 0, _controller.velocity.z).magnitude;

            // Karakter hareket ediyor mu?
            if (_input.move != Vector2.zero && speed > 0.1f)
            {
                // Koşuyorsak frekansı artır
                // Koşuyorsak frekansı artır
                float freq = _input.sprint ? BobFrequency * 1.3f : BobFrequency;

                // Varsayılan sallantı genliklerini al
                float currentBobY = BobYAmplitude;
                float currentBobX = BobXAmplitude;

                // --- YENİ: YORGUNLUK (EXHAUSTED) ETKİSİ ---
                if (isExhausted)
                {
                    // Yorgunken adımlar daha ağır ve dengesiz olur
                    currentBobY *= 2.0f; // Aşağı/Yukarı sarsıntı 2 katına çıkar (Ağırlaşmış adımlar)
                    currentBobX *= 1.8f; // Sağa/Sola yalpalanma neredeyse 2 kat artar (Denge kaybı)
                    freq *= 0.8f; // Adım atma sıklığı %20 yavaşlar (Bitkinlik)
                }

                _bobTimer += Time.deltaTime * freq;

                // --- DOĞAL YÜRÜME FORMÜLÜ (Lissajous Curve / 8 Çizme) ---

                // 1. Y EKSENİ (Yukarı/Aşağı): Sinüs dalgası (Adım atma)
                float bobYOffset = Mathf.Sin(_bobTimer) * currentBobY;

                // 2. X EKSENİ (Sağa/Sola): Kosinüs dalgası (Ağırlık verme)
                float bobXOffset = Mathf.Cos(_bobTimer / 2f) * currentBobX;

                // Kameranın pozisyonunu hedef pozisyona doğru yumuşakça (Lerp) kaydır
                Vector3 currentPos = CinemachineCameraTarget.transform.localPosition;

                Vector3 targetPos = new Vector3(
                    bobXOffset,
                    _defaultYPos + bobYOffset,
                    currentPos.z
                );

                // Lerp ile geçişi yumuşatıyoruz ki "kütük" gibi titremesin
                CinemachineCameraTarget.transform.localPosition = Vector3.Lerp(
                    currentPos,
                    targetPos,
                    Time.deltaTime * 8f
                );
            }
            else
            {
                // Durduysak zamanlayıcıyı sıfırla (Adım ortasında kalmasın, nötr pozisyona dönsün)
                _bobTimer = 0;

                Vector3 currentPos = CinemachineCameraTarget.transform.localPosition;
                Vector3 targetPos = new Vector3(0f, _defaultYPos, currentPos.z);

                // Yavaşça merkeze dön (Reset)
                if (Vector3.Distance(currentPos, targetPos) > 0.001f)
                {
                    CinemachineCameraTarget.transform.localPosition = Vector3.Lerp(
                        currentPos,
                        targetPos,
                        Time.deltaTime * 4f
                    );
                }
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f)
                lfAngle += 360f;
            if (lfAngle > 360f)
                lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded)
                Gizmos.color = transparentGreen;
            else
                Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(
                    transform.position.x,
                    transform.position.y - GroundedOffset,
                    transform.position.z
                ),
                GroundedRadius
            );
        }

        // --- BU FONKSİYONU EKLE ---
        public void ForceCameraRotation(float yaw, float pitch)
        {
            // Scriptin hafızasındaki açıları, şu anki gerçek açılara eşitliyoruz
            _cinemachineTargetYaw = yaw;
            _cinemachineTargetPitch = pitch;

            // Cinemachine objesini de hemen güncelliyoruz ki "kayma" olmasın
            if (CinemachineCameraTarget != null)
            {
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(pitch, yaw, 0.0f);
            }
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);

                    // YENİ KOD: Artık Mixeri yok sayan PlayClipAtPoint KULLANMIYORUZ
                    if (_audioSource != null)
                    {
                        _audioSource.PlayOneShot(FootstepAudioClips[index], FootstepAudioVolume);
                    }
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                // YENİ KOD
                if (_audioSource != null)
                {
                    _audioSource.PlayOneShot(LandingAudioClip, FootstepAudioVolume);
                }
            }
        }

        // --- DIŞARIDAN STAMINA DOLDURMA (Kitap Okurken vb.) ---
        // --- DIŞARIDAN STAMINA DOLDURMA (Kitap Okurken vb.) ---
        public void ExternalStaminaRegen(float deltaTime)
        {
            // Eğer stamina zaten doluysa işlem yapma
            if (currentStamina >= maxStamina)
                return;

            // Staminayı artır
            currentStamina += staminaRegenRate * deltaTime;
            if (currentStamina > maxStamina)
                currentStamina = maxStamina;

            // Yorgunluk cezasını (Exhausted) kontrol et
            if (isExhausted && currentStamina >= maxStamina * exhaustionThreshold)
            {
                isExhausted = false;
            }

            // UI Güncelle (YUVARLAK BAR İÇİN YENİ KOD)
            if (staminaCircularImage != null)
            {
                staminaCircularImage.fillAmount = currentStamina / maxStamina;
                staminaCircularImage.color = isExhausted ? Color.red : Color.white;
            }
        }
    }
}
