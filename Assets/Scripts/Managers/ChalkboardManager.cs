using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChalkboardManager : MonoBehaviour
{
    public static ChalkboardManager Instance { get; private set; }

    [Header("Tahta Referansları")]
    [Tooltip("Tahtanın tam merkez noktası (Formüllerin nerede duracağını belirler)")]
    public Transform boardTransform;

    [Tooltip("Tahta Sınırları (Local Space)")]
    public Vector2 minBounds = new Vector2(-1.5f, -1f);
    public Vector2 maxBounds = new Vector2(1.5f, 1f);

    [Header("Sentez Ayarları")]
    public ReactionZone synthesisZone;
    public List<RecipeSO> allRecipes;

    // Oyuncu tahtaya oturduğunda bu True olur
    public bool isMachineActive { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void SetMachineActive(bool state)
    {
        isMachineActive = state;
    }

    public Vector3 ClampToBoardArea(Vector3 localPos)
    {
        localPos.x = Mathf.Clamp(localPos.x, minBounds.x, maxBounds.x);
        localPos.y = Mathf.Clamp(localPos.y, minBounds.y, maxBounds.y);
        return localPos;
    }

    // Yeni Gelişmiş Sınırlandırma Sistemi (Tüm molekülü kapsar)
    public Vector3 ClampMoleculeToBoardArea(
        DraggableFormula root,
        Vector3 proposedLocalPos,
        float padding = 0.15f
    )
    {
        // 1. Formülün şu an nerede olduğunu ve nereye gitmek istediğini (delta) buluyoruz
        Vector3 currentLocalPos = boardTransform.InverseTransformPoint(root.transform.position);
        Vector3 delta = proposedLocalPos - currentLocalPos;

        // 2. Birbirine bağlı tüm parçaları (kolları/dalları) bul
        DraggableFormula[] allParts = root.GetComponentsInChildren<DraggableFormula>();

        float minX = float.MaxValue,
            maxX = float.MinValue;
        float minY = float.MaxValue,
            maxY = float.MinValue;

        // 3. Her bir parçanın "eğer hareket edersek" nerede olacağını hesapla
        foreach (var part in allParts)
        {
            Vector3 partLocal =
                boardTransform.InverseTransformPoint(part.transform.position) + delta;

            if (partLocal.x < minX)
                minX = partLocal.x;
            if (partLocal.x > maxX)
                maxX = partLocal.x;
            if (partLocal.y < minY)
                minY = partLocal.y;
            if (partLocal.y > maxY)
                maxY = partLocal.y;
        }

        // 4. Herhangi bir uç parça sınırları aşıyorsa, gitmek istediğimiz hedefi geriye doğru it (Kelepçele)
        if (minX - padding < minBounds.x)
            proposedLocalPos.x += (minBounds.x - (minX - padding));
        if (maxX + padding > maxBounds.x)
            proposedLocalPos.x -= ((maxX + padding) - maxBounds.x);
        if (minY - padding < minBounds.y)
            proposedLocalPos.y += (minBounds.y - (minY - padding));
        if (maxY + padding > maxBounds.y)
            proposedLocalPos.y -= ((maxY + padding) - maxBounds.y);

        return proposedLocalPos;
    }

    public void AttemptSynthesis()
    {
        if (!isMachineActive || synthesisZone == null || synthesisZone.currentItems.Count == 0)
        {
            Debug.Log("Çember boş veya makine aktif değil, sentez yapılamaz.");
            return;
        }

        // Çöken (Null olan) objeleri listeden temizle
        synthesisZone.currentItems.RemoveAll(item => item == null);

        // 1. Çemberdeki tüm parçaların bağlı olduğu "Kök" (Root) molekülleri bul
        // HashSet kullandık ki aynı molekülün 3 parçasını 3 ayrı molekülmüş gibi saymasın
        HashSet<DraggableFormula> rootMolecules = new HashSet<DraggableFormula>();
        foreach (var item in synthesisZone.currentItems)
        {
            if (item != null)
                rootMolecules.Add(item.GetRootFormula());
        }

        bool recipeFound = false;

        // 2. Çemberin içindeki her bir bağımsız Molekül Salkımı için tarif kontrolü yap
        foreach (DraggableFormula root in rootMolecules)
        {
            if (root == null)
                continue;

            // Bu molekülün içindeki tüm parçaların ID'lerini alıp A'dan Z'ye sırala
            List<string> moleculeIDs = new List<string>();
            foreach (var so in root.GetMoleculeData())
            {
                moleculeIDs.Add(so.itemID);
            }
            moleculeIDs.Sort();

            // 3. Sistemdeki tariflerle karşılaştır
            foreach (var recipe in allRecipes)
            {
                if (recipe.recipeType != RecipeType.Synthesis)
                    continue;

                List<string> recipeIDs = new List<string>();
                foreach (var input in recipe.inputs)
                {
                    recipeIDs.Add(input.itemID);
                }
                recipeIDs.Sort();

                // Eğer molekülün içeriği tarifle BİREBİR eşleşiyorsa (Örn: 2 Hidrojen, 1 Oksijen = Su)
                if (moleculeIDs.SequenceEqual(recipeIDs))
                {
                    Debug.Log($"Sentez Başarılı! Üretilen: {recipe.outputs[0].itemName}");
                    recipeFound = true;

                    // Yeni objenin çıkacağı pozisyonu (Eski molekülün merkezi) ayarla
                    Vector3 spawnPos = root.transform.position;

                    // Eski molekülü tamamen yok et (Root silinince ona bağlı tüm child parçalar da yok olur)
                    Destroy(root.gameObject);

                    // Yeni çıktıyı (C formülünü) tahtaya yarat
                    foreach (var output in recipe.outputs)
                    {
                        if (output.chalkPrefab != null)
                        {
                            // Yeni objeyi direkt tahtanın çocuğu (child) olarak yarat
                            GameObject newObj = Instantiate(
                                output.chalkPrefab,
                                spawnPos,
                                Quaternion.identity,
                                boardTransform
                            );

                            // Havada asılı kalmasın diye Z eksenini tahtaya yapıştır
                            Vector3 localSpawn = boardTransform.InverseTransformPoint(
                                newObj.transform.position
                            );
                            localSpawn.z = 0.02f; // surfaceOffset değeri
                            newObj.transform.position = boardTransform.TransformPoint(localSpawn);
                        }
                    }

                    // Bir molekül sentezlendi, diğer salkımları kontrol etmeye devam et
                    break;
                }
            }
        }

        if (!recipeFound)
        {
            Debug.LogWarning(
                "Hata: Çemberdeki moleküller hiçbir tarifle uyuşmuyor veya yanlış bağlandılar!"
            );
        }
    }

    public void RemoveFormula(DraggableFormula formula)
    {
        if (synthesisZone != null && synthesisZone.currentItems.Contains(formula))
        {
            synthesisZone.currentItems.Remove(formula);
        }
        Destroy(formula.gameObject);
    }

    // ==========================================
    // EDITOR GÖRSELLEŞTİRME (DEBUG GIZMOS)
    // ==========================================
    private void OnDrawGizmos()
    {
        // Eğer tahta merkezi atanmamışsa çizim yapma
        if (boardTransform == null)
            return;

        // Gizmo rengini belirle (Yarı saydam yeşil yapalım)
        Gizmos.color = new Color(0f, 1f, 0f, 0.8f);

        // Çizimleri boardTransform'un kendi açısından (local space) yapmak için matrisi değiştiriyoruz
        Gizmos.matrix = boardTransform.localToWorldMatrix;

        // Kutunun merkezini hesapla (min ve max değerlerinin tam ortası)
        Vector3 center = new Vector3(
            (minBounds.x + maxBounds.x) / 2f,
            (minBounds.y + maxBounds.y) / 2f,
            0f
        );

        // Kutunun boyutlarını hesapla (Genişlik ve Yükseklik)
        Vector3 size = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0.01f);

        // 1. Sınırları belirten içi boş bir dikdörtgen çiz
        Gizmos.DrawWireCube(center, size);

        // 2. İstersen alanın içini de çok hafif belli belirsiz boyayabilirsin (İsteğe bağlı)
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}
