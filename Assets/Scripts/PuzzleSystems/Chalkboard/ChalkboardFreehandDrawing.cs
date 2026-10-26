using UnityEngine;

// Formül uçları ve parça gövdeleri önceliklidir; yalnız boş tahtada serbest çizim başlar.
public sealed class ChalkboardFreehandDrawing : MonoBehaviour
{
    public NotebookFreehandDrawer Drawer { get; private set; }
    private ChalkboardManager board;
    private bool drawing;
    private int mouseButton;
    private const float PixelsPerUnit = 300f;

    public void Initialize(ChalkboardManager manager)
    {
        board = manager;
        if (Drawer != null || board.boardTransform == null) return;
        var canvasObject = new GameObject("Serbest Tahta Çizimi", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(board.boardTransform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 2;
        var rect = (RectTransform)canvas.transform;
        rect.sizeDelta = (board.maxBounds - board.minBounds) * PixelsPerUnit;
        rect.localScale = Vector3.one / PixelsPerUnit;
        Vector2 center = (board.minBounds + board.maxBounds) * .5f;
        float side = Camera.main != null && Vector3.Dot(Camera.main.transform.position - board.boardTransform.position, board.boardTransform.forward) < 0 ? -1 : 1;
        rect.localPosition = new Vector3(center.x, center.y, side * .012f);
        var drawingObject = new GameObject("Tebeşir İzleri", typeof(RectTransform), typeof(NotebookFreehandDrawer));
        drawingObject.transform.SetParent(rect, false);
        Drawer = drawingObject.GetComponent<NotebookFreehandDrawer>();
        Drawer.rectTransform.sizeDelta = rect.sizeDelta;
        Drawer.ConfigurePen(new Color(.94f, .94f, .88f), 2.5f);
        // Giriş fiziksel tahta üzerinden alınır; bu canvas parça sürüklemelerini engellemez.
        Drawer.Tool = NotebookTool.Select;
    }

    private void Update()
    {
        if (board == null || !board.isMachineActive || Camera.main == null
            || (GameManager.Instance != null && GameManager.Instance.isGamePaused)
            || FormulaNode.IsPointerOverUI() || FormulaNode.activeDraggingNode != null)
        { EndDrawing(); return; }
        foreach (var formula in board.boardTransform.GetComponentsInChildren<DraggableFormula>())
            if (formula.isDragged) { EndDrawing(); return; }
        if (!board.TryGetBoardPoint(Input.mousePosition, out Vector3 world)) { EndDrawing(); return; }
        Vector2 local = Drawer.rectTransform.InverseTransformPoint(world);
        if (drawing)
        {
            if (!Input.GetMouseButton(mouseButton)) { EndDrawing(); return; }
            Drawer.ContinueStroke(local);
            return;
        }
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) return;
        foreach (var hit in Physics.RaycastAll(Camera.main.ScreenPointToRay(Input.mousePosition), 100f))
            if (hit.collider.GetComponentInParent<DraggableFormula>() != null) return;
        mouseButton = Input.GetMouseButtonDown(1) ? 1 : 0;
        drawing = true;
        Drawer.BeginStroke(local, mouseButton == 1);
    }

    private void EndDrawing()
    {
        drawing = false;
        if (Drawer != null) Drawer.EndStroke();
    }

    private void OnDisable() { EndDrawing(); }
}
