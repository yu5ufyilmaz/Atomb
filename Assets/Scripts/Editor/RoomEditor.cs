#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoomManager))]
public class RoomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RoomManager room = (RoomManager)target;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
        };
        GUIStyle sectionStyle = new GUIStyle(EditorStyles.helpBox);

        EditorGUILayout.Space(10);
        Rect rect = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
        EditorGUI.LabelField(rect, room.roomName.ToUpper(), titleStyle);
        EditorGUILayout.Space(5);

        // --- GENEL AYARLAR ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("🏠 Genel Ayarlar", EditorStyles.boldLabel);
        room.roomName = EditorGUILayout.TextField("Oda ID", room.roomName);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- IŞIK SİSTEMİ (ARTIK BURADA) ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("💡 Elektrik & Işıklandırma", EditorStyles.boldLabel);
        // Guderian olsun olmasın, ışıklar her zaman ayarlanabilir
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("roomLights"),
            new GUIContent("Oda Işıkları"),
            true
        );
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- LEES BÖLÜMÜ ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("👻 Lees Yapılandırması", EditorStyles.boldLabel);

        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = room.isDangerous
            ? new Color(1f, 0.4f, 0.4f)
            : new Color(0.4f, 1f, 0.4f);
        string statusText = room.isDangerous ? "DURUM: TEHLİKELİ BÖLGE" : "DURUM: GÜVENLİ BÖLGE";

        if (GUILayout.Button(statusText, GUILayout.Height(30)))
        {
            room.isDangerous = !room.isDangerous;
        }
        GUI.backgroundColor = defaultColor;

        if (room.isDangerous)
        {
            EditorGUILayout.Space(5);
            if (GUILayout.Button("📍 Yeni Lees Spawn Noktası Ekle", GUILayout.Height(25)))
                CreatePoint(room, "Lees_Spawn", "LeesSpawnPointParent", room.spawnPoints);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("spawnPoints"),
                new GUIContent("Spawn Noktaları"),
                true
            );
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- GUDERIAN BÖLÜMÜ ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("👹 Guderian Yapılandırması", EditorStyles.boldLabel);

        room.canGuderianSpawn = EditorGUILayout.Toggle(
            "Guderian Gelebilir Mi?",
            room.canGuderianSpawn
        );

        if (room.canGuderianSpawn)
        {
            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
            EditorGUILayout.HelpBox("Kapı Giriş/Çıkış noktalarını ayarlayın.", MessageType.Info);
            GUI.backgroundColor = defaultColor;

            EditorGUILayout.LabelField("1. Giriş Sistemi:", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("roomDoor"),
                new GUIContent("Oda Kapısı")
            );

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dışarı Noktası (Koridor)", GUILayout.Height(25)))
                CreateSinglePoint(room, "Door_Outside_Point", ref room.doorOutsidePoint);

            if (GUILayout.Button("İçeri Noktası (Oda)", GUILayout.Height(25)))
                CreateSinglePoint(room, "Door_Inside_Point", ref room.doorInsidePoint);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("doorOutsidePoint"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("doorInsidePoint"));

            EditorGUILayout.Space(5);
            // IŞIKLAR BURADAN KALDIRILDI!
            EditorGUILayout.LabelField("2. Saklanma & Rota:", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hidingSpots"), true);

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("👣 Yeni Devriye Noktası Ekle", GUILayout.Height(25)))
                CreatePoint(
                    room,
                    "Guderian_Patrol",
                    "GuderianSearchSpot",
                    room.guderianPatrolPoints
                );
            GUI.backgroundColor = defaultColor;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("guderianPatrolPoints"),
                new GUIContent("Devriye Rota Listesi"),
                true
            );
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(room);
            serializedObject.ApplyModifiedProperties();
        }
    }

    void CreatePoint(
        RoomManager room,
        string baseName,
        string parentName,
        System.Collections.Generic.List<Transform> list
    )
    {
        Transform parentTransform = room.transform.Find(parentName);
        if (parentTransform == null)
        {
            GameObject parentObj = new GameObject(parentName);
            parentObj.transform.SetParent(room.transform);
            parentObj.transform.localPosition = Vector3.zero;
            parentTransform = parentObj.transform;
            Undo.RegisterCreatedObjectUndo(parentObj, "Create Parent");
        }
        GameObject newPoint = new GameObject($"{baseName}_{list.Count + 1}");
        newPoint.transform.SetParent(parentTransform);
        newPoint.transform.localPosition = Vector3.zero;
        list.Add(newPoint.transform);
        Undo.RegisterCreatedObjectUndo(newPoint, "Create Point");
        Selection.activeGameObject = newPoint;
    }

    void CreateSinglePoint(RoomManager room, string name, ref Transform targetField)
    {
        if (targetField != null)
        {
            Selection.activeGameObject = targetField.gameObject;
            return;
        }

        GameObject newPoint = new GameObject(name);
        newPoint.transform.SetParent(room.transform);
        newPoint.transform.localPosition = new Vector3(0, 0, 2);
        targetField = newPoint.transform;
        Undo.RegisterCreatedObjectUndo(newPoint, "Create Single Point");
        Selection.activeGameObject = newPoint;
    }
}
#endif
