using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class NotebookDraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private const string ClueImageName = "ClueImage";
    private const string ClueFrameName = "ClueFrame";
    private static readonly Color ClueIdleFill = new Color(1f, 0.77f, 0.19f, 0.05f);
    private static readonly Color ClueHoverFill = new Color(1f, 0.85f, 0.3f, 0.22f);
    private static readonly Color ClueIdleFrame = new Color(1f, 0.77f, 0.19f, 0.85f);
    private static readonly Color ClueHoverFrame = new Color(1f, 0.95f, 0.5f, 1f);
    private Image[] clueFrame;
    private static Material overlayMaterial;

    // Kitap yüzeyindeki bölge, sayfa meshinin altında/içinde kalsa bile görünsün diye derinlik testi kapalı materyal.
    private static Material OverlayMaterial
    {
        get
        {
            if (overlayMaterial == null)
            {
                overlayMaterial = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "ClueOverlay (runtime)" };
                overlayMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            return overlayMaterial;
        }
    }

    [Header("Blok Görünümü")]
    [SerializeField] private Image background;
    [SerializeField] private Image sketchImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    public NotebookBlockData Data { get; private set; }
    public bool IsSource { get; private set; }
    public string DisplayTitle
    {
        get
        {
            var part = Data?.blockType == NotebookBlockType.PartClue ? PuzzleParts.Find(Data.itemID) : null;
            string role = part == null ? null : part.inputs == 0 ? "BAŞLATICI ›" : part.outputs == 0 ? "› SONLANDIRICI" : "› İŞLEM ›";
            return part == null ? GameLocalization.Translate(Data?.title)
                : GameLocalization.Format("{0}\n<size=65%>{1}</size>", Data.title, role);
        }
    }

    public void SetDisplayTitle(TMP_Text target)
    {
        var part = Data?.blockType == NotebookBlockType.PartClue ? PuzzleParts.Find(Data.itemID) : null;
        if (part == null) GameLocalization.Set(target, Data?.title);
        else GameLocalization.SetFormat(target, "{0}\n<size=65%>{1}</size>", Data.title,
            part.inputs == 0 ? "BAŞLATICI ›" : part.outputs == 0 ? "› SONLANDIRICI" : "› İŞLEM ›");
    }

    public void UsePrintedTitle()
    {
        if (Data.blockType == NotebookBlockType.PartClue) SetDisplayTitle(titleText);
        else GameLocalization.Set(titleText, Data.title);
    }

    private RectTransform floatingRect;
    private CanvasGroup canvasGroup;
    private bool dragging;
    private float originalAlpha;
    private bool originalRaycastState;
    private Vector3 pointerOffset;

    public void Initialize(NotebookBlockData data, bool source)
    {
        CancelDrag();
        Data = data != null ? data.Clone() : new NotebookBlockData();
        IsSource = source;
        var formula = Data.blockType == NotebookBlockType.PartClue && FormulaCatalog.Instance != null
            ? FormulaCatalog.Instance.ResolveFormula(Data.itemID) : null;
        if (formula != null)
        {
            if (formula.doubleBondNodes != null && formula.doubleBondNodes.Count > 0 && !(Data.title ?? "").Contains("ÇİFT BAĞ"))
                Data.title += " [ÇİFT BAĞ]";
            if ((Data.description ?? "").Contains("(x)")) Data.description = formula.description;
        }
        if (Data.blockType == NotebookBlockType.RuleClue && (Data.description ?? "").Contains("Kapalı (x) uç"))
        {
            Data.title = "OPERASYON / ÇİFT BAĞ";
            Data.description = "Yuvalar doğrudan kullanılabilir. (=) çift bağ yuvasıdır. Aynı iki uç arasında ikinci kez çiz: 1/2 > 2/2. Mevcut bağı silme.";
        }
        canvasGroup = GetComponent<CanvasGroup>();
        RenderBlock();
        NotebookWorkspaceController.Instance?.StyleNotebookNote(this);
        foreach (var localized in GetComponentsInChildren<LocalizedText>(true)) localized.RefreshBaseline();
    }

    private void RenderBlock()
    {
        RectTransform rect = (RectTransform)transform;
        if (TryRenderClueBlock(rect)) return;
        bool quest = Data.blockType == NotebookBlockType.Quest;
        bool researchNote = ResearchPuzzleTrial.IsResearch(ChalkboardManager.Instance);
        bool autoBuilt = background == null && titleText == null;
        if (autoBuilt || rect.sizeDelta.x <= 1f || rect.sizeDelta.y <= 1f)
            rect.sizeDelta = quest ? researchNote ? new Vector2(380f, 400f) : new Vector2(320f, 190f)
                : new Vector2(Data.blockType == NotebookBlockType.RuleClue ? 320f : 200f, 115f);
        if (background == null)
            background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        background.raycastTarget = true;
        background.color = quest
            ? new Color(0.94f, 0.82f, 0.49f, 0.98f)
            : Data.blockType == NotebookBlockType.RuleClue
                ? new Color(0.71f, 0.83f, 0.91f, 0.98f)
                : new Color(0.96f, 0.92f, 0.79f, 0.98f);
        var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        outline.effectColor = quest ? new Color(0.43f, 0.27f, 0.08f) : new Color(0.24f, 0.24f, 0.22f);
        outline.effectDistance = quest ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
        outline.enabled = IsSource;
        if (!IsSource)
        {
            var border = GetComponentInChildren<NotebookSketchBorder>();
            if (border == null)
            {
                var borderObject = new GameObject("El Çizimi Çerçeve", typeof(RectTransform));
                borderObject.transform.SetParent(transform, false);
                borderObject.transform.SetAsFirstSibling();
                border = borderObject.AddComponent<NotebookSketchBorder>();
                border.rectTransform.anchorMin = Vector2.zero;
                border.rectTransform.anchorMax = Vector2.one;
                border.rectTransform.offsetMin = new Vector2(3f, 3f);
                border.rectTransform.offsetMax = new Vector2(-3f, -3f);
                border.raycastTarget = false;
            }
            border.Variant = Data.sketchVariant;
            border.BlockType = Data.blockType;
            border.color = outline.effectColor;
            border.SetVerticesDirty();
        }

        if (IsSource)
        {
            var borderObject = new GameObject("Not Türü Çerçevesi", typeof(RectTransform), typeof(NotebookSketchBorder));
            borderObject.transform.SetParent(transform, false);
            var border = borderObject.GetComponent<NotebookSketchBorder>();
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = new Vector2(4, 4);
            border.rectTransform.offsetMax = new Vector2(-4, -4);
            border.BlockType = Data.blockType;
            border.color = outline.effectColor;
            border.raycastTarget = false;
            outline.enabled = false;
        }
        if (Data.blockType == NotebookBlockType.RuleClue && transform.Find("Bant") == null)
        {
            var tape = new GameObject("Bant", typeof(RectTransform), typeof(Image));
            tape.transform.SetParent(transform, false);
            var tapeRect = (RectTransform)tape.transform;
            tapeRect.anchorMin = tapeRect.anchorMax = new Vector2(0.5f, 1);
            tapeRect.sizeDelta = new Vector2(65, 13);
            tapeRect.localRotation = Quaternion.Euler(0, 0, -5);
            tape.GetComponent<Image>().color = new Color(0.79f, 0.72f, 0.5f, 0.85f);
            tape.GetComponent<Image>().raycastTarget = false;
        }

        if (titleText == null)
            titleText = CreateText("Başlık", new Vector2(10f, -7f), new Vector2(-10f, -34f), 17f);
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 11f;
        titleText.fontSizeMax = 17f;
        bool measurement = SignalExperiment.IsMeasurementNote(Data);
        GameLocalization.SetFormat(titleText, quest ? "◆ HEDEF  {0}" : measurement ? "ÖLÇÜM KARTI  {0}"
            : Data.blockType == NotebookBlockType.RuleClue ? researchNote ? "KAYIT  {0}" : "KURAL  {0}" : "PARÇA  {0}",
            measurement ? (Data.title ?? "").Replace("ÖLÇÜM / ", "") : Data.title);
        if (Data.blockType == NotebookBlockType.PartClue && PuzzleParts.Find(Data.itemID) != null)
        {
            SetDisplayTitle(titleText);
            titleText.rectTransform.offsetMin = new Vector2(10, -54);
        }
        if (descriptionText == null)
            descriptionText = CreateText("Açıklama", new Vector2(10f, -39f), new Vector2(-10f, 8f), 13f);
        GameLocalization.Set(descriptionText, measurement && researchNote
            ? "Turing'in sol klavyesindeki yuvaya sürükle." : Data.description);
        // Bağ/köprü kuralının son cümlesi bulmacanın zorunlu bilgisidir; üç nokta ile kesilmez.
        if (autoBuilt && Data.blockType == NotebookBlockType.RuleClue)
        {
            float bodyHeight = descriptionText.GetPreferredValues(descriptionText.text, rect.rect.width - 20f, 0f).y;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(115f, bodyHeight + 52f));
            descriptionText.overflowMode = TextOverflowModes.Overflow;
        }
        if (!IsSource && Data.blockType == NotebookBlockType.RuleClue)
        {
            bool linked = PuzzleInformation.TryResolve(Data, ChalkboardManager.Instance);
            var footer = transform.Find("Bulmaca Etiketi");
            var label = footer != null ? footer.GetComponent<TextMeshProUGUI>()
                : CreateText("Bulmaca Etiketi", new Vector2(10, -39), new Vector2(-10, 4), 10);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = new Vector2(1, 0);
            label.rectTransform.pivot = new Vector2(.5f, 0); label.rectTransform.offsetMin = new Vector2(10, 4);
            label.rectTransform.offsetMax = new Vector2(-10, 20);
            label.enableAutoSizing = true; label.fontSizeMin = 8; label.fontSizeMax = 10;
            GameLocalization.Set(label, measurement && researchNote ? "TURING'E TAKILAN KART" : researchNote ? "GENEL ARAŞTIRMA KAYDI"
                : linked ? PuzzleInformation.Title(Data.questID, ChalkboardManager.Instance) : "BAĞLANTISI BELİRSİZ");
            label.color = new Color(.3f, .34f, .3f);
            descriptionText.rectTransform.offsetMin = new Vector2(descriptionText.rectTransform.offsetMin.x, 26);
            if (autoBuilt) rect.sizeDelta += new Vector2(0, 18);
        }

        rect.localRotation = IsSource ? Quaternion.identity : Quaternion.Euler(0f, 0f, Data.rotation);
        var generator = FormulaCatalog.Instance;
        if (quest)
        {
            var blueprint = NotebookWorkspaceController.FindQuest(Data.questID);
            if (blueprint != null)
            {
                var diagram = GetComponentInChildren<QuestDiagramGraphic>();
                if (diagram == null)
                {
                    var diagramObject = new GameObject("Hedef Şema", typeof(RectTransform));
                    diagramObject.transform.SetParent(transform, false);
                    diagram = diagramObject.AddComponent<QuestDiagramGraphic>();
                    RectTransform diagramRect = diagram.rectTransform;
                    diagramRect.anchorMin = new Vector2(0f, researchNote ? .68f : SignalTrialContent.OwnsQuest(Data.questID) ? .26f : .18f);
                    diagramRect.anchorMax = new Vector2(1f, researchNote ? .86f : .8f);
                    diagramRect.offsetMin = new Vector2(8f, 0f);
                    diagramRect.offsetMax = new Vector2(-8f, 0f);
                }
                diagram.raycastTarget = false;
                diagram.SetQuest(blueprint);
                descriptionText.rectTransform.anchorMin = Vector2.zero;
                descriptionText.rectTransform.anchorMax = new Vector2(1f, researchNote ? .64f : SignalTrialContent.OwnsQuest(Data.questID) ? .24f : .18f);
                descriptionText.rectTransform.offsetMin = new Vector2(10f, 3f);
                descriptionText.rectTransform.offsetMax = new Vector2(-10f, 0f);
                descriptionText.fontSize = 11f;
                if (researchNote)
                {
                    descriptionText.enableAutoSizing = true;
                    descriptionText.fontSizeMin = 13; descriptionText.fontSizeMax = 18;
                    descriptionText.textWrappingMode = TextWrappingModes.Normal;
                    descriptionText.overflowMode = TextOverflowModes.Ellipsis;
                }
            }
        }

        if (!quest) SignalTrialGraphic.Decorate(this, descriptionText);
        if (Data.blockType == NotebookBlockType.PartClue && PuzzleParts.Find(Data.itemID) != null)
        {
            var drawing = GetComponentInChildren<SignalTrialGraphic>()?.rectTransform
                ?? GetComponentInChildren<MixtureGraphic>()?.rectTransform;
            if (drawing != null) drawing.anchorMax = new Vector2(drawing.anchorMax.x, .64f);
        }

        if (IsSource || Data.blockType != NotebookBlockType.PartClue || generator == null)
            return;
        FormulaItemSO formula = generator.ResolveFormula(Data.itemID);
        if (formula == null || formula.sketchSprites == null || formula.sketchSprites.Length == 0)
            return;
        Sprite sprite = formula.sketchSprites[Mathf.Abs(Data.sketchVariant % formula.sketchSprites.Length)];
        if (sprite == null)
            return;
        if (sketchImage == null)
        {
            var imageObject = new GameObject("Eskiz", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(transform, false);
            sketchImage = imageObject.GetComponent<Image>();
            sketchImage.rectTransform.anchorMin = new Vector2(0f, 0f);
            sketchImage.rectTransform.anchorMax = new Vector2(0.33f, 0.68f);
            sketchImage.rectTransform.offsetMin = new Vector2(8f, 8f);
            sketchImage.rectTransform.offsetMax = new Vector2(-4f, 0f);
            descriptionText.rectTransform.offsetMin = new Vector2(rect.rect.width * 0.34f, 8f);
        }
        sketchImage.sprite = sprite;
        sketchImage.preserveAspect = true;
        sketchImage.raycastTarget = false;
    }

    private TextMeshProUGUI CreateText(string objectName, Vector2 insetMin, Vector2 insetMax, float fontSize)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        var label = textObject.AddComponent<TextMeshProUGUI>();
        // Kapalı defterde de ölçüm yapılır; TMP'nin OnEnable'ını bekleme.
        label.font = TMP_Settings.defaultFontAsset;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        // Başlık üst kenara, gövde ise başlığın altındaki alana yerleşir.
        if (objectName == "Başlık")
        {
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.offsetMin = new Vector2(insetMin.x, insetMax.y);
            label.rectTransform.offsetMax = new Vector2(insetMax.x, insetMin.y);
        }
        else
        {
            label.rectTransform.offsetMin = new Vector2(insetMin.x, insetMax.y);
            label.rectTransform.offsetMax = new Vector2(insetMax.x, insetMin.y);
        }
        label.fontSize = fontSize;
        label.color = new Color(0.16f, 0.16f, 0.14f);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var workspace = NotebookWorkspaceController.Instance;
        if (Data == null || workspace == null || workspace.IsPaused || !workspace.CanMoveBlocks
            || eventData.button != PointerEventData.InputButton.Left)
            return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        bool fromWorld = canvas.renderMode == RenderMode.WorldSpace;
        if (fromWorld && workspace.DragCanvas != null) canvas = workspace.DragCanvas;
        canvas = canvas.rootCanvas;
        // Instantiate grafik ve biçimli metin verisini kopyalamaz; sürüklenen kart hep Data'dan yeniden kurulur.
        var copy = new GameObject("Sürüklenen Not", typeof(RectTransform), typeof(CanvasGroup), typeof(NotebookDraggableBlock));
        copy.transform.SetParent(canvas.transform, false);
        var copyBlock = copy.GetComponent<NotebookDraggableBlock>();
        copyBlock.Initialize(Data, false);
        copyBlock.enabled = false;
        floatingRect = (RectTransform)copy.transform;
        if (fromWorld)
        {
            floatingRect.localScale = Vector3.one;
            floatingRect.localRotation = Quaternion.identity;
            floatingRect.pivot = new Vector2(.5f, .5f);
            floatingRect.sizeDelta = Data.blockType == NotebookBlockType.Quest ? new Vector2(380, 400) : new Vector2(300, 230);
        }
        else
        {
            var source = (RectTransform)transform;
            floatingRect.pivot = source.pivot;
            floatingRect.sizeDelta = source.rect.size;
            floatingRect.localScale = Vector3.one * (source.lossyScale.x / canvas.transform.lossyScale.x);
            floatingRect.SetPositionAndRotation(source.position, source.rotation);
        }
        workspace.StyleNotebookNote(copyBlock);
        // Notebook ink is transparent on the page; every detached copy needs its own paper.
        var fill = copy.GetComponent<Image>();
        if (fill != null) { fill.sprite = null; fill.color = new Color(.96f, .93f, .83f, 1); }
        var shadow = copy.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .35f); shadow.effectDistance = new Vector2(5, -6);
        floatingRect.SetAsLastSibling();
        var floatingGroup = copy.GetComponent<CanvasGroup>();
        floatingGroup.blocksRaycasts = false;
        floatingGroup.interactable = false;
        floatingGroup.alpha = 1;
        canvasGroup = GetComponent<CanvasGroup>();
        originalAlpha = canvasGroup.alpha;
        originalRaycastState = canvasGroup.blocksRaycasts;
        canvasGroup.blocksRaycasts = false;
        var canvasRect = (RectTransform)canvas.transform;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, eventData.position, camera, out var point))
            pointerOffset = floatingRect.position - point;
        else
            pointerOffset = Vector3.zero;
        if (fromWorld)
        {
            pointerOffset = Vector3.zero;
            floatingRect.position = point;
        }
        dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || floatingRect == null)
            return;
        var workspace = NotebookWorkspaceController.Instance;
        if (workspace == null || workspace.IsPaused || !workspace.CanMoveBlocks)
        {
            CancelDrag();
            return;
        }
        Canvas canvas = floatingRect.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform,
            eventData.position, camera, out var point))
            floatingRect.position = point + pointerOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;
        var workspace = NotebookWorkspaceController.Instance;
        bool canDrop = workspace != null && !workspace.IsPaused && workspace.CanMoveBlocks;
        // Önce geçici görseli temizle; hedef sayfa yenilenirken OnDisable çağrılabilir.
        CancelDrag();
        if (canDrop)
            workspace.TryDropBlock(this, eventData.position);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var workspace = NotebookWorkspaceController.Instance;
        if (!IsSource && workspace != null && !workspace.IsPaused
            && eventData.button == PointerEventData.InputButton.Right)
            workspace.DeleteBlock(this);
    }

    private void Update()
    {
        var workspace = NotebookWorkspaceController.Instance;
        if (dragging && (workspace == null || workspace.IsPaused || !workspace.CanMoveBlocks))
            CancelDrag();
    }

    private void CancelDrag()
    {
        if (!dragging)
            return;
        dragging = false;
        if (floatingRect != null)
            Destroy(floatingRect.gameObject);
        floatingRect = null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = originalAlpha;
            canvasGroup.blocksRaycasts = originalRaycastState;
        }
    }

    internal void CancelSourceDrag() { if (IsSource) CancelDrag(); }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged -= RefreshClueTexture;
        GameLocalization.LanguageChanged += RefreshClueTexture;
        RefreshClueTexture();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshClueTexture;
        CancelDrag();
        SetClueHighlight(false);
    }

    private void RefreshClueTexture()
    {
        if (Data == null || Data.clueRegionIndex < 0 || string.IsNullOrEmpty(Data.clueDataID)) return;
        Transform imageTransform = transform.Find(ClueImageName);
        RawImage image = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
        if (image == null) return;
        BookClueData clue = BookClueData.Find(Data.clueDataID);
        Texture2D tex = clue != null ? clue.GetPageTexture() : null;
        if (tex != null) image.texture = tex;
    }

    public void OnPointerEnter(PointerEventData eventData) { SetClueHighlight(true); }
    public void OnPointerExit(PointerEventData eventData) { SetClueHighlight(false); }

    // Kitaptan gelen blok: gerçek sayfa görüntüsünün bir bölgesini gösterir.
    // Kitapta şeffaf alan + ince sarı çerçeve görünür; sürüklenince görüntü kopar, deftere düşünce görüntü olarak kalır.
    private bool TryRenderClueBlock(RectTransform rect)
    {
        if (Data.clueRegionIndex < 0 || string.IsNullOrEmpty(Data.clueDataID)) return false;
        BookClueData clue = BookClueData.Find(Data.clueDataID);
        Texture2D clueTex = clue != null ? clue.GetPageTexture() : null;
        if (clue == null || clueTex == null || !clue.TryGetRegion(Data.clueRegionIndex, out BookClueRegion region))
            return false;
        if (!IsSource)
        {
            // Parça için elle çizilmiş eskiz hazırsa defterde o kullanılır; kırpılan görüntü yer tutucudur.
            var generator = FormulaCatalog.Instance;
            FormulaItemSO formula = generator != null ? generator.ResolveFormula(Data.itemID) : null;
            if (formula != null && formula.sketchSprites != null && formula.sketchSprites.Length > 0) return false;
        }

        Vector2 pixels = clue.GetPixelSize(region);
        float aspect = pixels.x / Mathf.Max(1f, pixels.y);
        Vector2 fit = new Vector2(260f, 260f / aspect);
        if (fit.y > 160f) fit = new Vector2(160f * aspect, 160f);
        rect.sizeDelta = fit;
        rect.localRotation = IsSource ? Quaternion.identity : Quaternion.Euler(0f, 0f, Data.rotation);

        if (background == null) background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.raycastTarget = true;
        var outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        if (titleText != null) titleText.gameObject.SetActive(false);
        if (descriptionText != null) descriptionText.gameObject.SetActive(false);

        Transform imageTransform = transform.Find(ClueImageName);
        RawImage image = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
        if (image == null)
        {
            var imageObject = new GameObject(ClueImageName, typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(transform, false);
            image = imageObject.GetComponent<RawImage>();
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.raycastTarget = false;
        }
        image.texture = clueTex;
        image.uvRect = clue.GetAtlasRect(region);
        image.color = Color.white;

        if (IsSource)
        {
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.enabled = false;
            image.material = OverlayMaterial;
            background.material = OverlayMaterial;
            background.color = ClueIdleFill;
            BuildClueFrame();
        }
        else
        {
            image.rectTransform.offsetMin = new Vector2(4f, 4f);
            image.rectTransform.offsetMax = new Vector2(-4f, -4f);
            image.enabled = true;
            image.transform.SetAsLastSibling();
            background.color = new Color(0.96f, 0.92f, 0.79f, 0.98f);
            var border = GetComponentInChildren<NotebookSketchBorder>();
            if (border == null)
            {
                var borderObject = new GameObject("El Çizimi Çerçeve", typeof(RectTransform));
                borderObject.transform.SetParent(transform, false);
                borderObject.transform.SetAsFirstSibling();
                border = borderObject.AddComponent<NotebookSketchBorder>();
                border.rectTransform.anchorMin = Vector2.zero;
                border.rectTransform.anchorMax = Vector2.one;
                border.rectTransform.offsetMin = new Vector2(3f, 3f);
                border.rectTransform.offsetMax = new Vector2(-3f, -3f);
                border.raycastTarget = false;
            }
            border.Variant = Data.sketchVariant;
            border.color = new Color(0.24f, 0.24f, 0.22f);
            border.SetVerticesDirty();
        }
        return true;
    }

    private void BuildClueFrame()
    {
        Transform existing = transform.Find(ClueFrameName);
        if (existing != null) Destroy(existing.gameObject);
        var frame = new GameObject(ClueFrameName, typeof(RectTransform));
        frame.transform.SetParent(transform, false);
        var frameRect = (RectTransform)frame.transform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        const float t = 3f;
        // alt, üst, sol, sağ
        Vector2[,] anchors = { { Vector2.zero, new Vector2(1, 0) }, { new Vector2(0, 1), Vector2.one },
            { Vector2.zero, new Vector2(0, 1) }, { new Vector2(1, 0), Vector2.one } };
        Vector2[,] offsets = { { Vector2.zero, new Vector2(0, t) }, { new Vector2(0, -t), Vector2.zero },
            { Vector2.zero, new Vector2(t, 0) }, { new Vector2(-t, 0), Vector2.zero } };
        clueFrame = new Image[4];
        for (int side = 0; side < 4; side++)
        {
            var line = new GameObject("Kenar", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(frame.transform, false);
            var lineRect = (RectTransform)line.transform;
            lineRect.anchorMin = anchors[side, 0]; lineRect.anchorMax = anchors[side, 1];
            lineRect.offsetMin = offsets[side, 0]; lineRect.offsetMax = offsets[side, 1];
            clueFrame[side] = line.GetComponent<Image>();
            clueFrame[side].color = ClueIdleFrame;
            clueFrame[side].material = OverlayMaterial;
            clueFrame[side].raycastTarget = false;
        }
    }

    private void SetClueHighlight(bool on)
    {
        if (!IsSource || clueFrame == null || background == null) return;
        background.color = on ? ClueHoverFill : ClueIdleFill;
        foreach (var line in clueFrame)
            if (line != null) line.color = on ? ClueHoverFrame : ClueIdleFrame;
    }
}

// Görsel atanmamış bloklar da basılı kitaptan farklı, hafif asimetrik bir eskiz çerçevesi alır.
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
internal sealed class NotebookSketchBorder : MaskableGraphic
{
    [SerializeField] internal int Variant;
    [SerializeField] internal NotebookBlockType BlockType = NotebookBlockType.PartClue;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        Vector2[] corners =
        {
            new Vector2(rect.xMin + 1f, rect.yMin + 2f),
            new Vector2(rect.xMin + 3f, rect.yMax - 1f),
            new Vector2(rect.xMax - 1f, rect.yMax - 3f),
            new Vector2(rect.xMax - 3f, rect.yMin + 1f),
        };
        for (int side = 0; side < 4; side++)
        {
            Vector2 a = corners[side];
            Vector2 b = corners[(side + 1) % 4];
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized;
            Vector2 previous = a;
            for (int point = 1; point <= 8; point++)
            {
                float t = point / 8f;
                float jitter = point == 8 ? 0f : Mathf.Sin((point + side * 11 + Variant * 3) * 2.34f) * 1.5f;
                Vector2 next = Vector2.Lerp(a, b, t) + normal * jitter;
                if (BlockType == NotebookBlockType.PartClue && point % 2 == 0) { previous = next; continue; }
                Vector2 width = new Vector2(-(next - previous).y, (next - previous).x).normalized * 0.75f;
                if (BlockType == NotebookBlockType.Quest) width *= 2;
                int index = vh.currentVertCount;
                vh.AddVert(previous - width, color, Vector2.zero);
                vh.AddVert(previous + width, color, Vector2.zero);
                vh.AddVert(next + width, color, Vector2.zero);
                vh.AddVert(next - width, color, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
                if (BlockType == NotebookBlockType.Quest)
                {
                    Vector2 inset = normal * -6;
                    index = vh.currentVertCount;
                    vh.AddVert(previous - width + inset, color, Vector2.zero);
                    vh.AddVert(previous + width + inset, color, Vector2.zero);
                    vh.AddVert(next + width + inset, color, Vector2.zero);
                    vh.AddVert(next - width + inset, color, Vector2.zero);
                    vh.AddTriangle(index, index + 1, index + 2);
                    vh.AddTriangle(index, index + 2, index + 3);
                }
                previous = next;
            }
        }
    }
}
