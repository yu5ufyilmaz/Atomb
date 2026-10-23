using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DraggableFormula : MonoBehaviour
{
    [Header("Formül Verisi")]
    public FormulaItemSO formulaData;
    [Tooltip("İsteğe bağlı parça adı etiketi; atanmazsa parçanın kendi metni kullanılır")]
    public TMP_Text formulaLabel;
    public bool IsSynthesized { get; set; }
    [Header("Taşıma Ayarları")]
    public float dragSpeed = 25f;
    public float surfaceOffset = 0.02f;
    public float hoverOffset = 0.5f;
    [Header("Döndürme Ayarları")]
    public float rotationStep = 90f;
    public float rotationAnimSpeed = 15f;
    [HideInInspector] public bool isDragged;
    [HideInInspector] public Vector3 targetPosition;
    [HideInInspector] public Quaternion targetRotation;
    [HideInInspector] public Vector3 dragOffset;

    private Camera mainCam;
    private bool initialized;
    private bool nodeDefaultsApplied;

    public int ConnectionCapacity => formulaData != null ? Mathf.Max(0, formulaData.connectionCapacity) : 0;
    public int UsedCapacity
    {
        get
        {
            int count = 0;
            foreach (FormulaNode node in GetOwnNodes())
                if (node.isOccupied) count += (int)node.bondType;
            return count;
        }
    }

    private void Awake()
    {
        SyncMovementTarget();
    }

    private void Start()
    {
        mainCam = Camera.main;
        if (!initialized && formulaData != null) Initialize(formulaData);
    }

    public void Initialize(FormulaItemSO data)
    {
        formulaData = data;
        initialized = true;
        SyncMovementTarget();
        UpdateLabel();
        if (nodeDefaultsApplied || data == null) return;
        FormulaNode[] nodes = GetOwnNodes();
        for (int i = 0; i < nodes.Length; i++)
        {
            if (data.initiallyBlockedNodes != null && data.initiallyBlockedNodes.Contains(i))
                nodes[i].nodeState = FormulaNodeState.Blocked;
            if (data.initiallyUnstableNodes != null && data.initiallyUnstableNodes.Contains(i))
                nodes[i].nodeState = FormulaNodeState.Unstable;
        }
        nodeDefaultsApplied = true;
    }

    private void UpdateLabel()
    {
        if (formulaData == null) return;
        string label = string.IsNullOrEmpty(formulaData.itemName) ? formulaData.itemID : formulaData.itemName;
        if (formulaLabel != null) { formulaLabel.text = label; return; }
        bool found = false;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<DraggableFormula>() != this || text.GetComponentInParent<FormulaNode>() != null) continue;
            text.text = label;
            found = true;
        }
        foreach (TextMesh text in GetComponentsInChildren<TextMesh>(true))
        {
            if (text.GetComponentInParent<DraggableFormula>() != this || text.GetComponentInParent<FormulaNode>() != null) continue;
            text.text = label;
            found = true;
        }
        if (found) return;
        GameObject labelObject = new GameObject("Parça Kimliği");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.56f);
        formulaLabel = labelObject.AddComponent<TextMeshPro>();
        formulaLabel.text = label;
        formulaLabel.alignment = TextAlignmentOptions.Center;
        formulaLabel.fontSize = 3f;
        formulaLabel.textWrappingMode = TextWrappingModes.Normal;
        formulaLabel.rectTransform.sizeDelta = new Vector2(1.8f, 1f);
        formulaLabel.raycastTarget = false;
    }

    public void SyncMovementTarget()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        isDragged = false;
    }

    public FormulaNode[] GetOwnNodes()
    {
        List<FormulaNode> result = new List<FormulaNode>();
        foreach (FormulaNode node in GetComponentsInChildren<FormulaNode>(true))
        {
            // Yakındaki formül, Transform gruplamasından bağımsız düğüm sahibidir.
            if (node.GetComponentInParent<DraggableFormula>(true) != this) continue;
            node.parentFormula = this;
            result.Add(node);
        }
        return result.ToArray();
    }

    public List<DraggableFormula> GetConnectedParts()
    {
        List<DraggableFormula> result = new List<DraggableFormula>();
        HashSet<DraggableFormula> visited = new HashSet<DraggableFormula>();
        Queue<DraggableFormula> pending = new Queue<DraggableFormula>();
        visited.Add(this);
        pending.Enqueue(this);
        while (pending.Count > 0)
        {
            DraggableFormula part = pending.Dequeue();
            result.Add(part);
            foreach (FormulaNode node in part.GetOwnNodes())
            {
                DraggableFormula other = node.connectedNode != null ? node.connectedNode.parentFormula : null;
                if (other != null && visited.Add(other)) pending.Enqueue(other);
            }
        }
        return result;
    }

    public DraggableFormula GetRootFormula()
    {
        DraggableFormula current = this;
        while (current.transform.parent != null)
        {
            DraggableFormula parent = current.transform.parent.GetComponent<DraggableFormula>();
            if (parent == null) break;
            current = parent;
        }
        return current;
    }

    private void Update()
    {
        ChalkboardManager manager = ChalkboardManager.Instance;
        if (manager == null || !manager.isMachineActive || (GameManager.Instance != null && GameManager.Instance.isGamePaused))
        {
            if (isDragged) Drop(false);
            return;
        }
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;
        HandleInput();
        if (GetRootFormula() != this) return;
        if (isDragged) CalculateDragPosition();
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * dragSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationAnimSpeed);
    }

    private void HandleInput()
    {
        if (isDragged && Input.GetMouseButtonUp(0)) { Drop(true); return; }
        if (!FormulaNode.IsPointerOverUI() && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            DraggableFormula hitBody = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponent<FormulaNode>() != null) break;
                hitBody = hit.collider.GetComponentInParent<DraggableFormula>();
                if (hitBody != null) break;
            }
            if (hitBody != this) return;
            if (Input.GetMouseButtonDown(0) && FormulaNode.activeDraggingNode == null)
            {
                DraggableFormula root = GetRootFormula();
                root.SyncMovementTarget();
                root.isDragged = true;
                Transform board = ChalkboardManager.Instance.boardTransform;
                Vector3 normal = board != null ? board.forward : -mainCam.transform.forward;
                if (new Plane(normal, root.transform.position).Raycast(ray, out float distance))
                    root.dragOffset = root.transform.position - ray.GetPoint(distance);
            }
            else if (Input.GetMouseButtonDown(1) && !GetRootFormula().isDragged)
            {
                bool hasBonds = false;
                foreach (FormulaNode node in GetOwnNodes()) if (node.isOccupied) hasBonds = true;
                if (hasBonds) BreakAllBonds();
                else ChalkboardManager.Instance.RemoveFormula(this);
            }
        }
        if (!isDragged || GetRootFormula() != this || FormulaNode.IsPointerOverUI()) return;
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f || Input.GetKeyDown(KeyCode.Q)) targetRotation *= Quaternion.Euler(0f, 0f, rotationStep);
        else if (scroll < 0f || Input.GetKeyDown(KeyCode.E)) targetRotation *= Quaternion.Euler(0f, 0f, -rotationStep);
    }

    private void CalculateDragPosition()
    {
        ChalkboardManager manager = ChalkboardManager.Instance;
        Transform board = manager.boardTransform;
        if (board == null) return;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(board.forward, board.TransformPoint(new Vector3(0f, 0f, hoverOffset)));
        if (!plane.Raycast(ray, out float distance)) return;
        Vector3 local = board.InverseTransformPoint(ray.GetPoint(distance) + dragOffset);
        local = manager.ClampMoleculeToBoardArea(this, local, 0.15f);
        local.z = hoverOffset;
        targetPosition = board.TransformPoint(local);
    }

    private void Drop(bool allowNotebook)
    {
        isDragged = false;
        ChalkboardManager manager = ChalkboardManager.Instance;
        if (manager == null) return;
        if (allowNotebook && GetConnectedParts().Count == 1 && NotebookUI.Instance != null
            && NotebookUI.Instance.TryStoreBoardFormula(this, Input.mousePosition))
        {
            manager.RemoveFormula(this);
            return;
        }
        Transform board = manager.boardTransform;
        if (board == null) return;
        transform.SetParent(board, true);
        Vector3 local = manager.ClampMoleculeToBoardArea(this, board.InverseTransformPoint(targetPosition));
        local.z = surfaceOffset;
        targetPosition = board.TransformPoint(local);
    }

    public void BreakAllBonds()
    {
        FormulaNode[] nodes = GetOwnNodes();
        foreach (FormulaNode node in nodes) node.Disconnect();
    }

    public List<FormulaItemSO> GetMoleculeData()
    {
        List<FormulaItemSO> data = new List<FormulaItemSO>();
        foreach (DraggableFormula part in GetConnectedParts()) if (part.formulaData != null) data.Add(part.formulaData);
        return data;
    }
}
