using UnityEngine;

public class MachineNotebookPlacer : MonoBehaviour
{
    [Tooltip("Defterin makinede duracağı tam pozisyon (Boş bir GameObject oluşturup sürükle)")]
    public Transform targetSlot;

    [Header("Ölçek (Scale) Ayarı")]
    [Tooltip("Defter makinedeyken küçülüyorsa buradaki değerleri artır (Örn: 2, 2, 2 yap)")]
    public Vector3 onMachineScale = Vector3.one;

    [Tooltip("Defter konulurken/alınırken çalacak ses (Opsiyonel)")]
    public AudioClip plugSound;
    private AudioSource audioSource;
    
    private IForceExitable machineInterface;
    private bool wasUsingMachine = false;
    
    // Defterin eldeki orijinal değerlerini hatırlamak için
    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalLocalScale; // KÜÇÜLME SORUNU İÇİN EKLENDİ

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        machineInterface = GetComponent<IForceExitable>();
        
        if (machineInterface == null)
        {
            Debug.LogError("[MachineNotebookPlacer] Bu objede etkileşim scripti (IForceExitable) bulunamadı!");
        }
    }

    void Update()
    {
        if (machineInterface == null || targetSlot == null) return;

        bool isUsingThisMachine = (GameManager.Instance != null && 
                                   GameManager.Instance.activeInteraction == machineInterface);

        if (isUsingThisMachine && !wasUsingMachine)
        {
            PlaceNotebookOnMachine();
        }
        else if (!isUsingThisMachine && wasUsingMachine)
        {
            TakeNotebookBack();
        }

        wasUsingMachine = isUsingThisMachine;
    }

    private void PlaceNotebookOnMachine()
    {
        if (NotebookUI.Instance == null || NotebookUI.Instance.notebookPanel == null) return;

        GameObject realNotebook = NotebookUI.Instance.notebookPanel;

        // Orijinal değerleri kaydet (Boyut dahil)
        originalParent = realNotebook.transform.parent;
        originalLocalPos = realNotebook.transform.localPosition;
        originalLocalRot = realNotebook.transform.localRotation;
        originalLocalScale = realNotebook.transform.localScale;

        NotebookUI.Instance.PutArmDown();
        NotebookUI.Instance.isOnMachine = true;
        
        // Defteri makineye sabitle
        realNotebook.transform.SetParent(targetSlot, false); 
        realNotebook.transform.localPosition = Vector3.zero;
        realNotebook.transform.localRotation = Quaternion.identity;
        
        // KÜÇÜLME SORUNUNU ÇÖZEN SATIR: Boyutu Inspector'dan girdiğin değere zorla
        realNotebook.transform.localScale = onMachineScale;
        
        realNotebook.SetActive(true);
        NotebookUI.Instance.UpdateUI();

        if (plugSound != null && audioSource != null)
            audioSource.PlayOneShot(plugSound);
    }

    private void TakeNotebookBack()
    {
        if (NotebookUI.Instance == null || NotebookUI.Instance.notebookPanel == null) return;

        GameObject realNotebook = NotebookUI.Instance.notebookPanel;

        NotebookUI.Instance.isOnMachine = false;
        
        // Defteri ele geri ver
        realNotebook.transform.SetParent(originalParent, false);
        realNotebook.transform.localPosition = originalLocalPos;
        realNotebook.transform.localRotation = originalLocalRot;
        
        // Elimize geri aldığımızda eski boyutuna (Scale) geri döndür
        realNotebook.transform.localScale = originalLocalScale;

        NotebookUI.Instance.ForceClose();

        if (plugSound != null && audioSource != null)
            audioSource.PlayOneShot(plugSound);
    }
}