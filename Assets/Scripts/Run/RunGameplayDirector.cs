using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(900)]
public sealed class RunGameplayDirector : MonoBehaviour
{
    public static RunGameplayDirector Instance { get; private set; }

    public static int SelectedStageIndex { get; private set; }

    static Sprite roundedSprite;
    static GameObject arcadeMachinePrefab;
    static GameObject playerCharacterPrefab;
    static Material[] playerCharacterMaterials;
    static AnimationClip[] playerCharacterClips;

    readonly string[] symbolPool = { "7", "$", "H", "H", "BAR", "H", "X" };

    Canvas canvas;
    TMP_Text coinLabel;
    RectTransform coinPanel;
    CanvasGroup reviveGroup;
    TMP_Text reviveTitle;
    TMP_Text reviveSubtitle;
    TMP_Text[] slotTexts;
    bool reviveUsed;
    bool reviveRunning;

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

        MasterLevelInfo.ResetCoins();
        reviveUsed = false;
        reviveRunning = false;

        BuildHud();
        BuildReviveUi();
        StyleInGameUi();
        ConfigureStageVariant();
        ReplaceObstacleVisuals();
        ReplacePlayerVisual();
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
        coinPanel.anchoredPosition = new Vector2(0f, -26f);
        coinPanel.sizeDelta = new Vector2(240f, 56f);

        var panelImage = root.GetComponent<Image>();
        panelImage.sprite = GetRoundedSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.05f, 0.09f, 0.14f, 0.88f);
        root.transform.SetAsLastSibling();

        var outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.26f, 0.9f, 0.96f, 0.18f);
        outline.effectDistance = new Vector2(1f, -1f);

        var icon = new GameObject("CoinIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
        icon.transform.SetParent(root.transform, false);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(18f, 0f);
        iconRect.sizeDelta = new Vector2(34f, 34f);
        var iconText = icon.GetComponent<TextMeshProUGUI>();
        iconText.text = "$";
        iconText.fontSize = 26f;
        iconText.color = new Color(1f, 0.83f, 0.39f, 1f);
        iconText.alignment = TextAlignmentOptions.Center;

        var label = new GameObject("CoinLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(14f, 0f);
        labelRect.sizeDelta = new Vector2(170f, 36f);
        coinLabel = label.GetComponent<TextMeshProUGUI>();
        coinLabel.font = TMP_Settings.defaultFontAsset;
        coinLabel.fontSize = 24f;
        coinLabel.enableAutoSizing = true;
        coinLabel.fontSizeMin = 18f;
        coinLabel.fontSizeMax = 24f;
        coinLabel.color = Color.white;
        coinLabel.alignment = TextAlignmentOptions.Center;

        UpdateCoinHud(MasterLevelInfo.CoinCount);
        MasterLevelInfo.CoinCountChanged -= UpdateCoinHud;
        MasterLevelInfo.CoinCountChanged += UpdateCoinHud;
    }

    void UpdateCoinHud(int coinCount)
    {
        if (coinLabel == null || coinPanel == null)
            return;

        coinLabel.text = $"Coins {coinCount:00}";
        float width = coinCount >= 100 ? 300f : coinCount >= 10 ? 270f : 240f;
        coinPanel.sizeDelta = new Vector2(width, 56f);
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
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.65f;
        label.fontSizeMax = size;
        return label;
    }

    void StyleInGameUi()
    {
        var fadeOut = GameObject.Find("FadeOut");
        if (fadeOut != null)
            fadeOut.transform.SetAsLastSibling();
    }

    void ConfigureStageVariant()
    {
        var player = FindObjectOfType<PlayerMovement>(true);
        if (player != null)
        {
            player.playerSpeed = SelectedStageIndex == 0 ? 7.5f : 8.3f;
            player.horizontalSpeed = SelectedStageIndex == 0 ? 7f : 7.8f;
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
                Destroy(existingVisual.gameObject);

            var parentRenderer = obstacle.GetComponent<Renderer>();
            if (parentRenderer != null)
                parentRenderer.enabled = false;

            var box = obstacle.GetComponent<BoxCollider>();
            if (box == null)
                continue;

            var visual = Instantiate(prefab, obstacleTransform);
            visual.name = "ArcadeMachineVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            visual.transform.localScale = Vector3.one;

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;

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
        var targetBounds = box.bounds;
        float targetWidth = Mathf.Max(targetBounds.size.x * 0.9f, 0.4f);
        float targetDepth = Mathf.Max(targetBounds.size.z * 1.55f, 0.65f);
        float uniformScale = Mathf.Min(targetWidth / width, targetDepth / depth);
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visual.localScale = Vector3.one * uniformScale;

        renderers = visual.GetComponentsInChildren<Renderer>(true);
        visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visualBounds.Encapsulate(renderers[i].bounds);

        Vector3 desiredPosition = new Vector3(
            targetBounds.center.x,
            targetBounds.min.y - visualBounds.min.y + visual.position.y,
            targetBounds.center.z - (targetBounds.extents.z * 0.08f));

        visual.position = new Vector3(desiredPosition.x, desiredPosition.y, desiredPosition.z);
    }

    void FitVisualToPlayer(Transform playerRoot, Transform visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visualBounds.Encapsulate(renderers[i].bounds);

        var capsule = playerRoot.GetComponent<CapsuleCollider>();
        float targetHeight = capsule != null ? Mathf.Max(capsule.height * 1.55f, 1f) : 1.8f;
        float targetWidth = capsule != null ? Mathf.Max(capsule.radius * 1.9f, 0.6f) : 0.8f;

        float sourceHeight = Mathf.Max(visualBounds.size.y, 0.01f);
        float sourceWidth = Mathf.Max(Mathf.Max(visualBounds.size.x, visualBounds.size.z), 0.01f);
        float uniformScale = Mathf.Min(targetHeight / sourceHeight, targetWidth / sourceWidth);
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visual.localScale = Vector3.one * uniformScale;

        renderers = visual.GetComponentsInChildren<Renderer>(true);
        visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visualBounds.Encapsulate(renderers[i].bounds);

        float groundY = playerRoot.position.y;
        float centerX = playerRoot.position.x;
        float centerZ = playerRoot.position.z;
        visual.position = new Vector3(
            centerX,
            groundY - visualBounds.min.y + 0.02f,
            centerZ);
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
