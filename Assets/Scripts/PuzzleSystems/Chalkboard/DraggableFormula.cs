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

    private void HandleInput()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 15f);
            bool hitMyBody = false;
            bool hitAnyNode = false;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponent<FormulaNode>() != null)
                    hitAnyNode = true;
                if (hit.collider.gameObject == this.gameObject)
                    hitMyBody = true;
            }

            // Sol Tık: Taşıma
            if (Input.GetMouseButtonDown(0) && hitMyBody && !hitAnyNode)
            {
                DraggableFormula root = GetRootFormula();
                root.isDragged = true;
            }
            // Sağ Tık: Silinme / Kopma ayrımı yapıldı (HATA BURADAYDI)
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
                    // Bağlıysa sadece bağları kopar (Silme!)
                    BreakAllBonds();
                }
                else
                {
                    // Hiçbir yere bağlı değilse oyundan sil
                    ChalkboardManager.Instance.RemoveFormula(this);
                }
            }
        }
        else if (Input.GetMouseButtonUp(0) && isDragged)
        {
            Drop();
        }

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

        Plane boardPlane = new Plane(boardNormal, board.position);
        if (boardPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 hitPoint = ray.GetPoint(enterDistance);
            Vector3 localPos = board.InverseTransformPoint(hitPoint);
            localPos = ChalkboardManager.Instance.ClampToBoardArea(localPos);
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
