using System.Collections.Generic;
using UnityEngine;

public class HDRPOutlineController : MonoBehaviour
{
    // Inspector'dan Outline yapacağın objeleri seçebilirsin
    // Eğer scriptin olduğu obje ise boş bırak.
    public GameObject targetObject;

    private int outlineLayer;
    private int defaultLayer;
    private bool isActive = false;

    void Start()
    {
        if (targetObject == null)
            targetObject = gameObject;

        // Layer isimlerini ID'ye çeviriyoruz
        outlineLayer = LayerMask.NameToLayer("Outline");
        defaultLayer = LayerMask.NameToLayer("Default");
    }

    // Bu fonksiyonu başka scriptlerden çağırabilirsin
    public void ToggleOutline(bool status)
    {
        isActive = status;
        if (isActive)
        {
            // Objeyi ve varsa çocuklarını Outline katmanına al
            SetLayerRecursively(targetObject, outlineLayer);
        }
        else
        {
            // Normale döndür
            SetLayerRecursively(targetObject, defaultLayer);
        }
    }

    // Sadece objeyi değil, alt parçalarını da (örneğin silahın parçaları) değiştirir
    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    // TEST ETMEK İÇİN: Klavyeden T'ye basınca aç/kapa
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleOutline(!isActive);
        }
    }
}
