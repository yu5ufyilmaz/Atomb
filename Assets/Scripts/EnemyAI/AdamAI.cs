using System.Collections;
using UnityEngine;

public class AdamAI : MonoBehaviour
{
    public static AdamAI Instance;

    [Header("DEBUG - ANLIK DURUM")]
    [Tooltip("Adam şu an oyuncuyu hangi odada görüyor?")]
    public string currentDetectedRoom = "YOK (Koridor/Boşluk)";

    [Header("Zamanlama Ayarları")]
    public float timeToFirstWarning = 15f;
    public float timeToSecondWarning = 5f;
    public float timeToKill = 3f;

    [Header("Sesler")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip warning1Clip;

    [SerializeField]
    private AudioClip warning2Clip;

    [SerializeField]
    private AudioClip killSound;

    [Header("Jumpscare & Animasyon")]
    public Animator animator; // Animasyon kontrolcüsü

    [SerializeField]
    private GameObject adamModel;

    [SerializeField]
    private float jumpScareDistance = 1.0f;

    private RoomManager playerCurrentRoom;
    private float currentDarknessTimer = 0f;
    private int warningLevel = 0;

    [HideInInspector]
    public string debugStatus;

    [HideInInspector]
    public float debugTimer;

    [HideInInspector]
    public float debugTotalTimeNeeded;
    public JumpscareProfile adamJumpscareProfile; // <-- YENİ

    // Animasyon Hash
    private int _animIDAttack;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        if (adamModel)
            adamModel.SetActive(false);

        _animIDAttack = Animator.StringToHash("Attack");
    }

    private void Start()
    {
        debugTotalTimeNeeded = timeToFirstWarning + timeToSecondWarning + timeToKill;
    }

    private void Update()
    {
        // Debug Güncellemesi
        if (playerCurrentRoom == null)
            currentDetectedRoom = "YOK (Koridor/Boşluk)";
        else
            currentDetectedRoom =
                playerCurrentRoom.roomName
                + (playerCurrentRoom.isCorridor ? " (KORİDOR)" : " (ODA)");

        // 1. TEST MODU KONTROLÜ
        if (GlobalEnemyManager.Instance != null && GlobalEnemyManager.Instance.stopAllEnemies)
        {
            ResetAdam();
            debugStatus = "⛔ TEST MODU";
            return;
        }

        // 2. SALDIRI VARSA BEKLE
        if (GlobalEnemyManager.Instance != null && GlobalEnemyManager.Instance.isAttackInProgress)
        {
            debugStatus = "BEKLEMEDE (Saldırı Var)";
            return;
        }

        // 3. ORTAM ANALİZİ
        int environmentState = AnalyzeEnvironment();

        if (environmentState == 1) // TEHLİKE (Karanlık)
        {
            HandleDarknessProgression();
        }
        else if (environmentState == 0) // GÜVENLİ (Işık Var)
        {
            ResetAdam();
        }
        else if (environmentState == 2) // KORİDOR (Karanlık)
        {
            debugStatus = "KORİDOR (Süreç Durdu)";
            // Sayaç değişmiyor, olduğu yerde kalıyor.
        }

        debugTimer = currentDarknessTimer;
    }

    private int AnalyzeEnvironment()
    {
        // A) Oda Yoksa -> KORİDOR (2)
        if (playerCurrentRoom == null)
            return 2;

        // B) Oda "Is Corridor" ise -> KORİDOR (2)
        if (playerCurrentRoom.isCorridor)
            return 2;

        // C) Şartel Atıksa -> TEHLİKE (1)
        if (BreakerBox.Instance != null && BreakerBox.Instance.IsTripped)
            return 1;

        // D) Işık Varsa -> RESET (0)
        // Eğer şartel yoksa veya açıksa ve odada ışık varsa
        if (playerCurrentRoom.GetActiveLightCount() > 0)
            return 0;

        // E) Işık Yoksa -> TEHLİKE (1)
        return 1;
    }

    private void HandleDarknessProgression()
    {
        currentDarknessTimer += Time.deltaTime;

        if (warningLevel == 0 && currentDarknessTimer >= timeToFirstWarning)
        {
            warningLevel = 1;
            PlaySound(warning1Clip);
            debugStatus = "UYARI 1";
        }
        else if (
            warningLevel == 1
            && currentDarknessTimer >= (timeToFirstWarning + timeToSecondWarning)
        )
        {
            warningLevel = 2;
            PlaySound(warning2Clip);
            debugStatus = "UYARI 2";
        }
        else if (
            warningLevel == 2
            && currentDarknessTimer >= (timeToFirstWarning + timeToSecondWarning + timeToKill)
        )
        {
            KillPlayer();
        }
        else if (warningLevel == 0)
        {
            debugStatus = "Karanlıkta... (Sayaç İşliyor)";
        }
    }

    private void ResetAdam()
    {
        currentDarknessTimer = 0f;
        warningLevel = 0;
        debugStatus = "Güvenli / Reset";
    }

    public void KillPlayer()
    {
        if (GlobalEnemyManager.Instance != null && GlobalEnemyManager.Instance.stopAllEnemies)
            return;

        debugStatus = "ÖLDÜRÜYOR!";
        if (GlobalEnemyManager.Instance)
            GlobalEnemyManager.Instance.RegisterAttackStart();

        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        if (player != null)
        {
            Vector3 behindPos = player.position - (player.forward * jumpScareDistance);
            transform.position = new Vector3(behindPos.x, player.position.y, behindPos.z);
            transform.LookAt(player.position);

            if (adamModel)
            {
                adamModel.SetActive(true);
                // Animasyon Tetiklemesi
                if (animator != null)
                {
                    animator.SetTrigger(_animIDAttack);
                }
            }
        }

        if (audioSource)
            audioSource.PlayOneShot(killSound);
        if (JumpscareManager.Instance != null)
            JumpscareManager.Instance.StartJumpscare(transform, adamJumpscareProfile, true);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource && clip)
            audioSource.PlayOneShot(clip);
    }

    public void SetCurrentRoom(RoomManager room)
    {
        playerCurrentRoom = room;
    }

    public void ClearRoom(RoomManager room)
    {
        // Eğer çıktığımız oda, şu an kayıtlı olan odaysa kaydı sil
        if (playerCurrentRoom == room)
            playerCurrentRoom = null;
    }
}
