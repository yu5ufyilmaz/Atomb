using UnityEngine;

// Sadece Editörde ve Geliştirici Sürümünde derlenir.
// Nihai oyunda performansı etkilemez.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

public class DebugMenuManager : MonoBehaviour
{
    public static DebugMenuManager Instance;

    private bool isMenuOpen = false;
    private int currentTab = 0;
    private Vector2 enemyScrollPos = Vector2.zero;

    // Sekme İsimlerimiz
    private readonly string[] tabs = { "Game & World", "Enemy", "Player", "Puzzles" };

    // Menü Boyutları
    private Rect windowRect = new Rect(20, 20, 450, 350);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Eski Input sistemi ile Alt + Q kontrolü
        if (
            (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            && Input.GetKeyDown(KeyCode.Q)
        )
        {
            ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        // Menü açıldığında fare imlecini görünür yapmamız lazım ki butonlara tıklayabilelim.
        // GameManager'daki UpdateCursorState() yapısını bozmamak için şimdilik manuel açıyoruz.
        if (isMenuOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Menü kapandığında oyunun kendi imleç sistemine geri devrediyoruz.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UpdateCursorState();
            }
        }
    }

    private void OnGUI()
    {
        if (!isMenuOpen)
            return;

        // Karanlık bir arka plan stili ayarlayalım
        GUI.backgroundColor = Color.black;

        // Pencereyi çiz
        windowRect = GUILayout.Window(999, windowRect, DrawDebugWindow, "Senzora Developer Menu");
    }

    private void DrawDebugWindow(int windowID)
    {
        // 1. Sekmeleri (Tab) Çiz
        currentTab = GUILayout.Toolbar(currentTab, tabs);
        GUILayout.Space(10);

        // 2. Seçili sekmeye göre içerik çiz
        switch (currentTab)
        {
            case 0:
                DrawGameTab();
                break;
            case 1:
                DrawEnemyTab();
                break;
            case 2:
                DrawPlayerTab();
                break;
            case 3:
                DrawPuzzlesTab();
                break;
        }

        // Pencerenin üst kısmından sürüklenip taşınabilmesini sağlar
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // --- SEKME İÇERİKLERİ ---

    private void DrawGameTab()
    {
        GUILayout.Label("World & Environment Controls", GUI.skin.box);

        // --- BASINÇ SİSTEMİ ---
        if (PressureSystemManager.Instance != null)
        {
            GUILayout.Label($"Core Pressure: %{PressureSystemManager.Instance.currentPressure:F0}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-20% Pressure"))
                PressureSystemManager.Instance.currentPressure = Mathf.Max(
                    0,
                    PressureSystemManager.Instance.currentPressure - 20f
                );

            if (GUILayout.Button("+20% Pressure"))
                PressureSystemManager.Instance.currentPressure = Mathf.Min(
                    100,
                    PressureSystemManager.Instance.currentPressure + 20f
                );
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // --- ŞARTEL SİSTEMİ ---
        if (BreakerBox.Instance != null)
        {
            GUILayout.Label(
                $"Breaker Status: {(BreakerBox.Instance.IsTripped ? "TRIPPED (ATIK)" : "STABLE")}"
            );

            if (BreakerBox.Instance.IsTripped)
            {
                if (GUILayout.Button("Reset Breaker (Şarteli Kaldır)"))
                    BreakerBox.Instance.Interact();
            }
            else
            {
                if (GUILayout.Button("Force Trip Breaker (Şarteli Attır)"))
                    BreakerBox.Instance.ForceTrip(); // <-- YENİ EKLEDİĞİMİZ METOT
            }
        }
    }

    private void DrawPlayerTab()
    {
        GUILayout.Label("Player Controls", GUI.skin.box);
        GUILayout.Label("(Henüz bir kontrol eklenmedi. İhtiyaç oldukça dolduracağız.)");
    }

    private void DrawPuzzlesTab()
    {
        GUILayout.Label("Puzzle & Password Controls", GUI.skin.box);

        // --- ŞİFRE BİLGİLERİ ---
        if (PasswordManager.Instance != null)
        {
            GUILayout.Label("Bu Oturumdaki Tüm Şifreler:", GUI.skin.label);

            // Public yaptığımız listeyi dönüp ekrana yazdırıyoruz
            foreach (var pair in PasswordManager.Instance.currentSessionPasswords)
            {
                GUILayout.Label($"[{pair.objectName}] -> {pair.password}");
            }
        }
        else
        {
            GUILayout.Label("PasswordManager bulunamadı!");
        }

        GUILayout.Space(15);

        // --- OYUN AKIŞI KONTROLLERİ ---
        GUILayout.Label("Oyun Akışı (Tehlikeli Kontroller)", GUI.skin.box);
        if (GUILayout.Button("Trigger End Game (Finali Başlat)"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerFinalEnding(); // <-- PUBLIC YAPTIĞIMIZ METOT
            }
        }
    }

    private void DrawEnemyTab()
    {
        // Kaydırma çubuğu başlat
        enemyScrollPos = GUILayout.BeginScrollView(enemyScrollPos);

        GUILayout.Label("Global Enemy Settings", GUI.skin.box);
        if (GlobalEnemyManager.Instance != null)
        {
            var em = GlobalEnemyManager.Instance;
            em.stopAllEnemies = GUILayout.Toggle(
                em.stopAllEnemies,
                " Safe Mode (Düşmanları Tamamen Durdur)"
            );

            GUILayout.Space(5);
            GUILayout.Label($"Saldırı Durumu: {(em.isAttackInProgress ? "AKTİF" : "Sakin")}");
            GUILayout.Label($"Huzur Süresi (Cooldown): {em.currentGlobalCooldown:F1}s");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-10s"))
                em.currentGlobalCooldown = Mathf.Max(0, em.currentGlobalCooldown - 10f);
            if (GUILayout.Button("+10s"))
                em.currentGlobalCooldown += 10f;
            if (GUILayout.Button("Sıfırla"))
                em.currentGlobalCooldown = 0f;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Saldırıyı Zorla Başlat"))
                em.RegisterAttackStart();
            if (GUILayout.Button("Saldırıyı Zorla Bitir"))
                em.RegisterAttackEnd();
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(15);

        // --- ADAM KONTROLLERİ ---
        GUILayout.Label("Adam AI", GUI.skin.box);
        if (AdamAI.Instance != null)
        {
            GUILayout.Label($"Oda: {AdamAI.Instance.currentDetectedRoom}");
            GUILayout.Label($"Durum: {AdamAI.Instance.debugStatus}");
            GUILayout.Label(
                $"Karanlık Sayacı: {AdamAI.Instance.debugTimer:F1}s / {AdamAI.Instance.debugTotalTimeNeeded}s"
            );

            if (GUILayout.Button("Force Kill (Adam Jumpscare)"))
            {
                AdamAI.Instance.KillPlayer();
            }
        }
        else
            GUILayout.Label("AdamAI bulunamadı.");

        GUILayout.Space(15);

        // --- GUDERIAN KONTROLLERİ ---
        GUILayout.Label("Guderian AI", GUI.skin.box);
        if (GuderianAI.Instance != null)
        {
            GUILayout.Label($"State: {GuderianAI.Instance.currentState}");
            GUILayout.Label($"Durum: {GuderianAI.Instance.debugStatus}");
            GUILayout.Label($"Spawn Şansı: %{GuderianAI.Instance.GetCurrentChance():F1}");
            GUILayout.Label(
                $"Sonraki Spawn Kontrolü: {GuderianAI.Instance.GetTimeUntilNextSpawnCheck():F1}s"
            );

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Zorla Jumpscare"))
                GuderianAI.Instance.TriggerJumpscare();
            if (GUILayout.Button("Zorla Gönder (Force Leave)"))
                GuderianAI.Instance.ForceLeave();
            GUILayout.EndHorizontal();
        }
        else
            GUILayout.Label("GuderianAI bulunamadı.");

        GUILayout.Space(15);

        // --- LEES KONTROLLERİ ---
        GUILayout.Label("Lees AI", GUI.skin.box);
        if (LeesEnemyAI.Instance != null)
        {
            GUILayout.Label($"State: {LeesEnemyAI.Instance.currentState}");
            GUILayout.Label($"Spawn Şansı: %{LeesEnemyAI.Instance.GetCurrentSpawnChance():F1}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Zorla Spawn Et"))
                LeesEnemyAI.Instance.SpawnLeesInRoom();
            if (GUILayout.Button("Zorla Gönder"))
                LeesEnemyAI.Instance.DespawnLees();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Force Kill (Lees Jumpscare)"))
            {
                LeesEnemyAI.Instance.TriggerDeath("Debug Menüden Tetiklendi", false);
            }
        }
        else
            GUILayout.Label("LeesEnemyAI bulunamadı.");

        GUILayout.EndScrollView();
    }
}
#endif
