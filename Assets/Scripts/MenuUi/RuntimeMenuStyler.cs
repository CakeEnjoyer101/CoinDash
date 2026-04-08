using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
    static Sprite mainMenuBackdropSprite;

    Canvas canvas;
    TMP_FontAsset fontAsset;
    TMP_FontAsset titleFontAsset;
    TMP_FontAsset bodyFontAsset;
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
        bodyFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        titleFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
        fontAsset = bodyFontAsset != null ? bodyFontAsset : TMP_Settings.defaultFontAsset;
        if (titleFontAsset == null)
            titleFontAsset = fontAsset;

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

        ApplyScenePresentation(scene.name);
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
        CreateBackgroundDecor(root);

        var card = CreatePanel(root, "CenterCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(700f, 476f), new Color(0.04f, 0.08f, 0.14f, 0.9f));
        card.pivot = new Vector2(0.5f, 0.5f);
        card.gameObject.AddComponent<UiFloatMotion>().amplitude = 5f;
        DecoratePanel(card, AccentCyan, AccentGold);
        BuildMainMenuHighScorePanel(root);

        var eyebrow = CreateText(card, "Eyebrow", "COINDASH", 30, FontStyles.Bold, AccentGold, new Vector2(0f, -46f), new Vector2(260f, 34f), true);
        eyebrow.alignment = TextAlignmentOptions.Center;
        CenterInPanel(eyebrow.rectTransform, new Vector2(0f, 144f));

        var title = CreateText(card, "Title", "Neon Casino Run", 62, FontStyles.Bold, TextPrimary, new Vector2(0f, -94f), new Vector2(560f, 78f), true);
        title.alignment = TextAlignmentOptions.Center;
        CenterInPanel(title.rectTransform, new Vector2(0f, 86f));
        title.gameObject.AddComponent<UiPulseGlow>();

        var subtitle = CreateText(card, "Subtitle", "Three lanes, neon casino energy and a straight shot into the action.", 20, FontStyles.Normal, TextMuted, new Vector2(0f, -156f), new Vector2(560f, 58f));
        subtitle.alignment = TextAlignmentOptions.Center;
        CenterInPanel(subtitle.rectTransform, new Vector2(0f, 6f));

        var modes = CreateText(card, "ModesText", "3 LANES   /   2 MODES   /   HARDCORE X2", 18f, FontStyles.Bold, new Color(0.79f, 0.87f, 0.96f, 1f), new Vector2(0f, 0f), new Vector2(520f, 28f), true);
        modes.alignment = TextAlignmentOptions.Center;
        CenterInPanel(modes.rectTransform, new Vector2(0f, -64f));

        var startButton = CreateButton(card, "StartRunButton", "CHOOSE RUN", "Pick your run and dive in", new Vector2(0f, -232f), new Vector2(360f, 84f), AccentGold, new Color(1f, 0.9f, 0.58f, 1f));
        startButton.onClick.AddListener(() =>
        {
            MainMenuControl.hasClicked = true;
            menuControl.StartGame();
        });
        startButton.gameObject.AddComponent<UiTooltipTrigger>().Initialize(tooltip, "Choose your run and jump straight onto the floor.");
        CenterInPanel(startButton.GetComponent<RectTransform>(), new Vector2(0f, -136f));

        StartCoroutine(KeepObjectsDisabled("ClickToStart", "Button", "StartGame", "Text (TMP)"));
    }

    void BuildMainMenuHighScorePanel(RectTransform root)
    {
        var panel = CreatePanel(root, "HomeHighScorePanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-38f, -38f), new Vector2(340f, 206f), new Color(0.04f, 0.08f, 0.14f, 0.9f));
        DecoratePanel(panel, AccentGold, AccentCyan);

        var title = CreateText(panel, "HighScoreTitle", "HIGH SCORES", 24f, FontStyles.Bold, TextPrimary, new Vector2(24f, -24f), new Vector2(292f, 28f), true);
        title.alignment = TextAlignmentOptions.Center;

        CreateMainMenuHighScoreRow(panel, "Level1", "LEVEL 1", "BEST COINS", RunProgressStore.FormatPrimaryValue(0, RunProgressStore.GetHighScore(0)), new Vector2(24f, -66f), AccentGold);
        CreateMainMenuHighScoreRow(panel, "Level2", "LEVEL 2", "BEST DISTANCE", RunProgressStore.FormatPrimaryValue(1, RunProgressStore.GetHighScore(1)), new Vector2(24f, -126f), AccentCyan);

        var footer = CreateText(panel, "HighScoreFooter", "Updates when you beat your record.", 14f, FontStyles.Normal, TextMuted, new Vector2(24f, -174f), new Vector2(292f, 28f));
        footer.alignment = TextAlignmentOptions.Center;
    }

    void CreateMainMenuHighScoreRow(RectTransform parent, string namePrefix, string levelLabel, string metricLabel, string valueText, Vector2 anchoredPosition, Color accent)
    {
        var row = CreatePanel(parent, $"{namePrefix}Row", new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, new Vector2(292f, 44f), new Color(0.07f, 0.12f, 0.2f, 0.62f));

        var level = CreateText(row, $"{namePrefix}Level", levelLabel, 16f, FontStyles.Bold, accent, new Vector2(14f, -8f), new Vector2(90f, 18f), true);
        var metric = CreateText(row, $"{namePrefix}Metric", metricLabel, 13f, FontStyles.Normal, TextMuted, new Vector2(14f, -24f), new Vector2(120f, 16f));
        var value = CreateText(row, $"{namePrefix}Value", valueText, 24f, FontStyles.Bold, TextPrimary, new Vector2(166f, -8f), new Vector2(112f, 28f), true);
        var line = CreateImage(row, $"{namePrefix}Accent", accent, new Vector2(0f, 0f), new Vector2(4f, 44f));
        line.rectTransform.anchorMin = new Vector2(0f, 0f);
        line.rectTransform.anchorMax = new Vector2(0f, 1f);
        line.rectTransform.pivot = new Vector2(0f, 0.5f);
        line.rectTransform.anchoredPosition = Vector2.zero;
        line.rectTransform.sizeDelta = new Vector2(4f, 0f);

        level.alignment = TextAlignmentOptions.TopLeft;
        metric.alignment = TextAlignmentOptions.TopLeft;
        value.alignment = TextAlignmentOptions.MidlineRight;
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
        CreateBackgroundDecor(root);

        var centerPanel = CreatePanel(root, "LoadingPanel", new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(700f, 310f), PanelColor);
        DecoratePanel(centerPanel, AccentCyan, AccentGold);

        CreateText(centerPanel, "Eyebrow", "PREPARING RUN", 20, FontStyles.Bold, AccentCyan, new Vector2(40f, -30f), new Vector2(240f, 26f), true);
        CreateText(centerPanel, "Title", "Loading The Neon Floor", 48, FontStyles.Bold, TextPrimary, new Vector2(38f, -58f), new Vector2(540f, 54f), true);
        CreateText(centerPanel, "Body", "Align your route, catch the rhythm, and get ready to weave through the casino skyline.", 20, FontStyles.Normal, TextMuted, new Vector2(42f, -112f), new Vector2(560f, 58f));

        var progressBarBack = CreateImage(centerPanel, "ProgressBarBack", new Color(0.08f, 0.14f, 0.22f, 1f), new Vector2(42f, -214f), new Vector2(616f, 16f));
        var progressBar = CreateImage(centerPanel, "ProgressBar", AccentBlue, new Vector2(42f, -214f), new Vector2(616f, 12f));
        progressBar.gameObject.AddComponent<UiProgressPulse>();

        var loadingText = CreateText(centerPanel, "LoadingLabel", "Streaming environment", 18, FontStyles.Bold, AccentGold, new Vector2(42f, -246f), new Vector2(280f, 28f));
        loadingText.gameObject.AddComponent<UiLoadingDots>().prefix = "Streaming environment";

        var tipsPanel = CreatePanel(root, "TipsPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(760f, 68f), SecondaryPanelColor);
        CreateText(tipsPanel, "Tips", "Controls: A and D to dodge lanes. Keep your line clean and carry momentum.", 18, FontStyles.Normal, TextMuted, new Vector2(24f, -18f), new Vector2(700f, 30f));
    }

    void BuildStageSelect()
    {
        DisableObjectsNamed("StageName", "Text (TMP)", "SelectAndPlay", "Cube");

        var stageControls = FindObjectOfType<StageControls>(true);
        var root = CreateRoot("RuntimeStageSelectUI");
        CreatePlainBackdrop(root);
        CreateBackgroundDecor(root);
        var content = CreateSafeArea(root, "StageSelectContent", 1220f);

        var title = CreateText(content, "StageTitle", "Choose Your Run", 52f, FontStyles.Bold, TextPrimary, Vector2.zero, new Vector2(520f, 60f), true);
        title.alignment = TextAlignmentOptions.Center;
        CenterInPanel(title.rectTransform, new Vector2(0f, 250f));

        var infoPanel = CreatePanel(content, "StageInfoPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(560f, 344f), new Color(0.04f, 0.08f, 0.14f, 0.88f));
        DecoratePanel(infoPanel, AccentCyan, AccentGold);
        var badge = CreateText(infoPanel, "StageBadge", "LEVEL 1", 18f, FontStyles.Bold, AccentCyan, new Vector2(42f, -34f), new Vector2(180f, 22f), true);
        var name = CreateText(infoPanel, "StageName", "Token Sprint", 44f, FontStyles.Bold, TextPrimary, new Vector2(42f, -82f), new Vector2(430f, 48f), true);
        var objective = CreateText(infoPanel, "StageObjective", "Collect as many tokens as possible. Follow dense token trails and jump for high arcs.", 20f, FontStyles.Normal, TextMuted, new Vector2(42f, -150f), new Vector2(438f, 90f));
        var detail = CreateText(infoPanel, "StageDetail", "A wider route with room to breathe. This stage is all about score, rhythm, and stacking tokens.", 18f, FontStyles.Normal, TextMuted, new Vector2(42f, -256f), new Vector2(438f, 76f));

        var leftArrow = CreateButton(content, "LeftStageArrow", "<", "", new Vector2(0f, 0f), new Vector2(82f, 82f), new Color(0.08f, 0.14f, 0.22f, 0.96f), AccentCyan);
        var rightArrow = CreateButton(content, "RightStageArrow", ">", "", new Vector2(0f, 0f), new Vector2(82f, 82f), new Color(0.08f, 0.14f, 0.22f, 0.96f), AccentCyan);
        var playButton = CreateButton(content, "StagePlayButton", "PLAY LEVEL 1", "Launch selected run", new Vector2(0f, 0f), new Vector2(320f, 78f), AccentBlue, AccentCyan);
        var hardcoreButton = CreateButton(content, "HardcoreButton", "HARDCORE OFF", "x2 coins / mega speed", new Vector2(0f, 0f), new Vector2(320f, 64f), new Color(0.11f, 0.16f, 0.23f, 0.96f), new Color(1f, 0.62f, 0.26f, 0.55f));

        CenterInPanel(playButton.GetComponent<RectTransform>(), new Vector2(0f, -240f));
        CenterInPanel(hardcoreButton.GetComponent<RectTransform>(), new Vector2(0f, -312f));
        CenterInPanel(leftArrow.GetComponent<RectTransform>(), new Vector2(-368f, 18f));
        CenterInPanel(rightArrow.GetComponent<RectTransform>(), new Vector2(368f, 18f));

        var stagePreview = root.gameObject.AddComponent<RuntimeStageSelectCarousel>();
        stagePreview.Initialize(
            null,
            badge,
            name,
            objective,
            detail,
            leftArrow,
            rightArrow,
            playButton,
            hardcoreButton,
            stageControls);

        var backButton = CreateButton(content, "BackButton", "Back", "Return to menu", new Vector2(0f, 0f), new Vector2(180f, 62f), new Color(0.18f, 0.23f, 0.31f, 1f), TextPrimary);
        backButton.onClick.AddListener(() => SceneManager.LoadScene(0));
        backButton.gameObject.AddComponent<UiTooltipTrigger>().Initialize(tooltip, "Returns to the main menu without starting a run.");
        var backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(24f, -24f);
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
        var veil = CreateImage(root, "BackgroundVeil", new Color(0.02f, 0.04f, 0.08f, 0.22f), Vector2.zero, Vector2.zero);
        var veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
    }

    void CreatePlainBackdrop(RectTransform root)
    {
        var veil = CreateImage(root, "PlainBackdrop", new Color(0.03f, 0.05f, 0.09f, 0.82f), Vector2.zero, Vector2.zero);
        var veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
    }

    void CreateMainMenuBackground(RectTransform root)
    {
        var background = CreateImage(root, "MainMenuBackgroundImage", Color.white, Vector2.zero, Vector2.zero);
        var backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var sprite = GetMainMenuBackgroundSprite();
        if (sprite != null)
        {
            background.sprite = sprite;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }
        else
        {
            background.color = new Color(0.03f, 0.05f, 0.09f, 1f);
        }

        var veil = CreateImage(root, "MainMenuBackgroundVeil", new Color(0.02f, 0.04f, 0.08f, 0.52f), Vector2.zero, Vector2.zero);
        var veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;

        var lowerGlow = CreateImage(root, "MainMenuLowerGlow", new Color(0.08f, 0.2f, 0.32f, 0.18f), Vector2.zero, Vector2.zero);
        var lowerGlowRect = lowerGlow.rectTransform;
        lowerGlowRect.anchorMin = new Vector2(0f, 0f);
        lowerGlowRect.anchorMax = new Vector2(1f, 0.48f);
        lowerGlowRect.offsetMin = Vector2.zero;
        lowerGlowRect.offsetMax = Vector2.zero;

    }

    void DecoratePanel(RectTransform panel, Color primary, Color secondary)
    {
        if (panel == null)
            return;

        var topBar = CreateImage(panel, "TopBar", primary, new Vector2(0f, 0f), new Vector2(0f, 0f));
        topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
        topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
        topBar.rectTransform.offsetMin = new Vector2(18f, -4f);
        topBar.rectTransform.offsetMax = new Vector2(-18f, 0f);
        topBar.gameObject.AddComponent<UiPulseGlow>();

        var bottomBar = CreateImage(panel, "BottomBar", new Color(secondary.r, secondary.g, secondary.b, 0.38f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        bottomBar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bottomBar.rectTransform.anchorMax = new Vector2(1f, 0f);
        bottomBar.rectTransform.pivot = new Vector2(0.5f, 0f);
        bottomBar.rectTransform.offsetMin = new Vector2(18f, 0f);
        bottomBar.rectTransform.offsetMax = new Vector2(-18f, 2f);
    }

    void ApplyScenePresentation(string sceneName)
    {
        DestroyExistingPresentation();

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        foreach (var camera in cameras)
        {
            if (camera == null)
                continue;

            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.antialiasingQuality = AntialiasingQuality.Medium;
        }

        var volumeObject = new GameObject("RuntimeScenePresentation", typeof(Volume));
        var volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 1f;
        volume.sharedProfile = BuildPresentationProfile(sceneName);

        CreateSceneLightRig(sceneName);
    }

    void DestroyExistingPresentation()
    {
        var presentation = GameObject.Find("RuntimeScenePresentation");
        if (presentation != null)
            Destroy(presentation);

        var lightRig = GameObject.Find("RuntimeSceneLightRig");
        if (lightRig != null)
            Destroy(lightRig);
    }

    VolumeProfile BuildPresentationProfile(string sceneName)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.72f);
        bloom.intensity.Override(sceneName == "LoadingScene" ? 0.85f : 0.6f);
        bloom.scatter.Override(0.82f);
        bloom.highQualityFiltering.Override(true);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(sceneName == "LoadingScene" ? 0.22f : 0.14f);
        color.contrast.Override(22f);
        color.saturation.Override(8f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.82f);

        return profile;
    }

    void CreateSceneLightRig(string sceneName)
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        var rig = new GameObject("RuntimeSceneLightRig").transform;
        Vector3 focusPoint = mainCamera.transform.position + mainCamera.transform.forward * 10f;

        CreateSceneSpotLight(rig, "KeyLight", mainCamera.transform.position + (mainCamera.transform.right * -4.5f) + (mainCamera.transform.up * 3.5f) + (mainCamera.transform.forward * 7f), focusPoint, new Color(0.22f, 0.82f, 1f, 1f), 7.4f);
        CreateSceneSpotLight(rig, "FillLight", mainCamera.transform.position + (mainCamera.transform.right * 4.8f) + (mainCamera.transform.up * 2.8f) + (mainCamera.transform.forward * 8.2f), focusPoint + (mainCamera.transform.right * 1.8f), new Color(1f, 0.68f, 0.28f, 1f), 6.1f);
        CreateScenePointLight(rig, "AccentGlow", mainCamera.transform.position + (mainCamera.transform.up * 1.6f) + (mainCamera.transform.forward * 5.6f), sceneName == "LoadingScene" ? new Color(0.14f, 0.88f, 1f, 1f) : new Color(0.16f, 0.54f, 1f, 1f), 22f, 2.8f);
    }

    void CreateSceneSpotLight(Transform parent, string name, Vector3 position, Vector3 lookAt, Color color, float intensity)
    {
        var lightObject = new GameObject(name, typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = position;
        lightObject.transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.range = 36f;
        light.intensity = intensity;
        light.spotAngle = 54f;
        light.innerSpotAngle = 28f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForceVertex;
    }

    void CreateScenePointLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
    {
        var lightObject = new GameObject(name, typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = position;
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForceVertex;
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

    TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, FontStyles style, Color color, Vector2 anchoredPosition, Vector2 size, bool useTitleFont = false)
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
        tmp.font = useTitleFont ? titleFontAsset : fontAsset;
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
        go.AddComponent<UiButtonMotion>();

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

        var labelText = CreateText(rect, "Label", label, 23f, FontStyles.Bold, TextPrimary, new Vector2(0f, 0f), size, true);
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

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
        return whiteSprite;
    }

    static Sprite GetMainMenuBackgroundSprite()
    {
        if (mainMenuBackdropSprite != null)
            return mainMenuBackdropSprite;

        var texture = Resources.Load<Texture2D>("MenuBackgrounds/NeonCityCasinoNight");
        if (texture == null)
            return null;

        mainMenuBackdropSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return mainMenuBackdropSprite;
    }
}

public sealed class RuntimeStageSelectCarousel : MonoBehaviour
{
    sealed class StageEntry
    {
        public int stageIndex;
        public string badge;
        public string title;
        public string objective;
        public string detail;
        public Color primary;
        public Color secondary;
    }

    readonly StageEntry[] entries =
    {
        new()
        {
            stageIndex = 0,
            badge = "LEVEL 1",
            title = "Token Sprint",
            objective = "Collect as many tokens as possible. Follow dense token trails and jump for high arcs.",
            detail = "A wider route with room to breathe. This stage is all about score, rhythm, and stacking tokens.",
            primary = new Color(0.12f, 0.54f, 0.97f, 1f),
            secondary = new Color(1f, 0.78f, 0.34f, 1f)
        },
        new()
        {
            stageIndex = 1,
            badge = "LEVEL 2",
            title = "Survival Rush",
            objective = "Run as long as possible. Moving arcade machines sweep across lanes and punish late reactions.",
            detail = "Tighter spacing, faster pressure and side-moving blockers. Distance matters more than tokens here.",
            primary = new Color(1f, 0.48f, 0.22f, 1f),
            secondary = new Color(0.29f, 0.93f, 0.96f, 1f)
        }
    };

    RawImage previewImage;
    TMP_Text badgeLabel;
    TMP_Text titleLabel;
    TMP_Text objectiveLabel;
    TMP_Text detailLabel;
    Button leftArrow;
    Button rightArrow;
    Button playButton;
    Button hardcoreButton;
    StageControls stageControls;
    int currentIndex;
    readonly bool[] hardcoreEnabled = new bool[2];
    RenderTexture previewTexture;
    GameObject previewRoot;
    Transform rotatingCube;

    public void Initialize(
        Graphic previewHost,
        TMP_Text badge,
        TMP_Text title,
        TMP_Text objective,
        TMP_Text detail,
        Button left,
        Button right,
        Button play,
        Button hardcore,
        StageControls controls)
    {
        stageControls = controls;
        badgeLabel = badge;
        titleLabel = title;
        objectiveLabel = objective;
        detailLabel = detail;
        leftArrow = left;
        rightArrow = right;
        playButton = play;
        hardcoreButton = hardcore;

        if (previewHost != null)
        {
            var previewGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(previewHost.transform, false);
            var rect = previewGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, -16f);
            previewImage = previewGo.GetComponent<RawImage>();

            CreatePreviewWorld();
        }

        if (leftArrow != null)
            leftArrow.onClick.AddListener(() => SwitchStage(-1));
        if (rightArrow != null)
            rightArrow.onClick.AddListener(() => SwitchStage(1));
        playButton.onClick.AddListener(PlayCurrentStage);
        if (hardcoreButton != null)
            hardcoreButton.onClick.AddListener(ToggleHardcore);

        RefreshView();
    }

    void CreatePreviewWorld()
    {
        previewTexture = new RenderTexture(768, 768, 16)
        {
            antiAliasing = 2
        };

        previewRoot = new GameObject("RuntimeStagePreviewWorld");
        previewRoot.hideFlags = HideFlags.HideAndDontSave;

        var cameraObject = new GameObject("PreviewCamera");
        cameraObject.transform.SetParent(previewRoot.transform, false);
        cameraObject.transform.position = new Vector3(0f, 0.8f, -4.6f);
        cameraObject.transform.LookAt(new Vector3(0f, 0.35f, 0f));
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.01f, 0.03f, 0.07f, 1f);
        camera.fieldOfView = 26f;
        camera.targetTexture = previewTexture;

        var lightObject = new GameObject("PreviewLight");
        lightObject.transform.SetParent(previewRoot.transform, false);
        lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        light.color = Color.white;

        rotatingCube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        rotatingCube.name = "PreviewCube";
        rotatingCube.SetParent(previewRoot.transform, false);
        rotatingCube.localScale = new Vector3(2.1f, 1.3f, 2.1f);
        rotatingCube.localRotation = Quaternion.Euler(-18f, 28f, 0f);

        var topStripe = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        topStripe.SetParent(rotatingCube, false);
        topStripe.localPosition = new Vector3(0f, 0.58f, 0f);
        topStripe.localScale = new Vector3(1.05f, 0.08f, 1.05f);

        var centerLane = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        centerLane.SetParent(rotatingCube, false);
        centerLane.localPosition = new Vector3(0f, 0.67f, 0f);
        centerLane.localScale = new Vector3(0.16f, 0.04f, 1.02f);

        var leftLane = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        leftLane.SetParent(rotatingCube, false);
        leftLane.localPosition = new Vector3(-0.42f, 0.67f, 0f);
        leftLane.localScale = new Vector3(0.12f, 0.04f, 1.02f);

        var rightLane = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        rightLane.SetParent(rotatingCube, false);
        rightLane.localPosition = new Vector3(0.42f, 0.67f, 0f);
        rightLane.localScale = new Vector3(0.12f, 0.04f, 1.02f);

        var machine = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        machine.SetParent(rotatingCube, false);
        machine.localPosition = new Vector3(0f, 0.92f, 0.12f);
        machine.localScale = new Vector3(0.42f, 0.48f, 0.36f);

        var screen = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        screen.SetParent(machine, false);
        screen.localPosition = new Vector3(0f, 0.12f, -0.56f);
        screen.localScale = new Vector3(0.72f, 0.34f, 0.12f);

        previewImage.texture = previewTexture;
    }

    void RefreshView()
    {
        var entry = entries[currentIndex];
        badgeLabel.text = entry.badge;
        titleLabel.text = entry.title;
        objectiveLabel.text = entry.objective;
        detailLabel.text = entry.detail;
        SetButtonText(playButton, $"PLAY {entry.badge}", hardcoreEnabled[currentIndex] ? "Launch hardcore run" : "Launch selected run");
        RefreshHardcoreButton(entry);

        if (leftArrow != null)
            leftArrow.gameObject.SetActive(currentIndex > 0);
        if (rightArrow != null)
            rightArrow.gameObject.SetActive(currentIndex < entries.Length - 1);

        ApplyPreviewPalette(entry);
    }

    void ApplyPreviewPalette(StageEntry entry)
    {
        if (rotatingCube == null)
            return;

        var renderers = rotatingCube.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            Color color = i == 0
                ? entry.primary
                : i == 1
                    ? entry.secondary
                    : i == 2 || i == 3 || i == 4
                        ? new Color(0.04f, 0.08f, 0.14f, 1f)
                        : entry.secondary;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.45f);

            renderer.sharedMaterial = material;
        }
    }

    void SwitchStage(int direction)
    {
        currentIndex = Mathf.Clamp(currentIndex + direction, 0, entries.Length - 1);
        RefreshView();
    }

    void ToggleHardcore()
    {
        hardcoreEnabled[currentIndex] = !hardcoreEnabled[currentIndex];
        RefreshView();
    }

    void PlayCurrentStage()
    {
        if (stageControls == null)
            return;

        RunGameplayDirector.SetHardcoreMode(hardcoreEnabled[currentIndex]);

        if (entries[currentIndex].stageIndex == 0)
            stageControls.PressPlay();
        else
            stageControls.PressPlaySecond();
    }

    void RefreshHardcoreButton(StageEntry entry)
    {
        if (hardcoreButton == null)
            return;

        bool isEnabled = hardcoreEnabled[currentIndex];
        Color fill = isEnabled
            ? new Color(0.98f, 0.47f, 0.19f, 1f)
            : new Color(0.11f, 0.16f, 0.23f, 0.96f);
        Color outline = isEnabled
            ? entry.secondary
            : new Color(1f, 0.62f, 0.26f, 0.55f);

        ApplyButtonStyle(hardcoreButton, fill, outline);
        SetButtonText(
            hardcoreButton,
            isEnabled ? "HARDCORE ON" : "HARDCORE OFF",
            isEnabled ? "x2 coins / ultra speed" : "x2 coins / mega speed");
    }

    void ApplyButtonStyle(Button button, Color fill, Color outlineColor)
    {
        if (button == null)
            return;

        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = fill;

        var colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Color.Lerp(fill, Color.white, 0.1f);
        colors.pressedColor = Color.Lerp(fill, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(fill.r, fill.g, fill.b, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var outline = button.GetComponent<Outline>();
        if (outline != null)
            outline.effectColor = outlineColor;
    }

    void SetButtonText(Button button, string label, string subLabel)
    {
        if (button == null)
            return;

        var labelTransform = button.transform.Find("Label");
        if (labelTransform != null)
        {
            var labelText = labelTransform.GetComponent<TMP_Text>();
            if (labelText != null)
                labelText.text = label;
        }

        var subLabelTransform = button.transform.Find("SubLabel");
        if (subLabelTransform != null)
        {
            var subLabelText = subLabelTransform.GetComponent<TMP_Text>();
            if (subLabelText != null)
                subLabelText.text = subLabel;
        }
    }

    void Update()
    {
        if (rotatingCube != null)
            rotatingCube.Rotate(new Vector3(17f, 28f, 0f) * Time.unscaledDeltaTime, Space.Self);
    }

    void OnDestroy()
    {
        if (previewTexture != null)
            previewTexture.Release();

        if (previewRoot != null)
            Destroy(previewRoot);
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
