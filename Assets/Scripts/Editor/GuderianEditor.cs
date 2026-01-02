#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

[CustomEditor(typeof(GuderianAI))]
public class GuderianEditor : Editor
{
    bool showVisuals = true;
    bool showBehav = true;
    bool showSpawn = false;

    public override void OnInspectorGUI()
    {
        GuderianAI script = (GuderianAI)target;
        serializedObject.Update();

        // --- BAŞLIK ---
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.9f, 0.8f) },
        };
        EditorGUILayout.Space(10);
        Rect r = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(r, new Color(0.2f, 0.15f, 0.1f));
        EditorGUI.LabelField(r, "👹 GUDERIAN AI", titleStyle);
        EditorGUILayout.Space(5);

        // --- DURUM GÖSTERGESİ ---
        Color stateColor = Color.gray;
        if (script.currentState == GuderianAI.GuderianState.Searching)
            stateColor = Color.red;
        else if (script.currentState != GuderianAI.GuderianState.Hidden)
            stateColor = new Color(1f, 0.6f, 0.2f);

        GUI.backgroundColor = stateColor;
        GUILayout.Button(
            $"DURUM: {script.currentState}\n({script.debugStatus})",
            GUILayout.Height(30)
        );
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(5);

        // --- 1. GRUP: REFERANSLAR & GÖRSEL ---
        showVisuals = EditorGUILayout.BeginFoldoutHeaderGroup(
            showVisuals,
            "🎬 Referanslar ve Görsel"
        );
        if (showVisuals)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("NavMesh & Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("agent"));
            if (script.agent == null)
            {
                EditorGUILayout.HelpBox(
                    "NavMeshAgent atanmadı! AI hareket edemez.",
                    MessageType.Error
                );
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("guderianModel"));

            // --- YENİ EKLENEN KISIM ---
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("animator"),
                new GUIContent("Animator Controller")
            );
            // ---------------------------

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Sesler", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("footstepSounds"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("doorHandleSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("doorOpenSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("searchHumSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpscareSound"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 2. GRUP: DAVRANIŞ AYARLARI ---
        showBehav = EditorGUILayout.BeginFoldoutHeaderGroup(showBehav, "🧠 Davranış Ayarları");
        if (showBehav)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Süreler & Kırılma", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseSearchDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timePerLight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closedDoorBreachTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lockedDoorBreachTime"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Hareket", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("walkSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookAtDoorThreshold"));
            EditorGUILayout.EndVertical();

            // Progress Barlar (Oyun Çalışırken)
            if (Application.isPlaying && script.currentState != GuderianAI.GuderianState.Hidden)
            {
                EditorGUILayout.Space(5);
                DrawBar(script.debugBreachProgress, "Kapı Kırma", Color.yellow);
                DrawBar(script.debugSearchProgress, "Arama Süresi", Color.red);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 3. GRUP: SPAWN AYARLARI ---
        showSpawn = EditorGUILayout.BeginFoldoutHeaderGroup(showSpawn, "📍 Spawn ve Jumpscare");
        if (showSpawn)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("checkInterval"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseSpawnChance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minTimeBetweenAttacks"));
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpscareDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnYOffset"));
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Kişisel Jumpscare Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("guderianJumpscareProfile"),
                true
            );
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Spawn Şansı: {script.GetCurrentChance()}%");
            EditorGUILayout.LabelField(
                $"Sonraki Kontrol: {script.GetTimeUntilNextSpawnCheck():F1}s"
            );
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawBar(float val, string name, Color c)
    {
        if (val <= 0)
            return;
        Rect r = EditorGUILayout.GetControlRect(false, 18);
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(val), r.height), c);
        EditorGUI.LabelField(
            r,
            $"{name}: {val * 100:F0}%",
            new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.black },
                alignment = TextAnchor.MiddleCenter,
            }
        );
    }
}
#endif
