#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InteractableBook))]
[CanEditMultipleObjects]
public class InteractableBookEditor : Editor
{
    // Katlanabilir menülerin durumları (Hepsini başta tanımladık)
    private static bool showIdentity = true;
    private static bool showOutline = true;
    private static bool showGeneralSettings = false;
    private static bool showVisuals = false;
    private static bool showAudio = false;
    private static bool showCamera = false;
    private static bool showDebug = false;

    public override void OnInspectorGUI()
    {
        // Değişiklikleri kaydetmek için en başta çağırıyoruz
        serializedObject.Update();

        InteractableBook book = (InteractableBook)target;

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        headerStyle.normal.textColor = new Color(0.7f, 0.8f, 1f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(
            $"📚 KİTAP EDİTÖRÜ (ID: {book.gameObject.GetInstanceID()})",
            headerStyle
        );
        EditorGUILayout.Space(5);

        // --- 1. KİTAP KİMLİĞİ ---
        showIdentity = EditorGUILayout.BeginFoldoutHeaderGroup(
            showIdentity,
            "🆔 Kitap Kimliği & Şifre"
        );
        if (showIdentity)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("bookIdentity"),
                new GUIContent("Kitap Türü (Data)")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("canContainPassword"),
                new GUIContent("Şifre Çıkabilir mi?")
            );

            if (book.bookIdentity == null && book.canContainPassword)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Şifre çıkabilir dediniz ama 'Kitap Türü' (Data) atamadınız!",
                    MessageType.Warning
                );
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup(); // <--- KAPATILDI

        EditorGUILayout.Space(2);

        // --- 2. OUTLINE (VURGU) AYARLARI ---
        showOutline = EditorGUILayout.BeginFoldoutHeaderGroup(
            showOutline,
            "✨ Outline (Vurgu) Ayarları"
        );
        if (showOutline)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty colorProp = serializedObject.FindProperty("outlineColor");
            SerializedProperty widthProp = serializedObject.FindProperty("outlineWidth");

            if (colorProp != null && widthProp != null)
            {
                EditorGUILayout.PropertyField(colorProp, new GUIContent("Vurgu Rengi"));
                EditorGUILayout.PropertyField(widthProp, new GUIContent("Çizgi Kalınlığı"));
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Bu ayarlar 'Outline' bileşenini otomatik kontrol eder.",
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Outline değişkenleri scriptte bulunamadı!",
                    MessageType.Error
                );
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup(); // <--- KAPATILDI

        EditorGUILayout.Space(2);

        // --- 3. GENEL AYARLAR ---
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

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Debug Görseli", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("singlePageSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoYOffset"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup(); // <--- KAPATILDI

        EditorGUILayout.Space(2);

        // --- 4. GÖRSEL AYARLAR ---
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
        EditorGUILayout.EndFoldoutHeaderGroup(); // <--- KAPATILDI

        EditorGUILayout.Space(2);

        // --- 5. SES AYARLARI (DÜZELTİLEN KISIM) ---
        showAudio = EditorGUILayout.BeginFoldoutHeaderGroup(showAudio, "🔊 Sesler");
        if (showAudio)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookOpenSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookCloseSound"));

            // DİZİ OLARAK AYARLANDI:
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipSounds"), true);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("passwordFoundSound"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup(); // <--- KAPATILDI

        EditorGUILayout.Space(2);

        // --- 6. KAMERA AYARLARI ---
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
        EditorGUILayout.EndFoldoutHeaderGroup(); // <--- KAPATILDI

        EditorGUILayout.Space(10);

        // --- 7. DEBUG (İZLEME) ---
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "🐞 DEBUG (Sadece İzleme)");
        GUI.backgroundColor = Color.white;
        if (showDebug)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.enabled = false; // Sadece okunabilir
            EditorGUILayout.Toggle("Açık mı?", book.isOpen);
            EditorGUILayout.IntField("Şu anki Sayfa", book.currentPage);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Şifre Durumu:", EditorStyles.boldLabel);
            if (book.isPasswordBook)
            {
                EditorGUILayout.TextField("Şifre ID", book.passwordID);
                EditorGUILayout.IntField("Hedef Sayfa", book.passwordPage);
                EditorGUILayout.Toggle("Bulundu mu?", book.hasPasswordBeenFound);
                EditorGUILayout.RectField("Hotspot UV", book.passwordHotspotUV);
            }
            else
            {
                EditorGUILayout.LabelField("Bu kitapta şifre YOK");
            }
            EditorGUILayout.Space(5);
            EditorGUILayout.ObjectField(
                "Raycast Collider",
                serializedObject.FindProperty("bookCollider").objectReferenceValue,
                typeof(Collider),
                true
            );
            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup(); // <--- KAPATILDI

        // Son olarak değişiklikleri onayla
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
