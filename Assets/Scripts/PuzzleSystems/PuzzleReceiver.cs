using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PuzzleReceiver : MonoBehaviour
{
    public enum SurfacePlane
    {
        Dikey_Pano_XY, // Duvardaki tablolar veya dik duran kapaklar (Z kilitlenir)
        Yatay_Masa_XZ, // Masada yatan kitaplar veya zemin (Y kilitlenir)
    }

    [Header("Bulmaca Ayarları")]
    public string requiredItemID;
    public Transform puzzleAnchor;
    public int targetPage = -1;

    [Header("Spawn (Doğma) Ayarları")]
    [Tooltip("Sembolün yüzeyden ne kadar havada duracağı")]
    public float hoverDistance = -0.005f;
    public float customScaleMultiplier = 1f;
    public Vector3 customSpawnRotation = Vector3.zero;

    [Header("Eksen ve Hareket Ayarları")]
    [Tooltip("Yüzeyin duruşunu seçin. Yanlış eksende gidiyorsa bunu değiştirin.")]
    public SurfacePlane puzzlePlane = SurfacePlane.Dikey_Pano_XY;

    public float moveSpeed = 0.005f;
    public float rotationSpeed = 15f;
    public Vector3 scrollRotationAxis = new Vector3(0, 0, 1);

    [Tooltip("X ve Y/Z eksenlerindeki maksimum kayma sınırları")]
    public Vector2 movementLimits = new Vector2(0.5f, 0.5f);

    [Header("Çözüm Koordinatları (Lokal)")]
    public Vector3 targetLocalPosition;
    public Vector3 targetLocalRotation;
    public Vector3 targetLocalScale = Vector3.zero;

    [Header("Toleranslar")]
    public float distanceTolerance = 0.05f;
    public float angleTolerance = 15f;

    [Header("Tetiklenecek Olaylar")]
    public UnityEvent OnPuzzleSolved;

    [Header("Geliştirici Araçları")]
    public Transform debugPreviewSymbol;

    [HideInInspector]
    public bool isManipulating = false;
    private bool isSolved = false;
    private GameObject activeSymbolInstance;
    private InteractableBook linkedBook;

    private void Start()
    {
        linkedBook = GetComponent<InteractableBook>();
    }

    private Transform GetActiveAnchor()
    {
        // Artık kitaba gidip anchor aramıyor. Sadece kendi içindeki "puzzleAnchor"a bakıyor.
        if (puzzleAnchor != null)
            return puzzleAnchor;

        return transform;
    }

    // ==========================================
    // EDİTÖR ARAÇLARI (DÜZENLEME VE KAYDETME)
    // ==========================================
#if UNITY_EDITOR
    [Button("👁️ 1. Önizlemeyi Yükle (Düzenlemek İçin)", EButtonEnableMode.Editor)]
    private void LoadPreviewForEditing()
    {
        if (debugPreviewSymbol != null)
            return;
        if (string.IsNullOrEmpty(requiredItemID))
            return;

        string[] guids = AssetDatabase.FindAssets("t:PuzzleItemSO");
        PuzzleItemSO foundSO = null;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PuzzleItemSO so = AssetDatabase.LoadAssetAtPath<PuzzleItemSO>(path);
            if (so != null && so.itemID == requiredItemID)
            {
                foundSO = so;
                break;
            }
        }

        if (foundSO != null && foundSO.overlayPrefab != null)
        {
            Transform anchorToUse = GetActiveAnchor();
            GameObject preview = (GameObject)
                PrefabUtility.InstantiatePrefab(foundSO.overlayPrefab, anchorToUse);

            preview.transform.localPosition = targetLocalPosition;
            preview.transform.localEulerAngles = targetLocalRotation;

            debugPreviewSymbol = preview.transform;
            Selection.activeGameObject = preview;

            // ÖNİZLEME YÜKLENDİĞİ AN KİTABI OTOMATİK OLARAK AÇAR
            InteractableBook bookScript = GetComponent<InteractableBook>();
            if (bookScript != null)
            {
                bookScript.initialState = InteractableBook.BookInitialState.Open;
                bookScript.startPageIndex = targetPage >= 0 ? targetPage : 0;
                bookScript.PreviewBookInEditor();
            }
        }
    }

    [Button("💾 2. Mevcut Prefabı Güncelle (Save)", EButtonEnableMode.Editor)]
    private void SaveExistingPrefab()
    {
        RecordAndClean(); // Koordinatları al ve sembolü sil
        ForceCloseBook(); // KİTABI ZORLA KAPAT

        string localPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

        // Eğer obje bir prefab değilse veya orijinal base prefab ise engelle!
        if (string.IsNullOrEmpty(localPath) || !localPath.Contains("GeneratedPuzzles"))
        {
            Debug.LogError(
                "[PuzzleReceiver] DİKKAT: Bu obje ana model! Ana modelin üzerine yazamazsınız. Lütfen '3. Yeni Prefab Olarak Çıkart (Save As)' butonunu kullanın."
            );
            return;
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(
            gameObject,
            localPath,
            InteractionMode.UserAction,
            out _
        );
        Debug.Log($"[PuzzleReceiver] Mevcut Prefab KAPALI OLARAK GÜNCELLENDİ: {localPath}");
    }

    [Button("🆕 3. Yeni Prefab Olarak Çıkart (Save As)", EButtonEnableMode.Editor)]
    private void SaveAsNewPrefab()
    {
        RecordAndClean(); // Koordinatları al ve sembolü sil
        ForceCloseBook(); // KİTABI ZORLA KAPAT

        string folderPath = "Assets/Prefabs/GeneratedPuzzles";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
        {
            PrefabUtility.UnpackPrefabInstance(
                gameObject,
                PrefabUnpackMode.OutermostRoot,
                InteractionMode.UserAction
            );
        }

        string baseName = gameObject.name.Replace("(Clone)", "").Trim();
        string localPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{folderPath}/{baseName}_{requiredItemID}.prefab"
        );

        bool prefabSuccess;
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            gameObject,
            localPath,
            InteractionMode.UserAction,
            out prefabSuccess
        );

        if (prefabSuccess)
            Debug.Log($"[PuzzleReceiver] YENİ Prefab Kapalı Olarak Oluşturuldu: {localPath}");
        else
            Debug.LogError("[PuzzleReceiver] Prefab oluşturulurken hata oluştu!");
    }

    private void RecordAndClean()
    {
        // Eğer sahnede bir sembol varsa konumunu kaydet ve onu sil
        if (debugPreviewSymbol != null)
        {
            Undo.RecordObject(this, "Save Puzzle Target Pos");
            targetLocalPosition = debugPreviewSymbol.localPosition;
            targetLocalRotation = debugPreviewSymbol.localEulerAngles;

            GameObject previewObj = debugPreviewSymbol.gameObject;
            debugPreviewSymbol = null;
            DestroyImmediate(previewObj);
        }
    }

    private void ForceCloseBook()
    {
        // Kitabı bul
        InteractableBook bookScript = GetComponent<InteractableBook>();
        if (bookScript != null)
        {
            // --- HARİKA DOKUNUŞ BURADA ---
            // Kitabı kapatmadan hemen önce o anki açık olan sayfayı Hedef Sayfa (targetPage) olarak otomatik kaydet!
            Undo.RecordObject(this, "Save Target Page");
            targetPage = bookScript.startPageIndex;

            // Sonra kitabı kapalı hale getirip sıfırla
            bookScript.initialState = InteractableBook.BookInitialState.Closed;
            bookScript.startPageIndex = 0;
            bookScript.PreviewBookInEditor();

            // Unity'nin bu değişikliği algılayıp kaydetmesini garantile
            EditorUtility.SetDirty(bookScript);
            EditorUtility.SetDirty(this);
        }
    }
#endif

    public void ToggleSymbolMode()
    {
        if (isSolved)
            return;

        if (isManipulating)
        {
            CloseSymbol();
        }
        else
        {
            if (
                PuzzleInventoryManager.Instance != null
                && PuzzleInventoryManager.Instance.HasActiveItem()
            )
            {
                isManipulating = true;
                PuzzleItemSO item = PuzzleInventoryManager.Instance.activeItem;

                if (activeSymbolInstance == null)
                {
                    Transform anchorToUse = GetActiveAnchor();
                    activeSymbolInstance = Instantiate(item.overlayPrefab, anchorToUse);

                    // Seçilen düzleme göre Hover eksenini ayarla
                    if (puzzlePlane == SurfacePlane.Dikey_Pano_XY)
                        activeSymbolInstance.transform.localPosition = new Vector3(
                            0,
                            0,
                            hoverDistance
                        );
                    else
                        activeSymbolInstance.transform.localPosition = new Vector3(
                            0,
                            hoverDistance,
                            0
                        );

                    Vector3 startingRot = targetLocalRotation;
                    startingRot.z += customSpawnRotation.z;
                    activeSymbolInstance.transform.localEulerAngles = startingRot;

                    // BOYUT UYGULAMA SATIRLARINI SİLDİK. Sembol prefabın kendi boyutunda çıkacak.
                }
                activeSymbolInstance.SetActive(true);
            }
        }
    }

    public void CloseSymbol()
    {
        isManipulating = false;
        if (activeSymbolInstance != null)
            activeSymbolInstance.SetActive(false);
    }

    private void Update()
    {
        // 1. OYUN DURAKLATILDIYSA İŞLEM YAPMA
        if (GameManager.Instance != null && GameManager.Instance.isGamePaused)
            return;

        if (!isManipulating || isSolved || activeSymbolInstance == null)
            return;

        HandleManipulation();
        CheckSuccess();
    }

    private void HandleManipulation()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        float scroll = Input.mouseScrollDelta.y;

        Vector3 currentLocalPos = activeSymbolInstance.transform.localPosition;

        // Düzlem seçimine göre eksen hareketi ve kilitleme
        if (puzzlePlane == SurfacePlane.Dikey_Pano_XY)
        {
            currentLocalPos.x += mouseX * moveSpeed;
            currentLocalPos.y += mouseY * moveSpeed;

            currentLocalPos.x = Mathf.Clamp(currentLocalPos.x, -movementLimits.x, movementLimits.x);
            currentLocalPos.y = Mathf.Clamp(currentLocalPos.y, -movementLimits.y, movementLimits.y);

            currentLocalPos.z = hoverDistance; // Z Kilitli
        }
        else // Yatay_Masa_XZ
        {
            currentLocalPos.x += mouseX * moveSpeed;
            currentLocalPos.z += mouseY * moveSpeed; // Farenin yukarı/aşağı hareketi Z ekseninde (derinlikte) kaydırır

            currentLocalPos.x = Mathf.Clamp(currentLocalPos.x, -movementLimits.x, movementLimits.x);
            currentLocalPos.z = Mathf.Clamp(currentLocalPos.z, -movementLimits.y, movementLimits.y);

            currentLocalPos.y = hoverDistance; // Y Kilitli
        }

        activeSymbolInstance.transform.localPosition = currentLocalPos;

        if (scroll != 0)
        {
            activeSymbolInstance.transform.Rotate(
                scrollRotationAxis * scroll * rotationSpeed,
                Space.Self
            );
        }
    }

    private void CheckSuccess()
    {
        if (string.IsNullOrEmpty(requiredItemID))
            return;
        if (PuzzleInventoryManager.Instance.activeItem.itemID != requiredItemID)
            return;
        if (linkedBook != null && targetPage >= 0 && !linkedBook.IsOnPage(targetPage))
            return;

        // Vector3.Distance her iki düzlem için de doğru sonucu verir
        float distance = Vector3.Distance(
            activeSymbolInstance.transform.localPosition,
            targetLocalPosition
        );

        Quaternion currentRot = activeSymbolInstance.transform.localRotation;
        Quaternion targetRot = Quaternion.Euler(targetLocalRotation);
        float angleDiff = Quaternion.Angle(currentRot, targetRot);

        if (distance <= distanceTolerance && angleDiff <= angleTolerance)
        {
            isSolved = true;
            CloseSymbol();
            OnPuzzleSolved?.Invoke();

            if (linkedBook != null && linkedBook.isPasswordBook)
            {
                linkedBook.TriggerPasswordFind();
            }
        }
    }
}
