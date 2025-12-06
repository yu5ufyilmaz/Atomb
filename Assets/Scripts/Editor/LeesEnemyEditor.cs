#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LeesEnemyAI))]
public class LeesEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LeesEnemyAI ai = (LeesEnemyAI)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.9f, 0.8f, 1f) } };
        GUIStyle sectionStyle = new GUIStyle(EditorStyles.helpBox);

        // BAŞLIK
        EditorGUILayout.Space(10);
        Rect rect = EditorGUILayout.GetControlRect(false, 35);
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.15f));
        EditorGUI.LabelField(rect, "LEES AI AYARLARI", titleStyle);
        EditorGUILayout.Space(5);

        // CANLI TAKİP
        if (Application.isPlaying && ai.currentState == LeesEnemyAI.LeesState.Active)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("🔴 CANLI DURUM RAPORU", EditorStyles.boldLabel);
            
            // Görüş Durumu
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = ai.debugIsVisible ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 0.8f, 0.4f);
            GUILayout.Box(ai.debugIsVisible ? "👁️ GÖRÜYOR (EYE CONTACT)" : "🙈 GÖRMÜYOR (HIDDEN)", GUILayout.Height(25), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Barlar (Standart Stil)
            if (!ai.debugHasBeenSpotted)
            {
                DrawStandardBar(ai.debugIgnoranceTimer / ai.maxIgnoranceTime, "Fark Edilmedi", new Color(0.8f, 0.2f, 1f));
            }
            else
            {
                DrawStandardBar(ai.debugReactionTimer / ai.maxReactionTime, "Tepki Süresi (C)", Color.Lerp(Color.yellow, Color.red, ai.debugReactionTimer / ai.maxReactionTime));
                
                if (!ai.debugIsVisible)
                    DrawStandardBar(ai.debugSurvivalTimer / ai.survivalWaitTime, "Kurtuluş (D)", Color.green);
            }
            
            // Hız Uyarısı
            if (ai.debugHasBeenSpotted && !ai.debugIsVisible && ai.debugPlayerSpeed > ai.movementTolerance)
            {
                EditorGUILayout.Space(5);
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                EditorGUILayout.HelpBox($"⚠️ HAREKET EDİLİYOR! ({ai.debugPlayerSpeed:F2}) -> ÖLÜM", MessageType.Error);
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndVertical();
        }
        else if (Application.isPlaying)
        {
            // Cooldown Barı
            if (ai.debugCooldownTimer > 0)
            {
                DrawStandardBar(ai.debugCooldownTimer / ai.spawnCooldownAfterDespawn, $"Cooldown: {ai.debugCooldownTimer:F1}s", Color.cyan);
                EditorGUILayout.Space(5);
            }
            else
            {
                EditorGUILayout.HelpBox("💤 Lees Gizleniyor (Hidden State).", MessageType.Info);
            }
        }

        EditorGUILayout.Space(10);

        // TEST BUTONLARI
        EditorGUILayout.LabelField("🛠️ Test Kontrolleri", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn", GUILayout.Height(25))) ai.SpawnLeesInRoom();
        if (GUILayout.Button("Despawn", GUILayout.Height(25))) ai.DespawnLees();
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("KILL", GUILayout.Height(25))) if(Application.isPlaying) ai.TriggerDeath("Editör Butonu");
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        DrawDefaultInspector();

        // CANLI YENİLEME
        if (Application.isPlaying) Repaint();
    }

    void DrawStandardBar(float value, string label, Color color)
    {
        GUI.color = color;
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), Mathf.Clamp01(value), label);
        GUI.color = Color.white;
    }
}
#endif