using UnityEngine;

public class SymbolSpawner : MonoBehaviour
{
    public static SymbolSpawner Instance { get; private set; }

    [Header("Sembol Objeleri (Prefab veya Sahne Objesi)")]
    public GameObject[] possibleSymbolObjects;

    [Header("Spawn Konumları")]
    public Transform[] spawnPoints; // Müfettiş panelinden 4 adet boş Transform ekle

    [HideInInspector]
    public int spawnedSymbolID = -1;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void SpawnRandomSymbol()
    {
        if (possibleSymbolObjects == null || possibleSymbolObjects.Length == 0)
            return;

        // Önce tüm sembolleri deaktif et
        foreach (GameObject obj in possibleSymbolObjects)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        // 1. Rastgele bir sembol seç
        int randomSymbolIndex = Random.Range(0, possibleSymbolObjects.Length);
        GameObject selectedSymbol = possibleSymbolObjects[randomSymbolIndex];

        if (selectedSymbol != null)
        {
            // 2. Rastgele bir konum seç (Eğer spawnPoints doluysa)
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int randomPointIndex = Random.Range(0, spawnPoints.Length);
                Transform targetPoint = spawnPoints[randomPointIndex];

                // Sembolü seçilen konuma ışınla
                selectedSymbol.transform.position = targetPoint.position;
                selectedSymbol.transform.rotation = targetPoint.rotation;
                selectedSymbol.transform.SetParent(targetPoint);
            }

            // 3. Sembolü aktif et
            selectedSymbol.SetActive(true);

            // ID Ataması
            InteractableSymbol sym = selectedSymbol.GetComponent<InteractableSymbol>();
            if (sym != null)
                spawnedSymbolID = sym.symbolID;
            else
                spawnedSymbolID = randomSymbolIndex;

            Debug.Log(
                $"[Spawner] Sembol: {selectedSymbol.name} | Konum ID: {spawnedSymbolID} noktasında oluşturuldu."
            );
        }
    }
}
