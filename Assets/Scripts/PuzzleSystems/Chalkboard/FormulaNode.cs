using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Mevcut prefabların serileştirilmiş değerleri korunur.
public enum ConnectionType { Normal = 0, Core = 1, Branch = 2, DoubleBond = 3, Special = 4 }

[RequireComponent(typeof(Collider), typeof(LineRenderer))]
public class FormulaNode : MonoBehaviour
{
    [Header("Bağlantı Kuralları")]
    public bool acceptAnyFormula = true;
    public List<FormulaItemSO> allowedFormulas = new List<FormulaItemSO>();
    public ConnectionType connectionType = ConnectionType.Normal;
    public FormulaNodeState nodeState = FormulaNodeState.Open;
    public BondType bondType = BondType.Single;
    public bool isOccupied => connectedNode != null;
    [HideInInspector] public FormulaNode connectedNode;
    [HideInInspector] public DraggableFormula parentFormula;
    [HideInInspector] public bool isLineOwner;
    public static FormulaNode activeDraggingNode;

    [Header("Görsel Geribildirim (Heartbeat)")]
    public float pulseSpeed = 6f;
    public float maxPulseScale = 1.3f;
    [Header("Tebeşir Çizim Ayarları")]
    public float idealBondLength = 0.8f;
    [Min(2)] public int lineSegments = 8;
    public float chalkRoughness = 0.015f;
    [Tooltip("Çift bağın paralel çizgileri arasındaki mesafe")]
    public float doubleBondSpacing = 0.035f;

    private LineRenderer lineRenderer;
    private LineRenderer secondLine;
    private TextMeshPro blockedMark;
    private Camera mainCam;
    private bool isDraggingLine;
    private Vector3 originalScale;
    private float noiseSeed;

    private void Awake()
    {
        parentFormula = GetComponentInParent<DraggableFormula>();
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.numCapVertices = lineRenderer.numCornerVertices = 4;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;
        originalScale = transform.localScale;
        noiseSeed = Random.Range(0f, 100f);
    }

    private void OnDisable()
    {
        CancelDrawing();
        transform.localScale = originalScale;
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (secondLine != null) secondLine.enabled = false;
    }

    private void OnDestroy()
    {
        if (connectedNode == null) return;
        connectedNode.connectedNode = null;
        connectedNode.isLineOwner = false;
        connectedNode = null;
    }

    private void Update()
    {
        bool active = ChalkboardManager.Instance != null && ChalkboardManager.Instance.isMachineActive
            && (GameManager.Instance == null || !GameManager.Instance.isGamePaused);
        if (active) { HandleInput(); UpdateHeartbeatVisuals(); }
        else { CancelDrawing(); transform.localScale = originalScale; }
        UpdateBlockedVisual();
        if (isDraggingLine) DrawDraggingLine();
        else DrawConnection();
    }

    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void HandleInput()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;
        bool overUI = IsPointerOverUI();
        if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && !overUI)
        {
            if (FindNodeAtPointer() != this) return;
            if (Input.GetMouseButtonDown(1)) { Erase(); return; }
            if (nodeState == FormulaNodeState.Open && activeDraggingNode == null)
            { isDraggingLine = true; activeDraggingNode = this; }
        }
        if (isDraggingLine && Input.GetMouseButtonUp(0))
        {
            CancelDrawing();
            if (!overUI) ConnectTo(FindNodeAtPointer());
        }
    }

    private FormulaNode FindNodeAtPointer()
    {
        RaycastHit[] hits = Physics.RaycastAll(mainCam.ScreenPointToRay(Input.mousePosition), 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            FormulaNode node = hit.collider.GetComponent<FormulaNode>();
            if (node != null) return node;
            if (hit.collider.GetComponentInParent<DraggableFormula>() != null) return null;
        }
        return null;
    }

    private void CancelDrawing()
    {
        isDraggingLine = false;
        if (activeDraggingNode == this) activeDraggingNode = null;
    }

    public void Erase()
    {
        if (nodeState != FormulaNodeState.Open) { nodeState = FormulaNodeState.Open; return; }
        if (connectedNode == null) return;
        if (bondType == BondType.Double) bondType = connectedNode.bondType = BondType.Single;
        else Disconnect();
    }

    public bool CanConnect(DraggableFormula otherFormula, FormulaNode otherNode)
    {
        if (otherFormula == null || otherNode == null || otherNode == this || parentFormula == null
            || parentFormula == otherFormula || nodeState != FormulaNodeState.Open
            || otherNode.nodeState != FormulaNodeState.Open || connectionType != otherNode.connectionType) return false;
        bool upgrade = connectedNode == otherNode && otherNode.connectedNode == this;
        if ((isOccupied || otherNode.isOccupied) && !upgrade) return false;
        if (upgrade && bondType == BondType.Double) return false;
        if (!upgrade)
            foreach (FormulaNode node in parentFormula.GetOwnNodes())
                if (node.connectedNode != null && node.connectedNode.parentFormula == otherFormula) return false;
        int additionalOrder = upgrade ? 1 : connectionType == ConnectionType.DoubleBond ? 2 : 1;
        if (parentFormula.UsedCapacity + additionalOrder > parentFormula.ConnectionCapacity) return false;
        return IsFormulaAllowed(otherFormula);
    }

    private bool IsFormulaAllowed(DraggableFormula otherFormula)
    {
        if (acceptAnyFormula || (parentFormula.formulaData != null && parentFormula.formulaData.useUniversalNodeRules)) return true;
        if (allowedFormulas == null || allowedFormulas.Count == 0) return true;
        if (otherFormula.formulaData == null) return false;
        foreach (FormulaItemSO allowed in allowedFormulas)
            if (allowed != null && (allowed.itemID == otherFormula.formulaData.itemID
                || allowed.itemID == otherFormula.formulaData.connectionAliasID)) return true;
        return false;
    }

    public bool CanRestoreConnection(FormulaNode other, BondType savedType)
    {
        if (other == null || other == this || parentFormula == null || other.parentFormula == null
            || parentFormula == other.parentFormula || isOccupied || other.isOccupied
            || nodeState != FormulaNodeState.Open || other.nodeState != FormulaNodeState.Open
            || connectionType != other.connectionType || (savedType != BondType.Single && savedType != BondType.Double)) return false;
        foreach (FormulaNode own in parentFormula.GetOwnNodes())
            if (own.connectedNode != null && own.connectedNode.parentFormula == other.parentFormula) return false;
        int order = (int)savedType;
        return parentFormula.UsedCapacity + order <= parentFormula.ConnectionCapacity
            && other.parentFormula.UsedCapacity + order <= other.parentFormula.ConnectionCapacity
            && IsFormulaAllowed(other.parentFormula) && other.IsFormulaAllowed(parentFormula);
    }

    public void ConnectTo(FormulaNode otherNode)
    {
        if (otherNode == null || !CanConnect(otherNode.parentFormula, otherNode)
            || !otherNode.CanConnect(parentFormula, this)) return;
        bool upgrade = connectedNode == otherNode;
        bondType = upgrade || connectionType == ConnectionType.DoubleBond ? BondType.Double : BondType.Single;
        otherNode.bondType = bondType;
        connectedNode = otherNode;
        otherNode.connectedNode = this;
        isLineOwner = true;
        otherNode.isLineOwner = false;
        if (!upgrade && ChalkboardManager.Instance != null) ChalkboardManager.Instance.RebuildFormulaGroups();
    }

    public void Disconnect()
    {
        if (connectedNode == null) return;
        FormulaNode other = connectedNode;
        connectedNode = other.connectedNode = null;
        isLineOwner = other.isLineOwner = false;
        bondType = other.bondType = BondType.Single;
        if (ChalkboardManager.Instance != null) ChalkboardManager.Instance.RebuildFormulaGroups();
    }

    private void DrawDraggingLine()
    {
        if (mainCam == null) return;
        Transform board = ChalkboardManager.Instance.boardTransform;
        Vector3 normal = board != null ? board.forward : -mainCam.transform.forward;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (new Plane(normal, transform.position).Raycast(ray, out float distance))
            DrawLine(lineRenderer, transform.position, ray.GetPoint(distance), 0f);
        if (secondLine != null) secondLine.enabled = false;
    }

    private void DrawConnection()
    {
        lineRenderer.enabled = isOccupied && isLineOwner;
        if (secondLine != null) secondLine.enabled = false;
        if (!lineRenderer.enabled) return;
        bool isDouble = bondType == BondType.Double;
        DrawLine(lineRenderer, transform.position, connectedNode.transform.position, isDouble ? -doubleBondSpacing * 0.5f : 0f);
        if (!isDouble) return;
        if (secondLine == null)
        {
            GameObject second = new GameObject("Çift Bağ Çizgisi");
            second.transform.SetParent(transform, false);
            secondLine = second.AddComponent<LineRenderer>();
            secondLine.sharedMaterials = lineRenderer.sharedMaterials;
            secondLine.widthMultiplier = lineRenderer.widthMultiplier;
            secondLine.widthCurve = lineRenderer.widthCurve;
            secondLine.colorGradient = lineRenderer.colorGradient;
            secondLine.numCapVertices = lineRenderer.numCapVertices;
            secondLine.numCornerVertices = lineRenderer.numCornerVertices;
            secondLine.sortingLayerID = lineRenderer.sortingLayerID;
            secondLine.sortingOrder = lineRenderer.sortingOrder;
            secondLine.useWorldSpace = true;
        }
        DrawLine(secondLine, transform.position, connectedNode.transform.position, doubleBondSpacing * 0.5f);
    }

    private void DrawLine(LineRenderer line, Vector3 start, Vector3 end, float offset)
    {
        Transform board = ChalkboardManager.Instance != null ? ChalkboardManager.Instance.boardTransform : null;
        Vector3 normal = board != null ? board.forward : Vector3.forward;
        Vector3 right = board != null ? board.right : Vector3.right;
        Vector3 up = board != null ? board.up : Vector3.up;
        Vector3 side = Vector3.Cross(normal, end - start).normalized * offset;
        int count = Mathf.Max(2, lineSegments);
        line.enabled = true;
        line.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = Vector3.Lerp(start, end, i / (float)(count - 1)) + side;
            if (i > 0 && i < count - 1)
            {
                point += right * ((Mathf.PerlinNoise(noiseSeed, i * 2f + offset) - 0.5f) * chalkRoughness);
                point += up * ((Mathf.PerlinNoise(i * 2f, noiseSeed + offset) - 0.5f) * chalkRoughness);
            }
            line.SetPosition(i, point);
        }
    }

    private void UpdateBlockedVisual()
    {
        bool blocked = nodeState != FormulaNodeState.Open;
        if (!blocked && blockedMark == null) return;
        if (blockedMark == null)
        {
            GameObject mark = new GameObject("Kapalı Uç (x)");
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = new Vector3(0f, 0f, -0.015f);
            blockedMark = mark.AddComponent<TextMeshPro>();
            blockedMark.text = "x";
            blockedMark.alignment = TextAlignmentOptions.Center;
            blockedMark.fontSize = 5f;
            blockedMark.rectTransform.sizeDelta = Vector2.one;
            blockedMark.raycastTarget = false;
        }
        blockedMark.gameObject.SetActive(blocked);
        blockedMark.color = nodeState == FormulaNodeState.Unstable ? new Color(1f, 0.55f, 0.2f) : Color.white;
    }

    private void UpdateHeartbeatVisuals()
    {
        bool pulse = !isOccupied && nodeState == FormulaNodeState.Open;
        if (pulse && activeDraggingNode != null && activeDraggingNode != this)
            pulse = CanConnect(activeDraggingNode.parentFormula, activeDraggingNode) && activeDraggingNode.CanConnect(parentFormula, this);
        float scale = pulse ? Mathf.Lerp(1f, maxPulseScale, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f) : 1f;
        transform.localScale = originalScale * scale;
    }
}
