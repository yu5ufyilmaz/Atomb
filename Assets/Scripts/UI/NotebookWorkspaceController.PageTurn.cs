using UnityEngine;
using UnityEngine.UI;

public partial class NotebookWorkspaceController
{
    private const float NotebookPageTurnDuration = .2f;
    private RectTransform pageTurnLayer, pageTurnLeaf, pageTurnShadow, pageTurnSurface;
    private CanvasGroup pageTurnOpacity;
    private Vector2 pageTurnRestPosition;
    private float pageTurnRestAlpha, pageTurnElapsed = -1;
    private int pageTurnDirection;
    private bool openingNotebook;

    private void BeginNotebookPageTurn(int direction)
    {
        ResetNotebookPageTurn();
        if (!UsesNotebookSkin || openingNotebook || !IsActive || IsPaused || direction == 0
            || notebookWindow == null || !notebookWindow.gameObject.activeInHierarchy
            || !notebookExpanded || notebookReveal < 1) return;

        pageTurnSurface = page.gameObject.activeSelf ? page
            : targetPanel.gameObject.activeSelf ? targetPanel : informationPanel;
        if (!pageTurnSurface.gameObject.activeSelf) { pageTurnSurface = null; return; }
        pageTurnRestPosition = pageTurnSurface.anchoredPosition;
        pageTurnOpacity = pageTurnSurface.GetComponent<CanvasGroup>();
        if (pageTurnOpacity == null) pageTurnOpacity = pageTurnSurface.gameObject.AddComponent<CanvasGroup>();
        pageTurnRestAlpha = pageTurnOpacity.alpha;
        pageTurnDirection = direction > 0 ? 1 : -1;

        if (pageTurnLayer == null)
        {
            pageTurnLayer = Panel("Sayfa Çevirme", notebookWindow, Vector2.zero, Color.clear);
            pageTurnLayer.GetComponent<Image>().raycastTarget = false;
            pageTurnLayer.gameObject.AddComponent<RectMask2D>();
            pageTurnShadow = Panel("Sayfa Gölgesi", pageTurnLayer, Vector2.zero, Color.clear);
            pageTurnShadow.GetComponent<Image>().raycastTarget = false;
            pageTurnLeaf = Panel("Çevrilen Kağıt", pageTurnLayer, Vector2.zero, Color.clear);
            var leaf = pageTurnLeaf.GetComponent<Image>();
            leaf.sprite = notePaperSprite;
            leaf.type = notePaperSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            leaf.raycastTarget = false;
        }
        pageTurnLayer.anchorMin = pageTurnSurface.anchorMin;
        pageTurnLayer.anchorMax = pageTurnSurface.anchorMax;
        pageTurnLayer.pivot = pageTurnSurface.pivot;
        pageTurnLayer.anchoredPosition = pageTurnRestPosition;
        pageTurnLayer.sizeDelta = pageTurnSurface.sizeDelta;
        pageTurnLayer.SetAsLastSibling();
        pageTurnLayer.gameObject.SetActive(true);
        pageTurnElapsed = 0;
        UpdateNotebookPageTurn(0);
    }

    private void ResetNotebookPageTurn()
    {
        if (pageTurnSurface != null) pageTurnSurface.anchoredPosition = pageTurnRestPosition;
        if (pageTurnOpacity != null) pageTurnOpacity.alpha = pageTurnRestAlpha;
        if (pageTurnLayer != null) pageTurnLayer.gameObject.SetActive(false);
        pageTurnSurface = null;
        pageTurnOpacity = null;
        pageTurnElapsed = -1;
    }

    private void UpdateNotebookPageTurn(float delta)
    {
        if (pageTurnElapsed < 0) return;
        if (!IsActive || IsPaused || !notebookExpanded || notebookReveal < 1 || pageTurnSurface == null)
        { ResetNotebookPageTurn(); return; }
        pageTurnElapsed += Mathf.Max(0, delta);
        float t = Mathf.Clamp01(pageTurnElapsed / NotebookPageTurnDuration);
        if (t >= 1) { ResetNotebookPageTurn(); return; }

        float eased = Mathf.SmoothStep(0, 1, t);
        float fold = Mathf.Sin(t * Mathf.PI);
        pageTurnSurface.anchoredPosition = pageTurnRestPosition + new Vector2(pageTurnDirection * 10 * (1 - eased), 0);
        pageTurnOpacity.alpha = pageTurnRestAlpha * Mathf.Lerp(.82f, 1, eased);

        float width = 12 + 64 * fold;
        float edge = pageTurnDirection * Mathf.Lerp(pageTurnLayer.rect.width * .5f + 40,
            -pageTurnLayer.rect.width * .5f - 40, eased);
        pageTurnLeaf.sizeDelta = new Vector2(width, pageTurnLayer.rect.height + 12);
        pageTurnLeaf.anchoredPosition = new Vector2(edge, 0);
        pageTurnLeaf.localRotation = Quaternion.Euler(0, 0, -pageTurnDirection * 2 * fold);
        pageTurnLeaf.GetComponent<Image>().color = new Color(.79f, .80f, .76f, .48f * fold);
        pageTurnShadow.sizeDelta = new Vector2(20, pageTurnLayer.rect.height);
        pageTurnShadow.anchoredPosition = new Vector2(edge - pageTurnDirection * (width * .5f + 7), 0);
        pageTurnShadow.GetComponent<Image>().color = new Color(.16f, .12f, .07f, .12f * fold);
    }
}
