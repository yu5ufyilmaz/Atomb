#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PasswordData))]
public class PasswordDataEditor : Editor
{
    private bool isDragging = false;
    private Vector2 startPos;
    private int selectedIndex = 0; // Listeden hangisini düzenliyoruz?

    public override void OnInspectorGUI()
    {
        PasswordData data = (PasswordData)target;
        serializedObject.Update(); // Değişiklikleri yakala

        // --- 1. Standart Alanlar (Manuel Çizim) ---
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pageTexture"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("totalPages"));

        EditorGUILayout.Space(5);

        // Tutorial Ayarı (Conditional Display)
        SerializedProperty isTutorialProp = serializedObject.FindProperty("isTutorialData");
        EditorGUILayout.PropertyField(isTutorialProp);

        if (isTutorialProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "Bu data bir Tutorial kitabıdır. Aşağıdaki şifre sabit olarak atanacaktır.",
                MessageType.Info
            );
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tutorialPasswordID"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("possibleLocations"));

        serializedObject.ApplyModifiedProperties(); // Değişiklikleri kaydet

        // --- 2. Görsel Düzenleyici (Mevcut Kod) ---
        if (data.pageTexture == null)
            return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("GÖRSEL DÜZENLEYİCİ (HOTSPOT)", EditorStyles.boldLabel);

        if (data.possibleLocations.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Lütfen yukarıdaki 'Possible Locations' listesine '+' ile bir eleman ekleyin.",
                MessageType.Info
            );
            return;
        }

        // --- Seçim Menüsü ---
        string[] options = new string[data.possibleLocations.Count];
        for (int i = 0; i < data.possibleLocations.Count; i++)
            options[i] =
                $"{i}: {data.possibleLocations[i].note} (P: {data.possibleLocations[i].pageIndex})";

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Düzenlenen:", GUILayout.Width(80));
        selectedIndex = EditorGUILayout.Popup(selectedIndex, options);
        EditorGUILayout.EndHorizontal();

        // Dizi sınırlarını koru
        if (selectedIndex >= data.possibleLocations.Count)
            selectedIndex = 0;

        PasswordLocationEntry currentEntry = data.possibleLocations[selectedIndex];

        // --- Texture Çizimi (Crop) ---
        float singlePageW = data.pageTexture.width / (float)data.totalPages;
        float aspect = singlePageW / data.pageTexture.height;
        float dispW = EditorGUIUtility.currentViewWidth - 40;
        float dispH = dispW / aspect;

        Rect dispRect = GUILayoutUtility.GetRect(dispW, dispH);

        float uvW = 1.0f / data.totalPages;
        Rect uvCrop = new Rect(currentEntry.pageIndex * uvW, 0, uvW, 1);
        GUI.DrawTextureWithTexCoords(dispRect, data.pageTexture, uvCrop);

        // --- Mevcut Kutuyu Çiz ---
        Rect screenRect = UVToScreen(currentEntry.hotspotUV, dispRect);
        Handles.DrawSolidRectangleWithOutline(screenRect, new Color(0, 1, 0, 0.3f), Color.green);

        // --- Mouse Input ---
        HandleInput(dispRect, data, selectedIndex);
    }

    private void HandleInput(Rect bounds, PasswordData data, int idx)
    {
        Event e = Event.current;
        if (bounds.Contains(e.mousePosition) || isDragging)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                isDragging = true;
                startPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && isDragging)
            {
                Rect r = Clamp(FromPoints(startPos, e.mousePosition), bounds);
                Handles.DrawSolidRectangleWithOutline(r, new Color(0, 0.5f, 1, 0.2f), Color.cyan);
                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && isDragging)
            {
                isDragging = false;
                Rect final = Clamp(FromPoints(startPos, e.mousePosition), bounds);

                // Screen -> Local UV
                float uX = (final.x - bounds.x) / bounds.width;
                float uW = final.width / bounds.width;
                float uH = final.height / bounds.height;
                float uY = 1f - ((final.y - bounds.y) / bounds.height) - uH;

                Undo.RecordObject(data, "Set Hotspot");
                PasswordLocationEntry entry = data.possibleLocations[idx];
                entry.hotspotUV = new Rect(uX, uY, uW, uH);
                data.possibleLocations[idx] = entry;
                EditorUtility.SetDirty(data);
                e.Use();
            }
        }
    }

    Rect FromPoints(Vector2 a, Vector2 b) =>
        new Rect(
            Mathf.Min(a.x, b.x),
            Mathf.Min(a.y, b.y),
            Mathf.Abs(a.x - b.x),
            Mathf.Abs(a.y - b.y)
        );

    Rect Clamp(Rect r, Rect b) =>
        new Rect(
            Mathf.Max(r.x, b.x),
            Mathf.Max(r.y, b.y),
            Mathf.Min(r.xMax, b.xMax) - Mathf.Max(r.x, b.x),
            Mathf.Min(r.yMax, b.yMax) - Mathf.Max(r.y, b.y)
        );

    Rect UVToScreen(Rect uv, Rect b) =>
        new Rect(
            b.x + uv.x * b.width,
            b.y + (1 - (uv.y + uv.height)) * b.height,
            uv.width * b.width,
            uv.height * b.height
        );
}
#endif
