#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PuzzleGeneratorWindow : EditorWindow
{
    // Arama yapılacak klasör yolları (Kendi klasör yapına göre burayı güncelleyebilirsin)
    private readonly string interactablesFolderPath = "Assets/Prefabs/Interactables";
    private readonly string puzzleSymbolsFolderPath = "Assets/Scripts/Scriptables/PuzzleItems";

    // Dropdown listeleri için veriler
    private List<GameObject> basePrefabs = new List<GameObject>();
    private string[] basePrefabNames;
    private int selectedPrefabIndex = 0;

    private List<PuzzleItemSO> symbolSOs = new List<PuzzleItemSO>();
    private string[] symbolNames;
    private int selectedSymbolIndex = 0;

    [MenuItem("Senzora/Puzzle Üretici (Dropdown)")]
    public static void ShowWindow()
    {
        var window = GetWindow<PuzzleGeneratorWindow>("Puzzle Generator");
        window.minSize = new Vector2(350, 200);
        window.LoadAssets(); // Pencere açıldığında klasörleri tara
    }

    private void OnEnable()
    {
        LoadAssets();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        // Üst Kısım ve Yenile Butonu
        GUILayout.BeginHorizontal();
        GUILayout.Label("Yeni Bulmaca Objesi Üret", EditorStyles.boldLabel);
        if (GUILayout.Button("Klasörleri Yenile", GUILayout.Width(120)))
        {
            LoadAssets();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(15);

        // 1. Dropdown: Temel Prefab (Interactables)
        if (basePrefabNames != null && basePrefabNames.Length > 0)
        {
            selectedPrefabIndex = EditorGUILayout.Popup(
                "1. Temel Model (Prefab)",
                selectedPrefabIndex,
                basePrefabNames
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"{interactablesFolderPath} klasöründe Prefab bulunamadı!",
                MessageType.Warning
            );
        }

        GUILayout.Space(10);

        // 2. Dropdown: Sembol SO (PuzzleItems)
        if (symbolNames != null && symbolNames.Length > 0)
        {
            selectedSymbolIndex = EditorGUILayout.Popup(
                "2. Sembol (PuzzleItemSO)",
                selectedSymbolIndex,
                symbolNames
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"{puzzleSymbolsFolderPath} klasöründe PuzzleItemSO bulunamadı!",
                MessageType.Warning
            );
        }

        GUILayout.Space(25);

        // Üretim Butonu
        GUI.enabled = (basePrefabs.Count > 0 && symbolSOs.Count > 0); // Listeler boşsa butonu pasif yap
        if (GUILayout.Button("Sahneye Üret ve Kur", GUILayout.Height(40)))
        {
            GeneratePuzzleObject();
        }
        GUI.enabled = true;
    }

    private void LoadAssets()
    {
        // 1. Interactables Prefab'larını Yükle
        basePrefabs.Clear();
        string[] prefabGUIDs = AssetDatabase.FindAssets(
            "t:GameObject",
            new[] { interactablesFolderPath }
        );
        foreach (string guid in prefabGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (obj != null)
                basePrefabs.Add(obj);
        }
        basePrefabNames = basePrefabs.ConvertAll(p => p.name).ToArray();

        // 2. PuzzleItemSO verilerini Yükle
        symbolSOs.Clear();
        string[] symbolGUIDs = AssetDatabase.FindAssets(
            "t:PuzzleItemSO",
            new[] { puzzleSymbolsFolderPath }
        );
        foreach (string guid in symbolGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PuzzleItemSO so = AssetDatabase.LoadAssetAtPath<PuzzleItemSO>(path);
            if (so != null)
                symbolSOs.Add(so);
        }
        symbolNames = symbolSOs.ConvertAll(s => s.itemID + " (" + s.name + ")").ToArray();
    }

    private void GeneratePuzzleObject()
    {
        GameObject selectedPrefab = basePrefabs[selectedPrefabIndex];
        PuzzleItemSO selectedSymbol = symbolSOs[selectedSymbolIndex];

        // 1. Prefab'ı sahneye koy
        GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
        if (SceneView.lastActiveSceneView != null)
        {
            newObj.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        // İsimlendirme
        newObj.name = $"{selectedPrefab.name}_Puzzle_{selectedSymbol.itemID}";

        // 2. PuzzleReceiver scriptini tak
        PuzzleReceiver receiver = newObj.GetComponent<PuzzleReceiver>();
        if (receiver == null)
        {
            receiver = newObj.AddComponent<PuzzleReceiver>();
        }

        // 3. ID'yi SO'dan çekip otomatik ata
        receiver.requiredItemID = selectedSymbol.itemID;

        // 4. Yüzey Anchor'ını otomatik oluştur (Yoksa)
        if (receiver.puzzleAnchor == null)
        {
            GameObject anchorObj = new GameObject("PuzzleAnchor");
            anchorObj.transform.SetParent(newObj.transform);
            anchorObj.transform.localPosition = Vector3.zero;
            anchorObj.transform.localRotation = Quaternion.identity;
            receiver.puzzleAnchor = anchorObj.transform;
        }

        // 5. Sembolün 3D Modelini (Önizlemeyi) Anchor'ın altına yerleştir
        if (selectedSymbol.overlayPrefab != null)
        {
            GameObject previewInstance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    selectedSymbol.overlayPrefab,
                    receiver.puzzleAnchor
                );

            // Hafif öne al ve SO içindeki varsayılan değerleri uygula
            previewInstance.transform.localPosition = new Vector3(0, 0, -0.005f);
            previewInstance.transform.localRotation = Quaternion.Euler(
                selectedSymbol.defaultRotationOffset
            );

            // PuzzleReceiver'daki "Geliştirici Araçları" kısmına bu objeyi otomatik bağla
            receiver.debugPreviewSymbol = previewInstance.transform;
        }
        else
        {
            Debug.LogWarning(
                $"[Puzzle Üretici] {selectedSymbol.name} içinde 'Overlay Prefab' atanmamış! Lütfen SO'yu kontrol et."
            );
        }

        // 6. Objeyi seçili hale getir
        Selection.activeGameObject = newObj;

        Debug.Log(
            $"[{newObj.name}] başarıyla üretildi! Sembol ID: {selectedSymbol.itemID}. Şimdi sembolü hizalayıp kaydedebilirsin."
        );
    }
}
#endif
