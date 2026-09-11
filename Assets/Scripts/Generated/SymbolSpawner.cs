using UnityEngine;

public class SymbolSpawner : MonoBehaviour, ISaveable
{
    public static SymbolSpawner Instance;

    public GameObject[] symbolPrefabs;
    public Transform[] spawnPoints;

    [Header("Debug/Status")]
    public int spawnedSymbolID;
    private int lastSpawnPointIndex = -1;
    private GameObject currentSpawnedObject;

    private bool wasLoaded = false; // Yükleme yapılıp yapılmadığını takip eder

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Eğer 0.1 saniye içinde LoadData çalışmadıysa, bu yeni bir oyundur.
        // Yeni oyunsa rastgele spawn yap.
        Invoke(nameof(CheckInitialSpawn), 0.1f);
    }

    private void CheckInitialSpawn()
    {
        // Eğer yükleme yapılmadıysa ve sahnede obje yoksa rastgele spawn et
        if (!wasLoaded && currentSpawnedObject == null)
        {
            SpawnRandomSymbol();
        }
    }

    public void SpawnRandomSymbol()
    {
        if (spawnPoints.Length == 0 || symbolPrefabs.Length == 0)
            return;

        lastSpawnPointIndex = Random.Range(0, spawnPoints.Length);
        int randomSymbolIndex = Random.Range(0, symbolPrefabs.Length);

        SpawnSpecificSymbol(randomSymbolIndex, lastSpawnPointIndex);
    }

    private void SpawnSpecificSymbol(int prefabIndex, int pointIndex)
    {
        if (currentSpawnedObject != null)
            Destroy(currentSpawnedObject);

        currentSpawnedObject = Instantiate(
            symbolPrefabs[prefabIndex],
            spawnPoints[pointIndex].position,
            spawnPoints[pointIndex].rotation
        );
        currentSpawnedObject.transform.SetParent(spawnPoints[pointIndex]);

        InteractableSymbol symbolScript = currentSpawnedObject.GetComponent<InteractableSymbol>();
        if (symbolScript != null)
        {
            spawnedSymbolID = symbolScript.symbolID;
        }
    }

    // ==========================================
    // ISAVEABLE ARAYÜZÜ ENTEGRASYONU
    // ==========================================
    public void LoadData(GameData data)
    {
        wasLoaded = true; // Yükleme işleminin başladığını işaretle

        // 1. Eğer oyuncu sembolü zaten almışsa dünyadaki her şeyi temizle ve çık
        if (data.hasSymbol)
        {
            if (currentSpawnedObject != null)
                Destroy(currentSpawnedObject);
            return;
        }

        // 2. Eğer sembol dünyadaysa kayıtlı konuma spawn et
        if (data.isSymbolInWorld && data.spawnedSymbolLocationIndex != -1)
        {
            this.lastSpawnPointIndex = data.spawnedSymbolLocationIndex;
            this.spawnedSymbolID = data.spawnedSymbolID;

            int targetPrefabIndex = -1;
            for (int i = 0; i < symbolPrefabs.Length; i++)
            {
                if (
                    symbolPrefabs[i].GetComponent<InteractableSymbol>().symbolID
                    == data.spawnedSymbolID
                )
                {
                    targetPrefabIndex = i;
                    break;
                }
            }

            if (targetPrefabIndex != -1)
            {
                SpawnSpecificSymbol(targetPrefabIndex, lastSpawnPointIndex);
            }
        }
    }

    public void SaveData(ref GameData data)
    {
        data.isSymbolInWorld = (currentSpawnedObject != null);
        data.spawnedSymbolID = this.spawnedSymbolID;
        data.spawnedSymbolLocationIndex = this.lastSpawnPointIndex;
    }
}
