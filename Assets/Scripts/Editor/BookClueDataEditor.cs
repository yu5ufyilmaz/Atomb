#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Eski PasswordDataEditor'un görsel hotspot mantığı: sayfayı göster, fareyle sürükleyerek bölge seç.
// Farkı: her bölge bir parçaya (FormulaItemSO) bağlıdır ve sayfadaki tüm bölgeler birlikte görünür.
[CustomEditor(typeof(BookClueData))]
public class BookClueDataEditor : Editor
{
    private bool isDragging;
    private Vector2 startPos;
    private int selectedIndex;
    private PasswordData importSource;

    public override void OnInspectorGUI()
    {
        BookClueData data = (BookClueData)target;
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pageTexture"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("localizedPageTextures"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("totalPages"));
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regions"), true);
        serializedObject.ApplyModifiedProperties();

        DrawImport(data);
        if (data.pageTexture == null) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("GÖRSEL DÜZENLEYİCİ (PARÇA BÖLGELERİ)", EditorStyles.boldLabel);
        if (data.regions.Count == 0)
        {
            EditorGUILayout.HelpBox("Yukarıdaki 'Regions' listesine '+' ile bir bölge ekleyin.", MessageType.Info);
            return;
        }

        string[] options = new string[data.regions.Count];
        for (int i = 0; i < data.regions.Count; i++)
        {
            BookClueRegion r = data.regions[i];
            string part = r != null && r.formula != null ? r.formula.itemName : "(parça yok)";
            options[i] = r == null ? i + ": -" : $"{i}: {part}  [Sayfa {r.pageIndex}] {r.note}";
        }
        selectedIndex = Mathf.Clamp(selectedIndex, 0, data.regions.Count - 1);
        selectedIndex = EditorGUILayout.Popup("Düzenlenen:", selectedIndex, options);
        BookClueRegion current = data.regions[selectedIndex];
        if (current == null) return;
        if (current.formula == null)
            EditorGUILayout.HelpBox("Bu bölgeye bir Formula Item atanmadı; oyunda gösterilmez.", MessageType.Warning);

        float pageWidth = data.pageTexture.width / (float)Mathf.Max(1, data.totalPages);
        float aspect = pageWidth / data.pageTexture.height;
        float displayWidth = Mathf.Min(EditorGUIUtility.currentViewWidth - 40, 520f);
        float displayHeight = displayWidth / aspect;
        Rect display = GUILayoutUtility.GetRect(displayWidth, displayHeight);
        float uvWidth = 1f / Mathf.Max(1, data.totalPages);
        GUI.DrawTextureWithTexCoords(display, data.pageTexture, new Rect(current.pageIndex * uvWidth, 0, uvWidth, 1));

        // Aynı sayfadaki diğer bölgeler soluk, seçili bölge yeşil görünür.
        for (int i = 0; i < data.regions.Count; i++)
        {
            BookClueRegion r = data.regions[i];
            if (r == null || r.pageIndex != current.pageIndex) continue;
            bool selected = i == selectedIndex;
            Rect box = UVToScreen(r.regionUV, display);
            Handles.DrawSolidRectangleWithOutline(box, selected ? new Color(0, 1, 0, 0.3f) : new Color(1f, 0.77f, 0.19f, 0.2f),
                selected ? Color.green : new Color(1f, 0.77f, 0.19f, 1f));
            GUI.Label(box, r.formula != null ? r.formula.itemName : i.ToString(), EditorStyles.whiteBoldLabel);
        }
        HandleInput(display, data, selectedIndex);

        EditorGUILayout.LabelField("Defterin alacağı görüntü:", EditorStyles.miniBoldLabel);
        Rect atlas = data.GetAtlasRect(current);
        Vector2 pixels = data.GetPixelSize(current);
        float previewHeight = Mathf.Min(110f, 340f * pixels.y / pixels.x);
        Rect preview = GUILayoutUtility.GetRect(340f, previewHeight, GUILayout.ExpandWidth(false));
        GUI.DrawTextureWithTexCoords(preview, data.pageTexture, atlas);
    }

    private void DrawImport(BookClueData data)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        importSource = (PasswordData)EditorGUILayout.ObjectField("Eski PasswordData", importSource, typeof(PasswordData), false);
        using (new EditorGUI.DisabledScope(importSource == null))
            if (GUILayout.Button("İçe Aktar", GUILayout.Width(80)))
            {
                Undo.RecordObject(data, "Import Password Data");
                ImportPasswordData(data, importSource);
                EditorUtility.SetDirty(data);
            }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("İçe aktarma sayfa dokusunu, sayfa sayısını ve eski şifre konumlarını (parça atanmamış bölge olarak) ekler.", MessageType.None);
    }

    // Eski kitap kimliğinin dokusunu ve konumlarını yeni veriye taşır (mevcut bölgeler korunur).
    public static void ImportPasswordData(BookClueData data, PasswordData source)
    {
        if (data == null || source == null) return;
        data.pageTexture = source.pageTexture;
        data.totalPages = source.totalPages;
        foreach (var location in source.possibleLocations)
            data.regions.Add(new BookClueRegion { note = location.note, pageIndex = location.pageIndex, regionUV = location.hotspotUV });
    }

    // Blockout test kitabı için: kitap kimliğinin dokusunu kullanan, parçaları yer tutucu bölgelere koyan veri.
    public static BookClueData CreateTestData(string path, PasswordData identity, IList<FormulaItemSO> parts)
    {
        var data = CreateInstance<BookClueData>();
        data.pageTexture = identity.pageTexture;
        data.totalPages = identity.totalPages;
        // Yer tutucu: kitap açıldığında ilk görünen sayfalara yerleştirilir; sonra görsel editörden ayarlanır.
        int[] pages = { 0, 1, 1 };
        Rect[] rects = { new Rect(0.2f, 0.55f, 0.6f, 0.25f), new Rect(0.2f, 0.55f, 0.6f, 0.25f), new Rect(0.2f, 0.15f, 0.6f, 0.25f) };
        for (int i = 0; i < parts.Count && i < pages.Length; i++)
            data.regions.Add(new BookClueRegion { note = "Yer tutucu", formula = parts[i], pageIndex = pages[i], regionUV = rects[i] });
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        return data;
    }

    private void HandleInput(Rect bounds, BookClueData data, int index)
    {
        Event e = Event.current;
        if (!bounds.Contains(e.mousePosition) && !isDragging) return;
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
            if (final.width < 2f || final.height < 2f) { e.Use(); return; }
            float uvW = final.width / bounds.width;
            float uvH = final.height / bounds.height;
            float uvX = (final.x - bounds.x) / bounds.width;
            float uvY = 1f - ((final.y - bounds.y) / bounds.height) - uvH;
            Undo.RecordObject(data, "Set Clue Region");
            data.regions[index].regionUV = new Rect(uvX, uvY, uvW, uvH);
            EditorUtility.SetDirty(data);
            e.Use();
        }
    }

    private static Rect FromPoints(Vector2 a, Vector2 b) =>
        new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private static Rect Clamp(Rect r, Rect b) =>
        new Rect(Mathf.Max(r.x, b.x), Mathf.Max(r.y, b.y),
            Mathf.Max(0f, Mathf.Min(r.xMax, b.xMax) - Mathf.Max(r.x, b.x)),
            Mathf.Max(0f, Mathf.Min(r.yMax, b.yMax) - Mathf.Max(r.y, b.y)));

    private static Rect UVToScreen(Rect uv, Rect b) =>
        new Rect(b.x + uv.x * b.width, b.y + (1 - (uv.y + uv.height)) * b.height, uv.width * b.width, uv.height * b.height);
}
#endif
