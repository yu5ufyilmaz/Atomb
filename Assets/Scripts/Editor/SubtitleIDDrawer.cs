#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

[CustomPropertyDrawer(typeof(SubtitleIDSelectionAttribute))]
public class SubtitleIDDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // SubtitleIDs sınıfını bulmaya çalış
        System.Type type = System.Type.GetType("SubtitleIDs, Assembly-CSharp");

        if (type == null)
        {
            // Eğer sınıf yoksa (henüz oluşturmadıysan) düz yazı kutusu göster
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Sınıfın içindeki tüm 'const string' alanlarını çek
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
        );

        List<string> options = new List<string>();
        options.Add("NONE"); // Boş seçenek

        foreach (var field in fields)
        {
            if (field.IsLiteral && !field.IsInitOnly) // const kontrolü
                options.Add(field.Name);
        }

        // Mevcut seçili değeri bul
        string currentString = property.stringValue;
        int index = 0;

        // Şu anki değer listede var mı? Varsa indexini bul
        for (int i = 0; i < fields.Length; i++)
        {
            string val = (string)fields[i].GetRawConstantValue();
            if (val == currentString)
            {
                index = i + 1; // +1 çünkü başta "NONE" var
                break;
            }
        }

        // Dropdown çiz
        int newIndex = EditorGUI.Popup(position, label.text, index, options.ToArray());

        // Yeni seçimi kaydet
        if (newIndex == 0)
        {
            property.stringValue = "";
        }
        else
        {
            // Seçilen ismin gerçek değerini (ID stringini) al
            string selectedFieldName = options[newIndex];
            FieldInfo selectedField = type.GetField(selectedFieldName);
            if (selectedField != null)
            {
                property.stringValue = (string)selectedField.GetRawConstantValue();
            }
        }
    }
}
#endif
