using System.Collections.Generic;
using UnityEngine;

public enum ConnectionType
{
    Normal,
    Core,
    Branch,
    DoubleBond,
    Special,
}

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(LineRenderer))]
public class FormulaNode : MonoBehaviour
{
    [Header("Bağlantı Kuralları")]
    public bool acceptAnyFormula = true;
    public List<FormulaItemSO> allowedFormulas = new List<FormulaItemSO>();
    public ConnectionType connectionType = ConnectionType.Normal;

    public bool isOccupied => connectedNode != null;

    [HideInInspector]
    public FormulaNode connectedNode;

    [HideInInspector]
    public DraggableFormula parentFormula;

    private LineRenderer lineRenderer;
    private Camera mainCam;
    private bool isDraggingLine = false;

    [HideInInspector]
    public bool isLineOwner = false;

    public static FormulaNode activeDraggingNode;

    [Header("Görsel Geribildirim (Heartbeat)")]
    public float pulseSpeed = 6f;
    public float maxPulseScale = 1.3f;
    private Vector3 originalScale;
    private float noiseSeedX;
    private float noiseSeedY;

    // ==========================================
    // YENİ: TEBEŞİR VE ÇİZİM AYARLARI
    // ==========================================
    [Header("Tebeşir Çizim Ayarları")]
    [Tooltip("Bağlanan iki formülün arasındaki kusursuz uzaklık")]
    public float idealBondLength = 0.8f;

    [Tooltip("Tebeşir çizgisinin pürüzlülük/titreme detayı")]
    public int lineSegments = 8;

    [Tooltip("Pürüzlerin (tebeşir tozunun) ne kadar dağınık duracağı")]
    public float chalkRoughness = 0.015f;

    private void Awake()
    {
        parentFormula = GetComponentInParent<DraggableFormula>();
        lineRenderer = GetComponent<LineRenderer>();
        mainCam = Camera.main;

        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.enabled = false;

        originalScale = transform.localScale;

        // YENİ: Her objeye özel sabit bir rastgele sayı atıyoruz
        noiseSeedX = Random.Range(0f, 100f);
        noiseSeedY = Random.Range(0f, 100f);
    }

    private void OnDisable()
    {
        if (activeDraggingNode == this)
            activeDraggingNode = null;

        isDraggingLine = false;
        transform.localScale = originalScale;
    }

    private void Update()
    {
        if (
            ChalkboardManager.Instance == null
            || !ChalkboardManager.Instance.isMachineActive
            || (GameManager.Instance != null && GameManager.Instance.isGamePaused)
        )
        {
            transform.localScale = originalScale;
            return;
        }

        CheckForInitialClick();
        UpdateLineState();
        UpdateHeartbeatVisuals();
    }

    private void CheckForInitialClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 15f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == this.gameObject)
                {
                    if (!isOccupied)
                    {
                        isDraggingLine = true;
                        activeDraggingNode = this;
                        lineRenderer.enabled = true;
                    }
                    break;
                }
                if (
                    hit.collider.GetComponent<FormulaNode>() != null
                    || hit.collider.GetComponent<DraggableFormula>() != null
                )
                {
                    break;
                }
            }
        }
    }

    private void UpdateLineState()
    {
        if (isDraggingLine)
        {
            HandleLineDragging();
        }
        else if (isOccupied && connectedNode != null && isLineOwner)
        {
            lineRenderer.enabled = true;
            // Çizgiyi sertçe germek yerine organik tebeşir şeklinde çiziyoruz
            DrawRoughChalkLine(transform.position, connectedNode.transform.position);
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    private void HandleLineDragging()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        Transform board = ChalkboardManager.Instance.boardTransform;
        Vector3 boardNormal = board != null ? board.forward : -mainCam.transform.forward;

        if (Vector3.Dot(ray.direction, boardNormal) > 0)
            boardNormal = -boardNormal;

        Plane boardPlane = new Plane(boardNormal, transform.position);
        if (boardPlane.Raycast(ray, out float distance))
        {
            Vector3 mousePos = ray.GetPoint(distance);

            // Faremizle çizerken bile tebeşir hissini veriyoruz
            DrawRoughChalkLine(transform.position, mousePos);
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDraggingLine = false;
            if (activeDraggingNode == this)
                activeDraggingNode = null;

            TryConnect(ray);
        }
    }

    // --- TEBEŞİR ÇİZİM SİMÜLASYONU ---
    private void DrawRoughChalkLine(Vector3 start, Vector3 end)
    {
        lineRenderer.positionCount = lineSegments;
        for (int i = 0; i < lineSegments; i++)
        {
            float t = i / (float)(lineSegments - 1);
            Vector3 point = Vector3.Lerp(start, end, t);

            // Çizginin başı ve sonu tam Node'lara otursun, aradaki kısımlar pürüzlü olsun
            if (i > 0 && i < lineSegments - 1)
            {
                // DÜZELTME BURADA: Time.time YERİNE SABİT SEED KULLANIYORUZ
                // Artık zamanla değişmeyecek, olduğu yerde donuk ve pürüzlü duracak!
                float noiseX = (Mathf.PerlinNoise(noiseSeedX, i * 2f) - 0.5f) * chalkRoughness;
                float noiseY = (Mathf.PerlinNoise(i * 2f, noiseSeedY) - 0.5f) * chalkRoughness;

                point.x += noiseX;
                point.y += noiseY;
            }

            lineRenderer.SetPosition(i, point);
        }
    }

    private void TryConnect(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 15f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            FormulaNode targetNode = hit.collider.GetComponent<FormulaNode>();
            if (targetNode != null && targetNode != this)
            {
                DraggableFormula myRoot = this.parentFormula.GetRootFormula();
                DraggableFormula targetRoot = targetNode.parentFormula.GetRootFormula();

                if (myRoot != targetRoot)
                {
                    if (
                        CanConnect(targetNode.parentFormula, targetNode)
                        && targetNode.CanConnect(this.parentFormula, this)
                    )
                    {
                        ConnectTo(targetNode);
                        this.isLineOwner = true;
                        targetNode.isLineOwner = false;

                        // ========================================================
                        // KUSURSUZ BAĞ HİZALAMASI (SERT GERGİNLİĞİ BİTİREN KISIM)
                        // ========================================================
                        Vector3 direction = (
                            targetNode.transform.position - transform.position
                        ).normalized;
                        if (direction == Vector3.zero)
                            direction = Vector3.right;

                        // İki formülün olması gereken o "mükemmel" mesafeyi hesapla
                        Vector3 idealNodePos = transform.position + (direction * idealBondLength);
                        Vector3 offsetToMove = idealNodePos - targetNode.transform.position;

                        // Diğer formülü zorla ışınlamak yerine hedefini (targetPosition) güncelliyoruz.
                        // Böylece formül, tebeşir tahtasında yumuşakça kayarak yerine oturuyor!
                        targetRoot.targetPosition += offsetToMove;

                        targetRoot.transform.SetParent(myRoot.transform);
                        return;
                    }
                }
            }

            if (hit.collider.GetComponent<DraggableFormula>() != null)
            {
                break;
            }
        }
        lineRenderer.enabled = false;
    }

    public void Disconnect()
    {
        if (connectedNode != null)
        {
            this.isLineOwner = false;
            connectedNode.isLineOwner = false;
            connectedNode.connectedNode = null;
            connectedNode = null;
        }
    }

    public bool CanConnect(DraggableFormula otherFormula, FormulaNode otherNode)
    {
        if (isOccupied)
            return false;
        if (this.connectionType != otherNode.connectionType)
            return false;
        if (acceptAnyFormula)
            return true;

        if (allowedFormulas.Count > 0)
        {
            foreach (var allowedSO in allowedFormulas)
            {
                if (allowedSO != null && allowedSO.itemID == otherFormula.formulaData.itemID)
                    return true;
            }
            return false;
        }
        return true;
    }

    private void UpdateHeartbeatVisuals()
    {
        if (isOccupied)
        {
            transform.localScale = originalScale;
            return;
        }

        bool shouldPulse = false;

        if (activeDraggingNode == null)
        {
            shouldPulse = true;
        }
        else if (activeDraggingNode == this)
        {
            shouldPulse = true;
        }
        else
        {
            DraggableFormula activeRoot = activeDraggingNode.parentFormula.GetRootFormula();
            DraggableFormula myRoot = this.parentFormula.GetRootFormula();

            if (activeRoot != myRoot)
            {
                if (
                    CanConnect(activeDraggingNode.parentFormula, activeDraggingNode)
                    && activeDraggingNode.CanConnect(this.parentFormula, this)
                )
                {
                    shouldPulse = true;
                }
            }
        }

        if (shouldPulse)
        {
            float sineWave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            float scaleMulti = Mathf.Lerp(1f, maxPulseScale, sineWave);
            transform.localScale = originalScale * scaleMulti;
        }
        else
        {
            transform.localScale = originalScale;
        }
    }

    public void ConnectTo(FormulaNode otherNode)
    {
        connectedNode = otherNode;
        otherNode.connectedNode = this;
    }
}
