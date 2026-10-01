using System.Collections.Generic;
using UnityEngine;

// 1. AÇILIR LİSTE (DROPDOWN) SEÇENEKLERİMİZ
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
    public bool isLineOwner = false; // Çizgiyi çeken biz miyiz?

    private void Awake()
    {
        parentFormula = GetComponentInParent<DraggableFormula>();
        lineRenderer = GetComponent<LineRenderer>();
        mainCam = Camera.main;

        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
    }

    private void Update()
    {
        // 1. Oyun durmuşsa veya makine aktif değilse alt satırlara hiç inme (Performans)
        if (
            ChalkboardManager.Instance == null
            || !ChalkboardManager.Instance.isMachineActive
            || (GameManager.Instance != null && GameManager.Instance.isGamePaused)
        )
            return;

        // 2. Yeni bir çizgi çekilmeye başlanıyor mu kontrol et
        CheckForInitialClick();

        // 3. Çizginin durumuna (sürüklenme veya bağlı olma) göre görselini güncelle
        UpdateLineState();
    }

    private void CheckForInitialClick()
    {
        // Sadece sol tıka o an basıldıysa tarama yap
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 15f);

            foreach (var hit in hits)
            {
                // Eğer fare tam olarak bizim üzerimizdeyse ve boşsak
                if (hit.collider.gameObject == this.gameObject)
                {
                    if (!isOccupied)
                    {
                        isDraggingLine = true;
                        lineRenderer.enabled = true;
                    }
                    break;
                }
            }
        }
    }

    private void UpdateLineState()
    {
        // Durum A: Fare ile kablo çekiyoruz
        if (isDraggingLine)
        {
            HandleLineDragging();
        }
        // Durum B: Başka bir node'a zaten bağlandık ve çizgiyi BİZ çektik
        else if (isOccupied && connectedNode != null && isLineOwner)
        {
            KeepLineConnected();
        }
        // Durum C: Hiçbiri değilse çizgiyi gizle
        else
        {
            lineRenderer.enabled = false;
        }
    }

    private void HandleLineDragging()
    {
        // Çizginin başı hep kendi merkezimizde
        lineRenderer.SetPosition(0, transform.position);

        // Çizginin ucu fareyi takip etsin
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane boardPlane = new Plane(-mainCam.transform.forward, transform.position);

        if (boardPlane.Raycast(ray, out float distance))
        {
            Vector3 mousePos = ray.GetPoint(distance);
            lineRenderer.SetPosition(1, mousePos);
        }

        // Fare bırakıldığında bağlanmayı dene
        if (Input.GetMouseButtonUp(0))
        {
            isDraggingLine = false;
            TryConnect(ray);
        }
    }

    private void KeepLineConnected()
    {
        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, connectedNode.transform.position);
    }

    private void TryConnect(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 15f);
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

                        // DEĞİŞEN KISIM: Çizgiyi çeken biz olduğumuz için sahibi biziz, karşı taraf değil.
                        this.isLineOwner = true;
                        targetNode.isLineOwner = false;

                        targetRoot.transform.SetParent(myRoot.transform);
                        return;
                    }
                }
            }
        }
        lineRenderer.enabled = false;
    }

    public void Disconnect()
    {
        if (connectedNode != null)
        {
            // DEĞİŞEN KISIM: Ayrılırken her iki tarafın sahipliğini sıfırla
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

    public void ConnectTo(FormulaNode otherNode)
    {
        connectedNode = otherNode;
        otherNode.connectedNode = this;
    }
}
