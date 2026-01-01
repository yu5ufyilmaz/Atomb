#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LeesEnemyAI))]
public class LeesEnemyEditor : Editor
{
    // Katlanabilir menü durumları
    bool showAnim = true;
    bool showVision = true;
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

        string statusText = $"DURUM: {script.currentState}";
        if (script.currentState == LeesEnemyAI.LeesState.Active && script.debugHasBeenSpotted)
            statusText += " (FARK EDİLDİ)";

        if (GUILayout.Button(statusText, GUILayout.Height(25))) { }
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

        // --- 2. GÖRÜŞ & KAMERA ---
        showVision = EditorGUILayout.BeginFoldoutHeaderGroup(showVision, "👁️ Görüş ve Kamera");
        if (showVision)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Referanslar", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerCamera"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eyesPosition"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Hassasiyet Ayarları", EditorStyles.miniBoldLabel);

            SerializedProperty bufferProp = serializedObject.FindProperty("screenEdgeBuffer");
            EditorGUILayout.Slider(bufferProp, 0f, 0.4f, new GUIContent("Dead Zone (Kenar Payı)"));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("obstacleMask"),
                new GUIContent("Engel Maskesi (Spawn)")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("showDebugLogs"),
                new GUIContent("Debug Çizgilerini Göster")
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

            EditorGUILayout.LabelField("Temel Süreler", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxIgnoranceTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxReactionTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("survivalWaitTime"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hareket & Tolerans", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("movementTolerance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("movementGraceTime"));

            EditorGUILayout.Space(10);
            if (Application.isPlaying && script.currentState == LeesEnemyAI.LeesState.Active)
            {
                DrawBar(
                    script.debugIgnoranceTimer / script.maxIgnoranceTime,
                    "Ignorance (A)",
                    Color.magenta
                );
                DrawBar(
                    script.debugReactionTimer / script.maxReactionTime,
                    "Reaction (C)",
                    Color.red
                );
                DrawBar(
                    script.debugSurvivalTimer / script.survivalWaitTime,
                    "Survival (D)",
                    Color.green
                );
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 4. SES AYARLARI ---
        showAudio = EditorGUILayout.BeginFoldoutHeaderGroup(showAudio, "🔊 Ses Efektleri");
        if (showAudio)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioFadeDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stareSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpscareSound"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 5. SPAWN AYARLARI ---
        showSpawn = EditorGUILayout.BeginFoldoutHeaderGroup(showSpawn, "📍 Spawn Ayarları");
        if (showSpawn)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseSpawnChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("chanceIncreasePerSecond"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnCheckInterval"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("spawnCooldownAfterDespawn")
            );

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Jumpscare Pozisyonu", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpscareDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpscareYOffset"));

            // --- YENİ EKLENEN KISIM ---
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Kişisel Jumpscare Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("leesJumpscareProfile"),
                true
            );
            // ---------------------------

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
