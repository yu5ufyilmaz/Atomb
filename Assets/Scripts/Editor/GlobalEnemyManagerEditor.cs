#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GlobalEnemyManager))]
public class GlobalEnemyManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GlobalEnemyManager manager = (GlobalEnemyManager)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
        GUIStyle sectionStyle = new GUIStyle(EditorStyles.helpBox);

        // BAŞLIK
        EditorGUILayout.Space(15);
        Rect rect = EditorGUILayout.GetControlRect(false, 40);
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.12f));
        EditorGUI.LabelField(rect, "👾 DÜŞMAN KOMUTA MERKEZİ", titleStyle);
        EditorGUILayout.Space(10);

        // 1. GLOBAL TRAFİK
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("🌍 GLOBAL TRAFİK DURUMU", headerStyle);
        EditorGUILayout.Space(5);

        bool isAttack = manager.isAttackInProgress;
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = isAttack ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 0.8f, 0.4f);
        GUILayout.Box(isAttack ? "⛔ SALDIRI VAR (KİLİTLİ)" : "✅ SAKİN (MÜSAİT)", GUILayout.Height(30), GUILayout.ExpandWidth(true));
        GUI.backgroundColor = Color.white;
        
        if (isAttack && GUILayout.Button("KİLİDİ AÇ (Reset)", GUILayout.Height(30), GUILayout.Width(140)))
        {
            manager.RegisterAttackEnd();
            Debug.Log("Global kilit editörden zorla açıldı.");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(15);

        // 2. LEES PANELİ
        DrawLeesSection(headerStyle, sectionStyle);

        EditorGUILayout.Space(10);

        // 3. GUDERIAN PANELİ
        DrawGuderianSection(headerStyle, sectionStyle);

        EditorGUILayout.Space(20);
        
        if (Application.isPlaying) Repaint(); // Canlı Yenileme
    }

    // --- LEES ---
    void DrawLeesSection(GUIStyle headerStyle, GUIStyle sectionStyle)
    {
        LeesEnemyAI lees = FindObjectOfType<LeesEnemyAI>();
        GUI.backgroundColor = new Color(0.9f, 0.85f, 1f); 
        EditorGUILayout.BeginVertical(sectionStyle);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField("👻 LEES", headerStyle);
        if (lees == null) { EditorGUILayout.HelpBox("Yok!", MessageType.Warning); EditorGUILayout.EndVertical(); return; }

        bool isActive = lees.currentState == LeesEnemyAI.LeesState.Active;

        EditorGUILayout.BeginHorizontal();
        if (isActive)
        {
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            GUILayout.Box($"AKTİF: {lees.currentState}", GUILayout.Height(25), GUILayout.ExpandWidth(true));
        }
        else
        {
            GUI.backgroundColor = Color.gray;
            float chance = lees.GetCurrentSpawnChance();
            string chanceInfo = lees.debugCooldownTimer > 0 ? $"Cooldown: {lees.debugCooldownTimer:F1}s" : $"Gelme Şansı: %{chance:F1}";
            GUILayout.Box($"GİZLİ ({chanceInfo})", GUILayout.Height(25), GUILayout.ExpandWidth(true));
        }
        GUI.backgroundColor = Color.white;
        
        GUI.backgroundColor = lees.debugIsVisible ? Color.red : Color.green;
        GUILayout.Box(lees.debugIsVisible ? "👁️" : "🙈", GUILayout.Width(30), GUILayout.Height(25));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if(Application.isPlaying)
        {
            if (lees.debugCooldownTimer > 0)
                DrawBar(lees.debugCooldownTimer / lees.spawnCooldownAfterDespawn, $"Cooldown: {lees.debugCooldownTimer:F1}s", Color.cyan);
            else if (isActive)
            {
                if (!lees.debugHasBeenSpotted)
                    DrawBar(lees.debugIgnoranceTimer / lees.maxIgnoranceTime, "Fark Edilmedi (A)", new Color(0.8f, 0.2f, 1f));
                else
                {
                    DrawBar(lees.debugReactionTimer / lees.maxReactionTime, "Tepki Süresi (C)", Color.Lerp(Color.yellow, Color.red, lees.debugReactionTimer / lees.maxReactionTime));
                    if(!lees.debugIsVisible)
                    {
                        EditorGUILayout.Space(2);
                        DrawBar(lees.debugSurvivalTimer / lees.survivalWaitTime, $"Kurtuluş: %{lees.debugSurvivalTimer/lees.survivalWaitTime*100:F0}", Color.green);
                    }
                }
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn", GUILayout.Height(25))) lees.SpawnLeesInRoom();
        if (GUILayout.Button("Despawn", GUILayout.Height(25))) lees.DespawnLees();
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("KILL", GUILayout.Height(25))) lees.TriggerDeath("GM Kill");
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // --- GUDERIAN ---
    void DrawGuderianSection(GUIStyle headerStyle, GUIStyle sectionStyle)
    {
        GuderianAI guderian = FindObjectOfType<GuderianAI>();
        GUI.backgroundColor = new Color(1f, 0.9f, 0.8f);
        EditorGUILayout.BeginVertical(sectionStyle);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField("👹 GUDERIAN", headerStyle);
        if (guderian == null) { EditorGUILayout.HelpBox("Yok!", MessageType.Warning); EditorGUILayout.EndVertical(); return; }

        bool isActive = guderian.currentState != GuderianAI.GuderianState.Hidden;

        EditorGUILayout.BeginHorizontal();
        if (isActive)
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
            GUILayout.Box($"AKTİF: {guderian.currentState}", GUILayout.Height(25), GUILayout.ExpandWidth(true));
        }
        else
        {
            GUI.backgroundColor = Color.gray;
            string info;
            if (guderian.IsOnCooldown()) info = $"Cooldown: {guderian.debugCooldown:F1}s";
            else 
            {
                // YENİ: Artan Şansı Göster
                info = $"Şans: %{guderian.GetCurrentChance()} (Kontrol: {guderian.GetTimeUntilNextSpawnCheck():F1}s)";
            }
            
            GUILayout.Box($"GİZLİ ({info})", GUILayout.Height(25), GUILayout.ExpandWidth(true));
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if(isActive && Application.isPlaying) EditorGUILayout.HelpBox(guderian.debugStatus, MessageType.None);

        if (Application.isPlaying)
        {
            if (guderian.IsOnCooldown())
                DrawBar(guderian.debugCooldown / guderian.minTimeBetweenAttacks, "Cooldown", Color.cyan);
            else if (isActive)
            {
                if (guderian.currentState == GuderianAI.GuderianState.Approaching)
                    DrawBar(guderian.debugApproachProgress, "Adımlar", Color.yellow);
                else if (guderian.currentState == GuderianAI.GuderianState.Breaching)
                    DrawBar(guderian.debugBreachProgress, "Kapı Zorlanıyor", new Color(1f, 0.5f, 0f));
                else if (guderian.currentState == GuderianAI.GuderianState.Searching)
                    DrawBar(guderian.debugSearchProgress, "Arama Süresi", Color.Lerp(Color.green, Color.red, 1f - guderian.debugSearchProgress));
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn (Rastgele)", GUILayout.Height(25)))
        {
            RoomManager[] rooms = FindObjectsOfType<RoomManager>();
            foreach(var room in rooms) { if (room.canGuderianSpawn) { guderian.TrySpawnGuderian(room); break; } }
        }
        
        if (isActive)
        {
            if (GUILayout.Button("GİT (Leave)", GUILayout.Height(25))) guderian.ForceLeave();
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button("GİT (Leave)", GUILayout.Height(25));
            GUI.enabled = true;
        }

        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("KILL", GUILayout.Height(25))) guderian.TriggerJumpscare();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    void DrawBar(float value, string label, Color color)
    {
        GUI.color = color;
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), Mathf.Clamp01(value), label);
        GUI.color = Color.white;
    }
}
#endif