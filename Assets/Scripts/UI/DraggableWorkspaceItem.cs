using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Yalnızca pencerenin başlığına veya kenar tutamacına eklenir.</summary>
public class DraggableWorkspaceItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Çalışma Alanı")]
    [Tooltip("Taşınacak pencerenin kökü. Boşsa bu nesne kullanılır.")]
    public RectTransform target;
    [Tooltip("Pencerenin ekrandan kaybolmasını önleyen alan. Boşsa üst nesne kullanılır.")]
    public RectTransform workspaceBounds;
    [Tooltip("Pencerenin sağından çıkan ayraçlar için ek sınır; varsayılan sıfırdır.")]
    public float rightEdgeExtension;

    [Header("Fiziksel Kitap (İsteğe Bağlı)")]
    [Tooltip("Ekrandaki başlıkla birlikte kamera düzleminde taşınacak kitap.")]
    public Transform worldTarget;
    public Camera worldCamera;

    private bool dragging;
    private Vector2 previousPoint;
    private readonly Vector3[] corners = new Vector3[4];

    private bool CanDrag
    {
        get
        {
            if (NotebookWorkspaceController.Instance != null && NotebookWorkspaceController.Instance.IsPaused)
                return false;
            var book = worldTarget != null ? worldTarget.GetComponent<InteractableBook>() : null;
            return book == null || !book.isAnimating;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !CanDrag)
            return;
        if (target == null)
            target = transform as RectTransform;
        if (workspaceBounds == null && target != null)
            workspaceBounds = target.parent as RectTransform;
        if (target == null || workspaceBounds == null)
            return;
        dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            workspaceBounds, eventData.position, eventData.pressEventCamera, out previousPoint);
        if (dragging)
            target.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || !CanDrag)
        {
            dragging = false;
            return;
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            workspaceBounds, eventData.position, eventData.pressEventCamera, out var point))
            return;

        Vector2 oldScreen = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, target.position);
        target.position += workspaceBounds.TransformVector(point - previousPoint);
        previousPoint = point;
        ClampToWorkspace();

        if (worldTarget == null)
            return;
        Camera camera = worldCamera != null ? worldCamera : Camera.main;
        if (camera == null)
            return;
        Vector2 screenDelta = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, target.position) - oldScreen;
        Vector3 worldScreen = camera.WorldToScreenPoint(worldTarget.position);
        if (worldScreen.z > camera.nearClipPlane)
        {
            Vector3 shifted = worldScreen + new Vector3(screenDelta.x, screenDelta.y, 0f);
            worldTarget.position += camera.ScreenToWorldPoint(shifted) - camera.ScreenToWorldPoint(worldScreen);
        }
    }

    private void ClampToWorkspace()
    {
        target.GetWorldCorners(corners);
        Vector3 extension = target.TransformVector(new Vector3(Mathf.Max(0, rightEdgeExtension), 0, 0));
        corners[2] += extension; corners[3] += extension;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            Vector2 local = workspaceBounds.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        Rect bounds = workspaceBounds.rect;
        float x = max.x - min.x > bounds.width
            ? bounds.center.x - (min.x + max.x) * 0.5f
            : Mathf.Clamp(0f, bounds.xMin - min.x, bounds.xMax - max.x);
        float y = max.y - min.y > bounds.height
            ? bounds.yMax - max.y
            : Mathf.Clamp(0f, bounds.yMin - min.y, bounds.yMax - max.y);
        target.position += workspaceBounds.TransformVector(new Vector3(x, y, 0f));
    }

    public void OnEndDrag(PointerEventData eventData) { dragging = false; }
    private void OnDisable() { dragging = false; }
}
