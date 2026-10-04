using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StarterAssets;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(-10000)]
public sealed class MechanicsTestController : MonoBehaviour
{
    private enum PanelTab
    {
        Genel,
        Etkilesimler,
        Sistemler,
        Eventler,
    }

    private sealed class MechanicEntry
    {
        public MonoBehaviour Behaviour;
        public IInteractable Interactable;
        public string Group;
        public string HierarchyPath;
        public string ReloadPath;
        public string TypeName;
        public int ComponentIndex;
    }

    private sealed class ActionEntry
    {
        public MonoBehaviour Behaviour;
        public IAction Action;
        public string HierarchyPath;
    }

    private sealed class ConditionEntry
    {
        public MonoBehaviour Behaviour;
        public ICondition Condition;
        public string HierarchyPath;
    }

    [Header("Panel")]
    [SerializeField]
    private KeyCode panelKey = KeyCode.F1;

    [SerializeField]
    private bool openPanelOnStart = true;

    [SerializeField]
    private float teleportDistance = 1.8f;

    [Header("Güvenli Başlangıç")]
    [SerializeField]
    private bool disableStoryEvents = true;

    [SerializeField]
    private bool disableEnemies = true;

    [SerializeField]
    private bool disablePressureAndBreaker = true;

    private static string pendingReloadPath;
    private static string pendingTypeName;
    private static int pendingComponentIndex;
    private static bool pendingFocus;

    private readonly List<MechanicEntry> mechanics = new List<MechanicEntry>();
    private readonly List<ActionEntry> actions = new List<ActionEntry>();
    private readonly List<ConditionEntry> conditions = new List<ConditionEntry>();
    private readonly List<PuzzleReceiver> puzzleReceivers = new List<PuzzleReceiver>();
    private readonly List<EventLogicController> eventControllers =
        new List<EventLogicController>();
    private readonly List<WeightedRandomTrigger> randomTriggers =
        new List<WeightedRandomTrigger>();
    private readonly List<PlayableDirector> directors = new List<PlayableDirector>();

    private GameManager gameManager;
    private GlobalEnemyManager enemyManager;
    private PressureSystemManager pressureManager;
    private BreakerBox breakerBox;
    private PasswordManager passwordManager;
    private PuzzleInventoryManager inventoryManager;
    private MegaphoneSystem megaphoneSystem;
    private StarterAssets.CharacterController playerController;
    private PlayerInteraction playerInteraction;
    private DynomaFlashLight flashlight;
    private NotebookUI notebook;
    private PauseManager pauseManager;
    private DeathUIManager deathUIManager;

    private PanelTab currentTab;
    private Rect windowRect;
    private Vector2 mainScroll;
    private Vector2 interactionScroll;
    private Vector2 systemScroll;
    private Vector2 eventScroll;
    private string interactionSearch = string.Empty;
    private string eventSearch = string.Empty;
    private bool panelOpen;
    private string statusMessage = "Test sahnesi hazır.";
    private float previousTimeScale = 1f;

    public static bool IsActive { get; private set; }

    private void Awake()
    {
        IsActive = true;
        previousTimeScale = Time.timeScale;
        panelOpen = openPanelOnStart;
        windowRect = new Rect(20f, 20f, 1100f, 720f);

        ApplySafeBaseline();
    }

    private IEnumerator Start()
    {
        // Diğer Awake/Start metotları referanslarını kurduktan sonra envanteri çıkar.
        yield return null;

        SetupGameplayPresentation();
        CacheSceneContent();
        PreparePlayer();
        ApplyPanelState();

        if (pendingFocus)
        {
            yield return null;
            FocusPendingMechanic();
        }
    }

    private void OnDestroy()
    {
        IsActive = false;
        Time.timeScale = previousTimeScale;
    }

    private void Update()
    {
        if (Input.GetKeyDown(panelKey))
        {
            SetPanelOpen(!panelOpen);
        }
    }

    private void OnGUI()
    {
        if (!panelOpen)
        {
            GUI.Box(new Rect(12f, 12f, 245f, 32f), $"{panelKey}: Mekanik Test Paneli");
            return;
        }

        windowRect.width = Mathf.Min(1100f, Screen.width - 20f);
        windowRect.height = Mathf.Min(720f, Screen.height - 20f);
        windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
        windowRect.y = Mathf.Clamp(
            windowRect.y,
            0f,
            Mathf.Max(0f, Screen.height - windowRect.height)
        );

        GUI.backgroundColor = new Color(0.22f, 0.25f, 0.3f, 1f);
        windowRect = GUILayout.Window(
            64201,
            windowRect,
            DrawWindow,
            "ATOMB · BAĞIMSIZ MEKANİK TEST SAHNESİ"
        );
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        currentTab = (PanelTab)
            GUILayout.Toolbar(
                (int)currentTab,
                new[] { "Genel", "Etkileşimler", "Sistemler", "Event / Puzzle" },
                GUILayout.Height(28f)
            );

        GUI.backgroundColor = new Color(0.75f, 0.25f, 0.2f);
        if (GUILayout.Button("SAHNEYİ SIFIRLA", GUILayout.Width(150f), GUILayout.Height(28f)))
            ReloadScene();

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Kapat (F1)", GUILayout.Width(105f), GUILayout.Height(28f)))
            SetPanelOpen(false);
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label(statusMessage, GUI.skin.box, GUILayout.Height(24f));

        switch (currentTab)
        {
            case PanelTab.Genel:
                DrawOverviewTab();
                break;
            case PanelTab.Etkilesimler:
                DrawInteractionsTab();
                break;
            case PanelTab.Sistemler:
                DrawSystemsTab();
                break;
            case PanelTab.Eventler:
                DrawEventsTab();
                break;
        }

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
    }

    private void DrawOverviewTab()
    {
        mainScroll = GUILayout.BeginScrollView(mainScroll);

        GUILayout.Label("Nasıl kullanılır?", GUI.skin.box);
        GUILayout.Label(
            "• Git: oyuncuyu hedefin yakınına taşır ve normal oyun etkileşimiyle test etmenizi sağlar.\n"
                + "• Çalıştır: mekaniğin Interact metodunu doğrudan çağırır.\n"
                + "• Temiz Test: sahneyi sıfırlar, diğer akışları kapalı tutar ve yalnızca seçtiğiniz hedefe gider.\n"
                + "• F1: paneli açar/kapatır. Testler ana kayıt dosyasını kullanmaz.",
            GUILayout.MinHeight(82f)
        );

        GUILayout.Space(8f);
        GUILayout.Label("Güvenli sandbox başlangıcı", GUI.skin.box);
        DrawStateLine("Kayıt yazımı", "KAPALI", true);
        DrawStateLine("Tutorial / intro", "KAPALI", true);
        DrawStateLine("Otomatik event akışı", disableStoryEvents ? "KAPALI" : "AÇIK", disableStoryEvents);
        DrawStateLine(
            "Düşman saldırıları",
            enemyManager != null && enemyManager.stopAllEnemies ? "KAPALI" : "AÇIK",
            enemyManager != null && enemyManager.stopAllEnemies
        );
        DrawStateLine(
            "Basınç",
            pressureManager != null && pressureManager.isSystemActive ? "AÇIK" : "KAPALI",
            pressureManager == null || !pressureManager.isSystemActive
        );
        DrawStateLine(
            "Şartel riski",
            breakerBox != null && breakerBox.isSystemActive ? "AÇIK" : "KAPALI",
            breakerBox == null || !breakerBox.isSystemActive
        );

        GUILayout.Space(8f);
        GUILayout.Label("Sahne envanteri", GUI.skin.box);
        GUILayout.Label($"Etkileşim: {mechanics.Count}");
        GUILayout.Label($"Event action: {actions.Count}");
        GUILayout.Label($"Event condition: {conditions.Count}");
        GUILayout.Label($"Puzzle receiver: {puzzleReceivers.Count}");
        GUILayout.Label($"Event controller: {eventControllers.Count}");
        GUILayout.Label($"Weighted random: {randomTriggers.Count}");
        GUILayout.Label($"Timeline: {directors.Count}");

        GUILayout.Space(10f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Envanteri Yenile", GUILayout.Height(32f)))
        {
            CacheSceneContent();
            statusMessage = "Sahne envanteri yenilendi.";
        }

        if (GUILayout.Button("Güvenli Başlangıcı Tekrar Uygula", GUILayout.Height(32f)))
        {
            ApplySafeBaseline();
            PreparePlayer();
            statusMessage = "Otomatik akışlar ve tehlikeler tekrar kapatıldı.";
        }
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
    }

    private static void DrawStateLine(string label, string value, bool safe)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(240f));
        Color previous = GUI.color;
        GUI.color = safe ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.72f, 0.3f);
        GUILayout.Label(value, GUILayout.Width(100f));
        GUI.color = previous;
        GUILayout.EndHorizontal();
    }

    private void DrawInteractionsTab()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ara", GUILayout.Width(30f));
        interactionSearch = GUILayout.TextField(interactionSearch ?? string.Empty);
        if (GUILayout.Button("Temizle", GUILayout.Width(75f)))
            interactionSearch = string.Empty;
        GUILayout.EndHorizontal();

        interactionScroll = GUILayout.BeginScrollView(interactionScroll);

        string lastGroup = null;
        foreach (MechanicEntry entry in mechanics)
        {
            if (!MatchesSearch(
                    interactionSearch,
                    entry.Behaviour.name,
                    entry.Behaviour.GetType().Name,
                    entry.Group,
                    entry.HierarchyPath
                ))
                continue;

            if (!string.Equals(lastGroup, entry.Group, StringComparison.Ordinal))
            {
                GUILayout.Space(5f);
                GUILayout.Label(entry.Group, GUI.skin.box);
                lastGroup = entry.Group;
            }

            DrawMechanicRow(entry);
        }

        GUILayout.EndScrollView();
    }

    private void DrawMechanicRow(MechanicEntry entry)
    {
        if (entry.Behaviour == null)
            return;

        GUILayout.BeginHorizontal(GUI.skin.box);

        string activeMark = entry.Behaviour.gameObject.activeInHierarchy ? "●" : "○";
        GUILayout.Label(
            $"{activeMark} {entry.Behaviour.name}\n{entry.Behaviour.GetType().Name}",
            GUILayout.MinWidth(280f),
            GUILayout.MaxWidth(420f)
        );

        string prompt = SafePrompt(entry.Interactable);
        GUILayout.Label(prompt, GUILayout.MinWidth(180f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Git", GUILayout.Width(62f), GUILayout.Height(36f)))
        {
            EnsureActiveHierarchy(entry.Behaviour.transform);
            FocusTransform(entry.Behaviour.transform, true);
        }

        if (GUILayout.Button("Çalıştır", GUILayout.Width(76f), GUILayout.Height(36f)))
            InvokeInteractable(entry);

        IForceExitable forceExitable = entry.Behaviour as IForceExitable;
        GUI.enabled = forceExitable != null;
        if (GUILayout.Button("Çık", GUILayout.Width(54f), GUILayout.Height(36f)))
            SafeInvoke(() => forceExitable.ForceExit(), $"{entry.Behaviour.name}: çıkış çağrıldı.");
        GUI.enabled = true;

        if (GUILayout.Button("Temiz Test", GUILayout.Width(86f), GUILayout.Height(36f)))
            ReloadForMechanic(entry);

        GUILayout.EndHorizontal();
    }

    private void DrawSystemsTab()
    {
        systemScroll = GUILayout.BeginScrollView(systemScroll);

        GUILayout.Label("Oyuncu / zaman", GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Time Scale: {Time.timeScale:F2}", GUILayout.Width(130f));
        float newTimeScale = GUILayout.HorizontalSlider(Time.timeScale, 0f, 2f);
        if (!Mathf.Approximately(newTimeScale, Time.timeScale))
            Time.timeScale = newTimeScale;
        if (GUILayout.Button("1x", GUILayout.Width(48f)))
            Time.timeScale = 1f;
        GUILayout.EndHorizontal();

        if (playerController != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Sarhoşluk: {playerController.drunkIntensity:F2}", GUILayout.Width(130f));
            playerController.drunkIntensity = GUILayout.HorizontalSlider(
                playerController.drunkIntensity,
                0f,
                1f
            );
            if (GUILayout.Button("Hareket Aç", GUILayout.Width(95f)))
                playerController.SetFrozen(false, false, false);
            if (GUILayout.Button("Dondur", GUILayout.Width(70f)))
                playerController.SetFrozen(true, false, false);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Stamina: {playerController.CurrentStamina:F0}/{playerController.maxStamina:F0}"
                    + (playerController.IsExhausted ? " · YORGUN" : string.Empty),
                GUILayout.Width(210f)
            );
            float stamina = GUILayout.HorizontalSlider(
                playerController.CurrentStamina,
                0f,
                playerController.maxStamina
            );
            if (!Mathf.Approximately(stamina, playerController.CurrentStamina))
                playerController.SetStaminaForDebug(stamina);
            if (GUILayout.Button("Oyuncuyu Resetle", GUILayout.Width(130f)))
                playerController.ResetRuntimeStateForDebug();
            GUILayout.EndHorizontal();
        }

        if (flashlight != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Fener: {flashlight.CurrentEnergy:F0}/{flashlight.MaxEnergy:F0}",
                GUILayout.Width(130f)
            );
            float energy = GUILayout.HorizontalSlider(
                flashlight.CurrentEnergy,
                0f,
                flashlight.MaxEnergy
            );
            if (!Mathf.Approximately(energy, flashlight.CurrentEnergy))
                flashlight.SetEnergyForDebug(energy);
            if (GUILayout.Button("Bir kez şarj et", GUILayout.Width(110f)))
                flashlight.ChargeOnceForDebug();
            if (GUILayout.Button("Boşalt", GUILayout.Width(65f)))
                flashlight.SetEnergyForDebug(0f);
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        if (notebook != null)
        {
            if (GUILayout.Button(notebook.isNotebookOpen ? "Defteri kapat" : "Defteri aç"))
            {
                if (notebook.isNotebookOpen)
                    notebook.ForceClose();
                else
                    notebook.ToggleNotebook();
            }
            if (GUILayout.Button("Şifre sekmesi"))
                notebook.OpenNotebookToCategory(0);
            if (GUILayout.Button("Araştırma sekmesi"))
                notebook.OpenNotebookToCategory(1);
            if (GUILayout.Button("Log sekmesi"))
                notebook.OpenNotebookToCategory(2);
        }
        if (pauseManager != null && GUILayout.Button("Pause menüsü"))
            pauseManager.PauseGame();
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("Basınç sistemi", GUI.skin.box);
        if (pressureManager == null)
        {
            GUILayout.Label("PressureSystemManager bulunamadı.");
        }
        else
        {
            pressureManager.isSystemActive = GUILayout.Toggle(
                pressureManager.isSystemActive,
                "Otomatik basınç artışı"
            );
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Basınç: %{pressureManager.currentPressure:F0}", GUILayout.Width(130f));
            pressureManager.currentPressure = GUILayout.HorizontalSlider(
                pressureManager.currentPressure,
                0f,
                99f
            );
            foreach (float value in new[] { 0f, 50f, 90f, 99f })
            {
                if (GUILayout.Button($"%{value:F0}", GUILayout.Width(52f)))
                    pressureManager.ChangePressure(value);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8f);
        GUILayout.Label("Elektrik / şartel", GUI.skin.box);
        if (breakerBox == null)
        {
            GUILayout.Label("BreakerBox bulunamadı.");
        }
        else
        {
            breakerBox.isSystemActive = GUILayout.Toggle(
                breakerBox.isSystemActive,
                "Otomatik risk kontrolü"
            );
            GUILayout.Label(
                $"Durum: {(breakerBox.IsTripped ? "ATIK" : "STABİL")} · "
                    + $"Işık: {breakerBox.GetActiveLightCountPublic()}/{breakerBox.GetTotalLightCount()} · "
                    + $"Risk: %{breakerBox.GetCurrentRiskPercentage() * 100f:F0}"
            );
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Şarteli Attır"))
                breakerBox.ForceTrip();
            GUI.enabled = breakerBox.IsTripped;
            if (GUILayout.Button("Şarteli Kaldır"))
                breakerBox.Interact();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8f);
        GUILayout.Label("Düşmanlar", GUI.skin.box);
        if (enemyManager != null)
        {
            enemyManager.stopAllEnemies = GUILayout.Toggle(
                enemyManager.stopAllEnemies,
                "Safe Mode · tüm otomatik saldırıları durdur"
            );
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Global saldırı başlat"))
                enemyManager.RegisterAttackStart();
            if (GUILayout.Button("Global saldırıyı bitir"))
                enemyManager.RegisterAttackEnd();
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        if (GuderianAI.Instance != null)
        {
            if (GUILayout.Button("Guderian jumpscare"))
                GuderianAI.Instance.TriggerJumpscare();
            if (GUILayout.Button("Guderian gönder"))
                GuderianAI.Instance.ForceLeave();
        }
        if (LeesEnemyAI.Instance != null)
        {
            if (GUILayout.Button("Lees güvenli spawn"))
            {
                LeesEnemyAI.Instance.isSafeSpawn = true;
                LeesEnemyAI.Instance.SpawnLeesInRoom();
            }
            if (GUILayout.Button("Lees gönder"))
                LeesEnemyAI.Instance.DespawnLees();
        }
        if (AdamAI.Instance != null && GUILayout.Button("Adam jumpscare"))
            AdamAI.Instance.KillPlayer();
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("Şifre / puzzle envanteri", GUI.skin.box);
        if (passwordManager != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Bulunan: {passwordManager.GetFoundCount()} · "
                    + $"Doğrulanan: {passwordManager.GetValidatedPasswordCount()}/"
                    + $"{passwordManager.GetTotalRequiredCount()}"
            );
            if (GUILayout.Button("Yeni test şifreleri üret", GUILayout.Width(190f)))
                passwordManager.InitializeNewGame();
            GUILayout.EndHorizontal();
        }

        if (inventoryManager != null && inventoryManager.allItemsDatabase != null)
        {
            GUILayout.Label("Puzzle item seç:");
            GUILayout.BeginHorizontal();
            foreach (PuzzleItemSO item in inventoryManager.allItemsDatabase.Where(item => item != null))
            {
                if (GUILayout.Button(item.name, GUILayout.MaxWidth(180f)))
                    inventoryManager.PickupItem(item);
            }
            if (inventoryManager.HasActiveItem() && GUILayout.Button("Seçimi bırak"))
                inventoryManager.RemoveActiveItem();
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8f);
        GUILayout.Label("Anonslar / final", GUI.skin.box);
        if (megaphoneSystem != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Başlangıç"))
                megaphoneSystem.TriggerGameStartAudio();
            if (GUILayout.Button("Not defteri"))
                megaphoneSystem.OnNotepadPickedUp();
            if (GUILayout.Button("İlk hata"))
                megaphoneSystem.OnFirstMistake();
            if (GUILayout.Button("Tutorial bitti"))
                megaphoneSystem.OnTutorialSolved();
            if (GUILayout.Button("Basınç uyarısı"))
                megaphoneSystem.OnPressureThresholdExceeded();
            if (GUILayout.Button("Şartel uyarısı"))
                megaphoneSystem.OnBreakerTripped();
            GUILayout.EndHorizontal();
        }
        if (gameManager != null && GUILayout.Button("Final akışını test et"))
            gameManager.TriggerFinalEnding();
        if (deathUIManager != null && GUILayout.Button("Ölüm UI'sini test et"))
            deathUIManager.ShowDeathScreen("Mekanik test sahnesi");

        GUILayout.EndScrollView();
    }

    private void DrawEventsTab()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ara", GUILayout.Width(30f));
        eventSearch = GUILayout.TextField(eventSearch ?? string.Empty);
        if (GUILayout.Button("Temizle", GUILayout.Width(75f)))
            eventSearch = string.Empty;
        GUILayout.EndHorizontal();

        eventScroll = GUILayout.BeginScrollView(eventScroll);

        GUILayout.Label("Event controller'lar (başlangıçta kapalı)", GUI.skin.box);
        foreach (EventLogicController controller in eventControllers)
        {
            if (controller == null || !MatchesSearch(eventSearch, controller.name, "EventLogicController"))
                continue;
            GUILayout.BeginHorizontal();
            bool enabled = GUILayout.Toggle(controller.enabled, string.Empty, GUILayout.Width(22f));
            if (enabled != controller.enabled)
                controller.enabled = enabled;
            GUILayout.Label(controller.name);
            GUILayout.Label(controller.HasTriggered ? "Tetiklendi" : "Bekliyor", GUILayout.Width(90f));
            if (GUILayout.Button("Zorla", GUILayout.Width(62f)))
                controller.DebugTrigger();
            if (GUILayout.Button("Reset", GUILayout.Width(62f)))
                controller.DebugReset();
            if (GUILayout.Button("Git", GUILayout.Width(52f)))
                FocusTransform(controller.transform, true);
            GUILayout.EndHorizontal();
        }

        foreach (WeightedRandomTrigger trigger in randomTriggers)
        {
            if (trigger == null || !MatchesSearch(eventSearch, trigger.name, "WeightedRandomTrigger"))
                continue;
            GUILayout.BeginHorizontal();
            bool enabled = GUILayout.Toggle(trigger.enabled, string.Empty, GUILayout.Width(22f));
            if (enabled != trigger.enabled)
                trigger.enabled = enabled;
            GUILayout.Label($"{trigger.name} · Weighted Random");
            GUILayout.Label(trigger.HasTriggered ? "Tetiklendi" : "Bekliyor", GUILayout.Width(90f));
            if (GUILayout.Button("Zar At", GUILayout.Width(62f)))
                trigger.DebugTrigger();
            if (GUILayout.Button("Reset", GUILayout.Width(62f)))
                trigger.DebugReset();
            if (GUILayout.Button("Git", GUILayout.Width(52f)))
                FocusTransform(trigger.transform, true);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(7f);
        GUILayout.Label("Tekil action'lar", GUI.skin.box);
        foreach (ActionEntry entry in actions)
        {
            if (entry.Behaviour == null
                || !MatchesSearch(
                    eventSearch,
                    entry.Behaviour.name,
                    entry.Behaviour.GetType().Name,
                    entry.HierarchyPath
                ))
                continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"{entry.Behaviour.name} · {entry.Behaviour.GetType().Name}",
                GUILayout.MinWidth(330f)
            );
            GUILayout.Label(entry.HierarchyPath, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Çalıştır", GUILayout.Width(72f)))
            {
                EnsureActiveHierarchy(entry.Behaviour.transform);
                SafeInvoke(entry.Action.Execute, $"{entry.Behaviour.GetType().Name} çalıştırıldı.");
            }
            if (GUILayout.Button("Git", GUILayout.Width(52f)))
                FocusTransform(entry.Behaviour.transform, true);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(7f);
        GUILayout.Label("Koşullar (salt okunur)", GUI.skin.box);
        foreach (ConditionEntry entry in conditions)
        {
            if (entry.Behaviour == null
                || !MatchesSearch(
                    eventSearch,
                    entry.Behaviour.name,
                    entry.Behaviour.GetType().Name,
                    entry.HierarchyPath
                ))
                continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"{entry.Behaviour.name} · {entry.Behaviour.GetType().Name}",
                GUILayout.MinWidth(330f)
            );
            GUILayout.Label(GetConditionState(entry.Condition), GUILayout.Width(90f));
            if (GUILayout.Button("Git", GUILayout.Width(52f)))
                FocusTransform(entry.Behaviour.transform, true);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(7f);
        GUILayout.Label("Puzzle receiver'lar", GUI.skin.box);
        foreach (PuzzleReceiver receiver in puzzleReceivers)
        {
            if (receiver == null
                || !MatchesSearch(eventSearch, receiver.name, "PuzzleReceiver", receiver.requiredItemID))
                continue;
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"{receiver.name} · item: {receiver.requiredItemID}",
                GUILayout.ExpandWidth(true)
            );
            if (GUILayout.Button("Sembol Modu", GUILayout.Width(95f)))
                receiver.ToggleSymbolMode();
            if (GUILayout.Button("Kapat", GUILayout.Width(62f)))
                receiver.CloseSymbol();
            if (GUILayout.Button("Git", GUILayout.Width(52f)))
                FocusTransform(receiver.transform, true);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(7f);
        GUILayout.Label("Timeline'lar", GUI.skin.box);
        foreach (PlayableDirector director in directors)
        {
            if (director == null || !MatchesSearch(eventSearch, director.name, "Timeline"))
                continue;
            GUILayout.BeginHorizontal();
            GUILayout.Label(director.name, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Oynat", GUILayout.Width(62f)))
            {
                EnsureActiveHierarchy(director.transform);
                director.enabled = true;
                director.Play();
            }
            if (GUILayout.Button("Durdur", GUILayout.Width(62f)))
                director.Stop();
            if (GUILayout.Button("Git", GUILayout.Width(52f)))
                FocusTransform(director.transform, true);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    private void ApplySafeBaseline()
    {
        foreach (SaveManager saveManager in FindAll<SaveManager>())
            saveManager.gameObject.SetActive(false);

        foreach (TutorialManager tutorial in FindAll<TutorialManager>())
            tutorial.enabled = false;

        foreach (IntroCutsceneManager intro in FindAll<IntroCutsceneManager>())
            intro.enabled = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        foreach (DebugMenuManager debugMenu in FindAll<DebugMenuManager>())
            debugMenu.enabled = false;
#endif

        foreach (MainMenuManager mainMenu in FindAll<MainMenuManager>())
            mainMenu.HideAllMenusForMechanicsTest();

        foreach (PlayableDirector director in FindAll<PlayableDirector>())
        {
            director.playOnAwake = false;
            director.Stop();
        }

        if (disableStoryEvents)
        {
            foreach (EventLogicController controller in FindAll<EventLogicController>())
                controller.enabled = false;
            foreach (WeightedRandomTrigger trigger in FindAll<WeightedRandomTrigger>())
                trigger.enabled = false;
        }

        gameManager = FindAny<GameManager>();
        if (gameManager != null)
        {
            gameManager.isGameStarted = true;
            gameManager.isGamePaused = false;
        }

        playerInteraction = FindAny<PlayerInteraction>();
        if (playerInteraction != null)
            playerInteraction.DisableTutorialMode();

        pressureManager = FindAny<PressureSystemManager>();
        if (pressureManager != null && disablePressureAndBreaker)
        {
            pressureManager.isSystemActive = false;
            pressureManager.currentPressure = 0f;
            pressureManager.overridePostProcessing = false;
        }

        breakerBox = FindAny<BreakerBox>();
        if (breakerBox != null && disablePressureAndBreaker)
        {
            breakerBox.isSystemActive = false;
            if (breakerBox.IsTripped)
                breakerBox.Interact();
        }

        enemyManager = FindAny<GlobalEnemyManager>();
        if (enemyManager != null && disableEnemies)
        {
            enemyManager.stopAllEnemies = true;
            enemyManager.RegisterAttackEnd();
        }
    }

    private void PreparePlayer()
    {
        gameManager = FindAny<GameManager>();
        enemyManager = FindAny<GlobalEnemyManager>();
        pressureManager = FindAny<PressureSystemManager>();
        breakerBox = FindAny<BreakerBox>();
        passwordManager = FindAny<PasswordManager>();
        inventoryManager = FindAny<PuzzleInventoryManager>();
        megaphoneSystem = FindAny<MegaphoneSystem>();
        playerController = FindAny<StarterAssets.CharacterController>();
        playerInteraction = FindAny<PlayerInteraction>();
        flashlight = FindAny<DynomaFlashLight>();
        notebook = FindAny<NotebookUI>();
        pauseManager = FindAny<PauseManager>();
        deathUIManager = FindAny<DeathUIManager>();

        if (gameManager != null)
        {
            gameManager.isGamePaused = false;
            gameManager.RefreshReferences();
            gameManager.StartGameMode();
        }

        if (playerInteraction != null)
            playerInteraction.DisableTutorialMode();

        if (playerController != null)
            playerController.SetFrozen(panelOpen, false, false);
    }

    private void SetupGameplayPresentation()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (InGameMenuController menuController in FindAll<InGameMenuController>())
        {
            if (menuController.gameObject.scene == activeScene)
                menuController.InstantSetupForLoad();
        }
    }

    private void CacheSceneContent()
    {
        mechanics.Clear();
        actions.Clear();
        conditions.Clear();
        puzzleReceivers.Clear();
        eventControllers.Clear();
        randomTriggers.Clear();
        directors.Clear();

        Scene activeScene = SceneManager.GetActiveScene();
        MonoBehaviour[] behaviours = FindAll<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this || behaviour.gameObject.scene != activeScene)
                continue;

            if (behaviour is IInteractable interactable)
            {
                Type type = behaviour.GetType();
                MonoBehaviour[] sameType = behaviour.GetComponents<MonoBehaviour>()
                    .Where(component => component != null && component.GetType() == type)
                    .ToArray();

                mechanics.Add(
                    new MechanicEntry
                    {
                        Behaviour = behaviour,
                        Interactable = interactable,
                        Group = GetMechanicGroup(behaviour),
                        HierarchyPath = GetReadablePath(behaviour.transform),
                        ReloadPath = GetSiblingIndexPath(behaviour.transform),
                        TypeName = type.AssemblyQualifiedName,
                        ComponentIndex = Array.IndexOf(sameType, behaviour),
                    }
                );
            }

            if (behaviour is IAction action)
            {
                actions.Add(
                    new ActionEntry
                    {
                        Behaviour = behaviour,
                        Action = action,
                        HierarchyPath = GetReadablePath(behaviour.transform),
                    }
                );
            }

            if (behaviour is ICondition condition)
            {
                conditions.Add(
                    new ConditionEntry
                    {
                        Behaviour = behaviour,
                        Condition = condition,
                        HierarchyPath = GetReadablePath(behaviour.transform),
                    }
                );
            }
        }

        puzzleReceivers.AddRange(
            FindAll<PuzzleReceiver>().Where(receiver => receiver.gameObject.scene == activeScene)
        );
        eventControllers.AddRange(
            FindAll<EventLogicController>().Where(controller => controller.gameObject.scene == activeScene)
        );
        randomTriggers.AddRange(
            FindAll<WeightedRandomTrigger>().Where(trigger => trigger.gameObject.scene == activeScene)
        );
        directors.AddRange(
            FindAll<PlayableDirector>().Where(director => director.gameObject.scene == activeScene)
        );

        mechanics.Sort(
            (left, right) =>
            {
                int groupCompare = string.Compare(left.Group, right.Group, StringComparison.Ordinal);
                return groupCompare != 0
                    ? groupCompare
                    : string.Compare(left.HierarchyPath, right.HierarchyPath, StringComparison.Ordinal);
            }
        );
        actions.Sort(
            (left, right) => string.Compare(
                left.HierarchyPath,
                right.HierarchyPath,
                StringComparison.Ordinal
            )
        );
        conditions.Sort(
            (left, right) => string.Compare(
                left.HierarchyPath,
                right.HierarchyPath,
                StringComparison.Ordinal
            )
        );

        Debug.Log(
            $"[MechanicsTest] Envanter hazır · {mechanics.Count} etkileşim · "
                + $"{actions.Count} action · {conditions.Count} condition · "
                + $"{puzzleReceivers.Count} puzzle receiver · {directors.Count} timeline"
        );
    }

    private void SetPanelOpen(bool open)
    {
        panelOpen = open;
        ApplyPanelState();
    }

    private void ApplyPanelState()
    {
        if (panelOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (playerController != null)
                playerController.SetFrozen(true, false, false);
        }
        else
        {
            if (playerController != null && (gameManager == null || gameManager.activeInteraction == null))
                playerController.SetFrozen(false, false, false);

            if (gameManager != null)
                gameManager.UpdateCursorState();
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void InvokeInteractable(MechanicEntry entry)
    {
        EnsureActiveHierarchy(entry.Behaviour.transform);
        SafeInvoke(entry.Interactable.Interact, $"{entry.Behaviour.name} çalıştırıldı.");
        SetPanelOpen(false);
    }

    private void ReloadForMechanic(MechanicEntry entry)
    {
        pendingReloadPath = entry.ReloadPath;
        pendingTypeName = entry.TypeName;
        pendingComponentIndex = entry.ComponentIndex;
        pendingFocus = true;
        ReloadScene();
    }

    private void FocusPendingMechanic()
    {
        string reloadPath = pendingReloadPath;
        string typeName = pendingTypeName;
        int componentIndex = pendingComponentIndex;

        pendingReloadPath = null;
        pendingTypeName = null;
        pendingComponentIndex = 0;
        pendingFocus = false;

        Transform target = FindTransformBySiblingIndexPath(reloadPath);
        Type targetType = Type.GetType(typeName);
        if (target == null || targetType == null)
        {
            statusMessage = "Temiz test hedefi yeniden bulunamadı.";
            return;
        }

        Component[] candidates = target.GetComponents(targetType);
        if (componentIndex < 0 || componentIndex >= candidates.Length)
        {
            statusMessage = "Temiz test bileşeni yeniden bulunamadı.";
            return;
        }

        EnsureActiveHierarchy(target);
        FocusTransform(target, true);
        statusMessage = $"Temiz test hazır: {target.name} · {targetType.Name}";
    }

    private void ReloadScene()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        EditorSceneManager.LoadSceneInPlayMode(
            scene.path,
            new LoadSceneParameters(LoadSceneMode.Single)
        );
#else
        SceneManager.LoadScene(scene.name, LoadSceneMode.Single);
#endif
    }

    private void FocusTransform(Transform target, bool closePanel)
    {
        if (target == null)
            return;

        PreparePlayer();
        if (playerController == null)
        {
            statusMessage = "Oyuncu bulunamadığı için hedefe gidilemedi.";
            return;
        }

        if (gameManager != null && gameManager.activeInteraction is IForceExitable forceExitable)
        {
            SafeInvoke(forceExitable.ForceExit, "Aktif etkileşim kapatıldı.");
        }

        Vector3 focusPoint = GetFocusPoint(target);
        Vector3 away = playerController.transform.position - focusPoint;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = -target.forward;
            away.y = 0f;
        }
        if (away.sqrMagnitude < 0.01f)
            away = Vector3.back;
        away.Normalize();

        Vector3 destination = focusPoint + away * teleportDistance;
        if (
            Physics.Raycast(
                destination + Vector3.up * 4f,
                Vector3.down,
                out RaycastHit floorHit,
                10f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            destination.y = floorHit.point.y + 0.05f;
        }
        else
        {
            destination.y = playerController.transform.position.y;
        }

        UnityEngine.CharacterController physicsController =
            playerController.GetComponent<UnityEngine.CharacterController>();
        bool wasEnabled = physicsController != null && physicsController.enabled;
        if (physicsController != null)
            physicsController.enabled = false;

        playerController.transform.position = destination;

        if (physicsController != null)
            physicsController.enabled = wasEnabled;

        Camera mainCamera = Camera.main;
        Vector3 lookOrigin = mainCamera != null
            ? mainCamera.transform.position
            : playerController.transform.position + Vector3.up * 1.6f;
        Vector3 lookDirection = (focusPoint - lookOrigin).normalized;
        float yaw = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(Mathf.Clamp(lookDirection.y, -1f, 1f)) * Mathf.Rad2Deg;

        playerController.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        playerController.ForceCameraRotation(yaw, pitch);

        statusMessage = $"Hedef hazır: {target.name}";
        if (closePanel)
            SetPanelOpen(false);
    }

    private static Vector3 GetFocusPoint(Transform target)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);
            return bounds.center;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        return target.position;
    }

    private static void EnsureActiveHierarchy(Transform target)
    {
        if (target == null)
            return;

        Stack<GameObject> hierarchy = new Stack<GameObject>();
        Transform current = target;
        while (current != null)
        {
            hierarchy.Push(current.gameObject);
            current = current.parent;
        }

        while (hierarchy.Count > 0)
            hierarchy.Pop().SetActive(true);
    }

    private void SafeInvoke(Action action, string successMessage)
    {
        try
        {
            action?.Invoke();
            statusMessage = successMessage;
        }
        catch (Exception exception)
        {
            statusMessage = $"Hata: {exception.GetType().Name} · {exception.Message}";
            Debug.LogException(exception);
        }
    }

    private static string SafePrompt(IInteractable interactable)
    {
        try
        {
            string prompt = interactable?.GetInteractionPrompt();
            return string.IsNullOrWhiteSpace(prompt) ? "(prompt yok)" : prompt;
        }
        catch (Exception exception)
        {
            return $"Prompt hatası: {exception.GetType().Name}";
        }
    }

    private static string GetConditionState(ICondition condition)
    {
        try
        {
            return condition != null && condition.IsMet() ? "DOĞRU" : "YANLIŞ";
        }
        catch (Exception exception)
        {
            return $"HATA ({exception.GetType().Name})";
        }
    }

    private static bool MatchesSearch(string search, params string[] values)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return values.Any(
            value =>
                !string.IsNullOrEmpty(value)
                && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
        );
    }

    private static string GetMechanicGroup(MonoBehaviour behaviour)
    {
        if (behaviour is InteractableDoor || behaviour is InteractableDoorLock)
            return "Kapılar ve kilitler";
        if (behaviour is ControllableLight || behaviour is BreakerBox || behaviour is RemotePowerLever)
            return "Elektrik ve ışık";
        if (
            behaviour is InteractableTuringMachine
            || behaviour is InteractableOscilloscope
            || behaviour is InteractableMassSpectrometer
            || behaviour is InteractablePressureValve
        )
            return "Makineler";
        if (behaviour is InteractableBook || behaviour is InteractableNote || behaviour is InteractableSymbol)
            return "Notlar, kitaplar ve semboller";
        if (behaviour is InteractableDrawer || behaviour is InteractableHidingSpot)
            return "Çevre etkileşimleri";
        if (behaviour is EndGameButton)
            return "Oyun akışı";
        return "Diğer";
    }

    private static string GetReadablePath(Transform target)
    {
        Stack<string> names = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }

    private static string GetSiblingIndexPath(Transform target)
    {
        Stack<int> indices = new Stack<int>();
        Transform current = target;
        while (current != null)
        {
            indices.Push(current.GetSiblingIndex());
            current = current.parent;
        }
        return string.Join("/", indices);
    }

    private static Transform FindTransformBySiblingIndexPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string[] parts = path.Split('/');
        if (parts.Length == 0 || !int.TryParse(parts[0], out int rootIndex))
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        if (rootIndex < 0 || rootIndex >= roots.Length)
            return null;

        Transform current = roots[rootIndex].transform;
        for (int i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int childIndex))
                return null;
            if (childIndex < 0 || childIndex >= current.childCount)
                return null;
            current = current.GetChild(childIndex);
        }
        return current;
    }

    private static T FindAny<T>()
        where T : UnityEngine.Object
    {
        return FindAll<T>().FirstOrDefault();
    }

    private static T[] FindAll<T>()
        where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
    }
}
