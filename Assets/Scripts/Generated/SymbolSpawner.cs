using UnityEngine;

public class SymbolSpawner : MonoBehaviour
{
    public static SymbolSpawner Instance { get; private set; }

    [Header("Rastgele Spawn Ayarları")]
    public GameObject[] possibleSymbolObjects;

    [HideInInspector]
    public int spawnedSymbolID = -1; // Hangi sembolün aktif olduğunu tutacağız

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // PasswordManager Start'ta çalıştığı için çakışma olmasın diye
        // buradaki Start'ı boş bırakıp, fonksiyonu dışarıdan tetikleteceğiz.
    }

    public void SpawnRandomSymbol()
    {
        if (possibleSymbolObjects == null || possibleSymbolObjects.Length == 0)
            return;

        foreach (GameObject obj in possibleSymbolObjects)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        int randomIndex = Random.Range(0, possibleSymbolObjects.Length);

        if (possibleSymbolObjects[randomIndex] != null)
        {
            possibleSymbolObjects[randomIndex].SetActive(true);

            // Seçilen objenin ID'sini alıp hafızaya yazıyoruz
            InteractableSymbol sym = possibleSymbolObjects[randomIndex]
                .GetComponent<InteractableSymbol>();
            if (sym != null)
                spawnedSymbolID = sym.symbolID;
            else
                spawnedSymbolID = randomIndex; // Eğer script yoksa sırasını ID kabul et

            Debug.Log($"[Spawner] Rastgele Sembol Aktif Edildi. ID: {spawnedSymbolID}");
        }
    }
}
