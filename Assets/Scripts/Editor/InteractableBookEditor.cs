#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InteractableBook))]
[CanEditMultipleObjects] // 450 Kitabı aynı anda seçip editleyebilmen için!
public class InteractableBookEditor : Editor
{
    // Foldout durumlarını hafızada tutmak için
    private static bool showGeneralSettings = true;
    private static bool showVisuals = false;
    private static bool showAudio = false;
    private static bool showCamera = false;
    private static bool showDebug = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update(); // Değişiklikleri yakala

        InteractableBook book = (InteractableBook)target;

        // Başlık Stili
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        headerStyle.normal.textColor = new Color(0.7f, 0.8f, 1f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(
            $"📚 KITAP EDİTÖRÜ (ID: {book.gameObject.GetInstanceID()})",
            headerStyle
        );
        EditorGUILayout.Space(5);

        // --- 1. GENEL AYARLAR ---
        showGeneralSettings = EditorGUILayout.BeginFoldoutHeaderGroup(
            showGeneralSettings,
            "⚙️ Genel Ayarlar"
        );
        if (showGeneralSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookAnimator"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("totalPages"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowLoop"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookUI"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageNumberText"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // --- 2. GÖRSEL AYARLAR ---
        showVisuals = EditorGUILayout.BeginFoldoutHeaderGroup(showVisuals, "🎨 Görsel & Materyal");
        if (showVisuals)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookSkinnedMeshRenderer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookMaterialIndex"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Sayfa Çevirme Efekti", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipRenderer"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // --- 3. SES AYARLARI ---
        showAudio = EditorGUILayout.BeginFoldoutHeaderGroup(showAudio, "🔊 Sesler");
        if (showAudio)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookOpenSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookCloseSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("passwordFoundSound"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // --- 4. KAMERA AYARLARI ---
        showCamera = EditorGUILayout.BeginFoldoutHeaderGroup(showCamera, "🎥 Okuma Kamerası");
        if (showCamera)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("viewPositionOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("viewRotationOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("moveDuration"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(10);

        // --- 5. CANLI DURUM (DEBUG) ---
        // Burası oyun çalışırken işine yarar, normalde kapalı kalsın.
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "🐞 DEBUG (Sadece İzleme)");
        GUI.backgroundColor = Color.white;

        if (showDebug)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Read-Only alanlar çiziyoruz
            GUI.enabled = false;
            EditorGUILayout.Toggle("Açık mı?", book.isOpen);
            EditorGUILayout.IntField("Şu anki Sayfa", book.currentPage);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Şifre Durumu:", EditorStyles.boldLabel);

            if (book.isPasswordBook)
            {
                EditorGUILayout.TextField("Şifre ID", book.passwordID);
                EditorGUILayout.IntField("Hedef Sayfa", book.passwordPage);
                EditorGUILayout.Toggle("Bulundu mu?", book.hasPasswordBeenFound);
            }
            else
            {
                EditorGUILayout.LabelField("Bu kitapta şifre YOK (Normal Kitap)");
            }

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Değişiklikleri uygula
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
