using UnityEngine;
using StarterAssets;

/// <summary>
/// Merkezi singleton: Tüm script'lerin FindObjectOfType yapmak yerine
/// bu sınıftan player referanslarına erişmesini sağlar.
/// </summary>
public class PlayerReferences : MonoBehaviour
{
    public static PlayerReferences Instance { get; private set; }

    [HideInInspector] public UnityEngine.CharacterController physicsController;
    [HideInInspector] public StarterAssets.CharacterController gameController;
    [HideInInspector] public StarterAssetsInputs inputs;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Camera mainCamera;
    [HideInInspector] public PlayerInteraction interaction;
    [HideInInspector] public Transform playerTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Referansları bir kere bul
        physicsController = FindObjectOfType<UnityEngine.CharacterController>();
        if (physicsController != null)
        {
            playerTransform = physicsController.transform;
            gameController = physicsController.GetComponent<StarterAssets.CharacterController>();
            inputs = physicsController.GetComponent<StarterAssetsInputs>();
            animator = physicsController.GetComponent<Animator>();
        }
        mainCamera = Camera.main;
        interaction = FindObjectOfType<PlayerInteraction>();
    }
}
