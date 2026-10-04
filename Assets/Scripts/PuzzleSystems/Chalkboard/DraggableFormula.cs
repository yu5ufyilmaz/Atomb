using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DraggableFormula : MonoBehaviour
{
    [Header("Formül Verisi")]
    public FormulaItemSO formulaData;

    [Header("Taşıma Ayarları")]
    public float dragSpeed = 25f;
    public float surfaceOffset = 0.02f;
    public float hoverOffset = 0.5f;

    [Header("Döndürme Ayarları")]
    public float rotationStep = 90f;
    public float rotationAnimSpeed = 15f;

    private Camera mainCam;

    [HideInInspector]
    public bool isDragged = false;

    [HideInInspector]
    public Vector3 targetPosition;

    [HideInInspector]
    public Quaternion targetRotation;

    public void Initialize(FormulaItemSO data)
    {
        formulaData = data;
    }

    private void Start()
    {
        mainCam = Camera.main;
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    private void Update()
    {
        if (
            ChalkboardManager.Instance == null
            || !ChalkboardManager.Instance.isMachineActive
            || (GameManager.Instance != null && GameManager.Instance.isGamePaused)
        )
        {
            if (isDragged)
                Drop();
            return;
        }

        HandleInput();

        if (GetRootFormula() == this)
        {
            if (isDragged)
                CalculateSimpleDragPosition();
            ApplyMovement();
        }
    }

    public DraggableFormula GetRootFormula()
    {
        DraggableFormula current = this;
        while (
            current.transform.parent != null
            && current.transform.parent.GetComponent<DraggableFormula>() != null
        )
        {
            current = current.transform.parent.GetComponent<DraggableFormula>();
        }
        return current;
    }

    [HideInInspector]
    public Vector3 dragOffset; // Fare ile formülün merkezi arasındaki farkı tutacak

    private void HandleInput()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 15f);

            // KRİTİK ÇÖZÜM: Çarpan objeleri kameraya olan mesafelerine göre yakından uzağa sırala!
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool hitMyBody = false;
            bool hitAnyNode = false;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponent<FormulaNode>() != null)
                {
                    hitAnyNode = true;
                    break; // Eğer fareye en yakın obje bir Node ise formülü TAŞIMA, döngüden çık.
                }
                if (hit.collider.gameObject == this.gameObject)
                {
                    hitMyBody = true;
                    break; // Eğer fareye en yakın obje gövdeyse tut!
                }
            }

            // Sol Tık: Taşı
            if (Input.GetMouseButtonDown(0) && hitMyBody && !hitAnyNode)
            {
                DraggableFormula root = GetRootFormula();
                root.isDragged = true;

                Transform board = ChalkboardManager.Instance.boardTransform;

                // DÜZELTME BURADA: Sanal düzlemi tahtanın pozisyonunda değil,
                // tuttuğumuz formülün tam o anki derinliğinde (Z pozisyonunda) oluşturuyoruz!
                Plane formulaPlane = new Plane(board.forward, root.transform.position);

                if (formulaPlane.Raycast(ray, out float enterDistance))
                {
                    Vector3 hitPoint = ray.GetPoint(enterDistance);
                    root.dragOffset = root.transform.position - hitPoint;
                }
            }
            // Sağ tık: Silinme / Kopma ayrı yapı
            else if (Input.GetMouseButtonDown(1) && hitMyBody && !isDragged)
            {
                bool hasAnyBonds = false;
                foreach (var node in GetComponentsInChildren<FormulaNode>(true))
                {
                    if (node.isOccupied)
                        hasAnyBonds = true;
                }
                if (hasAnyBonds)
                {
                    BreakAllBonds();
                }
                else
                {
                    ChalkboardManager.Instance.RemoveFormula(this);
                }
            }
        }
        else if (Input.GetMouseButtonUp(0) && isDragged)
        {
            Drop();
        }

        // Döndürme kodların aynı kalıyor...
        if (isDragged && GetRootFormula() == this)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f || Input.GetKeyDown(KeyCode.Q))
                targetRotation *= Quaternion.Euler(0, 0, rotationStep);
            else if (scroll < 0f || Input.GetKeyDown(KeyCode.E))
                targetRotation *= Quaternion.Euler(0, 0, -rotationStep);
        }
    }

    private void CalculateSimpleDragPosition()
    {
        Transform board = ChalkboardManager.Instance.boardTransform;
        if (board == null)
            return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        Vector3 boardNormal = board.forward;
        if (Vector3.Dot(ray.direction, boardNormal) > 0)
            boardNormal = -boardNormal;

        Vector3 hoverPosition = board.position + (boardNormal * hoverOffset);
        Plane hoverPlane = new Plane(boardNormal, hoverPosition);

        if (hoverPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 hitPoint = ray.GetPoint(enterDistance);

            Vector3 targetWorldPos = hitPoint + dragOffset;

            // Tahtanın lokal koordinatlarına çevir
            Vector3 localPos = board.InverseTransformPoint(targetWorldPos);

            // DÜZELTME BURADA: Eski tekli clamp yerine yeni molekül clamp'i kullanıyoruz!
            // 0.15f değeri tebeşirlerin yarıçapı gibidir (padding). Eğer formüller kenara çok değiyorsa 0.20f yapabilirsin.
            localPos = ChalkboardManager.Instance.ClampMoleculeToBoardArea(
                GetRootFormula(),
                localPos,
                0.15f
            );

            localPos.z = hoverOffset;
            targetPosition = board.TransformPoint(localPos);
        }
    }

    private void Drop()
    {
        isDragged = false;
        if (ChalkboardManager.Instance.boardTransform == null)
            return;
        Transform board = ChalkboardManager.Instance.boardTransform;
        transform.SetParent(board);
        Vector3 localPos = board.InverseTransformPoint(targetPosition);
        localPos.z = surfaceOffset;
        targetPosition = board.TransformPoint(localPos);
    }

    private void BreakAllBonds()
    {
        FormulaNode[] allNodes = GetComponentsInChildren<FormulaNode>(true);
        foreach (var node in allNodes)
        {
            if (node.parentFormula == this && node.isOccupied)
            {
                DraggableFormula otherFormula = node.connectedNode.parentFormula;

                // Diğer formülü tahtanın içine bağımsız olarak geri gönder
                otherFormula.transform.SetParent(ChalkboardManager.Instance.boardTransform);
                otherFormula.targetPosition = otherFormula.transform.position;
                otherFormula.targetRotation = otherFormula.transform.rotation;

                // Kendi formülümüzü de tahtaya bağımsız olarak gönder
                this.transform.SetParent(ChalkboardManager.Instance.boardTransform);
                this.targetPosition = this.transform.position;
                this.targetRotation = this.transform.rotation;

                node.Disconnect();
            }
        }
    }

    private void ApplyMovement()
    {
        if (isDragged || Vector3.Distance(transform.position, targetPosition) > 0.001f)
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.deltaTime * dragSpeed
            );

        if (transform.rotation != targetRotation)
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationAnimSpeed
            );
    }

    public List<FormulaItemSO> GetMoleculeData()
    {
        DraggableFormula root = GetRootFormula();
        DraggableFormula[] allParts = root.GetComponentsInChildren<DraggableFormula>();
        List<FormulaItemSO> dataList = new List<FormulaItemSO>();
        foreach (var part in allParts)
        {
            if (part.formulaData != null)
                dataList.Add(part.formulaData);
        }
        return dataList;
    }
}
