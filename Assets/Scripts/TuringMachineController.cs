using UnityEngine;

public class TuringMachineController : MonoBehaviour
{
    [SerializeField] private InteractableTuringMachine machineInteractable;
    
    private int currentGroup = 0; 
    
    private int currentWheelIndex = 0; 
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            currentGroup = (currentGroup - 1 + 3) % 3;
            currentWheelIndex = 0; 
            UpdateHighlights();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            currentGroup = (currentGroup + 1) % 3;
            currentWheelIndex = 0;
            UpdateHighlights();
        }
        
        if (Input.GetKeyDown(KeyCode.E))
        {
            currentWheelIndex = GetNextWheelIndex(1);
            UpdateHighlights();
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentWheelIndex = GetNextWheelIndex(-1);
            UpdateHighlights();
        }
        
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            ChangeWheelValue(-1);
        }
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            ChangeWheelValue(1);
        }
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f) ChangeWheelValue(-1);
        if (scroll < 0f) ChangeWheelValue(1);

        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            machineInteractable.ExitMachine();
        }
    }

    private int GetNextWheelIndex(int direction)
    {
        int maxIndex = 0;
        if (currentGroup == 0) maxIndex = 7; // 8 kelime çarkı [cite: 48]
        else if (currentGroup == 1) maxIndex = 0; // 1 sembol çarkı [cite: 50]
        else if (currentGroup == 2) maxIndex = 2; // 3 sayı çarkı [cite: 52]

        int newIndex = (currentWheelIndex + direction + (maxIndex + 1)) % (maxIndex + 1);
        return newIndex;
    }

    private void ChangeWheelValue(int direction)
    {
       
    }

    private void UpdateHighlights()
    {
      // TODO: Aktif olan çarkı görsel olarak vurgula [cite: 63]
        
    }
}