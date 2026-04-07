using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(900)]
public sealed class RunGameplayDirector : MonoBehaviour
{
    public static RunGameplayDirector Instance { get; private set; }

    public static int SelectedStageIndex { get; private set; }
    public static bool IsHardcoreMode { get; private set; }

    static Sprite roundedSprite;
    static GameObject arcadeMachinePrefab;
    static GameObject playerCharacterPrefab;
    static GameObject bitcoinCoinModelPrefab;
    static Material[] playerCharacterMaterials;
    static Material arcadeMachineBodyMaterial;
    static Material arcadeMachineMarqueeMaterial;
    static Material bitcoinCoinFaceMaterial;
    static Material bitcoinCoinSideMaterial;
    static AnimationClip[] playerCharacterClips;
    static TMP_FontAsset titleFontAsset;
    static TMP_FontAsset bodyFontAsset;

    readonly string[] symbolPool = { "7", "$", "H", "H", "BAR", "H", "X" };

    Canvas canvas;
    TMP_Text coinLabel;
    TMP_Text objectiveLabel;
    RectTransform coinPanel;
    TMP_Text distanceLabel;
    CanvasGroup reviveGroup;
    TMP_Text reviveTitle;
    TMP_Text reviveSubtitle;
    TMP_Text[] slotTexts;
    bool reviveUsed;
    bool reviveRunning;
    Coroutine refreshRoutine;
    PlayerMovement trackedPlayer;
    float runStartZ;
    float gameplayFloorY;
    readonly HashSet<int> arrangedObstacleIds = new();
    readonly HashSet<int> arrangedCoinIds = new();
    readonly int[] lanePatternLevelOne = { 1, 0, 2, 1, 0, 1, 2, 1, 0, 2, 1, 1, 2, 0 };
    readonly int[] lanePatternLevelTwo = { 1, 2, 0, 1, 2, 1, 0, 2, 1, 0, 1, 2, 0, 1 };
    readonly int[][] obstacleRowsLevelOne =
    {
        new[] { 0, 1 },
        new[] { 2 },
        new[] { 1, 2 },
        new[] { 0 },
        new[] { 0, 2 },
        new[] { 1 },
        new[] { 0, 1 },
        new[] { 2 },
        new[] { 0, 2 },
        new[] { 1 },
        new[] { 1, 2 },
        new[] { 0 },
        new[] { 0, 1 },
        new[] { 2 },
        new[] { 0, 2 },
        new[] { 1 }
    };
    readonly int[][] obstacleRowsLevelTwo =
    {
        new[] { 0, 1 },
        new[] { 2 },
        new[] { 1, 2 },
        new[] { 0 },
        new[] { 0, 2 },
        new[] { 0, 2 },
        new[] { 1 },
        new[] { 0, 1 },
        new[] { 2 },
        new[] { 1 },
        new[] { 2, 1 },
        new[] { 0 },
        new[] { 0, 1 },
        new[] { 2 },
        new[] { 0, 2 },
        new[] { 1 },
        new[] { 1, 2 },
        new[] { 0 }
    };
    int nextObstacleIndex;
    int nextCoinIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static RunGameplayDirector EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("RunGameplayDirector");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<RunGameplayDirector>();
        return Instance;
    }

    public static void SetSelectedStage(int stageIndex)
    {
        SelectedStageIndex = Mathf.Clamp(stageIndex, 0, 1);
    }

    public static void SetHardcoreMode(bool enabled)
    {
        IsHardcoreMode = enabled;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        StartCoroutine(ApplyWhenReady());
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "CasinoRun")
        {
            StartCoroutine(ApplyWhenReady());
            return;
        }

        if (canvas != null)
            canvas = null;
    }

    IEnumerator ApplyWhenReady()
    {
        yield return null;
        yield return null;
        ApplyToCurrentScene();
    }

    void ApplyToCurrentScene()
    {
        if (SceneManager.GetActiveScene().name != "CasinoRun")
            return;

        bool sceneAuthoringMode = IsSceneAuthoringModeEnabled();

        MasterLevelInfo.ResetCoins();
        MasterLevelInfo.SetCoinMultiplier(IsHardcoreMode ? 2 : 1);
        reviveUsed = false;
        reviveRunning = false;
        arrangedObstacleIds.Clear();
        arrangedCoinIds.Clear();
        nextObstacleIndex = 0;
        nextCoinIndex = 0;
        trackedPlayer = FindObjectOfType<PlayerMovement>(true);
        runStartZ = trackedPlayer != null ? trackedPlayer.transform.position.z : 0f;
        gameplayFloorY = ResolveGameplayFloorY();
        ConfigurePlayerCollisionRig();
        if (trackedPlayer != null)
        {
            trackedPlayer.SetRuntimeGroundPlane(gameplayFloorY);
            Vector3 startPosition = trackedPlayer.transform.position;
            startPosition.y = gameplayFloorY;
            trackedPlayer.transform.position = startPosition;
        }

        BuildHud();
        BuildReviveUi();
        StyleInGameUi();
        ConfigureStageVariant();
        ApplySceneMood();
        ApplyRuntimePresentation();
        if (sceneAuthoringMode)
        {
            CleanupRuntimeGeneratedObjects();
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }
        }
        else
        {
            BuildTrackDressing();
            EnsureRuntimeObstaclePool();
            ArrangeGameplayObjects();
            ReplaceObstacleVisuals();
            ReplacePlayerVisual();

            if (refreshRoutine != null)
                StopCoroutine(refreshRoutine);
            refreshRoutine = StartCoroutine(RefreshRuntimeVisuals());
        }
    }

    bool IsSceneAuthoringModeEnabled()
    {
#if UNITY_EDITOR
        return GameObject.Find("RunSceneAuthoringMode") != null;
#else
        return false;
#endif
    }

    void CleanupRuntimeGeneratedObjects()
    {
        var runtimeDressing = GameObject.Find("RuntimeStageDressing");
        if (runtimeDressing != null)
            Destroy(runtimeDressing);

        var runtimeObstaclePool = GameObject.Find("RuntimeObstaclePool");
        if (runtimeObstaclePool != null)
            Destroy(runtimeObstaclePool);
    }

    void BuildHud()
    {
        canvas = FindObjectOfType<Canvas>(true);
        if (canvas == null)
            return;

        var oldBack = GameObject.Find("CoinBack");
        if (oldBack != null)
            oldBack.SetActive(false);

        var oldCount = GameObject.Find("CoinCount");
        if (oldCount != null)
            oldCount.SetActive(false);

        var existing = canvas.transform.Find("RuntimeCoinHud");
        if (existing != null)
            Destroy(existing.gameObject);

        var root = new GameObject("RuntimeCoinHud", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        coinPanel = root.GetComponent<RectTransform>();
        coinPanel.anchorMin = new Vector2(0.5f, 1f);
        coinPanel.anchorMax = new Vector2(0.5f, 1f);
        coinPanel.pivot = new Vector2(0.5f, 1f);
        coinPanel.anchoredPosition = new Vector2(0f, -18f);
        coinPanel.sizeDelta = new Vector2(820f, 112f);

        var panelImage = root.GetComponent<Image>();
        panelImage.sprite = GetRoundedSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.03f, 0.06f, 0.12f, 0.9f);
        root.transform.SetAsLastSibling();

        var outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.29f, 0.93f, 0.96f, 0.3f);
        outline.effectDistance = new Vector2(2f, -2f);

        var title = CreateHudText(root.transform, "RunTitle", "COINDASH", GetTitleFont(), 28f, FontStyles.Bold, new Color(1f, 0.79f, 0.36f, 1f), new Vector2(-280f, -18f), new Vector2(200f, 30f), TextAlignmentOptions.Center);
        string subtitleText = SelectedStageIndex == 0 ? "TOKEN CHASE" : "SURVIVAL RUSH";
        if (IsHardcoreMode)
            subtitleText += "  HARDCORE";
        var subtitle = CreateHudText(root.transform, "RunSubtitle", subtitleText, GetTitleFont(), 20f, FontStyles.Bold, Color.white, new Vector2(-280f, -48f), new Vector2(300f, 24f), TextAlignmentOptions.Center);
        subtitle.color = new Color(0.93f, 0.97f, 1f, 1f);
        string objectiveText = SelectedStageIndex == 0 ? "GOAL  COLLECT AS MANY TOKENS AS YOU CAN" : "GOAL  RUN AS FAR AS POSSIBLE";
        if (IsHardcoreMode)
            objectiveText += "   X2 COINS";
        objectiveLabel = CreateHudText(root.transform, "ObjectiveLabel", objectiveText, GetBodyFont(), 17f, FontStyles.Bold, new Color(0.29f, 0.93f, 0.96f, 1f), new Vector2(0f, -86f), new Vector2(620f, 22f), TextAlignmentOptions.Center);

        var label = new GameObject("CoinLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, -22f);
        labelRect.sizeDelta = new Vector2(200f, 38f);
        coinLabel = label.GetComponent<TextMeshProUGUI>();
        coinLabel.font = GetBodyFont();
        coinLabel.fontSize = 24f;
        coinLabel.enableAutoSizing = true;
        coinLabel.fontSizeMin = 16f;
        coinLabel.fontSizeMax = 24f;
        coinLabel.color = Color.white;
        coinLabel.alignment = TextAlignmentOptions.Center;

        var coinBadge = CreateHudText(root.transform, "CoinBadge", "TOKENS", GetTitleFont(), 18f, FontStyles.Bold, new Color(0.29f, 0.93f, 0.96f, 1f), new Vector2(0f, -12f), new Vector2(180f, 20f), TextAlignmentOptions.Center);

        distanceLabel = CreateHudText(root.transform, "DistanceLabel", "0 M", GetBodyFont(), 24f, FontStyles.Bold, new Color(0.93f, 0.97f, 1f, 1f), new Vector2(282f, -22f), new Vector2(180f, 28f), TextAlignmentOptions.Center);
        var distanceCaption = CreateHudText(root.transform, "DistanceCaption", "RUN DISTANCE", GetTitleFont(), 18f, FontStyles.Bold, new Color(1f, 0.79f, 0.36f, 1f), new Vector2(282f, -48f), new Vector2(220f, 24f), TextAlignmentOptions.Center);

        title.gameObject.AddComponent<UiPulseGlow>();
        coinBadge.gameObject.AddComponent<UiPulseGlow>();
        distanceCaption.gameObject.AddComponent<UiPulseGlow>();
        objectiveLabel.gameObject.AddComponent<UiPulseGlow>();

        UpdateCoinHud(MasterLevelInfo.CoinCount);
        UpdateDistanceHud();
        MasterLevelInfo.CoinCountChanged -= UpdateCoinHud;
        MasterLevelInfo.CoinCountChanged += UpdateCoinHud;

        BuildControlsHud(canvas.transform);
    }

    void UpdateCoinHud(int coinCount)
    {
        if (coinLabel == null || coinPanel == null)
            return;

        coinLabel.text = $"{coinCount:00}";
    }

    TMP_Text CreateHudText(Transform parent, string name, string text, TMP_FontAsset font, float size, FontStyles style, Color color, Vector2 anchoredPosition, Vector2 rectSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = rectSize;

        var textLabel = go.GetComponent<TextMeshProUGUI>();
        textLabel.font = font != null ? font : TMP_Settings.defaultFontAsset;
        textLabel.text = text;
        textLabel.fontSize = size;
        textLabel.fontStyle = style;
        textLabel.color = color;
        textLabel.alignment = alignment;
        textLabel.enableAutoSizing = true;
        textLabel.fontSizeMin = size * 0.6f;
        textLabel.fontSizeMax = size;
        return textLabel;
    }

    void BuildControlsHud(Transform parent)
    {
        var existing = canvas.transform.Find("RuntimeControlsHud");
        if (existing != null)
            Destroy(existing.gameObject);

        var controlPanel = new GameObject("RuntimeControlsHud", typeof(RectTransform), typeof(Image));
        controlPanel.transform.SetParent(parent, false);
        var rect = controlPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 32f);
        rect.sizeDelta = new Vector2(560f, 72f);

        var image = controlPanel.GetComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(0.03f, 0.06f, 0.11f, 0.84f);

        var outline = controlPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.53f, 0.97f, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);

        var controls = CreateHudText(controlPanel.transform, "ControlsText", "A  LEFT LANE     D  RIGHT LANE     SPACE  JUMP", GetBodyFont(), 22f, FontStyles.Bold, Color.white, new Vector2(0f, -14f), new Vector2(500f, 28f), TextAlignmentOptions.Center);
        var caption = CreateHudText(controlPanel.transform, "ControlsCaption", "THREE-LANE ARCADE FLOW", GetTitleFont(), 18f, FontStyles.Bold, new Color(1f, 0.79f, 0.36f, 1f), new Vector2(0f, -42f), new Vector2(320f, 22f), TextAlignmentOptions.Center);
        controls.gameObject.AddComponent<UiPulseGlow>();
        caption.gameObject.AddComponent<UiPulseGlow>();
    }

    void BuildReviveUi()
    {
        if (canvas == null)
            return;

        var existing = canvas.transform.Find("RuntimeReviveUi");
        if (existing != null)
            Destroy(existing.gameObject);

        var root = new GameObject("RuntimeReviveUi", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        reviveGroup = root.GetComponent<CanvasGroup>();
        reviveGroup.alpha = 0f;
        reviveGroup.interactable = false;
        reviveGroup.blocksRaycasts = false;

        var veil = new GameObject("ReviveVeil", typeof(RectTransform), typeof(Image));
        veil.transform.SetParent(root.transform, false);
        var veilRect = veil.GetComponent<RectTransform>();
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
        var veilImage = veil.GetComponent<Image>();
        veilImage.color = new Color(0.01f, 0.04f, 0.08f, 0.56f);

        var panel = new GameObject("RevivePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 320f);
        var panelImage = panel.GetComponent<Image>();
        panelImage.sprite = GetRoundedSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.06f, 0.09f, 0.15f, 0.96f);

        reviveTitle = CreatePanelText(panelRect, "ReviveTitle", "Lucky Spin", 38f, FontStyles.Bold, Color.white, new Vector2(0f, 108f), new Vector2(300f, 44f));
        reviveTitle.alignment = TextAlignmentOptions.Center;
        reviveSubtitle = CreatePanelText(panelRect, "ReviveSubtitle", "Match 3 hearts to stay in the run", 18f, FontStyles.Normal, new Color(0.79f, 0.86f, 0.94f, 1f), new Vector2(0f, 70f), new Vector2(340f, 28f));
        reviveSubtitle.alignment = TextAlignmentOptions.Center;

        slotTexts = new TMP_Text[3];
        for (int i = 0; i < slotTexts.Length; i++)
        {
            var slot = new GameObject($"Slot{i}", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(panel.transform, false);
            var slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2((i - 1) * 116f, -8f);
            slotRect.sizeDelta = new Vector2(96f, 112f);
            var slotImage = slot.GetComponent<Image>();
            slotImage.sprite = GetRoundedSprite();
            slotImage.type = Image.Type.Sliced;
            slotImage.color = new Color(0.1f, 0.15f, 0.24f, 1f);

            var slotLabel = CreatePanelText(slotRect, "Symbol", "?", 42f, FontStyles.Bold, new Color(1f, 0.83f, 0.39f, 1f), Vector2.zero, new Vector2(76f, 76f));
            slotLabel.alignment = TextAlignmentOptions.Center;
            slotTexts[i] = slotLabel;
        }

        var footer = CreatePanelText(panelRect, "ReviveFooter", "One casino miracle per run", 16f, FontStyles.Italic, new Color(0.65f, 0.75f, 0.84f, 1f), new Vector2(0f, -118f), new Vector2(280f, 24f));
        footer.alignment = TextAlignmentOptions.Center;
    }

    TMP_Text CreatePanelText(RectTransform parent, string name, string text, float size, FontStyles style, Color color, Vector2 anchoredPosition, Vector2 rectSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = rectSize;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = size >= 24f || style.HasFlag(FontStyles.Bold) ? GetTitleFont() : GetBodyFont();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.65f;
        label.fontSizeMax = size;
        return label;
    }

    void UpdateDistanceHud()
    {
        if (distanceLabel == null || trackedPlayer == null)
            return;

        float distance = Mathf.Max(0f, trackedPlayer.transform.position.z - runStartZ);
        distanceLabel.text = $"{Mathf.FloorToInt(distance)} M";
    }

    void StyleInGameUi()
    {
        var fadeOut = GameObject.Find("FadeOut");
        if (fadeOut != null)
            fadeOut.transform.SetAsLastSibling();
    }

    void ConfigureStageVariant()
    {
        var player = trackedPlayer != null ? trackedPlayer : FindObjectOfType<PlayerMovement>(true);
        if (player != null)
        {
            // Normal mode: slightly faster baseline flow.
            float baseSpeed = SelectedStageIndex == 0 ? 12.0f : 13.1f;
            float baseLaneSpeed = SelectedStageIndex == 0 ? 16.8f : 18.2f;
            if (IsHardcoreMode)
            {
                baseSpeed = SelectedStageIndex == 0 ? 18.2f : 19.6f;
                baseLaneSpeed = SelectedStageIndex == 0 ? 25.4f : 27.2f;
            }

            player.playerSpeed = baseSpeed;
            player.laneSwitchSpeed = baseLaneSpeed;
        }

        var generator = FindObjectOfType<SegmentGenerator>(true);
        if (generator != null)
        {
            float delay = SelectedStageIndex == 0 ? 2.75f : 2.35f;
            if (IsHardcoreMode)
                delay = SelectedStageIndex == 0 ? 1.1f : 0.9f;
            generator.SetSpawnDelay(delay);
        }
    }

    float ResolveGameplayFloorY()
    {
        if (trackedPlayer != null)
        {
            Vector3 origin = trackedPlayer.transform.position + Vector3.up * 4f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
        }

        return 0.015f;
    }

    void EnsureRuntimeObstaclePool()
    {
        int desiredCount = SelectedStageIndex == 0 ? 42 : 56;
        if (IsHardcoreMode)
            desiredCount += 10;
        var obstacles = FindObjectsOfType<CollisionDetect>(true);
        int missing = Mathf.Max(0, desiredCount - obstacles.Length);
        if (missing == 0)
            return;

        var root = GameObject.Find("RuntimeObstaclePool");
        if (root == null)
            root = new GameObject("RuntimeObstaclePool");

        for (int i = 0; i < missing; i++)
            CreateRuntimeObstacle(root.transform, i);
    }

    void CreateRuntimeObstacle(Transform parent, int index)
    {
        var obstacle = new GameObject($"RuntimeObstacle_{index}", typeof(BoxCollider), typeof(CollisionDetect));
        obstacle.transform.SetParent(parent, false);
        obstacle.transform.position = new Vector3(0f, gameplayFloorY, 0f);

        var box = obstacle.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.95f, 0f);
        box.size = new Vector3(1.25f, 1.9f, 1.15f);

        arrangedObstacleIds.Remove(obstacle.GetInstanceID());
    }

    void ConfigurePlayerCollisionRig()
    {
        if (trackedPlayer == null)
            return;

        var playerTransform = trackedPlayer.transform;
        var rootBody = trackedPlayer.GetComponent<Rigidbody>();
        if (rootBody == null)
            rootBody = trackedPlayer.gameObject.AddComponent<Rigidbody>();

        rootBody.isKinematic = true;
        rootBody.useGravity = false;
        rootBody.interpolation = RigidbodyInterpolation.Interpolate;
        rootBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rootBody.constraints = RigidbodyConstraints.FreezeRotation;

        var rootCapsule = trackedPlayer.GetComponent<CapsuleCollider>();
        if (rootCapsule != null)
        {
            rootCapsule.enabled = true;
            rootCapsule.isTrigger = false;
            rootCapsule.center = new Vector3(0f, 1f, 0f);
            rootCapsule.height = 2f;
            rootCapsule.radius = 0.46f;
        }

        foreach (var childBody in playerTransform.GetComponentsInChildren<Rigidbody>(true))
        {
            if (childBody.transform == playerTransform)
                continue;

            childBody.isKinematic = true;
            childBody.detectCollisions = false;
        }

        foreach (var childCollider in playerTransform.GetComponentsInChildren<Collider>(true))
        {
            if (childCollider.transform == playerTransform)
                continue;

            if (childCollider.transform.name == "RuntimeCoinCollector")
                continue;

            childCollider.enabled = false;
        }

        EnsurePlayerCoinCollector(playerTransform);
    }

    void EnsurePlayerCoinCollector(Transform playerTransform)
    {
        var existing = playerTransform.Find("RuntimeCoinCollector");
        GameObject collectorObject;
        if (existing == null)
        {
            collectorObject = new GameObject("RuntimeCoinCollector");
            collectorObject.transform.SetParent(playerTransform, false);
        }
        else
        {
            collectorObject = existing.gameObject;
        }

        collectorObject.transform.localPosition = Vector3.zero;
        collectorObject.transform.localRotation = Quaternion.identity;
        collectorObject.transform.localScale = Vector3.one;

        var collider = collectorObject.GetComponent<CapsuleCollider>();
        if (collider == null)
            collider = collectorObject.AddComponent<CapsuleCollider>();

        collider.enabled = true;
        collider.isTrigger = true;
        collider.direction = 1;
        collider.center = new Vector3(0f, 0.95f, 0f);
        collider.height = 2.8f;
        collider.radius = 1.1f;
    }

    void ApplySceneMood()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.02f, 0.05f, 0.08f, 1f);
        RenderSettings.fogDensity = 0.01f;
        RenderSettings.ambientLight = new Color(0.18f, 0.26f, 0.34f, 1f);

        var camera = Camera.main;
        if (camera != null)
            camera.backgroundColor = new Color(0.01f, 0.03f, 0.05f, 1f);

        foreach (var light in FindObjectsOfType<Light>(true))
        {
            if (light.type == LightType.Directional)
            {
                light.color = new Color(0.53f, 0.8f, 1f, 1f);
                light.intensity = 1.15f;
            }
            else if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                light.color = SelectedStageIndex == 0
                    ? new Color(0.18f, 0.68f, 1f, 1f)
                    : new Color(1f, 0.62f, 0.26f, 1f);
            }
        }
    }

    void ApplyRuntimePresentation()
    {
        var existing = GameObject.Find("RuntimeRunPresentation");
        if (existing != null)
            Destroy(existing);

        foreach (var camera in FindObjectsOfType<Camera>(true))
        {
            if (camera == null)
                continue;

            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.antialiasingQuality = AntialiasingQuality.Medium;
        }

        var root = new GameObject("RuntimeRunPresentation", typeof(Volume));
        var volume = root.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 1f;
        volume.sharedProfile = BuildRunPresentationProfile();
    }

    VolumeProfile BuildRunPresentationProfile()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.76f);
        bloom.intensity.Override(IsHardcoreMode ? 0.72f : 0.48f);
        bloom.scatter.Override(0.72f);
        bloom.highQualityFiltering.Override(true);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(IsHardcoreMode ? 0.2f : 0.12f);
        color.contrast.Override(IsHardcoreMode ? 24f : 16f);
        color.saturation.Override(10f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(IsHardcoreMode ? 0.2f : 0.14f);
        vignette.smoothness.Override(0.84f);

        return profile;
    }

    void BuildTrackDressing()
    {
        var existing = GameObject.Find("RuntimeStageDressing");
        if (existing != null)
            Destroy(existing);

        var root = new GameObject("RuntimeStageDressing").transform;
        float startZ = trackedPlayer != null ? trackedPlayer.transform.position.z - 40f : -40f;
        float length = 820f;
        float centerY = gameplayFloorY - 0.14f;
        float ceilingY = centerY + 4.24f;
        float sideWallBottomY = centerY - 0.04f;
        float sideWallTopY = ceilingY + 0.02f;
        float sideWallHeight = sideWallTopY - sideWallBottomY;
        float sideWallCenterY = sideWallBottomY + (sideWallHeight * 0.5f);
        float laneStripThickness = 0.01f;

        CreateStrip(root, "FloorBase", new Vector3(0f, centerY - 0.02f, startZ + (length * 0.5f)), new Vector3(9.6f, 0.018f, length), new Color(0.03f, 0.05f, 0.08f, 1f));
        CreateStrip(root, "LaneGlowLeft", new Vector3(-3.5f, centerY, startZ + (length * 0.5f)), new Vector3(0.16f, laneStripThickness, length), new Color(0.17f, 0.75f, 1f, 0.95f), true);
        CreateStrip(root, "LaneGlowCenter", new Vector3(0f, centerY, startZ + (length * 0.5f)), new Vector3(0.18f, laneStripThickness, length), new Color(1f, 0.78f, 0.32f, 0.95f), true);
        CreateStrip(root, "LaneGlowRight", new Vector3(3.5f, centerY, startZ + (length * 0.5f)), new Vector3(0.16f, laneStripThickness, length), new Color(0.17f, 0.75f, 1f, 0.95f), true);
        CreateStrip(root, "TrackBorderLeft", new Vector3(-7.72f, sideWallCenterY, startZ + (length * 0.5f)), new Vector3(0.28f, sideWallHeight, length), new Color(0.03f, 0.06f, 0.11f, 1f));
        CreateStrip(root, "TrackBorderRight", new Vector3(7.72f, sideWallCenterY, startZ + (length * 0.5f)), new Vector3(0.28f, sideWallHeight, length), new Color(0.03f, 0.06f, 0.11f, 1f));
        CreateStrip(root, "SideWallLeft", new Vector3(-8.82f, sideWallCenterY, startZ + (length * 0.5f)), new Vector3(1.95f, sideWallHeight, length), new Color(0.02f, 0.04f, 0.08f, 1f));
        CreateStrip(root, "SideWallRight", new Vector3(8.82f, sideWallCenterY, startZ + (length * 0.5f)), new Vector3(1.95f, sideWallHeight, length), new Color(0.02f, 0.04f, 0.08f, 1f));
        CreateStrip(root, "WallGlowLeft", new Vector3(-7.5f, sideWallCenterY, startZ + (length * 0.5f)), new Vector3(0.045f, sideWallHeight - 0.2f, length), new Color(0.09f, 0.47f, 0.9f, 0.66f), true);
        CreateStrip(root, "WallGlowRight", new Vector3(7.5f, sideWallCenterY, startZ + (length * 0.5f)), new Vector3(0.045f, sideWallHeight - 0.2f, length), new Color(0.09f, 0.47f, 0.9f, 0.66f), true);
        CreateStrip(root, "CeilingPanel", new Vector3(0f, ceilingY, startZ + (length * 0.5f)), new Vector3(15.86f, 0.06f, length), new Color(0.04f, 0.08f, 0.13f, 1f));

        for (int i = 0; i < 16; i++)
        {
            float z = startZ + 18f + (i * 28f);
            Color pulseColor = i % 2 == 0 ? new Color(0.16f, 0.7f, 1f, 0.92f) : new Color(1f, 0.7f, 0.3f, 0.92f);
            CreateStrip(root, $"CrossBeam_{i}", new Vector3(0f, centerY - 0.01f, z), new Vector3(7.8f, 0.009f, 0.55f), pulseColor, true);
            CreateStrip(root, $"CeilingGlow_{i}", new Vector3(0f, ceilingY - 0.03f, z), new Vector3(6.2f, 0.045f, 0.32f), pulseColor, true);
            if (i % 4 == 1)
                CreateSpotlightPair(root, z + 5f, Color.Lerp(pulseColor, Color.white, 0.18f), ceilingY - 0.26f);
        }
    }

    void CreateSideCasinoBays(Transform root, float startZ, float centerY, float length)
    {
        int sectionCount = Mathf.CeilToInt(length / 32f) + 1;
        float firstZ = startZ + 8f;

        for (int i = 0; i < sectionCount; i++)
        {
            float z = firstZ + (i * 32f);
            bool warmAccent = i % 2 == 1;
            Color accent = warmAccent
                ? new Color(1f, 0.62f, 0.25f, 0.94f)
                : new Color(0.21f, 0.76f, 1f, 0.94f);

            float bayDepth = warmAccent ? 9.2f : 8.2f;
            float insetDepth = bayDepth - 1.25f;

            CreateStrip(root, $"LeftBayShell_{i}", new Vector3(-8.86f, centerY + 1.72f, z), new Vector3(1.72f, 2.95f, bayDepth), new Color(0.03f, 0.08f, 0.14f, 1f));
            CreateStrip(root, $"RightBayShell_{i}", new Vector3(8.86f, centerY + 1.72f, z), new Vector3(1.72f, 2.95f, bayDepth), new Color(0.03f, 0.08f, 0.14f, 1f));

            CreateStrip(root, $"LeftBayInset_{i}", new Vector3(-8.02f, centerY + 1.66f, z), new Vector3(0.05f, 2.45f, insetDepth), new Color(0.08f, 0.25f, 0.42f, 0.9f), true);
            CreateStrip(root, $"RightBayInset_{i}", new Vector3(8.02f, centerY + 1.66f, z), new Vector3(0.05f, 2.45f, insetDepth), new Color(0.08f, 0.25f, 0.42f, 0.9f), true);

            CreateStrip(root, $"LeftBayTopGlow_{i}", new Vector3(-8.1f, centerY + 2.95f, z), new Vector3(0.22f, 0.045f, insetDepth - 0.3f), accent, true);
            CreateStrip(root, $"RightBayTopGlow_{i}", new Vector3(8.1f, centerY + 2.95f, z), new Vector3(0.22f, 0.045f, insetDepth - 0.3f), accent, true);
            CreateStrip(root, $"LeftBayBottomGlow_{i}", new Vector3(-8.1f, centerY + 0.36f, z), new Vector3(0.22f, 0.03f, insetDepth - 0.5f), accent * 0.75f, true);
            CreateStrip(root, $"RightBayBottomGlow_{i}", new Vector3(8.1f, centerY + 0.36f, z), new Vector3(0.22f, 0.03f, insetDepth - 0.5f), accent * 0.75f, true);

            if (i % 2 == 0)
            {
                CreateGlowOrb(root, $"LeftBayOrb_{i}", new Vector3(-8.28f, centerY + 2.25f, z), 0.2f, accent);
                CreateGlowOrb(root, $"RightBayOrb_{i}", new Vector3(8.28f, centerY + 2.25f, z), 0.2f, accent);
            }
        }
    }

    void CreateArcadeCrowd(Transform parent, float startZ, float length)
    {
        var prefab = GetArcadeMachinePrefab();
        if (prefab == null)
            return;

        for (int i = 0; i < 16; i++)
        {
            float z = startZ + 26f + (i * (length / 16f));
            float scale = 0.62f + (i % 3) * 0.05f;
            CreateArcadeDecor(parent, new Vector3(-13.2f, 0f, z), Quaternion.Euler(0f, 90f, 0f), scale);
            CreateArcadeDecor(parent, new Vector3(13.2f, 0f, z), Quaternion.Euler(0f, -90f, 0f), scale);
        }
    }

    void CreateArcadeDecor(Transform parent, Vector3 position, Quaternion rotation, float scale)
    {
        var prefab = GetArcadeMachinePrefab();
        if (prefab == null)
            return;

        var decor = Instantiate(prefab, parent);
        decor.name = "ArcadeDecor";
        decor.transform.position = position;
        decor.transform.rotation = rotation;
        decor.transform.localScale = Vector3.one * scale;

        foreach (var collider in decor.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (var listener in decor.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        SnapDecorToGround(decor.transform);
        ClampDecorOutsideTrack(decor.transform, 8.8f, 0.55f);
    }

    void CreateStrip(Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool emissive = false)
    {
        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = name;
        strip.transform.SetParent(parent, false);
        strip.transform.position = position;
        strip.transform.localScale = scale;

        var collider = strip.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = strip.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial(color, emissive);
    }

    Material CreateRuntimeMaterial(Color color, bool emissive = false)
    {
        var shader = Shader.Find(emissive ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            Color emission = color * 2.4f;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emission);
        }
        else if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.4f);
        }

        return material;
    }

    void CreateGlowOrb(Transform parent, string name, Vector3 position, float diameter, Color color)
    {
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = name;
        orb.transform.SetParent(parent, false);
        orb.transform.position = position;
        orb.transform.localScale = Vector3.one * diameter;
        var collider = orb.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        var renderer = orb.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial(color, true);
    }

    void CreateSpotlightPair(Transform parent, float z, Color color, float y)
    {
        CreateLight(parent, $"LeftLight_{z:F0}", new Vector3(-5.7f, y, z), color, 6.5f, 4.4f);
        CreateLight(parent, $"RightLight_{z:F0}", new Vector3(5.7f, y, z), color, 6.5f, 4.4f);
    }

    void CreateLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
    {
        var go = new GameObject(name, typeof(Light));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var light = go.GetComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForceVertex;
        light.bounceIntensity = 0f;
    }

    void ArrangeGameplayObjects()
    {
        if (trackedPlayer == null)
            trackedPlayer = FindObjectOfType<PlayerMovement>(true);

        if (trackedPlayer == null)
            return;

        ArrangeObstacles();
        ArrangeCoins();
    }

    void ArrangeObstacles()
    {
        var obstacles = FindObjectsOfType<CollisionDetect>(true);
        if (obstacles == null || obstacles.Length == 0)
            return;

        for (int i = 0; i < obstacles.Length; i++)
        {
            var obstacle = obstacles[i];
            if (obstacle == null)
                continue;

            int obstacleId = obstacle.gameObject.GetInstanceID();
            if (arrangedObstacleIds.Contains(obstacleId))
                continue;

            var box = obstacle.GetComponent<BoxCollider>();
            if (box == null)
                continue;

            EnsureObstaclePhysicsRig(obstacle, box);

            GetObstaclePlacement(nextObstacleIndex, out float rowZ, out int laneIndex, out bool useMover);
            float laneX = trackedPlayer.GetLaneWorldX(laneIndex);
            float obstacleGroundY = GetGroundYAt(laneX, rowZ, gameplayFloorY);

            var mover = obstacle.GetComponent<LaneObstacleMover>();
            if (useMover)
            {
                if (mover == null)
                    mover = obstacle.gameObject.AddComponent<LaneObstacleMover>();

                float moverSpeed = 2.8f + ((nextObstacleIndex % 3) * 0.45f);
                if (IsHardcoreMode)
                    moverSpeed *= 1.9f;

                mover.Configure(
                    trackedPlayer.GetLaneWorldX(0),
                    trackedPlayer.GetLaneWorldX(2),
                    moverSpeed,
                    obstacleGroundY,
                    rowZ);
            }
            else
            {
                if (mover != null)
                    Destroy(mover);

                obstacle.transform.position = new Vector3(laneX, obstacleGroundY, rowZ);
            }

            arrangedObstacleIds.Add(obstacleId);
            nextObstacleIndex++;
        }
    }

    void GetObstaclePlacement(int obstacleIndex, out float rowZ, out int laneIndex, out bool useMover)
    {
        int[][] rowPatterns = SelectedStageIndex == 0 ? obstacleRowsLevelOne : obstacleRowsLevelTwo;
        float rowSpacing = SelectedStageIndex == 0 ? 9.4f : 8.1f;
        if (IsHardcoreMode)
            rowSpacing -= 0.75f;
        float baseZ = Mathf.Max(trackedPlayer.transform.position.z + 18f, 24f);

        int remaining = obstacleIndex;
        int rowIndex = 0;
        int laneSlot = 0;

        while (true)
        {
            int[] pattern = rowPatterns[rowIndex % rowPatterns.Length];
            if (remaining < pattern.Length)
            {
                laneIndex = pattern[remaining];
                laneSlot = remaining;
                break;
            }

            remaining -= pattern.Length;
            rowIndex++;
        }

        rowZ = baseZ + (rowIndex * rowSpacing);
        useMover = SelectedStageIndex == 1 && laneSlot == 0 && rowIndex % 5 == 2 && rowPatterns[rowIndex % rowPatterns.Length].Length == 1;
    }

    void EnsureObstaclePhysicsRig(CollisionDetect obstacle, BoxCollider box)
    {
        if (obstacle == null || box == null)
            return;

        box.enabled = true;
        box.isTrigger = true;

        var body = obstacle.GetComponent<Rigidbody>();
        if (body == null)
            body = obstacle.gameObject.AddComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void ArrangeCoins()
    {
        var coins = FindObjectsOfType<CollectCoin>(true);
        if (coins == null || coins.Length == 0)
            return;
        
        float sequenceSpacing = SelectedStageIndex == 0 ? 4.8f : 6.2f;
        float coinStep = SelectedStageIndex == 0 ? 1.9f : 2.15f;
        int[] lanePattern = SelectedStageIndex == 0 ? lanePatternLevelOne : lanePatternLevelTwo;

        for (int i = 0; i < coins.Length; i++)
        {
            var coin = coins[i];
            if (coin == null)
                continue;

            int coinId = coin.gameObject.GetInstanceID();
            if (arrangedCoinIds.Contains(coinId))
                continue;

            ConfigureCoinCollider(coin);
            ConfigureCoinVisual(coin);

            int clusterIndex = nextCoinIndex / 5;
            int slotIndex = nextCoinIndex % 5;
            int laneIndex = lanePattern[clusterIndex % lanePattern.Length];
            float laneX = trackedPlayer.GetLaneWorldX(laneIndex);
            float z = Mathf.Max(trackedPlayer.transform.position.z + 10f, 12f) + (clusterIndex * (coinStep * 5f + sequenceSpacing)) + (slotIndex * coinStep);
            float jumpOffset = 0f;
            float laneOffset = 0f;

            if (SelectedStageIndex == 0)
            {
                if (clusterIndex % 4 == 1)
                    jumpOffset = 0.45f + Mathf.Sin((slotIndex / 4f) * Mathf.PI) * 0.45f;
                else if (clusterIndex % 4 == 2)
                    laneOffset = Mathf.Lerp(-0.24f, 0.24f, slotIndex / 4f);
            }
            else
            {
                if (clusterIndex % 5 == 2)
                    jumpOffset = 0.58f + Mathf.Sin((slotIndex / 4f) * Mathf.PI) * 0.42f;
                else if (clusterIndex % 5 == 3)
                    laneOffset = Mathf.Lerp(-0.2f, 0.2f, slotIndex / 4f);
            }

            Vector3 position = coin.transform.position;
            position.x = laneX + laneOffset;
            position.z = z;
            position.y = gameplayFloorY + 0.62f + jumpOffset;
            coin.transform.position = position;

            arrangedCoinIds.Add(coinId);
            nextCoinIndex++;
        }
    }

    float GetGroundYAt(float x, float z, float fallbackY)
    {
        Vector3 origin = new Vector3(x, 12f, z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return gameplayFloorY != 0f ? gameplayFloorY : fallbackY;
    }

    float GetObstacleCenterY(BoxCollider box, float groundY)
    {
        if (box == null)
            return groundY + 0.6f;

        return groundY - box.center.y + (box.size.y * 0.5f);
    }

    void SnapDecorToGround(Transform target)
    {
        if (target == null)
            return;

        Vector3 position = target.position;
        float groundY = gameplayFloorY != 0f ? gameplayFloorY : GetGroundYAt(position.x, position.z, position.y);
        position.y = groundY;
        target.position = position;
    }

    void ClampDecorOutsideTrack(Transform target, float protectedEdgeX, float padding)
    {
        if (target == null)
            return;

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 position = target.position;
        if (position.x < 0f)
        {
            float maxAllowedX = -(protectedEdgeX + padding);
            if (bounds.max.x > maxAllowedX)
                position.x -= bounds.max.x - maxAllowedX;
        }
        else
        {
            float minAllowedX = protectedEdgeX + padding;
            if (bounds.min.x < minAllowedX)
                position.x += minAllowedX - bounds.min.x;
        }

        target.position = position;
    }

    void ConfigureCoinCollider(CollectCoin coin)
    {
        if (coin == null)
            return;

        var capsule = coin.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = coin.gameObject.AddComponent<CapsuleCollider>();

        capsule.enabled = true;
        capsule.isTrigger = true;
        capsule.direction = 1;
        capsule.radius = 0.62f;
        capsule.height = 1.3f;
        capsule.center = Vector3.zero;
    }

    void NormalizeCoinRootScale(Transform coinTransform)
    {
        if (coinTransform == null)
            return;

        Vector3 scale = coinTransform.localScale;
        bool flattened = scale.y < 0.32f || scale.x < 0.32f || scale.z < 0.32f;
        bool nonUniform = Mathf.Abs(scale.x - scale.y) > 0.08f || Mathf.Abs(scale.y - scale.z) > 0.08f;
        if (!flattened && !nonUniform)
            return;

        coinTransform.localScale = Vector3.one;
    }

    void ConfigureCoinVisual(CollectCoin coin)
    {
        if (coin == null)
            return;

        NormalizeCoinRootScale(coin.transform);

        var faceMaterial = GetBitcoinCoinFaceMaterial();
        if (faceMaterial == null)
            return;
        var sideMaterial = GetBitcoinCoinSideMaterial();
        var runtimeVisual = EnsureBitcoinCoinVisual(coin.transform, faceMaterial, sideMaterial);
        if (runtimeVisual == null)
            return;

        var renderers = coin.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            // Hide old scene coin mesh, keep only the dedicated BTC visual mesh.
            renderer.enabled = renderer.transform.IsChildOf(runtimeVisual);
        }
    }

    Transform EnsureBitcoinCoinVisual(Transform coinRoot, Material faceMaterial, Material sideMaterial)
    {
        if (coinRoot == null || faceMaterial == null)
            return null;

        var visualRoot = coinRoot.Find("RuntimeBitcoinCoin");
        if (visualRoot == null)
        {
            visualRoot = new GameObject("RuntimeBitcoinCoin").transform;
            visualRoot.SetParent(coinRoot, false);
        }

        visualRoot.localPosition = Vector3.zero;
        // Scene coin roots are authored with X=90, so we compensate here to keep
        // runtime BTC visual upright and readable from the runner camera.
        visualRoot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        visualRoot.localScale = Vector3.one;

        bool hasImportedCoin = EnsureImportedBitcoinCoinVisual(visualRoot);
        if (!hasImportedCoin)
            EnsureFallbackBitcoinCoinVisual(visualRoot);

        foreach (var collider in visualRoot.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        ApplyBitcoinCoinMaterials(visualRoot, faceMaterial, sideMaterial);
        FitBitcoinCoinVisual(coinRoot, visualRoot);
        return visualRoot;
    }

    bool EnsureImportedBitcoinCoinVisual(Transform visualRoot)
    {
        var coinPrefab = GetBitcoinCoinModelPrefab();
        if (coinPrefab == null)
            return false;

        var modelRoot = visualRoot.Find("BitcoinModel");
        if (modelRoot == null || modelRoot.GetComponentInChildren<Renderer>(true) == null)
        {
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
                Destroy(visualRoot.GetChild(i).gameObject);

            var model = Instantiate(coinPrefab, visualRoot);
            model.name = "BitcoinModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;
            modelRoot = model.transform;
        }

        OrientBitcoinModelForRunner(modelRoot);
        return true;
    }

    void EnsureFallbackBitcoinCoinVisual(Transform visualRoot)
    {
        if (visualRoot.Find("BitcoinModel") != null)
            return;

        var side = visualRoot.Find("CoinSide");
        if (side == null)
        {
            side = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            side.name = "CoinSide";
            side.SetParent(visualRoot, false);
        }
        side.localPosition = Vector3.zero;
        side.localRotation = Quaternion.Euler(90f, 0f, 0f);
        side.localScale = new Vector3(0.66f, 0.095f, 0.66f);

        var front = visualRoot.Find("CoinFaceFront");
        if (front == null)
        {
            front = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            front.name = "CoinFaceFront";
            front.SetParent(visualRoot, false);
        }
        front.localPosition = new Vector3(0f, 0f, -0.096f);
        front.localRotation = Quaternion.Euler(0f, 180f, 0f);
        front.localScale = new Vector3(1.27f, 1.27f, 1f);

        var back = visualRoot.Find("CoinFaceBack");
        if (back == null)
        {
            back = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            back.name = "CoinFaceBack";
            back.SetParent(visualRoot, false);
        }
        back.localPosition = new Vector3(0f, 0f, 0.096f);
        back.localRotation = Quaternion.identity;
        back.localScale = new Vector3(1.27f, 1.27f, 1f);
    }

    void OrientBitcoinModelForRunner(Transform modelRoot)
    {
        if (modelRoot == null)
            return;

        var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localPosition = Vector3.zero;
            return;
        }

        modelRoot.localRotation = Quaternion.identity;

        renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        Bounds localBounds = GetLocalRendererBounds(modelRoot, renderers);
        modelRoot.localPosition = -localBounds.center;
    }

    void FitBitcoinCoinVisual(Transform coinRoot, Transform visualRoot)
    {
        if (coinRoot == null || visualRoot == null)
            return;

        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds localBounds = GetLocalRendererBounds(coinRoot, renderers);
        float sourceDiameter = Mathf.Max(localBounds.size.x, Mathf.Max(localBounds.size.y, localBounds.size.z));
        if (sourceDiameter < 0.001f)
            return;

        const float targetDiameter = 0.98f;
        float uniformScale = targetDiameter / sourceDiameter;
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visualRoot.localScale = Vector3.one * uniformScale;

        renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        localBounds = GetLocalRendererBounds(coinRoot, renderers);
        visualRoot.localPosition = -localBounds.center;
    }

    void ApplyBitcoinCoinMaterials(Transform visualRoot, Material faceMaterial, Material sideMaterial)
    {
        if (visualRoot == null || faceMaterial == null)
            return;

        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            bool isSide = renderer.transform.name == "CoinSide";
            Material chosen = isSide && sideMaterial != null ? sideMaterial : faceMaterial;
            int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            var assigned = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
                assigned[i] = chosen;

            renderer.sharedMaterials = assigned;
        }
    }

    IEnumerator RefreshRuntimeVisuals()
    {
        while (SceneManager.GetActiveScene().name == "CasinoRun")
        {
            ArrangeGameplayObjects();
            ReplaceObstacleVisuals();
            UpdateDistanceHud();
            yield return new WaitForSeconds(0.12f);
        }
    }

    void ReplaceObstacleVisuals()
    {
        var prefab = GetArcadeMachinePrefab();
        if (prefab == null)
            return;

        var obstacles = FindObjectsOfType<CollisionDetect>(true);
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null)
                continue;

            var obstacleTransform = obstacle.transform;
            var existingVisual = obstacleTransform.Find("ArcadeMachineVisual");
            if (existingVisual != null)
                continue;

            var box = obstacle.GetComponent<BoxCollider>();
            if (box == null)
                continue;

            var parentRenderer = obstacle.GetComponent<Renderer>();
            if (parentRenderer != null)
                parentRenderer.enabled = false;

            var visual = Instantiate(prefab, obstacleTransform);
            visual.name = "ArcadeMachineVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            visual.transform.localScale = Vector3.one;

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;

            ApplyArcadeMachineRuntimeMaterials(visual);
            FitVisualToObstacle(box, visual.transform);
        }
    }

    void ReplacePlayerVisual()
    {
        var player = FindObjectOfType<PlayerMovement>(true);
        var prefab = GetPlayerCharacterPrefab();
        var materials = GetPlayerCharacterMaterials();
        if (player == null || prefab == null || materials == null || materials.Length == 0)
            return;

        var playerTransform = player.transform;
        var existingVisual = playerTransform.Find("RuntimePlayerVisual");
        if (existingVisual != null)
            Destroy(existingVisual.gameObject);

        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(playerTransform) && renderer.transform.name != "RuntimePlayerVisual")
                renderer.enabled = false;
        }

        var visual = Instantiate(prefab, playerTransform);
        visual.name = "RuntimePlayerVisual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        ApplyImportedPlayerMaterialsOrFallback(visual, materials);
        FitVisualToPlayer(playerTransform, visual.transform);
        AttachPlayerAnimationController(player, visual);
    }

    void FitVisualToObstacle(BoxCollider box, Transform visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visualBounds.Encapsulate(renderers[i].bounds);

        float width = Mathf.Max(visualBounds.size.x, 0.01f);
        float depth = Mathf.Max(visualBounds.size.z, 0.01f);
        float height = Mathf.Max(visualBounds.size.y, 0.01f);
        var targetBounds = box.bounds;
        float targetWidth = Mathf.Max(targetBounds.size.x * 1.08f, 0.55f);
        float targetDepth = Mathf.Max(targetBounds.size.z * 1.34f, 0.82f);
        float targetHeight = Mathf.Max(targetBounds.size.y * 1.22f, 1.55f);
        float uniformScale = Mathf.Min(targetWidth / width, targetDepth / depth, targetHeight / height);
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visual.localScale = Vector3.one * uniformScale;

        renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds localBounds = GetLocalRendererBounds(box.transform, renderers);
        visual.localPosition = new Vector3(
            -localBounds.center.x,
            -localBounds.min.y - 0.02f,
            -(localBounds.center.z * 0.08f));
    }

    void ApplyArcadeMachineRuntimeMaterials(GameObject visualRoot)
    {
        if (visualRoot == null)
            return;

        var bodyMaterial = GetArcadeMachineBodyMaterial();
        var marqueeMaterial = GetArcadeMachineMarqueeMaterial();
        if (bodyMaterial == null && marqueeMaterial == null)
            return;

        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            string lowered = renderer.transform.name.ToLowerInvariant();
            bool likelyScreenPart = lowered.Contains("screen") ||
                                    lowered.Contains("display") ||
                                    lowered.Contains("marquee") ||
                                    lowered.Contains("sign") ||
                                    lowered.Contains("monitor");

            int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            var assignedMaterials = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                bool useMarquee = likelyScreenPart || (slotCount > 1 && i == 1);
                assignedMaterials[i] = useMarquee
                    ? marqueeMaterial != null ? marqueeMaterial : bodyMaterial
                    : bodyMaterial != null ? bodyMaterial : marqueeMaterial;
            }

            renderer.sharedMaterials = assignedMaterials;
        }
    }

    Bounds GetLocalRendererBounds(Transform root, Renderer[] renderers)
    {
        Vector3 rootLocal = root.InverseTransformPoint(renderers[0].bounds.center);
        Bounds bounds = new Bounds(rootLocal, Vector3.zero);

        foreach (var renderer in renderers)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 extents = worldBounds.extents;
            Vector3 center = worldBounds.center;

            Vector3[] corners =
            {
                center + new Vector3( extents.x,  extents.y,  extents.z),
                center + new Vector3( extents.x,  extents.y, -extents.z),
                center + new Vector3( extents.x, -extents.y,  extents.z),
                center + new Vector3( extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x, -extents.y, -extents.z)
            };

            for (int i = 0; i < corners.Length; i++)
                bounds.Encapsulate(root.InverseTransformPoint(corners[i]));
        }

        return bounds;
    }

    void FitVisualToPlayer(Transform playerRoot, Transform visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        var capsule = playerRoot.GetComponent<CapsuleCollider>();
        Bounds visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visualBounds.Encapsulate(renderers[i].bounds);

        float targetHeight = capsule != null ? Mathf.Max(capsule.height * 1.55f, 1f) : 1.8f;
        float targetWidth = capsule != null ? Mathf.Max(capsule.radius * 1.9f, 0.6f) : 0.8f;
        float sourceHeight = Mathf.Max(visualBounds.size.y, 0.01f);
        float sourceWidth = Mathf.Max(Mathf.Max(visualBounds.size.x, visualBounds.size.z), 0.01f);
        float uniformScale = Mathf.Min(targetHeight / sourceHeight, targetWidth / sourceWidth);
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visual.localScale = Vector3.one * uniformScale;

        renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds localBounds = GetLocalRendererBounds(playerRoot, renderers);
        visual.localPosition = new Vector3(
            -localBounds.center.x,
            -localBounds.min.y + 0.035f,
            -localBounds.center.z);
    }

    void ApplyMaterials(GameObject visualRoot, Material[] materials)
    {
        if (materials == null || materials.Length == 0)
            return;

        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            var assignedMaterials = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
                assignedMaterials[i] = materials[i % materials.Length];

            renderer.sharedMaterials = assignedMaterials;
        }
    }

    void ApplyImportedPlayerMaterialsOrFallback(GameObject visualRoot, Material[] fallbackMaterials)
    {
        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool hasUsableImportedMaterials = false;
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                if (material.mainTexture != null || material.HasProperty("_BaseMap") || material.HasProperty("_MainTex"))
                {
                    hasUsableImportedMaterials = true;
                    break;
                }
            }

            if (hasUsableImportedMaterials)
                break;
        }

        if (!hasUsableImportedMaterials)
            ApplyMaterials(visualRoot, fallbackMaterials);
    }

    void AttachPlayerAnimationController(PlayerMovement player, GameObject visual)
    {
        var clips = GetPlayerCharacterClips();
        if (clips == null || clips.Length == 0)
            return;

        var animator = visual.GetComponent<RuntimePlayerVisualAnimator>();
        if (animator == null)
            animator = visual.AddComponent<RuntimePlayerVisualAnimator>();

        animator.Initialize(player.transform, clips);
    }

    public bool TryHandleRevive(CollisionDetect source, Collider obstacle)
    {
        if (reviveUsed || reviveRunning || reviveGroup == null)
            return false;

        StartCoroutine(ReviveRoutine(source, obstacle));
        return true;
    }

    IEnumerator ReviveRoutine(CollisionDetect source, Collider obstacle)
    {
        reviveRunning = true;
        reviveGroup.alpha = 1f;
        reviveGroup.transform.SetAsLastSibling();
        reviveTitle.text = "Lucky Spin";
        reviveSubtitle.text = "Match 3 hearts to stay in the run";

        string[] finalSymbols = new string[3];
        bool win = Random.value <= 0.42f;
        for (int i = 0; i < finalSymbols.Length; i++)
            finalSymbols[i] = win ? "H" : symbolPool[Random.Range(0, symbolPool.Length)];

        if (!win)
            finalSymbols[Random.Range(0, finalSymbols.Length)] = "H";

        int heartCount = 0;
        for (int i = 0; i < finalSymbols.Length; i++)
        {
            if (finalSymbols[i] == "H")
                heartCount++;
        }
        win = heartCount == finalSymbols.Length;

        for (int step = 0; step < 16; step++)
        {
            for (int i = 0; i < slotTexts.Length; i++)
            {
                slotTexts[i].text = symbolPool[Random.Range(0, symbolPool.Length)];
                slotTexts[i].rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(34f, -34f, (step % 4) / 3f));
            }

            yield return new WaitForSecondsRealtime(0.06f + step * 0.01f);
        }

        for (int i = 0; i < slotTexts.Length; i++)
        {
            slotTexts[i].text = finalSymbols[i];
            slotTexts[i].rectTransform.anchoredPosition = Vector2.zero;
        }

        reviveTitle.text = win ? "Lucky Save" : "No Luck";
        reviveSubtitle.text = win ? "Three hearts. You get one more shot." : "The machine keeps the pot.";

        yield return new WaitForSecondsRealtime(0.9f);

        reviveGroup.alpha = 0f;
        reviveUsed = true;
        reviveRunning = false;

        if (win)
            source.ReviveAfterSlot(obstacle);
        else
            source.BeginDeathSequence();
    }

    static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null)
            return roundedSprite;

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
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
        return roundedSprite;
    }

    static TMP_FontAsset GetTitleFont()
    {
        if (titleFontAsset != null)
            return titleFontAsset;

        titleFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
        if (titleFontAsset == null)
            titleFontAsset = TMP_Settings.defaultFontAsset;
        return titleFontAsset;
    }

    static TMP_FontAsset GetBodyFont()
    {
        if (bodyFontAsset != null)
            return bodyFontAsset;

        bodyFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        if (bodyFontAsset == null)
            bodyFontAsset = TMP_Settings.defaultFontAsset;
        return bodyFontAsset;
    }

    static GameObject GetArcadeMachinePrefab()
    {
        if (arcadeMachinePrefab != null)
            return arcadeMachinePrefab;

        arcadeMachinePrefab = Resources.Load<GameObject>("ArcadeMachine");
        return arcadeMachinePrefab;
    }

    static GameObject GetPlayerCharacterPrefab()
    {
        if (playerCharacterPrefab != null)
            return playerCharacterPrefab;

        playerCharacterPrefab = Resources.Load<GameObject>("PlayerCharacter");
        return playerCharacterPrefab;
    }

    static GameObject GetBitcoinCoinModelPrefab()
    {
        if (bitcoinCoinModelPrefab != null)
            return bitcoinCoinModelPrefab;

        bitcoinCoinModelPrefab = Resources.Load<GameObject>("BitcoinModel/BitcoinCoin");
        return bitcoinCoinModelPrefab;
    }

    static Material[] GetPlayerCharacterMaterials()
    {
        if (playerCharacterMaterials != null)
            return playerCharacterMaterials;

        var albedo = Resources.Load<Texture2D>("PlayerCharacterTextures/PlayerCharacter_Albedo");
        if (albedo == null)
            albedo = Resources.Load<Texture2D>("PlayerCharacterTextures/PlayerCharacter_Body");
        if (albedo == null)
            return null;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = "Runtime_PlayerCharacter";
        material.mainTexture = albedo;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", albedo);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.12f);

        playerCharacterMaterials = new[] { material };
        return playerCharacterMaterials;
    }

    static Material GetArcadeMachineBodyMaterial()
    {
        if (arcadeMachineBodyMaterial != null)
            return arcadeMachineBodyMaterial;

        var texture = Resources.Load<Texture2D>("ArcadeMachineTextures/ArcadeCabinetBody");
        if (texture == null)
            texture = Resources.Load<Texture2D>("ArcadeMachineTextures/Screenshot_20220309_224546");
        if (texture == null)
            texture = Resources.Load<Texture2D>("ArcadeMachineTextures/mat0");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = "Runtime_ArcadeCabinet_Body";

        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            material.mainTexture = texture;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.2f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.28f);

        arcadeMachineBodyMaterial = material;
        return arcadeMachineBodyMaterial;
    }

    static Material GetArcadeMachineMarqueeMaterial()
    {
        if (arcadeMachineMarqueeMaterial != null)
            return arcadeMachineMarqueeMaterial;

        var texture = Resources.Load<Texture2D>("ArcadeMachineTextures/ArcadeCabinetMarquee");
        if (texture == null)
            texture = Resources.Load<Texture2D>("ArcadeMachineTextures/street-fighter-II_marquee");
        if (texture == null)
            texture = Resources.Load<Texture2D>("ArcadeMachineTextures/mat0.001");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = "Runtime_ArcadeCabinet_Marquee";

        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            material.mainTexture = texture;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.05f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.4f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.22f, 0.22f, 0.22f, 1f));
        }

        arcadeMachineMarqueeMaterial = material;
        return arcadeMachineMarqueeMaterial;
    }

    static Material GetBitcoinCoinFaceMaterial()
    {
        if (bitcoinCoinFaceMaterial != null)
            return bitcoinCoinFaceMaterial;

        var albedo = Resources.Load<Texture2D>("BitcoinTextures/bitcoin_albedo");
        var normal = Resources.Load<Texture2D>("BitcoinTextures/bitcoin_normals");
        var metallic = Resources.Load<Texture2D>("BitcoinTextures/bitcoin_metalic");
        if (metallic == null)
            metallic = Resources.Load<Texture2D>("BitcoinTextures/bitcoin_metalic1");
        var height = Resources.Load<Texture2D>("BitcoinTextures/bitcoin_heights");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = "Runtime_BitcoinCoinFace";

        if (albedo != null && material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", albedo);
        if (albedo != null && material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", albedo);
        material.mainTexture = albedo;
        material.mainTextureScale = Vector2.one;
        material.mainTextureOffset = Vector2.zero;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(1f, 0.96f, 0.84f, 1f));
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(1f, 0.96f, 0.84f, 1f));

        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_BumpScale"))
                material.SetFloat("_BumpScale", 1.15f);
            material.EnableKeyword("_NORMALMAP");
        }

        if (metallic != null && material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", metallic);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (height != null && material.HasProperty("_ParallaxMap"))
        {
            material.SetTexture("_ParallaxMap", height);
            if (material.HasProperty("_Parallax"))
                material.SetFloat("_Parallax", 0.016f);
            material.EnableKeyword("_PARALLAXMAP");
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.96f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.84f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.21f, 0.15f, 0.03f, 1f));
        }

        bitcoinCoinFaceMaterial = material;
        return bitcoinCoinFaceMaterial;
    }

    static Material GetBitcoinCoinSideMaterial()
    {
        if (bitcoinCoinSideMaterial != null)
            return bitcoinCoinSideMaterial;

        var metallic = Resources.Load<Texture2D>("BitcoinTextures/bitcoin_metalic1");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = "Runtime_BitcoinCoinSide";
        Color edgeColor = new Color(0.91f, 0.64f, 0.16f, 1f);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", edgeColor * 1.03f);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", edgeColor * 1.03f);

        if (metallic != null && material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", metallic);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.95f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.78f);

        bitcoinCoinSideMaterial = material;
        return bitcoinCoinSideMaterial;
    }

    static AnimationClip[] GetPlayerCharacterClips()
    {
        if (playerCharacterClips != null)
            return playerCharacterClips;

        var loadedClips = Resources.LoadAll<AnimationClip>("PlayerCharacter");
        if (loadedClips == null || loadedClips.Length == 0)
            return null;

        var filteredClips = new List<AnimationClip>();
        foreach (var clip in loadedClips)
        {
            if (clip == null)
                continue;

            string lowered = clip.name.ToLowerInvariant();
            if (lowered.Contains("__preview__"))
                continue;

            filteredClips.Add(clip);
        }

        playerCharacterClips = filteredClips.ToArray();
        return playerCharacterClips;
    }
}
