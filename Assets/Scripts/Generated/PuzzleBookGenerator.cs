#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NaughtyAttributes;
using System.Collections.Generic;

public class PuzzleBookGenerator : MonoBehaviour
{
    [Header("1. Kitap Ayarları")]
    [Dropdown("GetBookPrefabs")]
    public GameObject selectedBookPrefab;

    [Button("Kitabı Spawn Et", EButtonEnableMode.Editor)]
    public void SpawnBook()
    {
        if (selectedBookPrefab == null)
        {
            Debug.LogWarning("Lütfen listeden bir kitap seçin!");
            return;
        }
        if (currentBookInstance != null)
            DestroyImmediate(currentBookInstance);

        currentBookInstance = PrefabUtility.InstantiatePrefab(selectedBookPrefab) as GameObject;
        currentBookInstance.transform.position = transform.position;
        currentBookInstance.name = "GENERATOR_BOOK_PREVIEW";

        InteractableBook bookScript = currentBookInstance.GetComponent<InteractableBook>();
        if (bookScript != null)
        {
            bookScript.initialState = InteractableBook.BookInitialState.Open;
            bookScript.startPageIndex = targetPage;
            bookScript.PreviewBookInEditor();
        }
    }

    [Header("2. Sembol Ayarları")]
    [Dropdown("GetSymbolPrefabs")]
    public GameObject selectedSymbolPrefab;

    [Button("Sembolü Spawn Et", EButtonEnableMode.Editor)]
    public void SpawnSymbol()
    {
        if (currentBookInstance == null)
        {
            Debug.LogWarning("Önce Kitabı spawn etmelisiniz!");
            return;
        }
        if (selectedSymbolPrefab == null)
        {
            Debug.LogWarning("Lütfen listeden bir sembol seçin!");
            return;
        }

        if (currentSymbolInstance != null)
            DestroyImmediate(currentSymbolInstance);

        currentSymbolInstance = PrefabUtility.InstantiatePrefab(selectedSymbolPrefab) as GameObject;

        // Sembolü PuzzleAnchor içine veya kitabın içine yerleştir
        Transform existingAnchor = currentBookInstance.transform.Find("PuzzleAnchor");
        if (existingAnchor != null)
            currentSymbolInstance.transform.SetParent(existingAnchor, false);
        else
            currentSymbolInstance.transform.SetParent(currentBookInstance.transform, false);

        currentSymbolInstance.name = "TEMP_SYMBOL_PREVIEW";
        Debug.Log(
            "Sembol oluşturuldu. Lütfen sahne üzerinde sembolü istediğiniz yere taşıyıp boyutlandırın."
        );
    }

    [Header("3. Bulmaca Verileri & Kayıt")]
    public int targetPage = 4;
    public string puzzlePasswordID = "symbol_01";

    [ReadOnly]
    public GameObject currentBookInstance;

    [ReadOnly]
    public GameObject currentSymbolInstance;

    [Button("3. Prefab Olarak Kaydet", EButtonEnableMode.Editor)]
    private void GeneratePuzzleObject()
    {
        if (currentBookInstance == null)
        {
            Debug.LogWarning("Önce kitabı spawn etmelisiniz!");
            return;
        }

        // 1. PuzzleReceiver'ı bul veya ekle
        PuzzleReceiver receiver = currentBookInstance.GetComponent<PuzzleReceiver>();
        if (receiver == null)
        {
            receiver = currentBookInstance.AddComponent<PuzzleReceiver>();
        }

        // 2. Anchor objesini ayarla
        Transform existingAnchor = currentBookInstance.transform.Find("PuzzleAnchor");
        if (existingAnchor == null)
        {
            GameObject anchorObj = new GameObject("PuzzleAnchor");
            anchorObj.transform.SetParent(currentBookInstance.transform);
            anchorObj.transform.localPosition = Vector3.zero;
            anchorObj.transform.localRotation = Quaternion.identity;
            receiver.puzzleAnchor = anchorObj.transform;
        }
        else
        {
            receiver.puzzleAnchor = existingAnchor;
        }

        // 3. Verileri Receiver'a aktar
        receiver.requiredItemID = puzzlePasswordID;

        // DÜZELTME 1: Artık -1 yazmıyoruz, senin Inspector'dan girdiğin sayfayı alıyor!
        receiver.targetPage = targetPage;

        // 4. Eğer oyuncu sembolü eliyle yerleştirdiyse, koordinatları ve boyutları direkt al
        if (currentSymbolInstance != null)
        {
            receiver.targetLocalPosition = currentSymbolInstance.transform.localPosition;
            receiver.targetLocalRotation = currentSymbolInstance.transform.localEulerAngles;

            // Kaydettikten sonra geçici sembolü sil
            DestroyImmediate(currentSymbolInstance);
            currentSymbolInstance = null;
        }

        // DÜZELTME 2: KAYDETMEDEN ÖNCE KİTABI OTOMATİK KAPAT VE SIFIRLA
        InteractableBook bookScript = currentBookInstance.GetComponent<InteractableBook>();
        if (bookScript != null)
        {
            bookScript.initialState = InteractableBook.BookInitialState.Closed;
            bookScript.startPageIndex = 0; // Varsayılan kapalı duruma getirir
            bookScript.PreviewBookInEditor();
        }

        // 5. Prefab olarak kaydet
        string folderPath = "Assets/Prefabs/GeneratedPuzzles";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        string localPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{folderPath}/{currentBookInstance.name}_{puzzlePasswordID}.prefab"
        );
        bool success;
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            currentBookInstance,
            localPath,
            InteractionMode.UserAction,
            out success
        );

        if (success)
        {
            Debug.Log(
                $"[Jeneratör] Başarılı! Yeni bulmaca kitabı kapalı halde kaydedildi: {localPath}"
            );
        }
        else
        {
            Debug.LogError("[Jeneratör] Kayıt sırasında bir hata oluştu.");
        }
    }

    [Button("Sahneyi Temizle (Clear Scene)", EButtonEnableMode.Editor)]
    public void ClearScene()
    {
        if (currentSymbolInstance != null)
        {
            DestroyImmediate(currentSymbolInstance);
            currentSymbolInstance = null;
        }
        if (currentBookInstance != null)
        {
            DestroyImmediate(currentBookInstance);
            currentBookInstance = null;
        }
        Debug.Log("Sahnedeki geçici jeneratör objeleri temizlendi.");
    }

    #region Otomatik Klasör Okuma (Dropdown Listeleri)
    private DropdownList<GameObject> GetBookPrefabs()
    {
        return LoadPrefabsFromFolder("Assets/Prefabs/Interactables");
    }

    private DropdownList<GameObject> GetSymbolPrefabs()
    {
        return LoadPrefabsFromFolder("Assets/Prefabs/PuzzleSymbols");
    }

    private DropdownList<GameObject> LoadPrefabsFromFolder(string folderPath)
    {
        DropdownList<GameObject> list = new DropdownList<GameObject>();
        list.Add("Seçiniz...", null);
        if (!AssetDatabase.IsValidFolder(folderPath))
            return list;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                list.Add(prefab.name, prefab);
            }
        }
        return list;
    }
    #endregion
}
#endif
