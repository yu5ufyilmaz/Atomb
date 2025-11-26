using UnityEngine;
using System.Collections;

public class LeesEnemyAI : MonoBehaviour
{
    public enum LeesState
    {
        Hidden,     
        Spawning,   
        Observing,  
        Aggro,     
        Despawning  
    }

    [Header("Durum")]
    public LeesState currentState = LeesState.Hidden;

    [Header("Ayarlar (PDF Referansları)")]
    [Tooltip("Oyuncunun Lees'i fark etmeden geçirebileceği maksimum süre (Scenario A).")]
    public float maxIgnoranceTime = 30f; 

    [Tooltip("Kurtulmak için arkayı dönüp bekleme süresi (Scenario D).")]
    public float survivalWaitTime = 15f; 

    [Tooltip("Lees'e doğrudan bakma limiti (Scenario C).")]
    public float maxStareTime = 3.0f;

    [Tooltip("Lees'in oyuncuya ne kadar yakın spawn olacağı.")]
    public float spawnDistance = 5.0f;

    [Header("Referanslar")]
    public Transform playerTransform;
    public Camera playerCamera;
    
    // Zamanlayıcılar
    private float currentIgnoranceTimer;
    private float currentSurvivalTimer;
    private float currentStareTimer;
    private bool hasBeenSpotted = false; 

    private void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        DespawnLees();
       
        Invoke(nameof(SpawnLeesInBlindSpot), 5f);
    }

    private void Update()
    {
        if (currentState == LeesState.Aggro)
        {
            return;
        }

        if (currentState == LeesState.Observing)
        {
            HandleObservationLogic();
        }
    }
    
    private void HandleObservationLogic()
    {
        bool isVisible = CheckIfVisible();
        
        if (!hasBeenSpotted)
        {
            if (isVisible)
            {
                hasBeenSpotted = true;
                Debug.Log("Lees: SPOTTED! Protocol started.");
            }
            else
            {
                currentIgnoranceTimer += Time.deltaTime;
                if (currentIgnoranceTimer >= maxIgnoranceTime)
                {
                    TriggerDeath("Zaman Doldu (Ignorance)");
                }
            }
        }
        
        else 
        {
            if (isVisible)
            {
                currentStareTimer += Time.deltaTime;
                
                if (currentStareTimer >= maxStareTime)
                {
                    TriggerDeath("Çok uzun süre bakıldı (Staring)");
                }
                
                currentSurvivalTimer = 0f;
            }
            else
            {
                currentSurvivalTimer += Time.deltaTime;
                Debug.Log($"Survival Protocol: {currentSurvivalTimer:F1} / {survivalWaitTime}");

                if (currentSurvivalTimer >= survivalWaitTime)
                {
                    Debug.Log("Lees: Protocol Successful. Despawning.");
                    DespawnLees(); // [cite: 39]
                }
            }
        }
    }

    private void TriggerDeath(string reason)
    {
        Debug.LogError($"DEATH: {reason}");
        currentState = LeesState.Aggro;
        
    }


    private bool CheckIfVisible()
    {
        Vector3 viewPos = playerCamera.WorldToViewportPoint(transform.position);
        
        bool inCameraFrustum = (viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1 && viewPos.z > 0);
        
        if (!inCameraFrustum) return false;
        
        RaycastHit hit;
        Vector3 directionToPlayer = playerCamera.transform.position - transform.position;
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, directionToPlayer, out hit))
        {
            if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
            {
                return true; // Engel yok, görünüyor.
            }
        }
        return false; // Arada duvar var.
    }
    
    private void SpawnLeesInBlindSpot()
    {
        Vector3 randomDirection = -playerTransform.forward; 
        
        randomDirection = Quaternion.Euler(0, Random.Range(-45f, 45f), 0) * randomDirection;

        Vector3 spawnPos = playerTransform.position + (randomDirection.normalized * spawnDistance);
        
        transform.position = spawnPos;
        
        transform.LookAt(new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z));

        currentState = LeesState.Observing;
        ShowModel(true);
        
        currentIgnoranceTimer = 0;
        currentSurvivalTimer = 0;
        currentStareTimer = 0;
        hasBeenSpotted = false;
        
        Debug.Log("Lees Spawned!");
    }

    private void DespawnLees()
    {
        currentState = LeesState.Hidden;
        ShowModel(false);
    }

    private void ShowModel(bool show)
    {
        foreach(var r in GetComponentsInChildren<Renderer>())
            r.enabled = show;
        
        foreach(var c in GetComponentsInChildren<Collider>())
            c.enabled = show;
    }
}