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
    public float snapRadius = 0.8f;

    [Header("Döndürme Ayarları")]
    public float rotationStep = 90f;
    public float rotationAnimSpeed = 15f;

    private Camera mainCam;

    // Bunları public yaptık ki bağlar koparken diğer parçalar değerlerini güncelleyebilsin
    [HideInInspector]
    public bool isDragged = false;

    [HideInInspector]
    public Vector3 targetPosition;

    [HideInInspector]
    public Quaternion targetRotation;

    private FormulaNode mySnapNode = null;
    private FormulaNode targetSnapNode = null;

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

        // KODUN CAN DAMARI: Sadece molekülün "Kök" (Root) objesi hareket edebilir!
        // Alt objeler (çocuklar) kendi başlarına hareket etmeye çalışıp ebeveynle savaşmamalı.
        if (GetRootFormula() == this)
        {
            if (isDragged)
            {
                CalculateDragPosition();
            }
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
        bool isHovering = false;

        RaycastHit[] hits = Physics.RaycastAll(ray, 15f);
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<DraggableFormula>() == this)
            {
                isHovering = true;
                break;
            }
        }

        if (Input.GetMouseButtonDown(0) && isHovering)
        {
            DraggableFormula root = GetRootFormula();
            root.isDragged = true;
        }
        else if (Input.GetMouseButtonUp(0) && isDragged)
        {
            Drop();
        }
        else if (Input.GetMouseButtonDown(1) && isHovering && !isDragged)
        {
            if (
                transform.parent != null
                && transform.parent.GetComponent<DraggableFormula>() != null
            )
            {
                BreakAllBonds();
            }
            else
            {
                BreakAllBonds();
                ChalkboardManager.Instance.RemoveFormula(this);
            }
        }

        // DÖNDÜRME (Sadece kök obje dönebilir)
        if (isDragged && GetRootFormula() == this)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f)
                targetRotation *= Quaternion.Euler(0, 0, rotationStep);
            else if (scroll < 0f)
                targetRotation *= Quaternion.Euler(0, 0, -rotationStep);

            if (Input.GetKeyDown(KeyCode.Q))
                targetRotation *= Quaternion.Euler(0, 0, rotationStep);
            if (Input.GetKeyDown(KeyCode.E))
                targetRotation *= Quaternion.Euler(0, 0, -rotationStep);
        }
    }

    private void CalculateDragPosition()
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

            FindClosestSnapPoint();

            if (mySnapNode != null && targetSnapNode != null)
            {
                Vector3 offset = mySnapNode.transform.position - transform.position;
                targetPosition = targetSnapNode.transform.position - offset;
                targetPosition.z -= 0.05f;
            }
        }
    }

    private void FindClosestSnapPoint()
    {
        mySnapNode = null;
        targetSnapNode = null;
        float closestDist = float.MaxValue;

        FormulaNode[] myNodes = GetComponentsInChildren<FormulaNode>();
        FormulaNode[] allNodes = FindObjectsByType<FormulaNode>(FindObjectsSortMode.None);

        foreach (var mNode in myNodes)
        {
            if (mNode.isOccupied)
                continue;

            foreach (var oNode in allNodes)
            {
                if (oNode.transform.IsChildOf(this.transform))
                    continue;
                if (oNode.isOccupied)
                    continue;

                if (!oNode.CanConnect(this, mNode) || !mNode.CanConnect(oNode.parentFormula, oNode))
                    continue;

                float dist = Vector3.Distance(mNode.transform.position, oNode.transform.position);

                if (dist < snapRadius && dist < closestDist)
                {
                    closestDist = dist;
                    targetSnapNode = oNode;
                    mySnapNode = mNode;
                }
            }
        }
    }

    private void Drop()
    {
        isDragged = false;
        if (ChalkboardManager.Instance.boardTransform == null)
            return;

        if (mySnapNode != null && targetSnapNode != null)
        {
            mySnapNode.ConnectTo(targetSnapNode);
            transform.SetParent(targetSnapNode.parentFormula.transform);
        }
        else
        {
            Transform board = ChalkboardManager.Instance.boardTransform;
            transform.SetParent(board);

            Vector3 localPos = board.InverseTransformPoint(targetPosition);
            localPos.z = surfaceOffset;
            targetPosition = board.TransformPoint(localPos);
        }

        mySnapNode = null;
        targetSnapNode = null;

       
    }

    private void BreakAllBonds()
    {
        // Sadece BU formüle ait olan bağ noktalarını tarar ve koparır
        FormulaNode[] allNodes = GetComponentsInChildren<FormulaNode>();
        foreach (var node in allNodes)
        {
            if (node.parentFormula == this && node.connectedNode != null)
            {
                DraggableFormula otherFormula = node.connectedNode.parentFormula;

                // Eğer bağlandığım obje benim çocuğumsa, onu serbest bırak
                if (otherFormula.transform.parent == this.transform)
                {
                    otherFormula.transform.SetParent(ChalkboardManager.Instance.boardTransform);
                    otherFormula.targetPosition = otherFormula.transform.position;
                    otherFormula.targetRotation = otherFormula.transform.rotation;
                }
                // Eğer ben onun çocuğuysam, kendimi serbest bırak
                else if (this.transform.parent == otherFormula.transform)
                {
                    this.transform.SetParent(ChalkboardManager.Instance.boardTransform);
                    this.targetPosition = this.transform.position;
                    this.targetRotation = this.transform.rotation;
                }

                node.Disconnect();
            }
        }
    }

    private void ApplyMovement()
    {
        if (isDragged || Vector3.Distance(transform.position, targetPosition) > 0.001f)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.deltaTime * dragSpeed
            );
        }

        if (transform.rotation != targetRotation)
        {
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationAnimSpeed
            );
        }
    }

    public List<FormulaItemSO> GetMoleculeData()
    {
        DraggableFormula root = GetRootFormula();
        DraggableFormula[] allParts = root.GetComponentsInChildren<DraggableFormula>();

        List<FormulaItemSO> dataList = new List<FormulaItemSO>();
        foreach (var part in allParts)
        {
            if (part.formulaData != null)
            {
                dataList.Add(part.formulaData);
            }
        }
        return dataList;
    }
}
