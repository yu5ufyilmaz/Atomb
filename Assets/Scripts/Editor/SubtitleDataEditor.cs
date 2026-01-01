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

    // Hangi entry'nin detaylarının açık olduğunu takip etmek için
    private static int expandedIndex = -1;

    public override void OnInspectorGUI()
    {
        // ScriptableObject verisini "SerializedObject" olarak ele alıyoruz
        serializedObject.Update();

        SerializedProperty entriesProp = serializedObject.FindProperty("entries");

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
        };

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🌍 GELİŞMİŞ ALTYAZI MERKEZİ", titleStyle);
        EditorGUILayout.HelpBox(
            "Sıralı Sistem Aktif: Cümleler listedeki sıraya göre, süreleri (Duration) kadar ekranda kalıp değişecektir.",
            MessageType.Info
        );
        EditorGUILayout.Space(10);

        // ARAMA
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(25)))
            searchText = "";
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // LİSTELEME
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(600));

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            SerializedProperty idProp = entry.FindPropertyRelative("id");
            SerializedProperty noteProp = entry.FindPropertyRelative("note");

            string idVal = idProp.stringValue;

            // Arama Filtresi
            if (
                !string.IsNullOrEmpty(searchText) && !idVal.ToLower().Contains(searchText.ToLower())
            )
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Başlık Çubuğu
            EditorGUILayout.BeginHorizontal();

            bool isExpanded = (expandedIndex == i);
            string arrow = isExpanded ? "▼" : "▶";
            string displayName = string.IsNullOrEmpty(idVal) ? "[İsimsiz]" : idVal;

            if (
                GUILayout.Button($"{arrow} {displayName}", EditorStyles.label, GUILayout.Height(24))
            )
            {
                expandedIndex = isExpanded ? -1 : i;
            }

            if (GUILayout.Button("Sil", GUILayout.Width(40)))
            {
                entriesProp.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            // Detay Görünümü
            if (expandedIndex == i)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(idProp, new GUIContent("ID (Kod)"));
                EditorGUILayout.PropertyField(noteProp, new GUIContent("Not"));

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(
                    "🗣️ Konuşma Parçaları (Segmentler)",
                    EditorStyles.boldLabel
                );

                SerializedProperty segmentsProp = entry.FindPropertyRelative("segments");

                // Segment Ekleme Butonu
                if (GUILayout.Button("+ Yeni Cümle Ekle"))
                {
                    segmentsProp.InsertArrayElementAtIndex(segmentsProp.arraySize);
                    SerializedProperty newSeg = segmentsProp.GetArrayElementAtIndex(
                        segmentsProp.arraySize - 1
                    );

                    // --- DEĞİŞİKLİK: startTime kaldırıldı ---
                    newSeg.FindPropertyRelative("duration").floatValue = 2f;
                    newSeg.FindPropertyRelative("textTR").stringValue = "";
                }

                // Segmentleri Listele
                for (int j = 0; j < segmentsProp.arraySize; j++)
                {
                    SerializedProperty segment = segmentsProp.GetArrayElementAtIndex(j);
                    DrawSegment(segment, j, segmentsProp);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // YENİ KAYIT BUTONU
        if (GUILayout.Button("➕ Yeni Diyalog Oluştur", GUILayout.Height(40)))
        {
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
            SerializedProperty newEntry = entriesProp.GetArrayElementAtIndex(
                entriesProp.arraySize - 1
            );
            newEntry.FindPropertyRelative("id").stringValue =
                "new_dialogue_" + entriesProp.arraySize;
            expandedIndex = entriesProp.arraySize - 1;
        }

        EditorGUILayout.Space(5);

        // KOD OLUŞTURUCU
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("🚀 KODLARI GÜNCELLE (SubtitleIDs.cs)", GUILayout.Height(30)))
        {
            GenerateCode((SubtitleData)target);
        }
        GUI.backgroundColor = Color.white;

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSegment(SerializedProperty segment, int index, SerializedProperty listProp)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            $"#{index + 1}",
            EditorStyles.miniBoldLabel,
            GUILayout.Width(30)
        );
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            listProp.DeleteArrayElementAtIndex(index);
            return;
        }
        EditorGUILayout.EndHorizontal();

        // --- DEĞİŞİKLİK: Sadece Süre (Duration) kaldı ---
        EditorGUILayout.BeginHorizontal();
        SerializedProperty durProp = segment.FindPropertyRelative("duration");

        // Başlangıç (Start Time) alanı SİLİNDİ.
        EditorGUILayout.LabelField("Ekranda Kalma (sn):", GUILayout.Width(120));
        durProp.floatValue = EditorGUILayout.FloatField(durProp.floatValue, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Diller
        DrawLanguageArea("TR 🇹🇷", segment.FindPropertyRelative("textTR"));
        DrawLanguageArea("EN 🇬🇧", segment.FindPropertyRelative("textEN"));
        DrawLanguageArea("DE 🇩🇪", segment.FindPropertyRelative("textDE"));

        EditorGUILayout.EndVertical();
    }

    private void DrawLanguageArea(string label, SerializedProperty textProp)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(50));
        textProp.stringValue = EditorGUILayout.TextArea(textProp.stringValue, GUILayout.Height(35));
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
            writer.WriteLine("// BU DOSYA OTOMATİK OLUŞTURULDU.");
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
        Debug.Log("✅ ID'ler güncellendi.");
    }
}
#endif
