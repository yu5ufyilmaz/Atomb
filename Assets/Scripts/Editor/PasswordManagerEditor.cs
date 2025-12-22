#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PasswordManager))]
public class PasswordManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PasswordManager manager = (PasswordManager)target;

        // Özel Başlık Stili
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 13;
        headerStyle.normal.textColor = new Color(0.4f, 0.7f, 1f);

        // --- 1. TUTORIAL AYARLARI ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("📘 TUTORIAL AYARLARI", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        SerializedProperty tutorialNoteProp = serializedObject.FindProperty("tutorialNoteBook");
        EditorGUILayout.PropertyField(tutorialNoteProp, new GUIContent("Tutorial Notu (Sürükle)"));

        SerializedProperty tutorialPassProp = serializedObject.FindProperty("tutorialPassword");
        EditorGUILayout.PropertyField(tutorialPassProp, new GUIContent("Tutorial Şifresi"));

        if (tutorialNoteProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "⚠️ DİKKAT: Tutorial Notu atanmamış! Atanmazsa normal şifre gibi davranır.",
                MessageType.Error
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "✅ Tutorial Notu tanımlı. Bu not sayaça dahil edilmeyecek.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndVertical();

        // --- 2. OYUN AYARLARI ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎮 OYUN DÖNGÜSÜ", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        SerializedProperty totalNeededProp = serializedObject.FindProperty("totalPasswordsNeeded");
        EditorGUILayout.PropertyField(totalNeededProp, new GUIContent("Gereken Şifre Sayısı"));
        EditorGUILayout.HelpBox(
            $"Bu sayıya Tutorial dahil DEĞİLDİR. Toplam {totalNeededProp.intValue} rastgele şifre bulunca oyun biter.",
            MessageType.None
        );

        EditorGUILayout.EndVertical();

        // --- 3. REFERANSLAR ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🔗 BAĞLANTILAR", headerStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        base.OnInspectorGUI(); // Diğer standart değişkenleri (Makineler, Listeler vb.) buraya basar
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
