#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

[CustomEditor(typeof(SubtitleData))]
public class SubtitleDataEditor : Editor
{
    private string searchText = "";
    private Vector2 scrollPos;

    public override void OnInspectorGUI()
    {
        SubtitleData data = (SubtitleData)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
        };
        GUIStyle boxStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10),
        };

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🌍 GLOBAL ALTYAZI MERKEZİ", titleStyle);
        EditorGUILayout.Space(10);

        // ARAMA
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("Temizle", EditorStyles.toolbarButton, GUILayout.Width(50)))
            searchText = "";
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // LİSTELEME
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(400));

        for (int i = 0; i < data.entries.Count; i++)
        {
            SubtitleEntry entry = data.entries[i];

            if (
                !string.IsNullOrEmpty(searchText)
                && !entry.id.ToLower().Contains(searchText.ToLower())
            )
                continue;

            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🔑 {entry.id}", EditorStyles.boldLabel);
            if (GUILayout.Button("Sil", GUILayout.Width(50)))
            {
                data.entries.RemoveAt(i);
                EditorUtility.SetDirty(data);
                return;
            }
            EditorGUILayout.EndHorizontal();

            entry.id = EditorGUILayout.TextField("ID (Kod)", entry.id);
            entry.note = EditorGUILayout.TextField("Not", entry.note);
            entry.duration = EditorGUILayout.FloatField("Süre (Ses Yoksa)", entry.duration);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Çeviriler:", EditorStyles.miniBoldLabel);

            // Diller
            DrawLanguageField("TR 🇹🇷", ref entry.textTR);
            DrawLanguageField("EN 🇬🇧", ref entry.textEN);
            DrawLanguageField("DE 🇩🇪", ref entry.textDE);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        // BUTONLAR
        EditorGUILayout.Space(10);
        if (GUILayout.Button("➕ Yeni Altyazı Ekle", GUILayout.Height(40)))
        {
            data.entries.Add(new SubtitleEntry { id = "new_entry_" + data.entries.Count });
            EditorUtility.SetDirty(data);
        }

        EditorGUILayout.Space(5);

        // KOD OLUŞTURUCU BUTONU
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("🚀 KODLARI OLUŞTUR (SubtitleIDs.cs)", GUILayout.Height(40)))
        {
            GenerateCode(data);
        }
        GUI.backgroundColor = Color.white;

        if (GUI.changed)
            EditorUtility.SetDirty(data);
    }

    private void DrawLanguageField(string label, ref string text)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(40));
        text = EditorGUILayout.TextArea(text, GUILayout.Height(40));
        EditorGUILayout.EndHorizontal();
    }

    private void GenerateCode(SubtitleData data)
    {
        string path = "Assets/Scripts/Generated";
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string filePath = path + "/SubtitleIDs.cs";

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("// BU DOSYA OTOMATİK OLUŞTURULDU. ELLE DEĞİŞTİRME!");
            writer.WriteLine("public static class SubtitleIDs");
            writer.WriteLine("{");

            foreach (var entry in data.entries)
            {
                if (string.IsNullOrEmpty(entry.id))
                    continue;
                string varName = Regex.Replace(entry.id, @"\s+", "_");
                varName = Regex.Replace(varName, @"[^a-zA-Z0-9_]", "");
                if (char.IsDigit(varName[0]))
                    varName = "_" + varName;

                writer.WriteLine($"    public const string {varName} = \"{entry.id}\";");
            }
            writer.WriteLine("}");
        }
        AssetDatabase.Refresh();
        Debug.Log($"✅ Kodlar başarıyla güncellendi: {filePath}");
    }
}
#endif
