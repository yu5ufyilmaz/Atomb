#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AdamAI))]
public class AdamAIEditor : Editor
{
    bool showTime = true;
    bool showAudio = true;

    public override void OnInspectorGUI()
    {
        AdamAI script = (AdamAI)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
        };
        EditorGUILayout.Space(10);
        Rect r = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));
        EditorGUI.LabelField(r, "🌑 ADAM (THE DARKNESS)", titleStyle);
        EditorGUILayout.Space(5);

        // Canlı Takip
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"Şu anki Oda: {script.currentDetectedRoom}",
            EditorStyles.miniBoldLabel
        );

        float progress = script.debugTimer / script.debugTotalTimeNeeded;
        string status = script.debugTimer > 0 ? "⚠️ TEHDİT ARTIYOR" : "✅ GÜVENLİ";
        GUI.backgroundColor =
            script.debugTimer > 0 ? Color.Lerp(Color.yellow, Color.red, progress) : Color.green;
        GUILayout.Button(status, GUILayout.Height(25));
        GUI.backgroundColor = Color.white;

        if (Application.isPlaying && script.debugTimer > 0)
        {
            Rect bar = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(bar, progress, $"Karanlık Süresi: {script.debugTimer:F1}s");
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);

        // --- 1. ZAMANLAMA ---
        showTime = EditorGUILayout.BeginFoldoutHeaderGroup(showTime, "⏳ Zamanlama & Ölüm");
        if (showTime)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("timeToFirstWarning"),
                new GUIContent("1. Uyarı Süresi")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("timeToSecondWarning"),
                new GUIContent("2. Uyarı Süresi")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("timeToKill"),
                new GUIContent("Ölüm Süresi")
            );
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // --- 2. SES & JUMPSCARE ---
        showAudio = EditorGUILayout.BeginFoldoutHeaderGroup(showAudio, "🔊 Ses ve Görsel");
        if (showAudio)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("warning1Clip"),
                new GUIContent("Uyarı Sesi 1")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("warning2Clip"),
                new GUIContent("Uyarı Sesi 2")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("killSound"),
                new GUIContent("Ölüm Sesi")
            );
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("adamModel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpScareDistance"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
