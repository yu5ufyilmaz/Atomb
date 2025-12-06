#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GuderianAI))]
public class GuderianEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GuderianAI ai = (GuderianAI)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.9f, 0.8f) } };
        GUIStyle sectionStyle = new GUIStyle(EditorStyles.helpBox);

        // BAŞLIK
        EditorGUILayout.Space(10);
        Rect rect = EditorGUILayout.GetControlRect(false, 35);
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.15f, 0.1f));
        EditorGUI.LabelField(rect, "GUDERIAN AI AYARLARI", titleStyle);
        EditorGUILayout.Space(5);

        // CANLI TAKİP
        if (Application.isPlaying)
        {
            if (ai.currentState != GuderianAI.GuderianState.Hidden)
            {
                EditorGUILayout.BeginVertical(sectionStyle);
                
                GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
                GUILayout.Box($"DURUM: {ai.currentState.ToString().ToUpper()}", GUILayout.ExpandWidth(true), GUILayout.Height(25));
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.HelpBox(ai.debugStatus, MessageType.None);
                EditorGUILayout.Space(5);

                if (ai.currentState == GuderianAI.GuderianState.Approaching)
                    DrawStandardBar(ai.debugApproachProgress, "Adımlar", Color.yellow);
                
                else if (ai.currentState == GuderianAI.GuderianState.Breaching)
                    DrawStandardBar(ai.debugBreachProgress, "Kapı Zorlanıyor", new Color(1f, 0.5f, 0f));
                
                else if (ai.currentState == GuderianAI.GuderianState.Searching)
                    DrawStandardBar(ai.debugSearchProgress, "Arama Süresi", Color.Lerp(Color.green, Color.red, 1f - ai.debugSearchProgress)); 
                
                EditorGUILayout.EndVertical();
            }
            else
            {
                // Cooldown Barı
                if (ai.debugCooldown > 0)
                {
                    DrawStandardBar(ai.debugCooldown / ai.minTimeBetweenAttacks, $"Cooldown: {ai.debugCooldown:F1}s", Color.cyan);
                    EditorGUILayout.Space(5);
                }
                else
                {
                    EditorGUILayout.HelpBox("💤 Guderian Beklemede.", MessageType.Info);
                }
            }
        }

        EditorGUILayout.Space(10);

        // TEST
        EditorGUILayout.LabelField("🛠️ Test Kontrolleri", EditorStyles.boldLabel);

        if (GUILayout.Button("TEST: Guderian'ı Çağır (Rastgele)", GUILayout.Height(30)))
        {
            RoomManager[] rooms = FindObjectsOfType<RoomManager>();
            foreach(var room in rooms)
            {
                if (room.canGuderianSpawn) { ai.TrySpawnGuderian(room); break; }
            }
        }
        
        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("☠ FORCE JUMPSCARE ☠", GUILayout.Height(25))) if(Application.isPlaying) ai.TriggerJumpscare();
        GUI.backgroundColor = Color.white;

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