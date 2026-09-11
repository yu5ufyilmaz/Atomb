using System.Collections.Generic;
using UnityEngine;

public class HDRPOutlineController : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Outline yapılacak en üst ana obje. (Kitabın en tepesindeki Parent objeyi sürükle)")]
    public GameObject targetRootObject;

    // Performans için renderer'ları hafızada tutacağız
    private Renderer[] allChildRenderers;
    private int outlineLayer;
    private int defaultLayer;
    private bool isActive = false;

    void Awake()
    {
        // 1. Eğer inspector'dan root seçmediysen, otomatik olarak en tepeyi bulmaya çalışır
        if (targetRootObject == null)
        {
            // Bu scriptin takılı olduğu objeyi varsayılan yap
            targetRootObject = gameObject;
        }

        // 2. Layer ID'lerini al (Unity ayarlarındaki isimlerin "Outline" ve "Default" olduğundan emin ol)
        outlineLayer = LayerMask.NameToLayer("Outline");
        defaultLayer = LayerMask.NameToLayer("Default");

        // 3. Ana objenin altındaki (kendisi dahil) TÜM Renderer'ları (Mesh, SkinnedMesh) bul ve listele
        // 'true' parametresi, pasif olan objeleri de bulmasını sağlar.
        allChildRenderers = targetRootObject.GetComponentsInChildren<Renderer>(true);
    }

    public void ToggleOutline(bool status)
    {
        isActive = status;

        // Outline aktifse 'Outline' katmanını, değilse 'Default' katmanını seç
        int targetLayer = isActive ? outlineLayer : defaultLayer;

        if (allChildRenderers != null)
        {
            foreach (Renderer r in allChildRenderers)
            {
                if (r != null)
                {
                    r.gameObject.layer = targetLayer;
                }
            }
        }
    }

    // Debug: Editörde test etmek için T tuşu
#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleOutline(!isActive);
        }
    }
#endif
}
