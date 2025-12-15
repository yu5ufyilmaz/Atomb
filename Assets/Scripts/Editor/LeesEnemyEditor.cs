#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LeesEnemyAI))]
public class LeesEnemyEditor : Editor
{
    // Katlanabilir menü durumları
    bool showAnim = true;
    bool showVision = false;
    bool showSpawn = false;
    bool showScenario = true;
    bool showAudio = false;

    public override void OnInspectorGUI()
    {
        LeesEnemyAI script = (LeesEnemyAI)target;

        // --- BAŞLIK ---
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.8f, 0.7f, 1f) },
        };
        EditorGUILayout.Space(10);
        Rect r = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(r, new Color(0.15f, 0.1f, 0.2f));
        EditorGUI.LabelField(r, "👻 LEES AI KONTROL", titleStyle);
        EditorGUILayout.Space(5);

        // --- DURUM ÇUBUĞU ---
        GUI.backgroundColor =
            script.currentState == LeesEnemyAI.LeesState.Active
                ? new Color(1f, 0.4f, 0.4f)
                : Color.green;
        if (GUILayout.Button($"DURUM: {script.currentState}", GUILayout.Height(25))) { }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(5);

        // --- 1. MODEL & ANİMASYON ---
        showAnim = EditorGUILayout.BeginFoldoutHeaderGroup(showAnim, "🎬 Model ve Animasyon");
        if (showAnim)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("leesAnimator"));
            EditorGUILayout.HelpBox(
                "Animator Controller'da 'Jumpscare' trigger'ı olduğundan emin olun.",
                MessageType.Info
            );
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 2. SES AYARLARI (YENİ) ---
        showAudio = EditorGUILayout.BeginFoldoutHeaderGroup(showAudio, "🔊 Ses Efektleri");
        if (showAudio)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("stareSound"),
                new GUIContent("Bakışma Sesi (Loop)")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("jumpscareSound"),
                new GUIContent("Jumpscare Sesi")
            );
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 3. SENARYO VE ZAMANLAMA ---
        showScenario = EditorGUILayout.BeginFoldoutHeaderGroup(
            showScenario,
            "⏳ Senaryo Zamanlamaları"
        );
        if (showScenario)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("maxIgnoranceTime"),
                new GUIContent("Fark Edilmeme Süresi (A)")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("maxReactionTime"),
                new GUIContent("Bakışma Limiti (C)")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("survivalWaitTime"),
                new GUIContent("Arkası Dönük Bekleme (D)")
            );

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Canlı Sayaçlar (Read-Only)", EditorStyles.miniBoldLabel);
            if (Application.isPlaying && script.currentState == LeesEnemyAI.LeesState.Active)
            {
                DrawBar(
                    script.debugIgnoranceTimer / script.maxIgnoranceTime,
                    "Ignorance",
                    Color.magenta
                );
                DrawBar(script.debugReactionTimer / script.maxReactionTime, "Reaction", Color.red);
                DrawBar(
                    script.debugSurvivalTimer / script.survivalWaitTime,
                    "Survival",
                    Color.green
                );
            }
            else
            {
                EditorGUILayout.HelpBox("Sayaçlar sadece aktifken görünür.", MessageType.None);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 4. SPAWN & GÖRÜŞ ---
        showSpawn = EditorGUILayout.BeginFoldoutHeaderGroup(showSpawn, "📍 Spawn ve Görüş");
        if (showSpawn)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseSpawnChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("chanceIncreasePerSecond"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("spawnCooldownAfterDespawn")
            );
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerCamera"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eyesPosition"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("obstacleMask"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawBar(float val, string name, Color c)
    {
        Rect r = EditorGUILayout.GetControlRect(false, 18);
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(val), r.height), c);
        EditorGUI.LabelField(
            r,
            $"{name}: {val * 100:F0}%",
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
            }
        );
    }
}
#endif
