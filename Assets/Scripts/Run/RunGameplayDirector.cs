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
    const int MaxRuntimeLightCount = 22;
    static readonly bool ForceSafeRunBaseline = true;
    static readonly bool EnableSafeRunLaneDressing = true;
    static readonly bool EnableSafeRunCharacterVisual = true;
    static readonly bool EnableSafeRunSceneMood = true;
    static readonly bool EnableSafeRunArcadeObstacles = true;
    static readonly bool EnableSafeRunRopeObstacles = false;
    static readonly bool EnableLuckySpinRevive = true;

    public static RunGameplayDirector Instance { get; private set; }

    public static int SelectedStageIndex { get; private set; }
    public static bool IsHardcoreMode { get; private set; }

    static Sprite roundedSprite;
    static GameObject arcadeMachinePrefab;
    static GameObject buildingOnePrefab;
    static GameObject buildingTwoPrefab;
    static GameObject neonSign24hPrefab;
    static GameObject neonCardsPrefab;
    static GameObject playerCharacterPrefab;
    static GameObject bitcoinCoinModelPrefab;
    static GameObject ropeBarrierPrefab;
    static Avatar playerCharacterAvatar;
    static Material[] playerCharacterMaterials;
    static Material playerCharacterBodyMaterial;
    static Material arcadeMachineBodyMaterial;
    static Material arcadeMachineMarqueeMaterial;
    static Material bitcoinCoinFaceMaterial;
    static Material bitcoinCoinSideMaterial;
    static Material cityBuildingBodyMaterial;
    static Material cityBuildingAccentCoolMaterial;
    static Material cityBuildingAccentWarmMaterial;
    static Material neonSignFallbackMaterial;
    static Material neonCardsFallbackMaterial;
    static Material ropeBarrierMaterial;
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
    CanvasGroup runSummaryGroup;
    TMP_Text runSummaryBadge;
    TMP_Text runSummaryTitle;
    TMP_Text runSummaryPrimaryValue;
    TMP_Text runSummaryPrimaryLabel;
    TMP_Text runSummarySecondaryValue;
    TMP_Text runSummarySecondaryLabel;
    TMP_Text runSummaryRecordLabel;
    TMP_Text runSummaryFooter;
    bool reviveUsed;
    bool reviveRunning;
    Coroutine refreshRoutine;
    PlayerMovement trackedPlayer;
    float runStartZ;
    float gameplayFloorY;
    int runtimeLightCount;
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
        TryApplyToCurrentScene();
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
            TryApplyToCurrentScene();
            return;
        }

        if (canvas != null)
            canvas = null;
    }

    void TryApplyToCurrentScene()
    {
        if (SceneManager.GetActiveScene().name != "CasinoRun")
            return;

        if (FindObjectOfType<PlayerMovement>(true) != null)
        {
            ApplyToCurrentScene();
            return;
        }

        StartCoroutine(ApplyWhenReady());
    }

    IEnumerator ApplyWhenReady()
    {
        for (int i = 0; i < 12; i++)
        {
            if (SceneManager.GetActiveScene().name != "CasinoRun")
                yield break;

            if (FindObjectOfType<PlayerMovement>(true) != null)
            {
                ApplyToCurrentScene();
                yield break;
            }

            yield return null;
        }

        ApplyToCurrentScene();
    }

    void ApplyToCurrentScene()
    {
        if (SceneManager.GetActiveScene().name != "CasinoRun")
            return;

        bool sceneAuthoringMode = IsSceneAuthoringModeEnabled();
        bool useSafeRunBaseline = !sceneAuthoringMode && ForceSafeRunBaseline;

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
        if (useSafeRunBaseline)
        {
            RestoreOriginalCameraRig();
            if (trackedPlayer != null)
                trackedPlayer.ClearRuntimeGroundPlane();
        }
        else
        {
            ConfigureRuntimeCameraRig();
            if (trackedPlayer != null)
            {
                trackedPlayer.SetRuntimeGroundPlane(gameplayFloorY);
                Vector3 startPosition = trackedPlayer.transform.position;
                startPosition.y = gameplayFloorY;
                trackedPlayer.transform.position = startPosition;
            }
        }

        BuildHud();
        BuildReviveUi();
        StyleInGameUi();
        ConfigureStageVariant();
        runtimeLightCount = 0;
        if (sceneAuthoringMode)
        {
            CleanupRuntimeGeneratedObjects();
            RestoreOriginalPlayerVisual();
            RestoreOriginalObstacleVisuals();
            RestoreOriginalScenePresentation();
            SetLegacyStreetMeshesVisible(true);
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }
        }
        else if (useSafeRunBaseline)
        {
            CleanupRuntimeGeneratedObjects();
            RestoreOriginalObstacleVisuals();
            RestoreOriginalScenePresentation();
            SetLegacyStreetMeshesVisible(true);
            if (EnableSafeRunSceneMood)
                ApplySceneMood();
            RemoveLegacySceneBackdrop();
            if (EnableSafeRunArcadeObstacles)
            {
                ActivateSafeRunObstacles();
                EnsureRuntimeObstaclePool();
                ArrangeObstacles();
                ReplaceObstacleVisuals();
            }
            else
            {
                DeactivateSafeRunObstacles();
            }
            if (EnableSafeRunLaneDressing)
            {
                SetLegacyStreetMeshesVisible(false);
                BuildSafeLaneDressing();
            }
            ArrangeCoins();
            if (EnableSafeRunCharacterVisual)
                ReplacePlayerVisual();
            else
                RestoreOriginalPlayerVisual();
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }

            refreshRoutine = StartCoroutine(RefreshSafeRunVisuals());
        }
        else
        {
            SetLegacyStreetMeshesVisible(true);
            ApplySceneMood();
            ApplyRuntimePresentation();
            RemoveLegacySceneBackdrop();
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

    void RestoreOriginalObstacleVisuals()
    {
        var obstacles = FindObjectsOfType<CollisionDetect>(true);
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null)
                continue;

            var obstacleTransform = obstacle.transform;
            var arcadeVisual = obstacleTransform.Find("ArcadeMachineVisual");
            if (arcadeVisual != null)
                Destroy(arcadeVisual.gameObject);

            var ropeVisual = obstacleTransform.Find("RopeBarrierVisual");
            if (ropeVisual != null)
                Destroy(ropeVisual.gameObject);

            var mover = obstacle.GetComponent<LaneObstacleMover>();
            if (mover != null)
                Destroy(mover);

            var parentRenderer = obstacle.GetComponent<Renderer>();
            if (parentRenderer != null)
                parentRenderer.enabled = true;
        }
    }

    void DeactivateSafeRunObstacles()
    {
        var obstacles = FindObjectsOfType<CollisionDetect>(true);
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null)
                continue;

            if (trackedPlayer != null && obstacle.transform.IsChildOf(trackedPlayer.transform))
                continue;

            obstacle.gameObject.SetActive(false);
        }
    }

    void ActivateSafeRunObstacles()
    {
        var obstacles = FindObjectsOfType<CollisionDetect>(true);
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null)
                continue;

            if (trackedPlayer != null && obstacle.transform.IsChildOf(trackedPlayer.transform))
                continue;

            obstacle.gameObject.SetActive(true);
        }
    }

    void RestoreOriginalPlayerVisual()
    {
        var player = FindObjectOfType<PlayerMovement>(true);
        if (player == null)
            return;

        Transform playerTransform = player.transform;
        var runtimeVisual = playerTransform.Find("RuntimePlayerVisual");
        if (runtimeVisual != null)
            Destroy(runtimeVisual.gameObject);

        var runtimeFillLight = playerTransform.Find("RuntimePlayerFillLight");
        if (runtimeFillLight != null)
            Destroy(runtimeFillLight.gameObject);

        foreach (var renderer in playerTransform.GetComponentsInChildren<Renderer>(true))
        {
            if (runtimeVisual != null && renderer.transform.IsChildOf(runtimeVisual))
                continue;

            renderer.enabled = true;
        }
    }

    void SetLegacyStreetMeshesVisible(bool visible)
    {
        foreach (var renderer in FindObjectsOfType<MeshRenderer>(true))
        {
            if (renderer == null || renderer.transform == null)
                continue;

            if (!string.Equals(renderer.transform.name, "street", System.StringComparison.OrdinalIgnoreCase))
                continue;

            renderer.enabled = visible;
        }
    }

    void BuildSafeLaneDressing()
    {
        var existing = GameObject.Find("RuntimeStageDressing");
        if (existing != null)
            Destroy(existing);

        var root = new GameObject("RuntimeStageDressing").transform;
        float startZ = trackedPlayer != null ? trackedPlayer.transform.position.z - 80f : -80f;
        float length = 1400f;
        float centerY = gameplayFloorY + 0.001f;
        float ceilingY = centerY + 4.24f;
        float laneStripThickness = 0.012f;
        float lowerWallHeight = 1.42f;
        float lowerWallCenterY = centerY + 0.66f;
        float upperWallHeight = 0.48f;
        float upperWallCenterY = ceilingY - 0.24f;
        float borderHeight = ceilingY - centerY + 0.08f;
        float borderCenterY = centerY + (borderHeight * 0.5f) - 0.02f;
        float groundY = gameplayFloorY;

        CreateStrip(root, "FloorBase", new Vector3(0f, centerY - 0.02f, startZ + (length * 0.5f)), new Vector3(9.6f, 0.018f, length), new Color(0.06f, 0.09f, 0.14f, 1f));
        CreateStrip(root, "LaneGlowLeft", new Vector3(-3.5f, centerY, startZ + (length * 0.5f)), new Vector3(0.16f, laneStripThickness, length), new Color(0.17f, 0.75f, 1f, 0.95f), true);
        CreateStrip(root, "LaneGlowCenter", new Vector3(0f, centerY, startZ + (length * 0.5f)), new Vector3(0.18f, laneStripThickness, length), new Color(1f, 0.78f, 0.32f, 0.95f), true);
        CreateStrip(root, "LaneGlowRight", new Vector3(3.5f, centerY, startZ + (length * 0.5f)), new Vector3(0.16f, laneStripThickness, length), new Color(0.17f, 0.75f, 1f, 0.95f), true);
        CreateStrip(root, "TrackBorderLeft", new Vector3(-7.72f, borderCenterY, startZ + (length * 0.5f)), new Vector3(0.28f, borderHeight, length), new Color(0.05f, 0.08f, 0.14f, 1f));
        CreateStrip(root, "TrackBorderRight", new Vector3(7.72f, borderCenterY, startZ + (length * 0.5f)), new Vector3(0.28f, borderHeight, length), new Color(0.05f, 0.08f, 0.14f, 1f));
        CreateStrip(root, "SideWallBaseLeft", new Vector3(-8.82f, lowerWallCenterY, startZ + (length * 0.5f)), new Vector3(1.95f, lowerWallHeight, length), new Color(0.06f, 0.1f, 0.16f, 1f));
        CreateStrip(root, "SideWallBaseRight", new Vector3(8.82f, lowerWallCenterY, startZ + (length * 0.5f)), new Vector3(1.95f, lowerWallHeight, length), new Color(0.06f, 0.1f, 0.16f, 1f));
        CreateStrip(root, "SideCanopyLeft", new Vector3(-8.86f, upperWallCenterY, startZ + (length * 0.5f)), new Vector3(1.62f, upperWallHeight, length), new Color(0.05f, 0.09f, 0.15f, 1f));
        CreateStrip(root, "SideCanopyRight", new Vector3(8.86f, upperWallCenterY, startZ + (length * 0.5f)), new Vector3(1.62f, upperWallHeight, length), new Color(0.05f, 0.09f, 0.15f, 1f));
        CreateStrip(root, "WindowRailLeftLower", new Vector3(-7.66f, centerY + 1.46f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(0.12f, 0.58f, 0.96f, 0.72f), true);
        CreateStrip(root, "WindowRailRightLower", new Vector3(7.66f, centerY + 1.46f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(0.12f, 0.58f, 0.96f, 0.72f), true);
        CreateStrip(root, "WindowRailLeftUpper", new Vector3(-7.66f, centerY + 3.08f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(1f, 0.68f, 0.32f, 0.72f), true);
        CreateStrip(root, "WindowRailRightUpper", new Vector3(7.66f, centerY + 3.08f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(1f, 0.68f, 0.32f, 0.72f), true);
        CreateStrip(root, "CeilingPanel", new Vector3(0f, ceilingY, startZ + (length * 0.5f)), new Vector3(15.2f, 0.06f, length), new Color(0.07f, 0.11f, 0.18f, 1f));
        CreateSafeCeilingLightStrips(root, startZ, ceilingY, length);
        CreateSidePromenades(root, startZ, groundY, length);
        CreateSideNeonRails(root, startZ, centerY, ceilingY, length);
        CreateSafeLaneLedPillars(root, startZ, centerY, ceilingY, length);
        CreateSafeNeonCasinoEdgeEnvironment(root, startZ, groundY, length);

        for (int i = 0; i < 24; i++)
        {
            float z = startZ + 18f + (i * 30f);
            Color pulseColor = i % 2 == 0 ? new Color(0.16f, 0.7f, 1f, 0.92f) : new Color(1f, 0.7f, 0.3f, 0.92f);
            CreateStrip(root, $"CrossBeam_{i}", new Vector3(0f, centerY - 0.01f, z), new Vector3(7.8f, 0.009f, 0.55f), pulseColor, true);
        }
    }

    void CreateSafeCeilingLightStrips(Transform root, float startZ, float ceilingY, float length)
    {
        float zCenter = startZ + (length * 0.5f);
        float stripY = ceilingY - 0.035f;
        Color coolStrip = new Color(0.18f, 0.8f, 1f, 0.94f);
        Color warmStrip = new Color(1f, 0.7f, 0.32f, 0.92f);

        CreateStrip(root, "CeilingStripLeftOuter", new Vector3(-5.4f, stripY, zCenter), new Vector3(0.12f, 0.04f, length), coolStrip * 0.9f, true);
        CreateStrip(root, "CeilingStripLeftInner", new Vector3(-2.1f, stripY, zCenter), new Vector3(0.16f, 0.045f, length), warmStrip * 0.95f, true);
        CreateStrip(root, "CeilingStripCenter", new Vector3(0f, stripY, zCenter), new Vector3(0.2f, 0.05f, length), new Color(1f, 0.78f, 0.38f, 0.96f), true);
        CreateStrip(root, "CeilingStripRightInner", new Vector3(2.1f, stripY, zCenter), new Vector3(0.16f, 0.045f, length), warmStrip * 0.95f, true);
        CreateStrip(root, "CeilingStripRightOuter", new Vector3(5.4f, stripY, zCenter), new Vector3(0.12f, 0.04f, length), coolStrip * 0.9f, true);

        int lightCount = Mathf.CeilToInt(length / 190f);
        float firstLightZ = startZ + 70f;
        for (int i = 0; i < lightCount; i++)
        {
            float z = firstLightZ + (i * 190f);
            Color lightColor = i % 2 == 0
                ? Color.Lerp(coolStrip, Color.white, 0.16f)
                : Color.Lerp(warmStrip, Color.white, 0.14f);
            CreateLight(root, $"CeilingRunLight_{i}", new Vector3(0f, ceilingY - 0.24f, z), lightColor, 14.2f, 3.1f);
        }
    }

    void CreateSafeLaneLedPillars(Transform root, float startZ, float centerY, float ceilingY, float length)
    {
        int pillarCount = Mathf.CeilToInt(length / 52f) + 1;
        float firstZ = startZ + 18f;

        for (int i = 0; i < pillarCount; i++)
        {
            float z = firstZ + (i * 52f);
            Color accent = (i + SelectedStageIndex) % 2 == 0
                ? new Color(0.2f, 0.78f, 1f, 0.96f)
                : new Color(1f, 0.58f, 0.24f, 0.96f);

            CreateStrip(root, $"PillarShellLeft_{i}", new Vector3(-7.94f, centerY + 2.02f, z), new Vector3(0.24f, 3.96f, 0.42f), new Color(0.04f, 0.09f, 0.15f, 1f));
            CreateStrip(root, $"PillarShellRight_{i}", new Vector3(7.94f, centerY + 2.02f, z), new Vector3(0.24f, 3.96f, 0.42f), new Color(0.04f, 0.09f, 0.15f, 1f));
            CreateStrip(root, $"PillarLedLeft_{i}", new Vector3(-7.82f, centerY + 2.02f, z), new Vector3(0.045f, 3.54f, 0.11f), accent, true);
            CreateStrip(root, $"PillarLedRight_{i}", new Vector3(7.82f, centerY + 2.02f, z), new Vector3(0.045f, 3.54f, 0.11f), accent, true);
            CreateStrip(root, $"PillarCapLeft_{i}", new Vector3(-7.86f, ceilingY - 0.28f, z), new Vector3(0.3f, 0.06f, 0.5f), accent * 0.84f, true);
            CreateStrip(root, $"PillarCapRight_{i}", new Vector3(7.86f, ceilingY - 0.28f, z), new Vector3(0.3f, 0.06f, 0.5f), accent * 0.84f, true);
        }
    }

    void CreateSafeNeonCasinoEdgeEnvironment(Transform root, float startZ, float groundY, float length)
    {
        var buildingOne = GetBuildingOnePrefab();
        var signPrefab = GetNeonSign24hPrefab();
        var cardsPrefab = GetNeonCardsPrefab();
        if (buildingOne == null && signPrefab == null && cardsPrefab == null)
            return;

        int districtCount = Mathf.CeilToInt(length / 136f) + 1;
        float firstZ = startZ + 30f;

        for (int i = 0; i < districtCount; i++)
        {
            float districtZ = firstZ + (i * 136f);
            bool coolAccent = (i + SelectedStageIndex) % 2 == 0;
            bool leftSideLead = i % 2 == 0;
            float leftBuildingX = -10.18f - ((i % 2) * 0.16f);
            float rightBuildingX = 10.18f + (((i + 1) % 2) * 0.16f);
            float leftBuildingZ = districtZ - 8f + ((i % 2) * 5f);
            float rightBuildingZ = districtZ + 10f - (((i + 1) % 2) * 5f);
            Color leftAccent = coolAccent ? new Color(0.18f, 0.78f, 1f, 0.92f) : new Color(1f, 0.64f, 0.28f, 0.92f);
            Color rightAccent = !coolAccent ? new Color(0.18f, 0.78f, 1f, 0.92f) : new Color(1f, 0.64f, 0.28f, 0.92f);

            if (buildingOne != null)
            {
                CreateStrip(root, $"SafeBuildingPlinthLeft_{i}", new Vector3(-10.28f, groundY + 0.08f, leftBuildingZ), new Vector3(1.7f, 0.14f, 5.4f), new Color(0.04f, 0.08f, 0.12f, 1f));
                CreateStrip(root, $"SafeBuildingPlinthRight_{i}", new Vector3(10.28f, groundY + 0.08f, rightBuildingZ), new Vector3(1.7f, 0.14f, 5.4f), new Color(0.04f, 0.08f, 0.12f, 1f));
                CreateStrip(root, $"SafeBuildingGlowLeft_{i}", new Vector3(-9.92f, groundY + 0.03f, leftBuildingZ), new Vector3(0.06f, 0.02f, 4.9f), leftAccent * 0.92f, true);
                CreateStrip(root, $"SafeBuildingGlowRight_{i}", new Vector3(9.92f, groundY + 0.03f, rightBuildingZ), new Vector3(0.06f, 0.02f, 4.9f), rightAccent * 0.92f, true);

                CreateDecorAsset(
                    root,
                    $"SafeBuildingLeft_{i}",
                    buildingOne,
                    new Vector3(leftBuildingX, groundY + 0.14f, leftBuildingZ),
                    Quaternion.Euler(0f, 90f, 0f),
                    Quaternion.identity,
                    5.7f + ((i % 3) * 0.24f),
                    2.85f,
                    3.2f,
                    visual =>
                    {
                        if (!HasUsableImportedMaterials(visual))
                            ApplyCityBuildingMaterials(visual, coolAccent);
                        EnableDecorLights(visual, 0.72f, 0.96f, 1);
                    },
                    9.82f,
                    0.08f);

                CreateDecorAsset(
                    root,
                    $"SafeBuildingRight_{i}",
                    buildingOne,
                    new Vector3(rightBuildingX, groundY + 0.14f, rightBuildingZ),
                    Quaternion.Euler(0f, -90f, 0f),
                    Quaternion.identity,
                    5.9f + (((i + 1) % 3) * 0.24f),
                    2.9f,
                    3.25f,
                    visual =>
                    {
                        if (!HasUsableImportedMaterials(visual))
                            ApplyCityBuildingMaterials(visual, !coolAccent);
                        EnableDecorLights(visual, 0.72f, 0.96f, 1);
                    },
                    9.82f,
                    0.08f);
            }

            if (signPrefab != null)
            {
                float signX = leftSideLead ? -10.04f : 10.04f;
                float signYaw = signX < 0f ? 90f : -90f;
                float signZ = districtZ + 24f;
                Color signAccent = leftSideLead ? leftAccent : rightAccent;
                CreateStrip(root, $"SafeNeonSignBase_{i}", new Vector3(signX, groundY + 0.12f, signZ), new Vector3(0.42f, 0.18f, 0.42f), new Color(0.04f, 0.08f, 0.12f, 1f));
                CreateStrip(root, $"SafeNeonSignPole_{i}", new Vector3(signX, groundY + 1.08f, signZ), new Vector3(0.08f, 1.92f, 0.08f), new Color(0.05f, 0.09f, 0.14f, 1f));
                CreateStrip(root, $"SafeNeonSignCrossbar_{i}", new Vector3(signX + (signX < 0f ? 0.22f : -0.22f), groundY + 1.98f, signZ), new Vector3(0.46f, 0.06f, 0.08f), signAccent * 0.7f, true);
                CreateDecorAsset(
                    root,
                    $"SafeNeonSign_{i}",
                    signPrefab,
                    new Vector3(signX + (signX < 0f ? 0.42f : -0.42f), groundY + 1.92f, signZ),
                    Quaternion.Euler(0f, signYaw, 0f),
                    Quaternion.identity,
                    1.18f,
                    1.28f,
                    0.34f,
                    visual =>
                    {
                        ApplyImportedMaterialsOrFallback(visual, new[] { GetNeonSignFallbackMaterial() }, true);
                        EnableDecorLights(visual, 0.94f, 1.1f, 2);
                    },
                    9.76f,
                    0.04f);
            }

            if (cardsPrefab != null)
            {
                for (int variant = 0; variant < 3; variant++)
                {
                    bool leftSide = variant == 1 ? !leftSideLead : leftSideLead;
                    float cardsX = leftSide ? -10.08f - (variant * 0.06f) : 10.08f + (variant * 0.06f);
                    float cardsYaw = cardsX < 0f ? 90f : -90f;
                    float cardsZ = districtZ + (variant == 0 ? -26f : variant == 1 ? 6f : 34f);
                    float cardsY = groundY + 1.72f + (((i + variant) % 2) * 0.1f);
                    float roll = leftSide ? 12f - (variant * 6f) : -12f + (variant * 6f);
                    Color cardsAccent = leftSide ? leftAccent : rightAccent;
                    CreateStrip(root, $"SafeNeonCardsPole_{i}_{variant}", new Vector3(cardsX, groundY + 1.02f, cardsZ), new Vector3(0.08f, 1.74f, 0.08f), new Color(0.05f, 0.09f, 0.14f, 1f));
                    CreateStrip(root, $"SafeNeonCardsFrame_{i}_{variant}", new Vector3(cardsX, cardsY - 0.04f, cardsZ), new Vector3(0.18f, 0.86f, 0.08f), new Color(0.04f, 0.08f, 0.12f, 1f));
                    CreateStrip(root, $"SafeNeonCardsGlow_{i}_{variant}", new Vector3(cardsX, cardsY, cardsZ - 0.03f), new Vector3(0.14f, 0.68f, 0.03f), cardsAccent * 0.72f, true);
                    CreateDecorAsset(
                        root,
                        $"SafeNeonCards_{i}_{variant}",
                        cardsPrefab,
                        new Vector3(cardsX, cardsY, cardsZ),
                        Quaternion.Euler(0f, cardsYaw, 0f),
                        Quaternion.Euler(0f, 0f, roll),
                        0.88f + (variant * 0.06f),
                        0.92f,
                        0.24f,
                        visual =>
                        {
                            ApplyImportedMaterialsOrFallback(visual, new[] { GetNeonCardsFallbackMaterial() }, true);
                            EnableDecorLights(visual, 0.92f, 1.08f, 1);
                        },
                        9.82f,
                        0.04f);
                }
            }
        }
    }

    void RemoveLegacySceneBackdrop()
    {
        foreach (var transformChild in FindObjectsOfType<Transform>(true))
        {
            if (transformChild == null)
                continue;

            if (transformChild.name.StartsWith("SM_House_V"))
            {
                transformChild.gameObject.SetActive(false);
                Destroy(transformChild.gameObject);
            }
        }
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

    void BuildRunSummaryUi()
    {
        if (canvas == null)
            return;

        var existing = canvas.transform.Find("RuntimeRunSummaryUi");
        if (existing != null)
            Destroy(existing.gameObject);

        var root = new GameObject("RuntimeRunSummaryUi", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        runSummaryGroup = root.GetComponent<CanvasGroup>();
        runSummaryGroup.alpha = 0f;
        runSummaryGroup.interactable = false;
        runSummaryGroup.blocksRaycasts = false;

        var veil = new GameObject("SummaryVeil", typeof(RectTransform), typeof(Image));
        veil.transform.SetParent(root.transform, false);
        var veilRect = veil.GetComponent<RectTransform>();
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;
        var veilImage = veil.GetComponent<Image>();
        veilImage.color = new Color(0.01f, 0.04f, 0.08f, 0.68f);

        var panel = new GameObject("SummaryPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 360f);
        var panelImage = panel.GetComponent<Image>();
        panelImage.sprite = GetRoundedSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.05f, 0.09f, 0.16f, 0.97f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.29f, 0.93f, 0.96f, 0.3f);
        outline.effectDistance = new Vector2(2f, -2f);

        runSummaryBadge = CreatePanelText(panelRect, "SummaryBadge", "LEVEL RESULT", 18f, FontStyles.Bold, new Color(0.29f, 0.93f, 0.96f, 1f), new Vector2(0f, 132f), new Vector2(260f, 24f));
        runSummaryBadge.alignment = TextAlignmentOptions.Center;
        runSummaryTitle = CreatePanelText(panelRect, "SummaryTitle", "RUN COMPLETE", 42f, FontStyles.Bold, Color.white, new Vector2(0f, 92f), new Vector2(420f, 48f));
        runSummaryTitle.alignment = TextAlignmentOptions.Center;
        runSummaryPrimaryValue = CreatePanelText(panelRect, "SummaryPrimaryValue", "0", 62f, FontStyles.Bold, new Color(1f, 0.79f, 0.36f, 1f), new Vector2(0f, 18f), new Vector2(260f, 70f));
        runSummaryPrimaryValue.alignment = TextAlignmentOptions.Center;
        runSummaryPrimaryLabel = CreatePanelText(panelRect, "SummaryPrimaryLabel", "COINS COLLECTED", 20f, FontStyles.Bold, new Color(0.92f, 0.97f, 1f, 1f), new Vector2(0f, -34f), new Vector2(320f, 26f));
        runSummaryPrimaryLabel.alignment = TextAlignmentOptions.Center;

        runSummarySecondaryLabel = CreatePanelText(panelRect, "SummarySecondaryLabel", "DISTANCE", 16f, FontStyles.Bold, new Color(0.29f, 0.93f, 0.96f, 1f), new Vector2(-118f, -102f), new Vector2(180f, 22f));
        runSummarySecondaryLabel.alignment = TextAlignmentOptions.Center;
        runSummarySecondaryValue = CreatePanelText(panelRect, "SummarySecondaryValue", "0 M", 28f, FontStyles.Bold, Color.white, new Vector2(-118f, -136f), new Vector2(180f, 30f));
        runSummarySecondaryValue.alignment = TextAlignmentOptions.Center;

        runSummaryRecordLabel = CreatePanelText(panelRect, "SummaryRecordLabel", "BEST 0", 18f, FontStyles.Bold, new Color(1f, 0.79f, 0.36f, 1f), new Vector2(118f, -118f), new Vector2(220f, 48f));
        runSummaryRecordLabel.alignment = TextAlignmentOptions.Center;
        runSummaryFooter = CreatePanelText(panelRect, "SummaryFooter", "Returning to menu...", 16f, FontStyles.Italic, new Color(0.74f, 0.82f, 0.9f, 1f), new Vector2(0f, -158f), new Vector2(280f, 24f));
        runSummaryFooter.alignment = TextAlignmentOptions.Center;
    }

    void ApplyRunSummary(RunProgressStore.RunSummary summary)
    {
        if (runSummaryGroup == null ||
            runSummaryBadge == null ||
            runSummaryTitle == null ||
            runSummaryPrimaryValue == null ||
            runSummaryPrimaryLabel == null ||
            runSummarySecondaryLabel == null ||
            runSummarySecondaryValue == null ||
            runSummaryRecordLabel == null ||
            runSummaryFooter == null)
            return;

        runSummaryGroup.alpha = 1f;
        runSummaryGroup.interactable = false;
        runSummaryGroup.blocksRaycasts = false;
        runSummaryGroup.transform.SetAsLastSibling();

        runSummaryBadge.text = $"{RunProgressStore.GetStageLabel(summary.StageIndex)} RESULT";
        runSummaryTitle.text = summary.IsNewRecord ? "NEW RECORD" : "RUN COMPLETE";
        runSummaryPrimaryValue.text = RunProgressStore.FormatPrimaryValue(summary.StageIndex, summary.PrimaryValue);
        runSummaryPrimaryLabel.text = RunProgressStore.GetPrimaryResultLabel(summary.StageIndex);
        runSummarySecondaryLabel.text = RunProgressStore.GetSecondaryResultLabel(summary.StageIndex);
        runSummarySecondaryValue.text = RunProgressStore.FormatSecondaryValue(summary.StageIndex, summary.CoinsCollected, summary.DistanceMeters);
        runSummaryRecordLabel.text = summary.IsNewRecord
            ? $"NEW BEST  {RunProgressStore.FormatPrimaryValue(summary.StageIndex, summary.BestValue)}"
            : $"BEST  {RunProgressStore.FormatPrimaryValue(summary.StageIndex, summary.BestValue)}";
        runSummaryFooter.text = summary.IsNewRecord
            ? "Record updated."
            : "Returning to menu...";
    }

    void UpdateDistanceHud()
    {
        if (distanceLabel == null || trackedPlayer == null)
            return;

        distanceLabel.text = $"{GetCurrentRunDistanceMeters()} M";
    }

    int GetCurrentRunDistanceMeters()
    {
        if (trackedPlayer == null)
            trackedPlayer = FindObjectOfType<PlayerMovement>(true);

        if (trackedPlayer == null)
            return 0;

        float distance = Mathf.Max(0f, trackedPlayer.transform.position.z - runStartZ);
        return Mathf.FloorToInt(distance);
    }

    public IEnumerator ShowRunSummaryAndReturnToMenu(GameObject fadeOverlay = null)
    {
        var summary = RunProgressStore.RecordRun(SelectedStageIndex, MasterLevelInfo.CoinCount, GetCurrentRunDistanceMeters());

        if (fadeOverlay != null)
            fadeOverlay.SetActive(false);

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>(true);

        BuildRunSummaryUi();
        ApplyRunSummary(summary);

        yield return new WaitForSecondsRealtime(2.35f);

        if (runSummaryGroup != null)
        {
            runSummaryGroup.alpha = 0f;
            runSummaryGroup.interactable = false;
            runSummaryGroup.blocksRaycasts = false;
        }

        SceneManager.LoadScene(0);
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
            float baseLaneSpeed = SelectedStageIndex == 0 ? 19.4f : 20.8f;
            if (IsHardcoreMode)
            {
                baseSpeed = SelectedStageIndex == 0 ? 18.2f : 19.6f;
                baseLaneSpeed = SelectedStageIndex == 0 ? 28.0f : 29.6f;
            }

            player.playerSpeed = baseSpeed;
            player.laneSwitchSpeed = baseLaneSpeed;
            player.jumpForce = IsHardcoreMode ? 8.85f : 8.55f;
            player.gravity = IsHardcoreMode ? 18.1f : 17.3f;
            player.landingSnapThreshold = 0.18f;
            player.groundStickDistance = 0.12f;
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
            var hits = Physics.RaycastAll(origin, Vector3.down, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            float bestGroundY = float.NaN;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (!IsValidGroundHit(hit.transform))
                    continue;

                if (hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                bestGroundY = hit.point.y;
            }

            if (float.IsFinite(bestGroundY))
                return bestGroundY;
        }

        return 0.015f;
    }

    bool IsValidGroundHit(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        if (trackedPlayer != null && hitTransform.IsChildOf(trackedPlayer.transform))
            return false;

        return true;
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

    void ConfigureRuntimeCameraRig()
    {
        if (trackedPlayer == null)
            return;

        var camera = Camera.main;
        if (camera == null)
            return;

        Transform cameraTransform = camera.transform;
        Vector3 cameraLocalOffset = cameraTransform.parent == trackedPlayer.transform
            ? cameraTransform.localPosition
            : trackedPlayer.transform.InverseTransformPoint(cameraTransform.position);
        Quaternion cameraLocalRotation = cameraTransform.parent == trackedPlayer.transform
            ? cameraTransform.localRotation
            : Quaternion.Inverse(trackedPlayer.transform.rotation) * cameraTransform.rotation;
        cameraTransform.SetParent(null, true);

        var animator = camera.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
            animator.Rebind();
            animator.Update(0f);
        }

        var follow = camera.GetComponent<RuntimeRunCameraFollow>();
        if (follow == null)
            follow = camera.gameObject.AddComponent<RuntimeRunCameraFollow>();

        follow.Initialize(trackedPlayer.transform, cameraLocalOffset, cameraLocalRotation);
    }

    void RestoreOriginalCameraRig()
    {
        if (trackedPlayer == null)
            return;

        var camera = Camera.main;
        if (camera == null)
            return;

        var follow = camera.GetComponent<RuntimeRunCameraFollow>();
        if (follow != null)
            Destroy(follow);

        Transform cameraTransform = camera.transform;
        cameraTransform.SetParent(trackedPlayer.transform, false);
        cameraTransform.localPosition = new Vector3(0f, 3.9f, -7.94f);
        cameraTransform.localRotation = Quaternion.Euler(13.94f, 0f, 0f);
        cameraTransform.localScale = new Vector3(2f, 2f, 2f);

        var animator = camera.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }
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
        RenderSettings.fog = false;
        RenderSettings.fogColor = new Color(0.03f, 0.07f, 0.1f, 1f);
        RenderSettings.fogDensity = 0.0054f;
        RenderSettings.ambientLight = new Color(0.58f, 0.64f, 0.72f, 1f);
        RenderSettings.ambientIntensity = 1.22f;
        RenderSettings.reflectionIntensity = 0.78f;

        var camera = Camera.main;
        if (camera != null)
            camera.backgroundColor = new Color(0.02f, 0.04f, 0.07f, 1f);

        foreach (var light in FindObjectsOfType<Light>(true))
        {
            if (light.type == LightType.Directional)
            {
                light.color = new Color(0.62f, 0.86f, 1f, 1f);
                light.intensity = 1.55f;
            }
            else if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                light.enabled = false;
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

            camera.allowHDR = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
        }
    }

    void RestoreOriginalScenePresentation()
    {
        var camera = Camera.main;
        if (camera != null)
        {
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.None;
            }
        }

        foreach (var light in FindObjectsOfType<Light>(true))
            light.enabled = true;
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
        float laneStripThickness = 0.01f;
        float lowerWallHeight = 1.42f;
        float lowerWallCenterY = centerY + 0.66f;
        float upperWallHeight = 0.48f;
        float upperWallCenterY = ceilingY - 0.24f;
        float borderHeight = ceilingY - centerY + 0.08f;
        float borderCenterY = centerY + (borderHeight * 0.5f) - 0.02f;
        float groundY = centerY + 0.14f;

        CreateStrip(root, "FloorBase", new Vector3(0f, centerY - 0.02f, startZ + (length * 0.5f)), new Vector3(9.6f, 0.018f, length), new Color(0.06f, 0.09f, 0.14f, 1f));
        CreateStrip(root, "LaneGlowLeft", new Vector3(-3.5f, centerY, startZ + (length * 0.5f)), new Vector3(0.16f, laneStripThickness, length), new Color(0.17f, 0.75f, 1f, 0.95f), true);
        CreateStrip(root, "LaneGlowCenter", new Vector3(0f, centerY, startZ + (length * 0.5f)), new Vector3(0.18f, laneStripThickness, length), new Color(1f, 0.78f, 0.32f, 0.95f), true);
        CreateStrip(root, "LaneGlowRight", new Vector3(3.5f, centerY, startZ + (length * 0.5f)), new Vector3(0.16f, laneStripThickness, length), new Color(0.17f, 0.75f, 1f, 0.95f), true);
        CreateStrip(root, "TrackBorderLeft", new Vector3(-7.72f, borderCenterY, startZ + (length * 0.5f)), new Vector3(0.28f, borderHeight, length), new Color(0.05f, 0.08f, 0.14f, 1f));
        CreateStrip(root, "TrackBorderRight", new Vector3(7.72f, borderCenterY, startZ + (length * 0.5f)), new Vector3(0.28f, borderHeight, length), new Color(0.05f, 0.08f, 0.14f, 1f));
        CreateStrip(root, "SideWallBaseLeft", new Vector3(-8.82f, lowerWallCenterY, startZ + (length * 0.5f)), new Vector3(1.95f, lowerWallHeight, length), new Color(0.06f, 0.1f, 0.16f, 1f));
        CreateStrip(root, "SideWallBaseRight", new Vector3(8.82f, lowerWallCenterY, startZ + (length * 0.5f)), new Vector3(1.95f, lowerWallHeight, length), new Color(0.06f, 0.1f, 0.16f, 1f));
        CreateStrip(root, "SideCanopyLeft", new Vector3(-8.86f, upperWallCenterY, startZ + (length * 0.5f)), new Vector3(1.62f, upperWallHeight, length), new Color(0.05f, 0.09f, 0.15f, 1f));
        CreateStrip(root, "SideCanopyRight", new Vector3(8.86f, upperWallCenterY, startZ + (length * 0.5f)), new Vector3(1.62f, upperWallHeight, length), new Color(0.05f, 0.09f, 0.15f, 1f));
        CreateStrip(root, "WindowRailLeftLower", new Vector3(-7.66f, centerY + 1.46f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(0.12f, 0.58f, 0.96f, 0.72f), true);
        CreateStrip(root, "WindowRailRightLower", new Vector3(7.66f, centerY + 1.46f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(0.12f, 0.58f, 0.96f, 0.72f), true);
        CreateStrip(root, "WindowRailLeftUpper", new Vector3(-7.66f, centerY + 3.08f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(1f, 0.68f, 0.32f, 0.72f), true);
        CreateStrip(root, "WindowRailRightUpper", new Vector3(7.66f, centerY + 3.08f, startZ + (length * 0.5f)), new Vector3(0.12f, 0.05f, length), new Color(1f, 0.68f, 0.32f, 0.72f), true);
        CreateStrip(root, "CeilingPanel", new Vector3(0f, ceilingY, startZ + (length * 0.5f)), new Vector3(15.2f, 0.06f, length), new Color(0.07f, 0.11f, 0.18f, 1f));
        CreateSidePromenades(root, startZ, groundY, length);
        CreateSideNeonRails(root, startZ, centerY, ceilingY, length);
        CreateSideCasinoBays(root, startZ, centerY, length);
        CreateSideLedPillars(root, startZ, centerY, ceilingY, length);
        CreateSideBillboards(root, startZ, centerY, length);
        CreateSideCityBackdrop(root, startZ, groundY, length);
        CreateNeonSignage(root, startZ, groundY, length);

        for (int i = 0; i < 16; i++)
        {
            float z = startZ + 18f + (i * 28f);
            Color pulseColor = i % 2 == 0 ? new Color(0.16f, 0.7f, 1f, 0.92f) : new Color(1f, 0.7f, 0.3f, 0.92f);
            CreateStrip(root, $"CrossBeam_{i}", new Vector3(0f, centerY - 0.01f, z), new Vector3(7.8f, 0.009f, 0.55f), pulseColor, true);
            CreateStrip(root, $"CeilingGlow_{i}", new Vector3(0f, ceilingY - 0.03f, z), new Vector3(6.2f, 0.045f, 0.32f), pulseColor, true);
            if (i % 3 != 0)
                CreateSpotlightPair(root, z + 5f, Color.Lerp(pulseColor, Color.white, 0.18f), ceilingY - 0.26f);
        }
    }

    void CreateSideNeonRails(Transform root, float startZ, float centerY, float ceilingY, float length)
    {
        Color coolRail = SelectedStageIndex == 0
            ? new Color(0.18f, 0.78f, 1f, 0.9f)
            : new Color(0.26f, 0.92f, 0.96f, 0.9f);
        Color warmRail = SelectedStageIndex == 0
            ? new Color(1f, 0.74f, 0.34f, 0.88f)
            : new Color(1f, 0.5f, 0.24f, 0.92f);
        float zCenter = startZ + (length * 0.5f);

        CreateStrip(root, "LowerRailLeft", new Vector3(-7.88f, centerY + 0.36f, zCenter), new Vector3(0.055f, 0.03f, length), coolRail, true);
        CreateStrip(root, "LowerRailRight", new Vector3(7.88f, centerY + 0.36f, zCenter), new Vector3(0.055f, 0.03f, length), coolRail, true);
        CreateStrip(root, "UpperRailLeft", new Vector3(-7.88f, ceilingY - 0.54f, zCenter), new Vector3(0.055f, 0.035f, length), warmRail, true);
        CreateStrip(root, "UpperRailRight", new Vector3(7.88f, ceilingY - 0.54f, zCenter), new Vector3(0.055f, 0.035f, length), warmRail, true);
        CreateStrip(root, "MidRailLeft", new Vector3(-8.22f, centerY + 1.86f, zCenter), new Vector3(0.045f, 0.028f, length), warmRail * 0.82f, true);
        CreateStrip(root, "MidRailRight", new Vector3(8.22f, centerY + 1.86f, zCenter), new Vector3(0.045f, 0.028f, length), warmRail * 0.82f, true);
    }

    void CreateSidePromenades(Transform root, float startZ, float groundY, float length)
    {
        float zCenter = startZ + (length * 0.5f);
        Color promenadeBase = new Color(0.05f, 0.09f, 0.14f, 1f);
        Color curbColor = new Color(0.08f, 0.13f, 0.2f, 1f);
        Color coolGlow = new Color(0.2f, 0.79f, 1f, 0.84f);
        Color warmGlow = new Color(1f, 0.68f, 0.3f, 0.84f);

        CreateStrip(root, "PromenadeLeft", new Vector3(-11.35f, groundY - 0.03f, zCenter), new Vector3(2.3f, 0.06f, length), promenadeBase);
        CreateStrip(root, "PromenadeRight", new Vector3(11.35f, groundY - 0.03f, zCenter), new Vector3(2.3f, 0.06f, length), promenadeBase);
        CreateStrip(root, "PromenadeCurbLeft", new Vector3(-9.76f, groundY + 0.1f, zCenter), new Vector3(0.14f, 0.18f, length), curbColor);
        CreateStrip(root, "PromenadeCurbRight", new Vector3(9.76f, groundY + 0.1f, zCenter), new Vector3(0.14f, 0.18f, length), curbColor);
        CreateStrip(root, "PromenadeGlowLeftInner", new Vector3(-9.98f, groundY + 0.04f, zCenter), new Vector3(0.06f, 0.02f, length), coolGlow, true);
        CreateStrip(root, "PromenadeGlowRightInner", new Vector3(9.98f, groundY + 0.04f, zCenter), new Vector3(0.06f, 0.02f, length), coolGlow, true);
        CreateStrip(root, "PromenadeGlowLeftOuter", new Vector3(-12.62f, groundY + 0.04f, zCenter), new Vector3(0.05f, 0.02f, length), warmGlow * 0.82f, true);
        CreateStrip(root, "PromenadeGlowRightOuter", new Vector3(12.62f, groundY + 0.04f, zCenter), new Vector3(0.05f, 0.02f, length), warmGlow * 0.82f, true);
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

    void CreateSideLedPillars(Transform root, float startZ, float centerY, float ceilingY, float length)
    {
        int pillarCount = Mathf.CeilToInt(length / 48f) + 1;
        float firstZ = startZ + 14f;

        for (int i = 0; i < pillarCount; i++)
        {
            float z = firstZ + (i * 48f);
            Color accent = (i + SelectedStageIndex) % 2 == 0
                ? new Color(0.2f, 0.78f, 1f, 0.96f)
                : new Color(1f, 0.58f, 0.24f, 0.96f);

            CreateStrip(root, $"PillarShellLeft_{i}", new Vector3(-7.94f, centerY + 2.02f, z), new Vector3(0.24f, 3.96f, 0.42f), new Color(0.04f, 0.09f, 0.15f, 1f));
            CreateStrip(root, $"PillarShellRight_{i}", new Vector3(7.94f, centerY + 2.02f, z), new Vector3(0.24f, 3.96f, 0.42f), new Color(0.04f, 0.09f, 0.15f, 1f));
            CreateStrip(root, $"PillarLedLeft_{i}", new Vector3(-7.82f, centerY + 2.02f, z), new Vector3(0.045f, 3.54f, 0.11f), accent, true);
            CreateStrip(root, $"PillarLedRight_{i}", new Vector3(7.82f, centerY + 2.02f, z), new Vector3(0.045f, 3.54f, 0.11f), accent, true);
            CreateStrip(root, $"PillarCapLeft_{i}", new Vector3(-7.86f, ceilingY - 0.28f, z), new Vector3(0.3f, 0.06f, 0.5f), accent * 0.84f, true);
            CreateStrip(root, $"PillarCapRight_{i}", new Vector3(7.86f, ceilingY - 0.28f, z), new Vector3(0.3f, 0.06f, 0.5f), accent * 0.84f, true);

            if (i % 3 != 1)
            {
                CreateLight(root, $"PillarLightLeft_{i}", new Vector3(-6.74f, centerY + 2.18f, z), accent, 5.6f, 2.8f);
                CreateLight(root, $"PillarLightRight_{i}", new Vector3(6.74f, centerY + 2.18f, z), accent, 5.6f, 2.8f);
            }
        }
    }

    void CreateSideCityBackdrop(Transform root, float startZ, float groundY, float length)
    {
        var buildingOne = GetBuildingOnePrefab();
        var buildingTwo = GetBuildingTwoPrefab();
        if (buildingOne == null && buildingTwo == null)
            return;

        int districtCount = Mathf.CeilToInt(length / 96f) + 1;
        float firstZ = startZ + 24f;

        for (int i = 0; i < districtCount; i++)
        {
            float districtZ = firstZ + (i * 96f);
            GameObject frontLeft = buildingOne != null ? buildingOne : buildingTwo;
            GameObject frontRight = i % 2 == 0
                ? (buildingTwo != null ? buildingTwo : buildingOne)
                : (buildingOne != null ? buildingOne : buildingTwo);
            GameObject rearCluster = buildingTwo != null ? buildingTwo : buildingOne;

            CreateCityBuildingInstance(root, $"CityFrontLeft_{i}", frontLeft, new Vector3(-8.7f, groundY, districtZ), Quaternion.Euler(0f, 90f, 0f), 7.3f + ((i % 3) * 0.35f), 3.5f, 3.8f, i % 2 == 0);
            CreateCityBuildingInstance(root, $"CityFrontRight_{i}", frontRight, new Vector3(8.7f, groundY, districtZ + 8f), Quaternion.Euler(0f, -90f, 0f), 7.45f + (((i + 1) % 3) * 0.38f), 3.7f, 4f, i % 2 != 0);
            CreateCityBuildingInstance(root, $"CityRearLeft_{i}", rearCluster, new Vector3(-10.1f, groundY, districtZ + 16f), Quaternion.Euler(0f, 90f, 0f), 8.9f + ((i % 2) * 0.5f), 4.5f, 4.9f, true);
            CreateCityBuildingInstance(root, $"CityRearRight_{i}", rearCluster, new Vector3(10.1f, groundY, districtZ + 22f), Quaternion.Euler(0f, -90f, 0f), 9.1f + (((i + 1) % 2) * 0.5f), 4.7f, 5.1f, false);
        }
    }

    void CreateCityBuildingInstance(Transform root, string name, GameObject prefab, Vector3 position, Quaternion rotation, float targetHeight, float maxWidth, float maxDepth, bool useCoolAccent)
    {
        if (prefab == null)
            return;

        CreateDecorAsset(
            root,
            name,
            prefab,
            position,
            rotation,
            Quaternion.identity,
            targetHeight,
            maxWidth,
            maxDepth,
            visual =>
            {
                if (!HasUsableImportedMaterials(visual))
                    ApplyCityBuildingMaterials(visual, useCoolAccent);
            },
            6.2f,
            0.08f);
    }

    void CreateNeonSignage(Transform root, float startZ, float groundY, float length)
    {
        var signPrefab = GetNeonSign24hPrefab();
        var cardsPrefab = GetNeonCardsPrefab();
        int signCount = Mathf.CeilToInt(length / 118f) + 1;
        float firstZ = startZ + 22f;

        for (int i = 0; i < signCount; i++)
        {
            float z = firstZ + (i * 118f);
            bool leftSideLead = i % 2 == 0;
            Color accent = i % 2 == 0
                ? new Color(0.22f, 0.82f, 1f, 1f)
                : new Color(1f, 0.68f, 0.3f, 1f);

            if (signPrefab != null)
            {
                float signX = leftSideLead ? -8.2f : 8.2f;
                float signYaw = signX < 0f ? 90f : -90f;
                CreateStrip(root, $"Neon24H_Pole_{i}", new Vector3(signX, groundY + 1.55f, z), new Vector3(0.12f, 2.9f, 0.12f), new Color(0.05f, 0.09f, 0.14f, 1f));
                CreateDecorAsset(
                    root,
                    $"Neon24H_{i}",
                    signPrefab,
                    new Vector3(signX, groundY + 3.25f, z),
                    Quaternion.Euler(0f, signYaw, 0f),
                    Quaternion.identity,
                    2.45f,
                    2.8f,
                    0.8f,
                    visual => ApplyImportedMaterialsOrFallback(visual, new[] { GetNeonSignFallbackMaterial() }),
                    6.05f,
                    0.08f);
                if (i % 2 == 0)
                    CreateLight(root, $"Neon24HLight_{i}", new Vector3(signX * 0.92f, groundY + 3.12f, z), accent, 4.2f, 1.7f);
            }

            if (cardsPrefab != null)
            {
                float cardsX = leftSideLead ? 8.85f : -8.85f;
                float cardsYaw = cardsX < 0f ? 90f : -90f;
                float cardsZ = z + 12f;
                CreateStrip(root, $"NeonCards_Pole_{i}", new Vector3(cardsX, groundY + 1.48f, cardsZ), new Vector3(0.11f, 2.7f, 0.11f), new Color(0.05f, 0.09f, 0.14f, 1f));
                CreateDecorAsset(
                    root,
                    $"NeonCards_{i}",
                    cardsPrefab,
                    new Vector3(cardsX, groundY + 2.88f, cardsZ),
                    Quaternion.Euler(0f, cardsYaw, 0f),
                    Quaternion.Euler(0f, 0f, cardsX < 0f ? 5f : -5f),
                    2.9f,
                    3.1f,
                    0.82f,
                    visual => ApplyImportedMaterialsOrFallback(visual, new[] { GetNeonCardsFallbackMaterial() }),
                    6.2f,
                    0.08f);
                if (i % 2 == 1)
                    CreateLight(root, $"NeonCardsLight_{i}", new Vector3(cardsX * 0.92f, groundY + 2.9f, cardsZ), accent, 4f, 1.5f);
            }
        }
    }

    void CreateSideBillboards(Transform root, float startZ, float centerY, float length)
    {
        int boardCount = Mathf.CeilToInt(length / 72f);
        float firstZ = startZ + 26f;

        for (int i = 0; i < boardCount; i++)
        {
            float z = firstZ + (i * 72f);
            Color accent = i % 2 == 0
                ? new Color(0.18f, 0.78f, 1f, 0.94f)
                : new Color(1f, 0.7f, 0.3f, 0.94f);
            float y = centerY + 2.42f;

            CreateStrip(root, $"BillboardStemLeft_{i}", new Vector3(-11.18f, centerY + 1.2f, z), new Vector3(0.16f, 2.1f, 0.16f), new Color(0.05f, 0.09f, 0.14f, 1f));
            CreateStrip(root, $"BillboardStemRight_{i}", new Vector3(11.18f, centerY + 1.2f, z), new Vector3(0.16f, 2.1f, 0.16f), new Color(0.05f, 0.09f, 0.14f, 1f));
            CreateStrip(root, $"BillboardFrameLeft_{i}", new Vector3(-11.18f, y, z), new Vector3(2.4f, 1.18f, 0.14f), new Color(0.03f, 0.07f, 0.12f, 1f));
            CreateStrip(root, $"BillboardFrameRight_{i}", new Vector3(11.18f, y, z), new Vector3(2.4f, 1.18f, 0.14f), new Color(0.03f, 0.07f, 0.12f, 1f));
            CreateStrip(root, $"BillboardGlowLeft_{i}", new Vector3(-11.18f, y, z - 0.02f), new Vector3(2.02f, 0.8f, 0.04f), accent, true);
            CreateStrip(root, $"BillboardGlowRight_{i}", new Vector3(11.18f, y, z - 0.02f), new Vector3(2.02f, 0.8f, 0.04f), accent, true);
            CreateStrip(root, $"BillboardTopLeft_{i}", new Vector3(-11.18f, y + 0.52f, z), new Vector3(2.48f, 0.05f, 0.16f), Color.Lerp(accent, Color.white, 0.2f), true);
            CreateStrip(root, $"BillboardTopRight_{i}", new Vector3(11.18f, y + 0.52f, z), new Vector3(2.48f, 0.05f, 0.16f), Color.Lerp(accent, Color.white, 0.2f), true);
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

        PrepareDecorInstance(decor);
        ApplyArcadeMachineRuntimeMaterials(decor);
        SnapDecorToGround(decor.transform);
        ClampDecorOutsideTrack(decor.transform, 8.8f, 0.55f);
    }

    void CreateDecorAsset(Transform parent, string name, GameObject prefab, Vector3 position, Quaternion rotation, Quaternion visualLocalRotation, float targetHeight, float maxWidth, float maxDepth, System.Action<GameObject> configureVisual = null, float protectedEdgeX = 9.2f, float padding = 0.9f)
    {
        if (parent == null || prefab == null)
            return;

        var anchor = new GameObject(name).transform;
        anchor.SetParent(parent, false);
        anchor.position = position;
        anchor.rotation = rotation;

        var visual = Instantiate(prefab, anchor);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = visualLocalRotation;
        visual.transform.localScale = Vector3.one;

        PrepareDecorInstance(visual);
        configureVisual?.Invoke(visual);
        FitDecorVisual(anchor, visual.transform, targetHeight, maxWidth, maxDepth);
        ClampDecorOutsideTrack(anchor, protectedEdgeX, padding);
    }

    void FitDecorVisual(Transform anchor, Transform visual, float targetHeight, float maxWidth, float maxDepth)
    {
        if (anchor == null || visual == null)
            return;

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds localBounds = GetLocalRendererBounds(anchor, renderers);
        float width = Mathf.Max(localBounds.size.x, 0.01f);
        float depth = Mathf.Max(localBounds.size.z, 0.01f);
        float height = Mathf.Max(localBounds.size.y, 0.01f);
        float uniformScale = Mathf.Min(targetHeight / height, maxWidth / width, maxDepth / depth);
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visual.localScale = Vector3.one * uniformScale;

        renderers = visual.GetComponentsInChildren<Renderer>(true);
        localBounds = GetLocalRendererBounds(anchor, renderers);
        visual.localPosition = new Vector3(
            -localBounds.center.x,
            -localBounds.min.y,
            -localBounds.center.z);
    }

    void PrepareDecorInstance(GameObject decor)
    {
        if (decor == null)
            return;

        foreach (var collider in decor.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (var listener in decor.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (var source in decor.GetComponentsInChildren<AudioSource>(true))
        {
            source.Stop();
            source.enabled = false;
        }

        foreach (var light in decor.GetComponentsInChildren<Light>(true))
            light.enabled = false;

        foreach (var camera in decor.GetComponentsInChildren<Camera>(true))
            camera.enabled = false;

        foreach (var canvas in decor.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = false;

        foreach (var animator in decor.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;

        foreach (var renderer in decor.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        foreach (var particle in decor.GetComponentsInChildren<ParticleSystem>(true))
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        foreach (var body in decor.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    void EnableDecorLights(GameObject visualRoot, float intensityMultiplier, float rangeMultiplier, int maxLightsToEnable)
    {
        if (visualRoot == null || maxLightsToEnable <= 0)
            return;

        var lights = visualRoot.GetComponentsInChildren<Light>(true);
        if (lights == null || lights.Length == 0)
            return;

        int enabledCount = 0;
        foreach (var light in lights)
        {
            if (light == null)
                continue;

            if (light.type == LightType.Directional)
            {
                light.enabled = false;
                continue;
            }

            if (enabledCount >= maxLightsToEnable || runtimeLightCount >= MaxRuntimeLightCount)
            {
                light.enabled = false;
                continue;
            }

            float baseIntensity = light.intensity > 0.01f ? light.intensity : 1f;
            float baseRange = light.range > 0.01f ? light.range : 3.5f;
            light.intensity = Mathf.Clamp(baseIntensity * intensityMultiplier, 0.55f, 3.2f);
            light.range = Mathf.Clamp(baseRange * rangeMultiplier, 2.2f, 9.5f);
            light.bounceIntensity = 0f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;
            light.enabled = true;
            enabledCount++;
            runtimeLightCount++;
        }
    }

    void ApplyCityBuildingMaterials(GameObject visualRoot, bool useCoolAccent)
    {
        if (visualRoot == null)
            return;

        var bodyMaterial = GetCityBuildingBodyMaterial();
        var accentMaterial = useCoolAccent ? GetCityBuildingAccentCoolMaterial() : GetCityBuildingAccentWarmMaterial();
        if (bodyMaterial == null)
            return;

        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            var assignedMaterials = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
                assignedMaterials[i] = slotCount > 1 && i == slotCount - 1 && accentMaterial != null ? accentMaterial : bodyMaterial;

            renderer.sharedMaterials = assignedMaterials;
        }
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
        {
            renderer.sharedMaterial = CreateRuntimeMaterial(color, emissive);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
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
        if (runtimeLightCount >= MaxRuntimeLightCount)
            return;

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
        runtimeLightCount++;
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

            GetObstaclePlacement(nextObstacleIndex, out float rowZ, out int laneIndex, out bool useMover, out bool useRope);
            if (EnableSafeRunArcadeObstacles && ForceSafeRunBaseline)
            {
                useMover = false;
                if (!EnableSafeRunRopeObstacles)
                    useRope = false;
            }
            ConfigureObstacleColliderShape(box, useRope);
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

    void GetObstaclePlacement(int obstacleIndex, out float rowZ, out int laneIndex, out bool useMover, out bool useRope)
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
        int[] rowPattern = rowPatterns[rowIndex % rowPatterns.Length];
        useMover = SelectedStageIndex == 1 && laneSlot == 0 && rowIndex % 5 == 2 && rowPattern.Length == 1;
        useRope = !useMover &&
                  rowPattern.Length == 1 &&
                  ((SelectedStageIndex == 0 && rowIndex % 3 == 1) ||
                   (SelectedStageIndex == 1 && rowIndex % 2 == 0));

        if (IsHardcoreMode && !useMover && rowPattern.Length == 1 && rowIndex % 3 == 0)
            useRope = true;
    }

    void ConfigureObstacleColliderShape(BoxCollider box, bool useRope)
    {
        if (box == null)
            return;

        box.isTrigger = true;
        if (useRope)
        {
            box.center = new Vector3(0f, 0.38f, 0f);
            box.size = new Vector3(1.55f, 0.72f, 1.8f);
            return;
        }

        box.center = new Vector3(0f, 0.95f, 0f);
        box.size = new Vector3(1.25f, 1.9f, 1.15f);
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

        PruneInactiveCoinTracking(coins);

        if (EnableSafeRunArcadeObstacles && ForceSafeRunBaseline)
        {
            ArrangeCoinsForSafeRunArcades(coins);
            return;
        }
        
        float sequenceSpacing = SelectedStageIndex == 0 ? 5.4f : 6.6f;
        float coinStep = SelectedStageIndex == 0 ? 2.15f : 2.32f;
        int[] lanePattern = SelectedStageIndex == 0 ? lanePatternLevelOne : lanePatternLevelTwo;
        float leftLaneX = trackedPlayer.GetLaneWorldX(0);
        float centerLaneX = trackedPlayer.GetLaneWorldX(1);
        float rightLaneX = trackedPlayer.GetLaneWorldX(2);
        int patternCycle = SelectedStageIndex == 0 ? 6 : 7;
        float baseCoinHeight = 0.72f;
        var pendingCoins = GetPendingActiveCoins(coins);
        if (pendingCoins.Count == 0)
            return;

        float batchStartZ = GetNextCoinBatchStartZ(coins, Mathf.Max(trackedPlayer.transform.position.z + 10f, 12f), sequenceSpacing + coinStep);
        int localAssignedIndex = 0;

        for (int i = 0; i < pendingCoins.Count; i++)
        {
            var coin = pendingCoins[i];
            if (coin == null)
                continue;

            int coinId = coin.gameObject.GetInstanceID();
            ConfigureCoinCollider(coin);
            ConfigureCoinVisual(coin);

            int patternClusterIndex = nextCoinIndex / 5;
            int localClusterIndex = localAssignedIndex / 5;
            int slotIndex = localAssignedIndex % 5;
            int baseLane = lanePattern[patternClusterIndex % lanePattern.Length];
            float laneX = trackedPlayer.GetLaneWorldX(baseLane);
            float z = batchStartZ + (localClusterIndex * (coinStep * 5f + sequenceSpacing)) + (slotIndex * coinStep);
            float jumpOffset = 0f;
            float zJitter = 0f;
            float t = slotIndex / 4f;
            int patternIndex = (patternClusterIndex + SelectedStageIndex) % patternCycle;

            switch (patternIndex)
            {
                case 1:
                    jumpOffset = BuildCoinJumpArc(t, 1.08f);
                    break;
                case 2:
                    laneX = Mathf.Lerp(leftLaneX, rightLaneX, t);
                    jumpOffset = 0.08f + BuildCoinJumpArc(t, 0.22f);
                    break;
                case 3:
                    laneX = Mathf.Lerp(rightLaneX, leftLaneX, t);
                    jumpOffset = 0.12f + BuildCoinJumpArc(t, 0.18f);
                    break;
                case 4:
                    float adjacentLaneX = trackedPlayer.GetLaneWorldX(Mathf.Clamp(baseLane + (patternClusterIndex % 2 == 0 ? 1 : -1), 0, 2));
                    laneX = Mathf.Lerp(trackedPlayer.GetLaneWorldX(baseLane), adjacentLaneX, t);
                    jumpOffset = BuildCoinJumpArc(t, 1.16f);
                    zJitter = slotIndex * 0.04f;
                    break;
                case 5:
                    laneX = slotIndex < 3
                        ? trackedPlayer.GetLaneWorldX(Mathf.Clamp(baseLane - 1, 0, 2))
                        : trackedPlayer.GetLaneWorldX(Mathf.Clamp(baseLane + 1, 0, 2));
                    jumpOffset = 0.14f + Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 0.18f;
                    break;
                case 6:
                    laneX = slotIndex % 2 == 0
                        ? centerLaneX
                        : (patternClusterIndex % 2 == 0 ? rightLaneX : leftLaneX);
                    jumpOffset = BuildCoinJumpArc(t, 1.02f) + 0.04f;
                    break;
                default:
                    laneX += Mathf.Lerp(-0.18f, 0.18f, t);
                    jumpOffset = patternClusterIndex % 3 == 0 ? 0.08f : 0f;
                    break;
            }

            Vector3 position = coin.transform.position;
            position.x = laneX;
            position.z = z + zJitter;
            position.y = gameplayFloorY + baseCoinHeight + jumpOffset;
            coin.transform.position = position;

            arrangedCoinIds.Add(coinId);
            nextCoinIndex++;
            localAssignedIndex++;
        }
    }

    void ArrangeCoinsForSafeRunArcades(CollectCoin[] coins)
    {
        if (coins == null || coins.Length == 0 || trackedPlayer == null)
            return;

        float rowSpacing = SelectedStageIndex == 0 ? 9.4f : 8.1f;
        if (IsHardcoreMode)
            rowSpacing -= 0.75f;

        float coinStep = SelectedStageIndex == 0 ? 2.15f : 2.32f;
        float baseCoinHeight = 0.72f;
        int[][] rowPatterns = SelectedStageIndex == 0 ? obstacleRowsLevelOne : obstacleRowsLevelTwo;
        var pendingCoins = GetPendingActiveCoins(coins);
        if (pendingCoins.Count == 0)
            return;

        float batchStartRowZ = GetNextCoinBatchStartZ(coins, Mathf.Max(trackedPlayer.transform.position.z + 18f, 24f), rowSpacing);
        int localAssignedIndex = 0;

        for (int i = 0; i < pendingCoins.Count; i++)
        {
            var coin = pendingCoins[i];
            if (coin == null)
                continue;

            int coinId = coin.gameObject.GetInstanceID();
            ConfigureCoinCollider(coin);
            ConfigureCoinVisual(coin);

            int patternClusterIndex = nextCoinIndex / 5;
            int localClusterIndex = localAssignedIndex / 5;
            int slotIndex = localAssignedIndex % 5;
            float t = slotIndex / 4f;
            float rowZ = batchStartRowZ + (localClusterIndex * rowSpacing);
            float z = rowZ - (coinStep * 2f) + (slotIndex * coinStep);
            int[] blockedLanes = rowPatterns[patternClusterIndex % rowPatterns.Length];
            int[] safeLanes = GetSafeLanesForPattern(blockedLanes);
            float laneX = trackedPlayer.GetLaneWorldX(1);
            float jumpOffset = 0f;

            if (safeLanes.Length <= 1)
            {
                int safeLane = safeLanes.Length == 1 ? safeLanes[0] : 1;
                laneX = trackedPlayer.GetLaneWorldX(safeLane);
                if (patternClusterIndex % 3 == 1)
                    jumpOffset = BuildCoinJumpArc(t, 1.04f);
                else if (patternClusterIndex % 4 == 2)
                    jumpOffset = 0.1f + BuildCoinJumpArc(t, 0.2f);
            }
            else
            {
                bool reverse = patternClusterIndex % 2 == 1;
                int startLane = reverse ? safeLanes[safeLanes.Length - 1] : safeLanes[0];
                int endLane = reverse ? safeLanes[0] : safeLanes[safeLanes.Length - 1];
                laneX = Mathf.Lerp(trackedPlayer.GetLaneWorldX(startLane), trackedPlayer.GetLaneWorldX(endLane), t);

                if (patternClusterIndex % 3 == 0)
                    jumpOffset = BuildCoinJumpArc(t, 0.92f);
                else if (patternClusterIndex % 4 == 1)
                    jumpOffset = 0.08f + BuildCoinJumpArc(t, 0.18f);
            }

            Vector3 position = coin.transform.position;
            position.x = laneX;
            position.z = z;
            position.y = gameplayFloorY + baseCoinHeight + jumpOffset;
            coin.transform.position = position;

            arrangedCoinIds.Add(coinId);
            nextCoinIndex++;
            localAssignedIndex++;
        }
    }

    void PruneInactiveCoinTracking(CollectCoin[] coins)
    {
        if (coins == null)
            return;

        var activeCoinIds = new HashSet<int>();
        for (int i = 0; i < coins.Length; i++)
        {
            var coin = coins[i];
            if (coin == null || !coin.gameObject.activeInHierarchy)
                continue;

            activeCoinIds.Add(coin.gameObject.GetInstanceID());
        }

        arrangedCoinIds.RemoveWhere(id => !activeCoinIds.Contains(id));
    }

    List<CollectCoin> GetPendingActiveCoins(CollectCoin[] coins)
    {
        var pendingCoins = new List<CollectCoin>();
        if (coins == null)
            return pendingCoins;

        for (int i = 0; i < coins.Length; i++)
        {
            var coin = coins[i];
            if (coin == null)
                continue;

            int coinId = coin.gameObject.GetInstanceID();
            if (arrangedCoinIds.Contains(coinId))
                continue;

            if (!coin.gameObject.activeInHierarchy)
                coin.PrepareForReuse();

            pendingCoins.Add(coin);
        }

        pendingCoins.Sort((left, right) =>
        {
            float leftZ = left != null ? left.transform.position.z : 0f;
            float rightZ = right != null ? right.transform.position.z : 0f;
            int zCompare = leftZ.CompareTo(rightZ);
            if (zCompare != 0)
                return zCompare;

            int leftId = left != null ? left.gameObject.GetInstanceID() : 0;
            int rightId = right != null ? right.gameObject.GetInstanceID() : 0;
            return leftId.CompareTo(rightId);
        });

        return pendingCoins;
    }

    float GetNextCoinBatchStartZ(CollectCoin[] coins, float fallbackStartZ, float preferredGap)
    {
        float furthestCoinZ = float.NegativeInfinity;
        if (coins != null)
        {
            for (int i = 0; i < coins.Length; i++)
            {
                var coin = coins[i];
                if (coin == null || !coin.gameObject.activeInHierarchy)
                    continue;

                int coinId = coin.gameObject.GetInstanceID();
                if (!arrangedCoinIds.Contains(coinId))
                    continue;

                furthestCoinZ = Mathf.Max(furthestCoinZ, coin.transform.position.z);
            }
        }

        if (!float.IsFinite(furthestCoinZ))
            return fallbackStartZ;

        return Mathf.Max(fallbackStartZ, furthestCoinZ + preferredGap);
    }

    int[] GetSafeLanesForPattern(int[] blockedLanes)
    {
        var safeLanes = new List<int>(3);
        for (int lane = 0; lane < 3; lane++)
        {
            bool isBlocked = false;
            if (blockedLanes != null)
            {
                for (int i = 0; i < blockedLanes.Length; i++)
                {
                    if (blockedLanes[i] != lane)
                        continue;

                    isBlocked = true;
                    break;
                }
            }

            if (!isBlocked)
                safeLanes.Add(lane);
        }

        return safeLanes.Count > 0 ? safeLanes.ToArray() : new[] { 1 };
    }

    float BuildCoinJumpArc(float t, float peakHeight)
    {
        return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * Mathf.Max(0f, peakHeight);
    }

    float GetGroundYAt(float x, float z, float fallbackY)
    {
        Vector3 origin = new Vector3(x, 12f, z);
        var hits = Physics.RaycastAll(origin, Vector3.down, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        float bestGroundY = float.NaN;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (!IsValidGroundHit(hit.transform))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            bestGroundY = hit.point.y;
        }

        if (float.IsFinite(bestGroundY))
            return bestGroundY;

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
            UpdateDistanceHud();
            yield return new WaitForSeconds(0.25f);
        }
    }

    IEnumerator RefreshSafeRunVisuals()
    {
        while (SceneManager.GetActiveScene().name == "CasinoRun")
        {
            RemoveLegacySceneBackdrop();
            if (EnableSafeRunArcadeObstacles)
            {
                ActivateSafeRunObstacles();
                EnsureRuntimeObstaclePool();
                ArrangeObstacles();
                ReplaceObstacleVisuals();
            }
            else
            {
                DeactivateSafeRunObstacles();
            }
            if (EnableSafeRunLaneDressing)
                SetLegacyStreetMeshesVisible(false);

            ArrangeCoins();
            UpdateDistanceHud();
            yield return new WaitForSeconds(0.35f);
        }
    }

    void ReplaceObstacleVisuals()
    {
        var arcadePrefab = GetArcadeMachinePrefab();
        var ropePrefab = GetRopeBarrierPrefab();

        var obstacles = FindObjectsOfType<CollisionDetect>(true);
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null)
                continue;

            var obstacleTransform = obstacle.transform;
            var box = obstacle.GetComponent<BoxCollider>();
            if (box == null)
                continue;

            bool wantsRope = IsRopeObstacle(box) && ropePrefab != null;
            var arcadeVisual = obstacleTransform.Find("ArcadeMachineVisual");
            var ropeVisual = obstacleTransform.Find("RopeBarrierVisual");

            var parentRenderer = obstacle.GetComponent<Renderer>();
            if (parentRenderer != null)
                parentRenderer.enabled = false;

            if (wantsRope)
            {
                if (arcadeVisual != null)
                    Destroy(arcadeVisual.gameObject);

                if (ropeVisual == null && ropePrefab != null)
                {
                    var rope = Instantiate(ropePrefab, obstacleTransform);
                    rope.name = "RopeBarrierVisual";
                    rope.transform.localPosition = Vector3.zero;
                    rope.transform.localRotation = Quaternion.identity;
                    rope.transform.localScale = Vector3.one;
                    PrepareDecorInstance(rope);
                    ropeVisual = rope.transform;
                }

                if (ropeVisual != null)
                {
                    ApplyImportedMaterialsOrFallback(ropeVisual.gameObject, new[] { GetRopeBarrierMaterial() });
                    FitRopeBarrierVisual(box, ropeVisual);
                }

                continue;
            }

            if (ropeVisual != null)
                Destroy(ropeVisual.gameObject);

            if (arcadeVisual != null || arcadePrefab == null)
                continue;

            var arcade = Instantiate(arcadePrefab, obstacleTransform);
            arcade.name = "ArcadeMachineVisual";
            arcade.transform.localPosition = Vector3.zero;
            arcade.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            arcade.transform.localScale = Vector3.one;
            PrepareDecorInstance(arcade);
            ApplyArcadeMachineRuntimeMaterials(arcade);
            FitVisualToObstacle(box, arcade.transform);
        }
    }

    bool IsRopeObstacle(BoxCollider box)
    {
        return box != null && box.size.y <= 0.9f;
    }

    void ReplacePlayerVisual()
    {
        var player = FindObjectOfType<PlayerMovement>(true);
        var prefab = GetPlayerCharacterPrefab();
        var materials = GetPlayerCharacterMaterials();
        if (player == null || prefab == null)
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

        ApplyImportedMaterialsOrFallback(visual, materials);
        FitVisualToPlayer(playerTransform, visual.transform);
        AttachPlayerAnimationController(player, visual);
        AttachPlayerFillLight(playerTransform);
    }

    void AttachPlayerFillLight(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        var existing = playerRoot.Find("RuntimePlayerFillLight");
        if (existing != null)
            Destroy(existing.gameObject);

        var fill = new GameObject("RuntimePlayerFillLight", typeof(Light));
        fill.transform.SetParent(playerRoot, false);
        fill.transform.localPosition = new Vector3(0f, 2.25f, -1.35f);
        var light = fill.GetComponent<Light>();
        light.type = LightType.Point;
        light.range = 7.6f;
        light.intensity = 2.25f;
        light.color = new Color(0.96f, 0.94f, 0.86f, 1f);
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForceVertex;
        light.bounceIntensity = 0f;
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

    void FitRopeBarrierVisual(BoxCollider box, Transform visual)
    {
        if (box == null || visual == null)
            return;

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        float targetWidth = Mathf.Max(box.bounds.size.x * 1.16f, 1.36f);
        float targetHeight = Mathf.Max(box.bounds.size.y * 1.68f, 1.06f);
        float targetDepth = Mathf.Max(box.bounds.size.z * 0.3f, 0.42f);
        Quaternion[] candidateRotations =
        {
            Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(-90f, 90f, 0f),
            Quaternion.Euler(90f, 90f, 0f),
            Quaternion.Euler(0f, 90f, 90f),
            Quaternion.Euler(0f, 90f, -90f),
            Quaternion.Euler(0f, 0f, 90f),
            Quaternion.Euler(0f, 180f, 90f),
            Quaternion.Euler(90f, 0f, 90f)
        };

        visual.localScale = Vector3.one;
        Quaternion bestRotation = candidateRotations[0];
        float bestScore = float.MaxValue;

        foreach (var candidate in candidateRotations)
        {
            visual.localScale = Vector3.one;
            visual.localRotation = candidate;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            Bounds candidateBounds = GetLocalRendererBounds(box.transform, renderers);
            float candidateWidth = Mathf.Max(candidateBounds.size.x, 0.01f);
            float candidateHeight = Mathf.Max(candidateBounds.size.y, 0.01f);
            float candidateDepth = Mathf.Max(candidateBounds.size.z, 0.01f);
            float candidateScale = Mathf.Min(targetWidth / candidateWidth, targetHeight / candidateHeight, targetDepth / candidateDepth);
            if (!float.IsFinite(candidateScale) || candidateScale <= 0f)
                continue;

            Vector3 scaledSize = candidateBounds.size * candidateScale;
            float widthError = Mathf.Abs(scaledSize.x - targetWidth) / targetWidth;
            float heightError = Mathf.Abs(scaledSize.y - targetHeight) / targetHeight;
            float depthError = Mathf.Abs(scaledSize.z - targetDepth) / targetDepth;
            float posturePenalty = scaledSize.y < scaledSize.x * 0.38f ? 0.85f : 0f;
            posturePenalty += scaledSize.x < scaledSize.y * 0.95f ? 0.45f : 0f;
            float score = widthError + heightError + (depthError * 1.4f) + posturePenalty;

            if (score < bestScore)
            {
                bestScore = score;
                bestRotation = candidate;
            }
        }

        visual.localRotation = bestRotation;
        renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds localBounds = GetLocalRendererBounds(box.transform, renderers);
        float width = Mathf.Max(localBounds.size.x, 0.01f);
        float height = Mathf.Max(localBounds.size.y, 0.01f);
        float depth = Mathf.Max(localBounds.size.z, 0.01f);
        float uniformScale = Mathf.Min(targetWidth / width, targetHeight / height, targetDepth / depth);
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visual.localScale = Vector3.one * uniformScale;
        renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds bestBounds = GetLocalRendererBounds(box.transform, renderers);
        visual.localPosition = new Vector3(
            -bestBounds.center.x,
            -bestBounds.min.y + 0.01f,
            -bestBounds.center.z);
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

        float targetHeight = capsule != null ? Mathf.Max(capsule.height * 1.2f, 1f) : 1.95f;
        float targetWidth = capsule != null ? Mathf.Max(capsule.radius * 2.15f, 0.72f) : 0.92f;
        float sourceHeight = Mathf.Max(visualBounds.size.y, 0.01f);
        float sourceWidth = Mathf.Max(Mathf.Max(visualBounds.size.x, visualBounds.size.z), 0.01f);
        float uniformScale = Mathf.Min(targetHeight / sourceHeight, targetWidth / sourceWidth);
        if (!float.IsFinite(uniformScale) || uniformScale <= 0f)
            uniformScale = 1f;

        visual.localScale = Vector3.one * uniformScale;

        renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds localBounds = GetLocalRendererBounds(playerRoot, renderers);
        float footOffset = capsule != null
            ? capsule.center.y - (capsule.height * 0.5f)
            : 0f;
        visual.localPosition = new Vector3(
            -localBounds.center.x,
            footOffset - localBounds.min.y + 0.08f,
            -localBounds.center.z + 0.02f);
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

    bool HasUsableImportedMaterials(GameObject visualRoot)
    {
        return HasUsableImportedMaterials(visualRoot, false);
    }

    bool HasUsableImportedMaterials(GameObject visualRoot, bool allowColorOnly)
    {
        if (visualRoot == null)
            return false;

        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                bool hasTexture = material.mainTexture != null;
                if (!hasTexture && material.HasProperty("_BaseMap"))
                    hasTexture = material.GetTexture("_BaseMap") != null;
                if (!hasTexture && material.HasProperty("_MainTex"))
                    hasTexture = material.GetTexture("_MainTex") != null;

                if (hasTexture)
                    return true;

                if (!allowColorOnly)
                    continue;

                bool hasVisibleColor = false;
                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    hasVisibleColor = color.a > 0.05f && color.maxColorComponent > 0.06f;
                }
                else if (material.HasProperty("_Color"))
                {
                    Color color = material.GetColor("_Color");
                    hasVisibleColor = color.a > 0.05f && color.maxColorComponent > 0.06f;
                }

                bool hasImportedName = !string.IsNullOrWhiteSpace(material.name)
                    && !material.name.StartsWith("Default-Material")
                    && !material.name.StartsWith("Runtime_");

                if (hasImportedName && hasVisibleColor)
                    return true;
            }
        }

        return false;
    }

    void ApplyImportedMaterialsOrFallback(GameObject visualRoot, Material[] fallbackMaterials)
    {
        ApplyImportedMaterialsOrFallback(visualRoot, fallbackMaterials, false);
    }

    void ApplyImportedMaterialsOrFallback(GameObject visualRoot, Material[] fallbackMaterials, bool allowColorOnly)
    {
        if (!HasUsableImportedMaterials(visualRoot, allowColorOnly))
            ApplyMaterials(visualRoot, fallbackMaterials);
    }

    void AttachPlayerAnimationController(PlayerMovement player, GameObject visual)
    {
        EnsurePlayerAnimator(visual);
        var clips = GetPlayerCharacterClips();
        var animator = visual.GetComponent<RuntimePlayerVisualAnimator>();
        if (animator == null)
            animator = visual.AddComponent<RuntimePlayerVisualAnimator>();

        animator.Initialize(player.transform, clips);
    }

    void EnsurePlayerAnimator(GameObject visual)
    {
        if (visual == null)
            return;

        Animator targetAnimator = visual.GetComponent<Animator>();
        Animator importedAnimator = null;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null)
                continue;

            if (targetAnimator == null)
                targetAnimator = animator;

            if (animator != targetAnimator)
                importedAnimator = animator;
        }

        Avatar avatar = GetPlayerCharacterAvatar();
        if (importedAnimator != null && importedAnimator.avatar != null)
            avatar = importedAnimator.avatar;
        else if (targetAnimator != null && targetAnimator.avatar != null)
            avatar = targetAnimator.avatar;

        if (targetAnimator == null)
            targetAnimator = visual.AddComponent<Animator>();

        targetAnimator.enabled = true;
        targetAnimator.applyRootMotion = false;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.updateMode = AnimatorUpdateMode.Normal;

        if (avatar != null)
            targetAnimator.avatar = avatar;
    }

    public bool TryHandleRevive(CollisionDetect source, Collider obstacle)
    {
        if (!EnableLuckySpinRevive || source == null || reviveUsed || reviveRunning)
            return false;

        if (reviveGroup == null || reviveTitle == null || reviveSubtitle == null || slotTexts == null || slotTexts.Length != 3)
            BuildReviveUi();

        if (reviveGroup == null || reviveTitle == null || reviveSubtitle == null || slotTexts == null || slotTexts.Length != 3)
            return false;

        StartCoroutine(ReviveRoutine(source, obstacle));
        return true;
    }

    IEnumerator ReviveRoutine(CollisionDetect source, Collider obstacle)
    {
        reviveRunning = true;
        reviveGroup.alpha = 1f;
        reviveGroup.interactable = true;
        reviveGroup.blocksRaycasts = true;
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
        reviveGroup.interactable = false;
        reviveGroup.blocksRaycasts = false;
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

    static GameObject GetBuildingOnePrefab()
    {
        if (buildingOnePrefab != null)
            return buildingOnePrefab;

        buildingOnePrefab = Resources.Load<GameObject>("Building1/building1");
        return buildingOnePrefab;
    }

    static GameObject GetBuildingTwoPrefab()
    {
        if (buildingTwoPrefab != null)
            return buildingTwoPrefab;

        buildingTwoPrefab = Resources.Load<GameObject>("buildings2/Building2");
        return buildingTwoPrefab;
    }

    static GameObject GetNeonSign24hPrefab()
    {
        if (neonSign24hPrefab != null)
            return neonSign24hPrefab;

        neonSign24hPrefab = Resources.Load<GameObject>("neonsign24h/uploads_files_867191_cgtrader_optimized_sign_neon_24h");
        return neonSign24hPrefab;
    }

    static GameObject GetNeonCardsPrefab()
    {
        if (neonCardsPrefab != null)
            return neonCardsPrefab;

        neonCardsPrefab = Resources.Load<GameObject>("neoncards/FBX");
        return neonCardsPrefab;
    }

    static GameObject GetPlayerCharacterPrefab()
    {
        if (playerCharacterPrefab != null)
            return playerCharacterPrefab;

        playerCharacterPrefab = Resources.Load<GameObject>("Character/Steve");
        return playerCharacterPrefab;
    }

    static Avatar GetPlayerCharacterAvatar()
    {
        if (playerCharacterAvatar != null)
            return playerCharacterAvatar;

        var loadedAssets = Resources.LoadAll<Object>("Character/Steve");
        foreach (var asset in loadedAssets)
        {
            if (asset is Avatar avatar)
            {
                playerCharacterAvatar = avatar;
                break;
            }
        }

        return playerCharacterAvatar;
    }

    static GameObject GetBitcoinCoinModelPrefab()
    {
        if (bitcoinCoinModelPrefab != null)
            return bitcoinCoinModelPrefab;

        bitcoinCoinModelPrefab = Resources.Load<GameObject>("BitcoinModel/BitcoinCoin");
        return bitcoinCoinModelPrefab;
    }

    static GameObject GetRopeBarrierPrefab()
    {
        if (ropeBarrierPrefab != null)
            return ropeBarrierPrefab;

        ropeBarrierPrefab = Resources.Load<GameObject>("rope_red/barrier");
        return ropeBarrierPrefab;
    }

    static Material[] GetPlayerCharacterMaterials()
    {
        if (playerCharacterMaterials != null)
            return playerCharacterMaterials;

        var bodyMaterial = GetPlayerCharacterBodyMaterial();
        if (bodyMaterial == null)
            return null;

        playerCharacterMaterials = new[] { bodyMaterial };
        return playerCharacterMaterials;
    }

    static Material GetPlayerCharacterBodyMaterial()
    {
        if (playerCharacterBodyMaterial != null)
            return playerCharacterBodyMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var diffuse = Resources.Load<Texture2D>("CharacterTextures/Ch28_1001_Diffuse");
        var normal = Resources.Load<Texture2D>("CharacterTextures/Ch28_1001_Normal");

        var material = new Material(shader);
        material.name = "Runtime_PlayerCharacter_Body";

        if (diffuse != null && material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", diffuse);
        if (diffuse != null && material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", diffuse);
        material.mainTexture = diffuse;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_BumpScale"))
                material.SetFloat("_BumpScale", 0.9f);
            material.EnableKeyword("_NORMALMAP");
        }

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.22f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.02f);

        playerCharacterBodyMaterial = material;
        return playerCharacterBodyMaterial;
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

    static Material GetCityBuildingBodyMaterial()
    {
        if (cityBuildingBodyMaterial != null)
            return cityBuildingBodyMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = "Runtime_CityBuildingBody";
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.11f, 0.16f, 0.22f, 1f));
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(0.11f, 0.16f, 0.22f, 1f));

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.08f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.05f, 0.08f, 0.12f, 1f));
        }

        cityBuildingBodyMaterial = material;
        return cityBuildingBodyMaterial;
    }

    static Material GetCityBuildingAccentCoolMaterial()
    {
        if (cityBuildingAccentCoolMaterial != null)
            return cityBuildingAccentCoolMaterial;

        cityBuildingAccentCoolMaterial = CreateGlowMaterial("Runtime_CityAccentCool", null, new Color(0.22f, 0.82f, 1f, 1f), 1.8f, false);
        return cityBuildingAccentCoolMaterial;
    }

    static Material GetCityBuildingAccentWarmMaterial()
    {
        if (cityBuildingAccentWarmMaterial != null)
            return cityBuildingAccentWarmMaterial;

        cityBuildingAccentWarmMaterial = CreateGlowMaterial("Runtime_CityAccentWarm", null, new Color(1f, 0.66f, 0.28f, 1f), 1.8f, false);
        return cityBuildingAccentWarmMaterial;
    }

    static Material GetNeonSignFallbackMaterial()
    {
        if (neonSignFallbackMaterial != null)
            return neonSignFallbackMaterial;

        var texture = Resources.Load<Texture2D>("neonsign24h/neon_signs_tile_c");
        if (texture == null)
            texture = Resources.Load<Texture2D>("neonsign24h/neon_signs_unique_o");

        neonSignFallbackMaterial = CreateGlowMaterial("Runtime_Neon24H", texture, new Color(0.22f, 0.82f, 1f, 1f), 2.35f, true);
        return neonSignFallbackMaterial;
    }

    static Material GetNeonCardsFallbackMaterial()
    {
        if (neonCardsFallbackMaterial != null)
            return neonCardsFallbackMaterial;

        var texture = Resources.Load<Texture2D>("neoncards/Heavily Scratched 1 With Dirt");
        neonCardsFallbackMaterial = CreateGlowMaterial("Runtime_NeonCards", texture, new Color(1f, 0.68f, 0.3f, 1f), 2.15f, false);
        return neonCardsFallbackMaterial;
    }

    static Material GetRopeBarrierMaterial()
    {
        if (ropeBarrierMaterial != null)
            return ropeBarrierMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var albedo = Resources.Load<Texture2D>("rope_red/barrier_Albedo");
        var normal = Resources.Load<Texture2D>("rope_red/barrier_Normal");
        var metallic = Resources.Load<Texture2D>("rope_red/barrier_Metallic");
        var occlusion = Resources.Load<Texture2D>("rope_red/barrier_AO");

        var material = new Material(shader);
        material.name = "Runtime_RopeBarrier";

        if (albedo != null && material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", albedo);
        if (albedo != null && material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", albedo);
        material.mainTexture = albedo;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        if (metallic != null && material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", metallic);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (occlusion != null && material.HasProperty("_OcclusionMap"))
            material.SetTexture("_OcclusionMap", occlusion);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.22f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.16f);

        ropeBarrierMaterial = material;
        return ropeBarrierMaterial;
    }

    static Material CreateGlowMaterial(string name, Texture2D texture, Color color, float emissionStrength, bool preferUnlit)
    {
        Shader shader = preferUnlit ? Shader.Find("Universal Render Pipeline/Unlit") : null;
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader);
        material.name = name;

        if (texture != null && material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (texture != null && material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        material.mainTexture = texture;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emissionStrength);
        }

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.46f);

        return material;
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

        playerCharacterClips = new[]
        {
            LoadPrimaryCharacterClip("Character/Steve", "take 001", "mixamo"),
            LoadPrimaryCharacterClip("Character/Steve-BigJump", "jump", "take 001", "mixamo"),
            LoadPrimaryCharacterClip("Character/Steve-Strafe", "strafe", "take 001", "mixamo"),
            LoadPrimaryCharacterClip("Character/Steve-Stumble-Backwards", "stumble", "back", "take 001", "mixamo")
        };
        return playerCharacterClips;
    }

    static AnimationClip LoadPrimaryCharacterClip(string resourcePath, params string[] preferredKeywords)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        AnimationClip bestClip = null;
        float bestScore = float.NegativeInfinity;
        var loadedAssets = Resources.LoadAll<Object>(resourcePath);
        if (loadedAssets == null || loadedAssets.Length == 0)
            return null;

        foreach (var asset in loadedAssets)
        {
            if (asset is not AnimationClip clip)
                continue;

            if (!IsUsableCharacterClip(clip))
                continue;

            float score = GetCharacterClipScore(clip, preferredKeywords);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestClip = clip;
        }

        return bestClip;
    }

    static bool IsUsableCharacterClip(AnimationClip clip)
    {
        if (clip == null)
            return false;

        string lowered = clip.name.ToLowerInvariant();
        return !lowered.Contains("__preview__");
    }

    static float GetCharacterClipScore(AnimationClip clip, string[] preferredKeywords)
    {
        if (clip == null)
            return float.NegativeInfinity;

        string lowered = clip.name.ToLowerInvariant();
        float clipLength = Mathf.Max(0f, clip.length);
        float score = 0f;
        score += Mathf.Clamp(clip.frameRate, 0f, 120f) * 0.08f;

        if (clipLength >= 0.35f && clipLength <= 3.5f)
            score += 18f;
        if (clipLength > 5f)
            score -= Mathf.Min(120f, (clipLength - 5f) * 22f);

        if (preferredKeywords != null)
        {
            for (int i = 0; i < preferredKeywords.Length; i++)
            {
                string keyword = preferredKeywords[i];
                if (string.IsNullOrWhiteSpace(keyword) || !lowered.Contains(keyword))
                    continue;

                score += 180f - (i * 22f);
                break;
            }
        }

        if (lowered.Contains("take 001"))
            score += 14f;
        if (lowered.Contains("mixamo"))
            score += 6f;

        return score;
    }
}

[DefaultExecutionOrder(1200)]
public sealed class RuntimeRunCameraFollow : MonoBehaviour
{
    Transform target;
    Vector3 localOffset;
    Vector3 velocity;
    Quaternion localRotation;
    bool initialized;

    [SerializeField] float positionSmoothTime = 0.03f;
    [SerializeField] float rotationSharpness = 24f;

    public void Initialize(Transform followTarget, Vector3 followLocalOffset, Quaternion followLocalRotation)
    {
        target = followTarget;
        initialized = target != null;
        velocity = Vector3.zero;

        if (!initialized)
        {
            enabled = false;
            return;
        }

        localOffset = followLocalOffset;
        localRotation = followLocalRotation;
        transform.position = target.TransformPoint(localOffset);
        transform.rotation = target.rotation * localRotation;
        enabled = true;
    }

    void LateUpdate()
    {
        if (!initialized || target == null)
            return;

        var animator = GetComponent<Animator>();
        if (animator != null && animator.enabled)
            return;

        Vector3 desiredPosition = target.TransformPoint(localOffset);
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            positionSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);

        Quaternion desiredRotation = target.rotation * localRotation;
        float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
    }
}

public static class RunProgressStore
{
    const string LevelOneCoinsKey = "CoinDash.HighScore.Level1Coins";
    const string LevelTwoDistanceKey = "CoinDash.HighScore.Level2Distance";

    public struct RunSummary
    {
        public int StageIndex;
        public int CoinsCollected;
        public int DistanceMeters;
        public int PrimaryValue;
        public int PreviousBest;
        public int BestValue;
        public bool IsNewRecord;
    }

    public static RunSummary RecordRun(int stageIndex, int coinsCollected, int distanceMeters)
    {
        int normalizedStage = Mathf.Clamp(stageIndex, 0, 1);
        int normalizedCoins = Mathf.Max(0, coinsCollected);
        int normalizedDistance = Mathf.Max(0, distanceMeters);
        int primaryValue = normalizedStage == 0 ? normalizedCoins : normalizedDistance;
        int previousBest = GetHighScore(normalizedStage);
        int bestValue = previousBest;
        bool isNewRecord = primaryValue > previousBest;

        if (isNewRecord)
        {
            bestValue = primaryValue;
            PlayerPrefs.SetInt(GetHighScoreKey(normalizedStage), bestValue);
            PlayerPrefs.Save();
        }

        return new RunSummary
        {
            StageIndex = normalizedStage,
            CoinsCollected = normalizedCoins,
            DistanceMeters = normalizedDistance,
            PrimaryValue = primaryValue,
            PreviousBest = previousBest,
            BestValue = bestValue,
            IsNewRecord = isNewRecord
        };
    }

    public static int GetHighScore(int stageIndex)
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(GetHighScoreKey(stageIndex), 0));
    }

    public static string GetStageLabel(int stageIndex)
    {
        return Mathf.Clamp(stageIndex, 0, 1) == 0 ? "LEVEL 1" : "LEVEL 2";
    }

    public static string GetPrimaryResultLabel(int stageIndex)
    {
        return Mathf.Clamp(stageIndex, 0, 1) == 0 ? "COINS COLLECTED" : "DISTANCE RUN";
    }

    public static string GetSecondaryResultLabel(int stageIndex)
    {
        return Mathf.Clamp(stageIndex, 0, 1) == 0 ? "DISTANCE" : "COINS";
    }

    public static string FormatPrimaryValue(int stageIndex, int value)
    {
        int normalized = Mathf.Max(0, value);
        return Mathf.Clamp(stageIndex, 0, 1) == 0 ? normalized.ToString() : $"{normalized} M";
    }

    public static string FormatSecondaryValue(int stageIndex, int coinsCollected, int distanceMeters)
    {
        return Mathf.Clamp(stageIndex, 0, 1) == 0
            ? $"{Mathf.Max(0, distanceMeters)} M"
            : Mathf.Max(0, coinsCollected).ToString();
    }

    static string GetHighScoreKey(int stageIndex)
    {
        return Mathf.Clamp(stageIndex, 0, 1) == 0 ? LevelOneCoinsKey : LevelTwoDistanceKey;
    }
}
