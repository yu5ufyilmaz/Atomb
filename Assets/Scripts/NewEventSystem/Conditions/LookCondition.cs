using System;
using UnityEngine;

public class LookCondition : MonoBehaviour, ICondition
{
    public event Action OnConditionChanged;

    [Tooltip(
        "Oyuncunun bakması gereken hedef obje. Üzerinde mutlaka bir Collider olmalıdır! Boş bırakılırsa scriptin eklendiği objeyi baz alır."
    )]
    public GameObject targetObject;

    [Tooltip("Oyuncu bu objeye kaç saniye kesintisiz bakmalı?")]
    public float requiredLookTime = 1.5f;

    [Tooltip("Oyuncu en fazla ne kadar uzaktan bakabilir?")]
    public float maxDistance = 5f;

    [Tooltip(
        "Işın (Raycast) hangi katmanlara çarpsın? Duvarların arkasından bakmayı engellemek için Default seçili kalmalıdır."
    )]
    public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

    private bool conditionMet = false;
    private float currentLookTime = 0f;
    private Camera mainCam;

private float checkInterval = 0.1f; // Saniyede 10 kez kontrol
private float nextCheckTime = 0f;
private bool isCurrentlyLooking = false;

    public bool IsMet() => conditionMet;

    private void Start()
    {
        mainCam = Camera.main;

        if (targetObject == null)
        {
            targetObject = gameObject;
        }
    }

   private void Update()
{
    if (conditionMet) return;

    // Raycast'i her kare yerine 0.1 saniyede bir atıyoruz
    if (Time.time >= nextCheckTime)
    {
        isCurrentlyLooking = IsLookingAtTarget();
        nextCheckTime = Time.time + checkInterval;
    }

    if (isCurrentlyLooking)
    {
        currentLookTime += Time.deltaTime;
        if (currentLookTime >= requiredLookTime)
        {
            conditionMet = true;
            OnConditionChanged?.Invoke(); 
        }
    }
    else
    {
        currentLookTime = 0f;
    }
}

    private bool IsLookingAtTarget()
    {
        if (mainCam == null || targetObject == null)
            return false;

        // Kameranın tam ortasından ileriye doğru ışın at
        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, obstacleMask))
        {
            // Baktığımız obje hedefimiz mi veya hedefimizin altındaki (child) bir obje mi?
            if (
                hit.collider.gameObject == targetObject
                || hit.collider.transform.IsChildOf(targetObject.transform)
            )
            {
                return true;
            }
        }

        return false;
    }
}
