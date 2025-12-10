#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InteractableBook))]
[CanEditMultipleObjects]
public class InteractableBookEditor : Editor
{
    private static bool showIdentity = true; // YENİ: Kimlik bölümü için
    private static bool showGeneralSettings = false;
    private static bool showVisuals = false;
    private static bool showAudio = false;
    private static bool showCamera = false;
    private static bool showDebug = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        InteractableBook book = (InteractableBook)target;

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        headerStyle.normal.textColor = new Color(0.7f, 0.8f, 1f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(
            $"📚 KITAP EDİTÖRÜ (ID: {book.gameObject.GetInstanceID()})",
            headerStyle
        );
        EditorGUILayout.Space(5);

        // --- 1. KİTAP KİMLİĞİ (YENİ EKLENEN KISIM) ---
        // Burayı en üste koyuyorum ki kolayca ayarla.
        GUI.backgroundColor = new Color(0.8f, 1f, 0.8f); // Hafif yeşil dikkat çeksin
        showIdentity = EditorGUILayout.BeginFoldoutHeaderGroup(
            showIdentity,
            "🆔 Kitap Kimliği & Şifre"
        );
        GUI.backgroundColor = Color.white;

        if (showIdentity)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            // İşte eklediğimiz yeni değişkenler:
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
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(5);

        // --- 2. GENEL AYARLAR ---
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

            // Gizmos ayarlarını da buraya ekleyelim ki kaybolmasın
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Debug Görseli", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("singlePageSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoYOffset"));

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // --- 3. GÖRSEL AYARLAR ---
        showVisuals = EditorGUILayout.BeginFoldoutHeaderGroup(showVisuals, "🎨 Görsel & Materyal");
        if (showVisuals)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookSkinnedMeshRenderer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookMaterialIndex"));

            // Kullanıcı kafası karışmasın diye bu runtime materyallerini gizleyebiliriz
            // ama görmek istersen PropertyField ile ekleyebilirsin.
            // EditorGUILayout.PropertyField(serializedObject.FindProperty("bookPagesMaterial"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Sayfa Çevirme Efekti", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipRenderer"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(2);

        // --- 4. SES AYARLARI ---
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

        // --- 5. KAMERA AYARLARI ---
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

        // --- 6. CANLI DURUM (DEBUG) ---
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "🐞 DEBUG (Sadece İzleme)");
        GUI.backgroundColor = Color.white;

        if (showDebug)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

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
                EditorGUILayout.RectField("Hotspot UV", book.passwordHotspotUV);
            }
            else
            {
                EditorGUILayout.LabelField("Bu kitapta şifre YOK (Normal Kitap)");
            }

            // Raycast collider kontrolü
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
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
