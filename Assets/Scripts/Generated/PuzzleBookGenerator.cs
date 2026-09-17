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

        // Editörde kitabın kapağını açalım ki içine rahatça sembolü yerleştirelim
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
        currentSymbolInstance.transform.SetParent(currentBookInstance.transform, false);
        currentSymbolInstance.name = "TEMP_SYMBOL_PREVIEW";

        Debug.Log(
            "Sembol oluşturuldu. Lütfen sahne üzerinde sembolü istediğiniz yere taşıyıp döndürün."
        );
    }

    [Header("3. Bulmaca Verileri & Kayıt")]
    public int targetPage = 4;
    public string puzzlePasswordID = "SYMBOL_01";

    [ReadOnly]
    public GameObject currentBookInstance;

    [ReadOnly]
    public GameObject currentSymbolInstance;

    [Button("3. Prefab Olarak Kaydet", EButtonEnableMode.Editor)]
    public void SavePrefab()
    {
        if (currentBookInstance == null || currentSymbolInstance == null)
        {
            Debug.LogWarning(
                "Kayıt yapabilmek için hem kitabın hem de sembolün spawn edilmiş olması gerekir!"
            );
            return;
        }

        InteractableBook bookScript = currentBookInstance.GetComponent<InteractableBook>();
        if (bookScript != null)
        {
            // Anchor yoksa otomatik oluştur
            if (bookScript.targetSymbolAnchor == null)
            {
                GameObject anchorObj = new GameObject("SymbolAnchor");
                anchorObj.transform.SetParent(currentBookInstance.transform);
                bookScript.targetSymbolAnchor = anchorObj.transform;
            }

            // EŞİTLEME (MÜKEMMEL MANTIK): Anchor'ın pozisyon ve açısını senin ayarladığın sembolle aynı yapıyoruz.
            bookScript.targetSymbolAnchor.position = currentSymbolInstance.transform.position;
            bookScript.targetSymbolAnchor.rotation = currentSymbolInstance.transform.rotation;

            // Kitap ayarlarını yapıyoruz
            bookScript.isSymbolTargetBook = true;
            bookScript.symbolPuzzlePage = targetPage;
            bookScript.AssignPuzzlePassword(puzzlePasswordID);
        }

        // Sembolü siliyoruz, çünkü oyunda zaten runtime'da spawn olacak.
        DestroyImmediate(currentSymbolInstance);

        // Klasör kontrolü
        string folderPath = "Assets/Prefabs/GeneratedPuzzles";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentFolder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(parentFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder(parentFolder, "GeneratedPuzzles");
        }

        // Kaydet
        string savePath =
            $"{folderPath}/{selectedBookPrefab.name}_Page{targetPage}_{puzzlePasswordID}.prefab";
        savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

        PrefabUtility.SaveAsPrefabAssetAndConnect(
            currentBookInstance,
            savePath,
            InteractionMode.UserAction
        );
        Debug.Log(
            $"<color=green>Başarılı!</color> Yeni puzzle kitabı şuraya kaydedildi: {savePath}"
        );
    }

    [Button("Sahneyi Temizle (Clear Scene)", EButtonEnableMode.Editor)]
    public void ClearScene()
    {
        // Önce sembolü sil
        if (currentSymbolInstance != null)
        {
            DestroyImmediate(currentSymbolInstance);
            currentSymbolInstance = null;
        }

        // Sonra kitabı sil
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
        list.Add("Seçiniz...", null); // Boş seçenek

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
