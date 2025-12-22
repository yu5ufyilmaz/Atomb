#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoomManager))]
public class RoomEditor : Editor
{
    // Serialized Properties
    SerializedProperty roomNameProp;
    SerializedProperty isCorridorProp;
    SerializedProperty roomLightsProp;
    SerializedProperty isDangerousProp;
    SerializedProperty spawnPointsProp;
    SerializedProperty canGuderianSpawnProp;
    SerializedProperty roomDoorProp;
    SerializedProperty doorOutsidePointProp;
    SerializedProperty doorInsidePointProp;
    SerializedProperty hidingSpotsProp;
    SerializedProperty guderianPatrolPointsProp;

    // Pusu Özellikleri
    SerializedProperty allowAmbushProp;
    SerializedProperty ambushTimeoutProp;
    SerializedProperty ambushSpawnPointProp;

    // YENİ: Ses Özelliği
    SerializedProperty onFirstEnterSoundProp;

    private void OnEnable()
    {
        roomNameProp = serializedObject.FindProperty("roomName");
        isCorridorProp = serializedObject.FindProperty("isCorridor");
        roomLightsProp = serializedObject.FindProperty("roomLights");
        isDangerousProp = serializedObject.FindProperty("isDangerous");
        spawnPointsProp = serializedObject.FindProperty("spawnPoints");
        canGuderianSpawnProp = serializedObject.FindProperty("canGuderianSpawn");
        roomDoorProp = serializedObject.FindProperty("roomDoor");
        doorOutsidePointProp = serializedObject.FindProperty("doorOutsidePoint");
        doorInsidePointProp = serializedObject.FindProperty("doorInsidePoint");
        hidingSpotsProp = serializedObject.FindProperty("hidingSpots");
        guderianPatrolPointsProp = serializedObject.FindProperty("guderianPatrolPoints");

        allowAmbushProp = serializedObject.FindProperty("allowAmbush");
        ambushTimeoutProp = serializedObject.FindProperty("ambushTimeout");
        ambushSpawnPointProp = serializedObject.FindProperty("ambushSpawnPoint");

        // YENİ: Ses özelliğini bağla
        onFirstEnterSoundProp = serializedObject.FindProperty("onFirstEnterSound");
    }

    public override void OnInspectorGUI()
    {
        RoomManager room = (RoomManager)target;
        serializedObject.Update();

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

        // --- 1. GENEL ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("🏠 Genel Ayarlar", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(roomNameProp, new GUIContent("Oda ID"));
        EditorGUILayout.Space(5);
        GUI.backgroundColor = room.isCorridor ? Color.yellow : Color.white;
        EditorGUILayout.PropertyField(isCorridorProp, new GUIContent("Bu Bir Koridor Mu?"));
        if (room.isCorridor)
            EditorGUILayout.HelpBox("AdamAI sayacı burada DURAKLAYACAK.", MessageType.Info);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- 2. SES & ATMOSFER (YENİ EKLENDİ) ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("🔊 Ses & Atmosfer", EditorStyles.boldLabel);
        // DialogueEvent drawer'ı sayesinde burada Dropdown ve Clip alanı çıkacak
        EditorGUILayout.PropertyField(onFirstEnterSoundProp, new GUIContent("İlk Giriş Tanıtımı"));
        EditorGUILayout.HelpBox(
            "Oyuncu odaya ilk girdiğinde çalacak ses ve altyazı.",
            MessageType.None
        );
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- 3. IŞIK ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("💡 Elektrik & Işıklandırma", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(roomLightsProp, new GUIContent("Oda Işıkları"), true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- 4. LEES ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("👻 Lees Yapılandırması", EditorStyles.boldLabel);
        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = room.isDangerous
            ? new Color(1f, 0.4f, 0.4f)
            : new Color(0.4f, 1f, 0.4f);
        if (
            GUILayout.Button(
                room.isDangerous ? "DURUM: TEHLİKELİ BÖLGE" : "DURUM: GÜVENLİ BÖLGE",
                GUILayout.Height(30)
            )
        )
            room.isDangerous = !room.isDangerous;
        GUI.backgroundColor = defaultColor;

        if (room.isDangerous)
        {
            EditorGUILayout.Space(5);
            if (GUILayout.Button("📍 Yeni Lees Spawn Noktası Ekle", GUILayout.Height(25)))
                CreatePoint(room, "Lees_Spawn", "LeesSpawnPointParent", room.spawnPoints);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnPointsProp, new GUIContent("Spawn Noktaları"), true);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- 5. GUDERIAN ---
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("👹 Guderian Yapılandırması", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            canGuderianSpawnProp,
            new GUIContent("Guderian Gelebilir Mi?")
        );

        if (room.canGuderianSpawn)
        {
            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
            EditorGUILayout.HelpBox("Kapı Giriş/Çıkış noktalarını ayarlayın.", MessageType.Info);
            GUI.backgroundColor = defaultColor;

            EditorGUILayout.LabelField("1. Giriş Sistemi:", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(roomDoorProp, new GUIContent("Oda Kapısı"));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dışarı Noktası (Koridor)", GUILayout.Height(25)))
                CreateSinglePoint(room, "Door_Outside_Point", ref room.doorOutsidePoint);
            if (GUILayout.Button("İçeri Noktası (Oda)", GUILayout.Height(25)))
                CreateSinglePoint(room, "Door_Inside_Point", ref room.doorInsidePoint);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(doorOutsidePointProp);
            EditorGUILayout.PropertyField(doorInsidePointProp);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("2. Saklanma & Rota:", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(hidingSpotsProp, true);

            EditorGUILayout.Space(5);
            if (GUILayout.Button("👣 Yeni Devriye Noktası Ekle", GUILayout.Height(25)))
                CreatePoint(
                    room,
                    "Guderian_Patrol",
                    "GuderianSearchSpot",
                    room.guderianPatrolPoints
                );
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                guderianPatrolPointsProp,
                new GUIContent("Devriye Rota Listesi"),
                true
            );
            EditorGUI.indentLevel--;

            // --- PUSU SİSTEMİ ---
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("3. Pusu (Ceza) Sistemi:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(allowAmbushProp, new GUIContent("Pusu Aktif mi?"));

            if (allowAmbushProp.boolValue)
            {
                EditorGUILayout.PropertyField(
                    ambushTimeoutProp,
                    new GUIContent("Bekleme Süresi (Sn)")
                );
                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(ambushSpawnPointProp, new GUIContent("Pusu Konumu"));
                if (GUILayout.Button("➕ Oluştur", GUILayout.Width(70)))
                {
                    CreateSinglePoint(room, "Guderian_Ambush_Spot", ref room.ambushSpawnPoint);
                }
                EditorGUILayout.EndHorizontal();

                if (room.ambushSpawnPoint == null)
                    EditorGUILayout.HelpBox(
                        "Pusu Konumu yoksa varsayılan kapı önü kullanılır.",
                        MessageType.Warning
                    );
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
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
