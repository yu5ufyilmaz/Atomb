#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GuderianAI))]
public class GuderianEditor : Editor
{
    // Katlanabilir menülerin durumları
    bool showVisuals = true;
    bool showBehav = true;
    bool showSpawn = false;

    private void OnEnable()
    {
        // Eski 'ambushDoorOffset' referansı buradan kaldırıldı.
    }

    public override void OnInspectorGUI()
    {
        GuderianAI script = (GuderianAI)target;

        serializedObject.Update();

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

        GUI.backgroundColor =
            script.currentState != GuderianAI.GuderianState.Hidden
                ? new Color(1f, 0.6f, 0.2f)
                : Color.gray;
        if (GUILayout.Button($"DURUM: {script.currentState}", GUILayout.Height(25))) { }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(5);

        // --- 1. GRUP: SES & GÖRSEL ---
        showVisuals = EditorGUILayout.BeginFoldoutHeaderGroup(showVisuals, "🎬 Görsel ve Sesler");
        if (showVisuals)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("guderianModel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.Space(2);
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
            EditorGUILayout.LabelField("Süreler", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseSearchDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timePerLight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closedDoorBreachTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lockedDoorBreachTime"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Hareket", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("walkSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookAtDoorThreshold"));

            // Hata veren 'Pusu' kısmı buradan kaldırıldı çünkü artık RoomManager üzerinden yönetiliyor.

            EditorGUILayout.EndVertical();
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
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
