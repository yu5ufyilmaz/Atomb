using UnityEngine;
public class InteractableTuringMachine : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject turingMachineUIPanel;
    [SerializeField] private TuringMachineController machineController; 
    
    [SerializeField] private UnityEngine.CharacterController playerController;
    [SerializeField] private MonoBehaviour playerLookScript; 

    public void Interact()
    {
        // Player kontrolünü devre dışı bırak
        playerController.enabled = false;
        if (playerLookScript != null) playerLookScript.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Makine UI'ını aç
        turingMachineUIPanel.SetActive(true);
        machineController.enabled = true; 
    }

    public string GetInteractionPrompt()
    {
        return "[E] Makineyi Kullan";
    }
    
    public void ExitMachine()
    {
        playerController.enabled = true;
        if (playerLookScript != null) playerLookScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        turingMachineUIPanel.SetActive(false);
        machineController.enabled = false; 
        
    }
}