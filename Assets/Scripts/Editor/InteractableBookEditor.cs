#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InteractableBook))]
[CanEditMultipleObjects]
public class InteractableBookEditor : Editor
{
    private static bool showIdentity = true;
    private static bool showHighlight = true;
    private static bool showSymbolPuzzle = true; // YENİ EKLENDİ
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
        EditorGUILayout.LabelField($"📚 KİTAP EDİTÖRÜ (Skinned)", headerStyle);
        EditorGUILayout.Space(5);

        // --- 1. KİMLİK ---
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
                EditorGUILayout.HelpBox("⚠️ 'Kitap Türü' atanmamış!", MessageType.Warning);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2);
        // --- YENİ: 3D SEMBOL BULMACASI ---
        showSymbolPuzzle = EditorGUILayout.BeginFoldoutHeaderGroup(
            showSymbolPuzzle,
            "🧩 3D Sembol Bulmacası"
        );
        if (showSymbolPuzzle)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty isSymbolTargetProp = serializedObject.FindProperty(
                "isSymbolTargetBook"
            );
            EditorGUILayout.PropertyField(
                isSymbolTargetProp,
                new GUIContent("Sembol Hedef Kitabı mı?")
            );

            // Sadece tik açıkken diğer ayarları göster (Daha temiz bir görünüm için)
            if (isSymbolTargetProp.boolValue)
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("requiredSymbolID"),
                    new GUIContent("Gereken Sembol ID")
                );
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("symbolPuzzlePage"),
                    new GUIContent("Çözüm Sayfası")
                );
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("targetSymbolAnchor"),
                    new GUIContent("Hedef Çapa (Anchor)")
                );

                if (book.targetSymbolAnchor == null)
                    EditorGUILayout.HelpBox(
                        "Lütfen sayfaya yerleştirdiğiniz Anchor objesini atayın!",
                        MessageType.Warning
                    );
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2);
        // --- 2. HIGHLIGHT & OUTLINE AYARLARI ---
        showHighlight = EditorGUILayout.BeginFoldoutHeaderGroup(
            showHighlight,
            "✨ Vurgu (Highlight / Outline) Ayarları"
        );
        if (showHighlight)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // --- YENİ EKLENEN KISIM: OUTLINE CONTROLLER ---
            SerializedProperty outlineProp = serializedObject.FindProperty("outlineController");
            EditorGUILayout.PropertyField(outlineProp, new GUIContent("HDRP Outline Scripti"));

            if (outlineProp.objectReferenceValue != null)
            {
                // Eğer Outline atanmışsa kullanıcıyı bilgilendir
                GUI.backgroundColor = new Color(0.8f, 1f, 0.8f); // Hafif yeşil arka plan
                EditorGUILayout.HelpBox(
                    "✅ Outline Scripti algılandı! Eski 'Emission' (Parlama) sistemi yerine Outline kullanılacak.",
                    MessageType.Info
                );
                GUI.backgroundColor = Color.white;
            }
            else
            {
                // Atanmamışsa uyarı verilebilir veya boş bırakılabilir
                EditorGUILayout.HelpBox(
                    "Outline Scripti boşsa, aşağıdaki Parlama (Emission) ayarları kullanılır.",
                    MessageType.None
                );
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(
                "Yedek Sistem (Emission/Parlama)",
                EditorStyles.miniBoldLabel
            );
            // ----------------------------------------------

            SerializedProperty colorProp = serializedObject.FindProperty("highlightColor");
            SerializedProperty intensityProp = serializedObject.FindProperty("emissionIntensity");

            EditorGUILayout.PropertyField(colorProp, new GUIContent("Vurgu Rengi"));
            EditorGUILayout.PropertyField(
                intensityProp,
                new GUIContent("Parlaklık Şiddeti (HDRP)")
            );

            if (book.emissionIntensity < 1f)
            {
                EditorGUILayout.HelpBox(
                    "Dikkat: Şiddet 1'den küçükse HDRP'de parlama GÖRÜNMEZ. 10 veya 50 gibi değerler deneyin.",
                    MessageType.Info
                );
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2);

        // --- 3. GENEL ---
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
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookUI"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageNumberText"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("bookCollider"),
                new GUIContent("Sayfa Collider'i")
            );
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animationDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("singlePageSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoYOffset"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2);

        // --- 4. GÖRSEL ---
        showVisuals = EditorGUILayout.BeginFoldoutHeaderGroup(showVisuals, "🎨 Görsel & Materyal");
        if (showVisuals)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Ana Model", EditorStyles.miniBoldLabel);
            SerializedProperty skinnedProp = serializedObject.FindProperty(
                "bookSkinnedMeshRenderer"
            );
            EditorGUILayout.PropertyField(skinnedProp);

            if (book.bookSkinnedMeshRenderer == null)
                EditorGUILayout.HelpBox(
                    "SkinnedMeshRenderer BOŞ! Kitap çalışmaz.",
                    MessageType.Error
                );

            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookMaterialIndex"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookPagesMaterial"));
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Sayfa Çevirme Efekti", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipObject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipRenderer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageTurnMaterial"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2);

        // --- 5. SES, 6. KAMERA, 7. DEBUG (Standart) ---
        showAudio = EditorGUILayout.BeginFoldoutHeaderGroup(showAudio, "🔊 Sesler");
        if (showAudio)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookOpenSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bookCloseSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pageFlipSounds"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("passwordFoundSound"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2);

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

        showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "🐞 DEBUG");
        if (showDebug)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.enabled = false;
            EditorGUILayout.Toggle("Açık mı?", book.isOpen);
            EditorGUILayout.IntField("Sayfa", book.currentPage);
            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
