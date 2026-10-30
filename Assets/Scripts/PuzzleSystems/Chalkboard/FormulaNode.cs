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
    [HideInInspector] public FormulaNodeState nodeState = FormulaNodeState.Open; // Eski kayıt/prefab alanı.
    [HideInInspector] public bool doubleBondSocket;
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
    private LineRenderer previewLine;
    private ChalkBondStroke chalkConnection, chalkSecond, chalkPreview;
    private TextMeshPro socketMark;
    private Camera mainCam;
    private bool isDraggingLine;
    private Vector3 originalScale;
    private float noiseSeed;

    public bool IsDoubleBondSocket => doubleBondSocket || connectionType == ConnectionType.DoubleBond;
    public string SocketHint => IsDoubleBondSocket || (isOccupied && bondType == BondType.Double)
        ? "= " + (isOccupied ? (int)bondType : 0) + "/2" : "";

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
        if (previewLine != null) previewLine.enabled = false;
        HideChalk();
    }

    private void OnDestroy()
    {
        DestroyChalk(chalkConnection); DestroyChalk(chalkSecond); DestroyChalk(chalkPreview);
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
        nodeState = FormulaNodeState.Open;
        UpdateSocketVisual();
        DrawConnection();
        if (isDraggingLine) DrawDraggingLine();
        else
        {
            if (previewLine != null) previewLine.enabled = false;
            if (chalkPreview != null) chalkPreview.gameObject.SetActive(false);
        }
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
        if ((GameInput.Pressed("Interact") || GameInput.Pressed("SecondaryInteract")) && !overUI)
        {
            if (FindNodeAtPointer() != this) return;
            if (GameInput.Pressed("SecondaryInteract")) { Erase(); return; }
            if (activeDraggingNode == null)
            { isDraggingLine = true; activeDraggingNode = this; }
        }
        if (isDraggingLine && GameInput.Released("Interact"))
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
        if (previewLine != null) previewLine.enabled = false;
        if (chalkPreview != null) chalkPreview.gameObject.SetActive(false);
        if (activeDraggingNode == this) activeDraggingNode = null;
    }

    public void Erase()
    {
        if (connectedNode == null) return;
        if (bondType == BondType.Double) bondType = connectedNode.bondType = BondType.Single;
        else Disconnect();
    }

    public bool CanConnect(DraggableFormula otherFormula, FormulaNode otherNode)
    {
        if (otherFormula == null || otherNode == null || otherNode == this || parentFormula == null
            || parentFormula == otherFormula || connectionType != otherNode.connectionType) return false;
        if (!SignalPort.Compatible(this, otherNode)) return false;
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
            || connectionType != other.connectionType || (savedType != BondType.Single && savedType != BondType.Double)) return false;
        if (!SignalPort.Compatible(this, other) || (GetComponent<SignalPort>() != null && savedType != BondType.Single)) return false;
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
        {
            if (UsesChalk)
            {
                DrawChalk(ref chalkPreview, "Tebeşir Bağ Önizlemesi", transform.position, ray.GetPoint(distance), 0);
                return;
            }
            if (previewLine == null) previewLine = CreateExtraLine("Bağ Önizlemesi");
            DrawLine(previewLine, transform.position, ray.GetPoint(distance), 0f);
        }
    }

    private void DrawConnection()
    {
        if (UsesChalk)
        {
            lineRenderer.enabled = false;
            if (secondLine != null) secondLine.enabled = false;
            if (chalkConnection != null) chalkConnection.gameObject.SetActive(false);
            if (chalkSecond != null) chalkSecond.gameObject.SetActive(false);
            if (!isOccupied || !isLineOwner) return;
            bool two = bondType == BondType.Double;
            DrawChalk(ref chalkConnection, "Tebeşir Bağı", transform.position, connectedNode.transform.position, two ? -doubleBondSpacing * .5f : 0);
            if (two) DrawChalk(ref chalkSecond, "Tebeşir Çift Bağ", transform.position, connectedNode.transform.position, doubleBondSpacing * .5f);
            return;
        }
        lineRenderer.enabled = isOccupied && isLineOwner;
        if (secondLine != null) secondLine.enabled = false;
        if (!lineRenderer.enabled) return;
        bool isDouble = bondType == BondType.Double;
        DrawLine(lineRenderer, transform.position, connectedNode.transform.position, isDouble ? -doubleBondSpacing * 0.5f : 0f);
        if (!isDouble) return;
        if (secondLine == null)
            secondLine = CreateExtraLine("Çift Bağ Çizgisi");
        DrawLine(secondLine, transform.position, connectedNode.transform.position, doubleBondSpacing * 0.5f);
    }

    private bool UsesChalk => parentFormula != null && parentFormula.GetComponent<ChalkFormulaSketch>() != null
        && ChalkboardManager.Instance != null && ChalkboardManager.Instance.boardTransform != null;

    private void DrawChalk(ref ChalkBondStroke stroke, string label, Vector3 start, Vector3 end, float offset)
    {
        Transform board = ChalkboardManager.Instance.boardTransform;
        if (stroke == null)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(board, false);
            stroke = obj.AddComponent<ChalkBondStroke>();
        }
        Vector3 origin = board.InverseTransformPoint(start);
        stroke.Draw(board, lineRenderer.sharedMaterial, start, end, offset, origin.x * 23.7f + origin.y * 11.3f);
    }

    private void HideChalk()
    {
        if (chalkConnection != null) chalkConnection.gameObject.SetActive(false);
        if (chalkSecond != null) chalkSecond.gameObject.SetActive(false);
        if (chalkPreview != null) chalkPreview.gameObject.SetActive(false);
    }

    private static void DestroyChalk(ChalkBondStroke stroke)
    {
        if (stroke == null) return;
        if (Application.isPlaying) Destroy(stroke.gameObject); else DestroyImmediate(stroke.gameObject);
    }

    private LineRenderer CreateExtraLine(string label)
    {
        var obj = new GameObject(label);
        obj.transform.SetParent(transform, false);
        var line = obj.AddComponent<LineRenderer>();
        line.sharedMaterials = lineRenderer.sharedMaterials;
        line.widthMultiplier = lineRenderer.widthMultiplier;
        line.widthCurve = lineRenderer.widthCurve;
        line.colorGradient = lineRenderer.colorGradient;
        line.numCapVertices = lineRenderer.numCapVertices;
        line.numCornerVertices = lineRenderer.numCornerVertices;
        line.sortingLayerID = lineRenderer.sortingLayerID;
        line.sortingOrder = lineRenderer.sortingOrder;
        line.alignment = LineAlignment.View;
        line.useWorldSpace = true;
        line.shadowCastingMode = lineRenderer.shadowCastingMode;
        line.receiveShadows = lineRenderer.receiveShadows;
        return line;
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

    private void UpdateSocketVisual()
    {
        string hint = SocketHint;
        if (hint.Length == 0 && socketMark == null) return;
        if (socketMark == null)
        {
            GameObject mark = new GameObject("Bağ Türü");
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = new Vector3(0f, .48f, 0f);
            mark.transform.localRotation = Quaternion.Euler(0, 180, 0);
            socketMark = mark.AddComponent<TextMeshPro>();
            socketMark.alignment = TextAlignmentOptions.Center;
            socketMark.fontSize = 3.6f;
            socketMark.rectTransform.sizeDelta = new Vector2(2f, .5f);
            socketMark.textWrappingMode = TextWrappingModes.NoWrap;
            socketMark.raycastTarget = false;
            socketMark.color = new Color(1f, .9f, .52f);
        }
        socketMark.gameObject.SetActive(hint.Length > 0);
        socketMark.text = hint;
    }

    private void UpdateHeartbeatVisuals()
    {
        bool pulse = !isOccupied;
        if (pulse && activeDraggingNode != null && activeDraggingNode != this)
            pulse = CanConnect(activeDraggingNode.parentFormula, activeDraggingNode) && activeDraggingNode.CanConnect(parentFormula, this);
        float scale = pulse ? Mathf.Lerp(1f, maxPulseScale, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f) : 1f;
        transform.localScale = originalScale * scale;
    }
}
