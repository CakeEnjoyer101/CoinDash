using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public sealed class RuntimeMenuStyler : MonoBehaviour
{
    static RuntimeMenuStyler instance;

    static readonly Color BackgroundTint = new(0.03f, 0.05f, 0.09f, 0.34f);
    static readonly Color PanelColor = new(0.05f, 0.09f, 0.15f, 0.78f);
    static readonly Color SecondaryPanelColor = new(0.07f, 0.12f, 0.2f, 0.56f);
    static readonly Color AccentCyan = new(0.29f, 0.93f, 0.96f, 1f);
    static readonly Color AccentBlue = new(0.13f, 0.53f, 0.97f, 1f);
    static readonly Color AccentGold = new(1f, 0.79f, 0.36f, 1f);
    static readonly Color TextPrimary = new(0.95f, 0.98f, 1f, 1f);
    static readonly Color TextMuted = new(0.7f, 0.79f, 0.88f, 1f);

    static Sprite whiteSprite;

    Canvas canvas;
    TMP_FontAsset fontAsset;
    RuntimeTooltip tooltip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("RuntimeMenuStyler");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RuntimeMenuStyler>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        ApplyToScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyNextFrame(scene));
    }

    IEnumerator ApplyNextFrame(Scene scene)
    {
        yield return null;
        ApplyToScene(scene);
    }

    void ApplyToScene(Scene scene)
    {
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        ConfigureCanvas(canvas);
        fontAsset = TMP_Settings.defaultFontAsset;

        DestroyExistingRuntimeRoot();
        tooltip = EnsureTooltip();

        switch (scene.name)
        {
            case "MainMenu":
                BuildMainMenu();
                break;
            case "LoadingScene":
                BuildLoadingScreen();
                break;
            case "StageSelect":
                BuildStageSelect();
                break;
        }
    }

    void ConfigureCanvas(Canvas targetCanvas)
    {
        var scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : (16f / 9f);
        scaler.matchWidthOrHeight = aspect > 2f ? 1f : 0.65f;
        scaler.referencePixelsPerUnit = 100f;

        if (targetCanvas.GetComponent<GraphicRaycaster>() == null)
            targetCanvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    void BuildMainMenu()
    {
        var menuControl = FindObjectOfType<MainMenuControl>(true);
        if (menuControl == null)
            return;

        DisableObjectsNamed("ClickToStart", "Button", "StartGame", "Text (TMP)");

        var root = CreateRoot("RuntimeMainMenuUI");
        CreateMainMenuBackground(root);

        var card = CreatePanel(root, "CenterCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 420f), new Color(0.05f, 0.08f, 0.14f, 0.88f));
        card.pivot = new Vector2(0.5f, 0.5f);

        var eyebrow = CreateText(card, "Eyebrow", "COINDASH", 28, FontStyles.Bold, AccentGold, new Vector2(0f, -46f), new Vector2(260f, 34f));
        eyebrow.alignment = TextAlignmentOptions.Center;
        CenterInPanel(eyebrow.rectTransform, new Vector2(0f, 134f));

        var title = CreateText(card, "Title", "Neon Casino Run", 56, FontStyles.Bold, TextPrimary, new Vector2(0f, -94f), new Vector2(470f, 72f));
        title.alignment = TextAlignmentOptions.Center;
        CenterInPanel(title.rectTransform, new Vector2(0f, 78f));

        var subtitle = CreateText(card, "Subtitle", "Arcade speed, clean flow and a sharper UI pass.", 20, FontStyles.Normal, TextMuted, new Vector2(0f, -156f), new Vector2(430f, 42f));
        subtitle.alignment = TextAlignmentOptions.Center;
        CenterInPanel(subtitle.rectTransform, new Vector2(0f, 18f));

        var introButton = CreateButton(card, "EnterButton", "PLAY", "Start the experience", new Vector2(0f, -232f), new Vector2(320f, 82f), AccentBlue, AccentCyan);
        introButton.onClick.AddListener(menuControl.MenuBeginButton);
        introButton.gameObject.AddComponent<UiTooltipTrigger>().Initialize(tooltip, "Starts the menu camera move and reveals the main navigation.");

        var startButton = CreateButton(card, "StartRunButton", "PLAY", "Go to loading", new Vector2(0f, -232f), new Vector2(320f, 82f), AccentGold, new Color(1f, 0.9f, 0.58f, 1f));
        startButton.onClick.AddListener(menuControl.StartGame);
        startButton.gameObject.SetActive(MainMenuControl.hasClicked);
        startButton.gameObject.AddComponent<UiTooltipTrigger>().Initialize(tooltip, "Loads the run flow and takes you into the gameplay sequence.");

        var hint = CreateText(card, "Hint", "Tap play to begin. After the first intro, the flow goes straight into the run.", 16, FontStyles.Normal, TextMuted, new Vector2(0f, -336f), new Vector2(430f, 42f));
        hint.alignment = TextAlignmentOptions.Center;
        CenterInPanel(hint.rectTransform, new Vector2(0f, -144f));

        StartCoroutine(SwapMainMenuButtons(menuControl, introButton.gameObject, startButton.gameObject));
        StartCoroutine(KeepObjectsDisabled("ClickToStart", "Button", "StartGame", "Text (TMP)"));
    }

    IEnumerator SwapMainMenuButtons(MainMenuControl menuControl, GameObject introButton, GameObject startButton)
    {
        if (MainMenuControl.hasClicked)
        {
            introButton.SetActive(false);
            startButton.SetActive(true);
            yield break;
        }

        while (menuControl != null && !MainMenuControl.hasClicked)
            yield return null;

        if (introButton != null)
            introButton.SetActive(false);

        if (startButton != null)
            startButton.SetActive(true);
    }

    void BuildLoadingScreen()
    {
        DisableObjectsNamed("Loading", "Links", "AKey", "DKey", "Rechts");

        var root = CreateRoot("RuntimeLoadingUI");
        CreatePlainBackdrop(root);

        var centerPanel = CreatePanel(root, "LoadingPanel", new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(720f, 320f), PanelColor);

        CreateText(centerPanel, "Eyebrow", "PREPARING RUN", 20, FontStyles.Bold, AccentCyan, new Vector2(40f, -30f), new Vector2(240f, 26f));
        CreateText(centerPanel, "Title", "Loading The Neon Floor", 44, FontStyles.Bold, TextPrimary, new Vector2(38f, -58f), new Vector2(540f, 54f));
        CreateText(centerPanel, "Body", "Align your route, catch the rhythm, and get ready to weave through the casino skyline.", 20, FontStyles.Normal, TextMuted, new Vector2(42f, -112f), new Vector2(560f, 58f));

        var progressBar = CreateImage(centerPanel, "ProgressBar", AccentBlue, new Vector2(42f, -228f), new Vector2(636f, 12f));
        progressBar.gameObject.AddComponent<UiProgressPulse>();

        var loadingText = CreateText(centerPanel, "LoadingLabel", "Streaming environment", 18, FontStyles.Bold, AccentGold, new Vector2(42f, -250f), new Vector2(280f, 28f));
        loadingText.gameObject.AddComponent<UiLoadingDots>().prefix = "Streaming environment";

        var tipsPanel = CreatePanel(root, "TipsPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(760f, 68f), SecondaryPanelColor);
        CreateText(tipsPanel, "Tips", "Controls: A and D to dodge lanes. Keep your line clean and carry momentum.", 18, FontStyles.Normal, TextMuted, new Vector2(24f, -18f), new Vector2(700f, 30f));
    }

    void BuildStageSelect()
    {
        DisableObjectsNamed("StageName", "Text (TMP)", "SelectAndPlay");

        var stageControls = FindObjectOfType<StageControls>(true);
        var root = CreateRoot("RuntimeStageSelectUI");
        CreatePlainBackdrop(root);

        var title = CreateText(root, "StageTitle", "Choose Your Run", 44f, FontStyles.Bold, TextPrimary, Vector2.zero, new Vector2(520f, 60f));
        title.alignment = TextAlignmentOptions.Center;
        CenterInPanel(title.rectTransform, new Vector2(0f, 250f));

        var leftCard = CreatePanel(root, "StageCardOne", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230f, 30f), new Vector2(360f, 280f), SecondaryPanelColor);
        var rightCard = CreatePanel(root, "StageCardTwo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, 30f), new Vector2(360f, 280f), SecondaryPanelColor);

        BuildStageCard(leftCard, "LEVEL 1", "Skyline Start", "Cleaner route with wider spacing and a smoother intro into the run.", "Play Level 1", stageControls != null ? (UnityEngine.Events.UnityAction)stageControls.PressPlay : null, "Loads the first route with balanced spacing and a cleaner obstacle rhythm.");
        BuildStageCard(rightCard, "LEVEL 2", "Jackpot Rush", "Faster tempo, tighter spacing and a more aggressive coin pattern for risk-reward play.", "Play Level 2", stageControls != null ? (UnityEngine.Events.UnityAction)stageControls.PressPlaySecond : null, "Loads the second route with a faster rhythm and denser obstacle pattern.");

        var backButton = CreateButton(root, "BackButton", "Back", "Return to menu", new Vector2(0f, 0f), new Vector2(180f, 62f), new Color(0.18f, 0.23f, 0.31f, 1f), TextPrimary);
        backButton.onClick.AddListener(() => SceneManager.LoadScene(0));
        backButton.gameObject.AddComponent<UiTooltipTrigger>().Initialize(tooltip, "Returns to the main menu without starting a run.");
        var backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(48f, -48f);
    }

    RectTransform CreateRoot(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    RectTransform CreateSafeArea(RectTransform parent, string name, float maxWidth)
    {
        var safe = new GameObject(name, typeof(RectTransform));
        safe.transform.SetParent(parent, false);

        var rect = safe.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        float width = Mathf.Min(Screen.width - 96f, maxWidth);
        float height = Mathf.Min(Screen.height - 96f, 860f);
        rect.sizeDelta = new Vector2(Mathf.Max(900f, width), Mathf.Max(540f, height));
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    void CenterInPanel(RectTransform rect, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
    }

    void DestroyExistingRuntimeRoot()
    {
        if (canvas == null)
            return;

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = canvas.transform.GetChild(i);
            if (child.name.StartsWith("Runtime"))
                Destroy(child.gameObject);
        }
    }

    void CreateBackgroundDecor(RectTransform root)
    {
        var veil = CreateImage(root, "BackgroundVeil", BackgroundTint, Vector2.zero, Vector2.zero);
        var veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;

        CreateGlow(root, "GlowLeft", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 40f), new Vector2(220f, 540f), new Color(0.08f, 0.34f, 0.87f, 0.12f));
        CreateGlow(root, "GlowRight", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(180f, 520f), new Color(0.12f, 0.92f, 0.98f, 0.1f));
        CreateGlow(root, "GlowTop", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(680f, 96f), new Color(1f, 0.75f, 0.28f, 0.05f));
    }

    void CreatePlainBackdrop(RectTransform root)
    {
        var veil = CreateImage(root, "PlainBackdrop", new Color(0.04f, 0.07f, 0.12f, 0.58f), Vector2.zero, Vector2.zero);
        var veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
    }

    void BuildStageCard(RectTransform card, string badge, string title, string description, string buttonLabel, UnityEngine.Events.UnityAction onClick, string tooltipMessage)
    {
        var badgeText = CreateText(card, $"{badge}Badge", badge, 16f, FontStyles.Bold, AccentGold, new Vector2(24f, -24f), new Vector2(120f, 20f));
        badgeText.alignment = TextAlignmentOptions.TopLeft;
        var titleText = CreateText(card, $"{title}Title", title, 32f, FontStyles.Bold, TextPrimary, new Vector2(24f, -54f), new Vector2(260f, 40f));
        var bodyText = CreateText(card, $"{title}Description", description, 17f, FontStyles.Normal, TextMuted, new Vector2(24f, -102f), new Vector2(312f, 70f));

        if (onClick != null)
        {
            var playButton = CreateButton(card, $"{title}Button", buttonLabel, "Launch this run", new Vector2(24f, -188f), new Vector2(312f, 62f), AccentBlue, AccentCyan);
            playButton.onClick.AddListener(onClick);
            playButton.gameObject.AddComponent<UiTooltipTrigger>().Initialize(tooltip, tooltipMessage);
        }
    }

    void CreateMainMenuBackground(RectTransform root)
    {
        var veil = CreateImage(root, "MainMenuBackground", new Color(0.04f, 0.07f, 0.12f, 0.58f), Vector2.zero, Vector2.zero);
        var veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
    }

    Image CreateGlow(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var glow = CreateImage(parent, name, color, anchoredPosition, size);
        var rect = glow.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMax.x, 0.5f);
        return glow;
    }

    RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var image = CreateImage(parent, name, color, anchoredPosition, size);
        var rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return rect;
    }

    Image CreateImage(RectTransform parent, string name, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, FontStyles style, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = fontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = Mathf.Max(12f, fontSize * 0.55f);
        tmp.fontSizeMax = fontSize;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
        return tmp;
    }

    RuntimeTooltip EnsureTooltip()
    {
        if (tooltip != null)
            return tooltip;

        var existing = canvas.GetComponentInChildren<RuntimeTooltip>(true);
        if (existing != null)
            return existing;

        var root = new GameObject("RuntimeTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(RuntimeTooltip));
        root.transform.SetParent(canvas.transform, false);

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(360f, 88f);

        var image = root.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(0.04f, 0.08f, 0.14f, 0.94f);

        var outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.29f, 0.93f, 0.96f, 0.4f);
        outline.effectDistance = new Vector2(1f, -1f);

        var label = CreateText(rect, "TooltipText", "", 17f, FontStyles.Normal, TextPrimary, new Vector2(18f, -14f), new Vector2(322f, 54f));
        label.alignment = TextAlignmentOptions.MidlineLeft;

        var runtimeTooltip = root.GetComponent<RuntimeTooltip>();
        runtimeTooltip.Initialize(root.GetComponent<CanvasGroup>(), rect, label);
        return runtimeTooltip;
    }

    Button CreateButton(RectTransform parent, string name, string label, string subLabel, Vector2 anchoredPosition, Vector2 size, Color fill, Color outlineColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        if (parent.pivot == new Vector2(0.5f, 0.5f))
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        var image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = fill;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Color.Lerp(fill, Color.white, 0.1f);
        colors.pressedColor = Color.Lerp(fill, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(fill.r, fill.g, fill.b, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var shine = new GameObject("Shine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        shine.transform.SetParent(go.transform, false);
        var shineRect = shine.GetComponent<RectTransform>();
        shineRect.anchorMin = new Vector2(0f, 0f);
        shineRect.anchorMax = new Vector2(0f, 1f);
        shineRect.pivot = new Vector2(0f, 0.5f);
        shineRect.anchoredPosition = new Vector2(0f, 0f);
        shineRect.sizeDelta = new Vector2(10f, 0f);
        var shineImage = shine.GetComponent<Image>();
        shineImage.sprite = GetWhiteSprite();
        shineImage.color = new Color(1f, 1f, 1f, 0f);
        shineImage.raycastTarget = false;

        var labelText = CreateText(rect, "Label", label, 23f, FontStyles.Bold, TextPrimary, new Vector2(0f, 0f), size);
        labelText.alignment = TextAlignmentOptions.Center;
        var labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(0f, subLabel.Length > 0 ? 8f : 0f);
        labelRect.offsetMax = new Vector2(0f, subLabel.Length > 0 ? -12f : 0f);
        labelRect.anchoredPosition = new Vector2(0f, subLabel.Length > 0 ? 8f : 0f);

        if (!string.IsNullOrEmpty(subLabel))
        {
            var subLabelText = CreateText(rect, "SubLabel", subLabel, 12f, FontStyles.Normal, new Color(0.9f, 0.96f, 1f, 0.82f), new Vector2(0f, 0f), size);
            subLabelText.alignment = TextAlignmentOptions.Bottom;
            var subRect = subLabelText.rectTransform;
            subRect.anchorMin = Vector2.zero;
            subRect.anchorMax = Vector2.one;
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.offsetMin = new Vector2(0f, 8f);
            subRect.offsetMax = new Vector2(0f, -10f);
        }

        return button;
    }

    IEnumerator KeepObjectsDisabled(params string[] objectNames)
    {
        for (int i = 0; i < 240; i++)
        {
            DisableObjectsNamed(objectNames);
            yield return null;
        }
    }

    void DisableObjectsNamed(params string[] objectNames)
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
            DisableObjectsNamedRecursive(root.transform, objectNames);
    }

    void DisableObjectsNamedRecursive(Transform current, string[] objectNames)
    {
        for (int i = 0; i < objectNames.Length; i++)
        {
            if (current.name == objectNames[i])
                current.gameObject.SetActive(false);
        }

        for (int i = 0; i < current.childCount; i++)
            DisableObjectsNamedRecursive(current.GetChild(i), objectNames);
    }

    static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var pixels = new Color[32 * 32];

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float cornerX = x < 16 ? x : 31 - x;
                float cornerY = y < 16 ? y : 31 - y;
                bool outsideCorner = cornerX < 6 && cornerY < 6 &&
                    Vector2.Distance(new Vector2(cornerX, cornerY), new Vector2(6f, 6f)) > 6f;

                pixels[(y * 32) + x] = outsideCorner ? new Color(1f, 1f, 1f, 0f) : Color.white;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
        return whiteSprite;
    }
}

public sealed class UiFloatMotion : MonoBehaviour
{
    public float amplitude = 10f;
    public float speed = 1f;

    Vector3 startLocalPosition;

    void OnEnable()
    {
        startLocalPosition = transform.localPosition;
    }

    void Update()
    {
        transform.localPosition = startLocalPosition + Vector3.up * Mathf.Sin(Time.unscaledTime * speed) * amplitude;
    }
}

public sealed class UiPulseMotion : MonoBehaviour
{
    public float speed = 1.2f;
    public float scaleBoost = 0.045f;

    Vector3 baseScale;

    void OnEnable()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        var scale = 1f + ((Mathf.Sin(Time.unscaledTime * speed * 6f) + 1f) * 0.5f * scaleBoost);
        transform.localScale = baseScale * scale;
    }
}

public sealed class UiPulseGlow : MonoBehaviour
{
    public Graphic graphic;

    Color baseColor;

    void Awake()
    {
        if (graphic == null)
            graphic = GetComponent<Graphic>();

        if (graphic != null)
            baseColor = graphic.color;
    }

    void Update()
    {
        if (graphic == null)
            return;

        float pulse = 0.92f + (((Mathf.Sin(Time.unscaledTime * 1.7f) + 1f) * 0.5f) * 0.08f);
        graphic.color = new Color(baseColor.r * pulse, baseColor.g * pulse, baseColor.b * pulse, baseColor.a);
    }
}

public sealed class UiProgressPulse : MonoBehaviour
{
    RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (rectTransform == null)
            return;

        float t = (Mathf.Sin(Time.unscaledTime * 2.2f) + 1f) * 0.5f;
        rectTransform.localScale = new Vector3(0.55f + t * 0.45f, 1f, 1f);
    }
}

public sealed class UiLoadingDots : MonoBehaviour
{
    public string prefix = "Loading";

    TMP_Text label;

    void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (label == null)
            return;

        int dots = Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 4;
        label.text = prefix + new string('.', dots);
    }
}

public sealed class UiButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Vector3 targetScale = Vector3.one;
    Vector3 velocity;

    void Update()
    {
        transform.localScale = Vector3.SmoothDamp(transform.localScale, targetScale, ref velocity, 0.08f, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = Vector3.one * 1.04f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = Vector3.one * 0.97f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = Vector3.one * 1.04f;
    }
}

public sealed class RuntimeTooltip : MonoBehaviour
{
    CanvasGroup canvasGroup;
    RectTransform rectTransform;
    TMP_Text label;
    Coroutine fadeRoutine;

    public void Initialize(CanvasGroup group, RectTransform rect, TMP_Text textLabel)
    {
        canvasGroup = group;
        rectTransform = rect;
        label = textLabel;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void Show(string message, Vector2 screenPosition)
    {
        if (label == null || rectTransform == null || canvasGroup == null)
            return;

        label.text = message;
        rectTransform.anchoredPosition = screenPosition + new Vector2(18f, -18f);
        StartFade(1f);
    }

    public void Hide()
    {
        StartFade(0f);
    }

    void StartFade(float target)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeTo(target));
    }

    IEnumerator FadeTo(float target)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < 0.12f)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / 0.12f);
            yield return null;
        }

        canvasGroup.alpha = target;
    }
}

public sealed class UiTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    RuntimeTooltip tooltip;
    string message;
    RectTransform rectTransform;

    public void Initialize(RuntimeTooltip tooltipInstance, string tooltipMessage)
    {
        tooltip = tooltipInstance;
        message = tooltipMessage;
        rectTransform = transform as RectTransform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide();
    }

    public void OnSelect(BaseEventData eventData)
    {
        ShowTooltip();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        tooltip?.Hide();
    }

    void ShowTooltip()
    {
        if (tooltip == null || rectTransform == null || string.IsNullOrEmpty(message))
            return;

        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);
        tooltip.Show(message, screenPosition);
    }
}

public sealed class UiShineSweep : MonoBehaviour
{
    RectTransform rectTransform;
    float width;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (rectTransform == null || rectTransform.parent is not RectTransform parent)
            return;

        width = parent.rect.width;
        float t = Mathf.PingPong(Time.unscaledTime * 120f, width + 80f) - 40f;
        rectTransform.anchoredPosition = new Vector2(t, 0f);
    }
}
