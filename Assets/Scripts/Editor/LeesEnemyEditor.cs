#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LeesEnemyAI))]
public class LeesEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // --- SADECE BAŞLIK ---
        EditorGUILayout.Space(10);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.9f, 0.8f, 1f) },
        };

        Rect rect = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.15f));
        EditorGUI.LabelField(rect, "LEES AYARLARI", titleStyle);
        EditorGUILayout.Space(5);

        // --- STANDART DEĞİŞKENLERİ GÖSTER ---
        // Burası hız, süre, ses gibi ayarları normal şekilde listeler
        DrawDefaultInspector();

        // Not: Canlı barlar ve butonlar için Global Enemy Manager'ı kullanın uyarısı
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Canlı takip ve test butonları için 'GlobalEnemyManager' (Komuta Merkezi) panelini kullanın.",
            MessageType.Info
        );
    }
}
#endif
