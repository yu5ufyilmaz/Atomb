using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoomManager : MonoBehaviour
{
    public string roomName = "Oda İsmi";

    [Tooltip("Eğer bu kutuyu işaretlersen, burası KORİDOR olur ve Adam sayacı durur.")]
    public bool isCorridor = false;

    [Header("🔊 Olay Sesleri (Otomatik Tanıtım)")]
    [Tooltip("Oyuncu bu odaya İLK defa girdiğinde çalacak ses ve altyazı.")]
    public DialogueEvent onFirstEnterSound;

    // Odaya daha önce girildi mi kontrolü (Gizli değişken)
    private bool hasEnteredBefore = false;

    [Header("💡 Elektrik & Işık Sistemi")]
    public List<ControllableLight> roomLights = new List<ControllableLight>();

    [Header("Lees Ayarları (Spawn)")]
    public bool isDangerous = false;
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Guderian Ayarları")]
    public bool canGuderianSpawn = false;
    public InteractableDoor roomDoor;
    public Transform doorOutsidePoint;
    public Transform doorInsidePoint;
    public List<InteractableHidingSpot> hidingSpots = new List<InteractableHidingSpot>();
    public List<Transform> guderianPatrolPoints = new List<Transform>();

    [Header("Guderian Pusu (Ceza) Ayarları")]
    public bool allowAmbush = true;
    public float ambushTimeout = 15.0f;

    [Tooltip("Guderian pusu kurduğunda tam olarak nerede dursun?")]
    public Transform ambushSpawnPoint;

    // --- Private Takip Değişkenleri ---
    private float currentOpenTimer = 0f;
    private bool isPlayerInside = false;

    private void Awake()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null && !box.isTrigger)
            box.isTrigger = true;
    }

    private void Update()
    {
        // Koridorsa veya Guderian yoksa veya pusu kapalıysa işlem yapma
        if (isCorridor || !canGuderianSpawn || roomDoor == null || !allowAmbush)
            return;

        // Kural: Oyuncu odada DEĞİL + Kapı AÇIK + Guderian BOŞTA (Hidden)
        if (
            !isPlayerInside
            && roomDoor.isOpen
            && GuderianAI.Instance != null
            && GuderianAI.Instance.currentState == GuderianAI.GuderianState.Hidden
        )
        {
            currentOpenTimer += Time.deltaTime;

            if (currentOpenTimer >= ambushTimeout)
            {
                currentOpenTimer = 0f;
                GuderianAI.Instance.SetupAmbush(this);
            }
        }
        else
        {
            currentOpenTimer = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;

            // =========================================================
            // 🔊 YENİ SİSTEM: İLK GİRİŞ SESİNİ ÇAL
            // =========================================================
            // Eğer daha önce girilmediyse VE ses/yazı atanmışsa çal
            if (!hasEnteredBefore)
            {
                if (
                    onFirstEnterSound != null
                    && (
                        onFirstEnterSound.clip != null
                        || !string.IsNullOrEmpty(onFirstEnterSound.subtitleID)
                    )
                )
                {
                    Debug.Log($"📢 {roomName}: İlk giriş yapıldı, tanıtım çalınıyor.");

                    // MegaphoneSystem'e paketi gönderip çaldırıyoruz
                    onFirstEnterSound.Play();

                    hasEnteredBefore = true; // KİLİTLE: Bir daha çalmasın
                }
            }

            // Megafon "Idle" sayacını sıfırla çünkü oyuncu yeni bir yere girdi, aktif sayılır.
            if (MegaphoneSystem.Instance != null)
                MegaphoneSystem.Instance.ResetIdleTimer();
            // =========================================================

            // --- AI Logic (Pusu ve Takip) ---
            if (GuderianAI.Instance && GuderianAI.Instance.IsCampingPlayerInRoom(this))
            {
                GuderianAI.Instance.TriggerAmbushExecute();
            }

            if (AdamAI.Instance)
                AdamAI.Instance.SetCurrentRoom(this);
            if (LeesEnemyAI.Instance)
                LeesEnemyAI.Instance.EnterRoom(this);
            if (GuderianAI.Instance)
                GuderianAI.Instance.SetCurrentRoom(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            currentOpenTimer = 0f;

            if (AdamAI.Instance)
                AdamAI.Instance.ClearRoom(this);
            if (LeesEnemyAI.Instance)
                LeesEnemyAI.Instance.ExitRoom(this);
            if (GuderianAI.Instance)
                GuderianAI.Instance.ClearRoom(this);
        }
    }

    public int GetActiveLightCount()
    {
        int count = 0;
        foreach (var light in roomLights)
            if (light != null && light.IsOn)
                count++;
        return count;
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = isCorridor
                ? new Color(0, 1, 1, 0.2f)
                : new Color(1f, 0.92f, 0.016f, 0.1f);
            Gizmos.DrawCube(box.center, box.size);
        }

        // Pusu noktasını sahnede kırmızı bir küre olarak göster
        if (ambushSpawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(ambushSpawnPoint.position, 0.5f);
            Gizmos.DrawLine(
                ambushSpawnPoint.position,
                ambushSpawnPoint.position + ambushSpawnPoint.forward * 1f
            );
        }
    }
}
