using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class GameScoreManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    // Every knob that determines what it takes to win the level, gathered in one
    // place: how many protons/electrons craft a coin, how many coins to win, how
    // many enemies to defeat, and how long a full day/night cycle lasts.
    [Header("Win Condition Tuning")]
    [SerializeField, Min(1)] private int protonsPerCoin = 1;
    [SerializeField, Min(1)] private int electronsPerCoin = 1;
    [SerializeField, Min(1)] private int coinsToWin = 2;
    [SerializeField, Min(1)] private int enemiesToWin = 2;
    [SerializeField, Min(1f)] private float dayNightCycleSeconds = 200f;
    [SerializeField] private DayNightManager dayNightManager;

    // Read-only accessors so other scripts (e.g. NpcDialogue) can quote the
    // live win-condition numbers instead of hardcoding copies that go stale
    // the moment these are re-tuned in the Inspector.
    public int ProtonsPerCoin => protonsPerCoin;
    public int ElectronsPerCoin => electronsPerCoin;
    public int CoinsToWin => coinsToWin;
    public int EnemiesToWin => enemiesToWin;
    public int CoinsCollected => coinsCollected;
    public int EnemiesDefeated => enemiesDefeated;
    public float TimeLimitSeconds => timeLimitSeconds;

    // Timer pressure: a hard countdown running in parallel with the win condition.
    // If it hits zero before the player has crafted enough coins and defeated
    // enough enemies, the level ends in a loss instead of dragging on forever.
    [Header("Timer Pressure")]
    [SerializeField, Min(10f)] private float timeLimitSeconds = 600f;

    // Hazard challenge: static "poison gas" trigger zones scattered near the
    // player spawn. Standing inside one chip-damages the player - the tick rate
    // is naturally rate-limited by PlayerHealth's own invincibility window, so no
    // extra timing logic is needed here.
    [Header("Hazards")]
    [SerializeField, Min(0)] private int poisonGasZoneCount = 3;
    [SerializeField, Min(1f)] private float poisonGasZoneRadius = 3.5f;
    [SerializeField, Min(1)] private int poisonGasDamagePerTick = 1;
    [SerializeField] private Color poisonGasColor = new Color(0.35f, 0.9f, 0.25f, 0.4f);

    [Header("Collectibles")]
    [SerializeField] private Vector3 collectibleAreaCenter = new Vector3(789f, 49.8f, 582f);
    [SerializeField] private Vector2 collectibleAreaSize = new Vector2(20f, 16f);
    [SerializeField] private GameObject[] gunPrefabs;
    [SerializeField] private string[] gunNames =
    {
        "Revolver 1",
        "Revolver 2",
        "Revolver 3",
        "Shotgun 1",
        "Shotgun 2"
    };

    // Only one proton and one electron ever exist in the world at once. The moment
    // one is collected, its replacement spawns within atomSpawnRadius of the player
    // - so supply is effectively unlimited (never blocks crafting toward
    // coinsToWin), it's just paced one pickup at a time instead of a big scattered
    // batch, and the compass always has exactly one unambiguous target to lock onto.
    [Header("Atom Collectibles")]
    [SerializeField, Min(1f)] private float atomSpawnRadius = 5f;
    [SerializeField] private GameObject atomOrbitPrefab; // Coin_A.fbx - larger disc, orbit ring
    [SerializeField] private GameObject atomCorePrefab;  // Coin_C.fbx - smaller disc, core
    [SerializeField] private Color protonColor = new Color(0.95f, 0.35f, 0.15f);
    [SerializeField] private Color electronColor = new Color(0.2f, 0.75f, 0.95f);

    // Same single-instance-respawn model as protons/electrons: exactly one enemy
    // exists at a time, and a replacement spawns between enemySafeZoneRadius and
    // enemySpawnRadius of the player, enemyRespawnDelaySeconds after the current
    // one is killed (see AddEnemyDefeat). enemySafeZoneRadius keeps enemies from
    // ever spawning right on top of the player - including at game start.
    // Fallback model names loaded from Assets/Resources/Enemies/*.obj whenever
    // enemyPrefabs isn't manually wired up in the Inspector.
    private static readonly string[] HuskyWolfResourceNames = { "Husky", "Wolf" };

    [Header("Enemy Spawning")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField, Min(1f)] private float enemySafeZoneRadius = 10f; // no enemy spawns closer than this to the player
    [SerializeField, Min(1f)] private float enemySpawnRadius = 18f; // outer edge of the spawn ring, beyond the safe zone
    [SerializeField, Min(0f)] private float enemyRespawnDelaySeconds = 10f;
    [SerializeField, Min(0f)] private float spawnPersonalSpace = 2f; // never spawn literally on top of the player (protons/electrons/vehicle)

    // Exactly one drivable vehicle spawns near the player at Awake, same
    // radius/personal-space convention as the enemy. If vehiclePrefab isn't
    // wired up (e.g. AutoSetup hasn't been re-run in the Editor yet), a
    // procedural fallback car is built instead - so a vehicle always exists
    // to see and drive, the same "prefab if assigned, else fallback shape"
    // convention used for guns and atom pickups.
    [Header("Vehicle Spawning")]
    [SerializeField] private GameObject vehiclePrefab;
    [SerializeField, Min(1f)] private float vehicleSpawnRadius = 10f;
    [SerializeField, Min(0.05f)] private float vehicleScale = 0.4f;

    // How far the player's own Cinemachine follow camera (PlayerFollowCamera,
    // a CinemachineVirtualCamera whose 3rd-Person-Follow body normally sits at
    // CameraDistance 4) is pulled back WHILE DRIVING. When the player gets in,
    // they (and PlayerCameraRoot, the camera's follow target) are parented to
    // the vehicle, so that exact same camera already follows the car - this
    // just widens its distance so the whole vehicle and the road ahead stay in
    // frame, then restores the on-foot distance on exit. Set this in the
    // Inspector before pressing Play; VehicleController reads it on enter.
    [Header("Vehicle Camera")]
    [SerializeField, Min(0.1f)] private float drivingCameraDistance = 9f;

    public float DrivingCameraDistance => drivingCameraDistance;

    [Header("Coin Crafting Visuals")]
    [SerializeField] private Color coinColor = new Color(1f, 0.82f, 0.18f);

    [Header("UI")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color markerColor = new Color(1f, 0.82f, 0.18f, 1f);

    [Header("Weapon Hold Tuning")]
    [SerializeField] private Vector3 equippedGunLocalPosition = new Vector3(0.08f, 0.04f, 0.02f);
    [SerializeField] private Vector3 equippedGunLocalEulerAngles = new Vector3(0f, 135f, 90f);
    [SerializeField] private float prefabGunScale = 0.4f;
    [SerializeField] private float fallbackGunScale = 0.65f;
    [SerializeField] private bool showWeaponHoldGizmo = true;
    [SerializeField] private float weaponHoldGizmoSize = 0.18f;
    [SerializeField] private bool showWeaponHoldPreview = true;
    [SerializeField] private Color weaponHoldPreviewColor = new Color(1f, 0.9f, 0.1f, 0.85f);

    private Text scoreText;
    private Text enemyText;
    private Text weaponText;
    private Text chestText;
    private Text craftStatusText;
    private Text promptText;
    private Text dialogueText;
    private Text protonCountText;
    private Text electronCountText;
    private Text coinCountText;
    private Text timerText;
    private Text enemyRespawnText;
    private float nextEnemySpawnTime = -1f;
    private GameObject inventoryPanel;
    private GameObject instructionsPanel;
    private Text instructionsBodyText;
    private PlayerGunController gunController;
    private Transform rightHand;
    private GameObject equippedGun;
    private GameObject weaponHoldPreview;
    private GunSlot[] gunSlots;
    private ThirdPersonController playerController;
    private int enemiesDefeated;
    private int protonsCollected;
    private int electronsCollected;
    private int coinsCollected;
    private int equippedGunIndex = -1;
    private bool hasWon;
    private bool hasLost;
    private float remainingTime;
    private int protonSpawnCounter;
    private int electronSpawnCounter;
    private Transform enemiesParent;

    private static readonly Dictionary<Color, Sprite> circleIconCache = new Dictionary<Color, Sprite>();
    private static readonly Dictionary<Color, Sprite> arrowIconCache = new Dictionary<Color, Sprite>();

    [Header("Inventory Slot Colors")]
    [SerializeField] private Color slotBorderColor = new Color(0.92f, 0.6f, 0.22f, 1f);
    [SerializeField] private Color slotEquippedBorderColor = new Color(0.3f, 0.9f, 0.42f, 1f);
    [SerializeField] private Color slotFillColor = new Color(0.14f, 0.16f, 0.22f, 0.95f);

    private readonly Color[] gunColors =
    {
        new Color(0.95f, 0.22f, 0.18f),
        new Color(0.18f, 0.52f, 0.95f),
        new Color(0.18f, 0.78f, 0.34f),
        new Color(0.95f, 0.76f, 0.18f),
        new Color(0.68f, 0.32f, 0.95f)
    };

    // One slot per unique gun that exists in the game. A slot is "empty" until its
    // gun is acquired, after which it shows a live 3D preview of that gun's prefab.
    private sealed class GunSlot
    {
        public GameObject Prefab;
        public string Name;
        public Color Color;
        public bool Acquired;
        public Image Frame;
        public GameObject IconRoot;
        public Text NameLabel;
    }

    private int GunSlotCount => Mathf.Max(5, gunNames != null ? gunNames.Length : 0);

    private int AcquiredCount
    {
        get
        {
            int count = 0;
            if (gunSlots != null)
            {
                foreach (GunSlot slot in gunSlots)
                {
                    if (slot.Acquired)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    private void Awake()
    {
        if (player == null)
        {
            ThirdPersonController controller = FindAnyObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                player = controller.transform;
            }
        }

        remainingTime = timeLimitSeconds;

        SetupPlayerWeaponController();
        InitializeGunSlots();
        SyncDayNightCycle();
        BuildUi();
        SpawnAtoms();
        SpawnGunPickups();
        SpawnPoisonGasZones();
        SpawnInitialEnemy();
        SpawnVehicle();
        // SpawnNpc(); // Removed static guide NPC since AutoSetup handles NPCs now
        RefreshStatusTexts();
        UpdateWeaponText("None");
        UpdateChestText();
        RefreshInventorySlots();
    }

    private void InitializeGunSlots()
    {
        int count = GunSlotCount;
        gunSlots = new GunSlot[count];
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = gunPrefabs != null && i < gunPrefabs.Length ? gunPrefabs[i] : null;
            gunSlots[i] = new GunSlot
            {
                Prefab = prefab,
                Name = GetGunName(i, prefab),
                Color = GetGunColor(i),
                Acquired = false
            };
        }
    }

    private void Update()
    {
        ApplyEquippedGunHoldTransform();
        TickTimer();
        RefreshStatusTexts();
        CheckWinCondition();
        UpdateWeaponHoldPreview();

        if (WasInventoryKeyPressed())
        {
            ToggleInventory();
        }

        if (WasCraftKeyPressed())
        {
            CraftCoin();
        }

        // E always pulls up the Instructions panel. Talking to an NPC uses its
        // own key (N, see NpcDialogue) so the two never collide.
        if (WasInstructionsKeyPressed())
        {
            ToggleInstructions();
        }

        if (gunSlots != null)
        {
            int slotCount = Mathf.Min(gunSlots.Length, 9);
            for (int i = 0; i < slotCount; i++)
            {
                if (WasNumberKeyPressed(i + 1))
                {
                    ToggleEquipSlot(i);
                    break;
                }
            }
        }
    }

    public void AddProton()
    {
        if (hasWon || hasLost) return;

        protonsCollected++;
        RefreshInventorySlots();
        SpawnSingleProton();
    }

    public void AddElectron()
    {
        if (hasWon || hasLost) return;

        electronsCollected++;
        RefreshInventorySlots();
        SpawnSingleElectron();
    }

    // Combines protons + electrons into a Coin. Only crafting a coin this way
    // ever increments coinsCollected - raw protons/electrons don't count toward winning.
    public void CraftCoin()
    {
        if (hasWon || hasLost)
        {
            return;
        }

        if (protonsCollected < protonsPerCoin || electronsCollected < electronsPerCoin)
        {
            ShowDialogue($"Need {protonsPerCoin} protons and {electronsPerCoin} electrons to craft a coin. You have {protonsCollected} protons, {electronsCollected} electrons.");
            ShowCraftStatus($"Can't craft coin: need {protonsPerCoin}P + {electronsPerCoin}E (have {protonsCollected}P, {electronsCollected}E)", new Color(0.95f, 0.35f, 0.25f));
            return;
        }

        protonsCollected -= protonsPerCoin;
        electronsCollected -= electronsPerCoin;
        coinsCollected++;

        UpdateScoreText();
        RefreshInventorySlots();
        ShowDialogue($"Crafted a coin! Coins: {coinsCollected}/{coinsToWin}");
        ShowCraftStatus($"+1 Coin crafted from {protonsPerCoin} protons + {electronsPerCoin} electrons! Coins: {coinsCollected}/{coinsToWin}", coinColor);
        CheckWinCondition();
    }

    public void AddEnemyDefeat()
    {
        if (hasWon || hasLost)
        {
            return;
        }

        enemiesDefeated++;
        UpdateEnemyText();
        CheckWinCondition();

        // Keep exactly one enemy alive in the world, but give the player a
        // breather: its replacement spawns enemyRespawnDelaySeconds after
        // this one dies, rather than instantly.
        if (!hasWon && !hasLost)
        {
            CancelInvoke(nameof(SpawnReplacementEnemy));
            Invoke(nameof(SpawnReplacementEnemy), enemyRespawnDelaySeconds);
            nextEnemySpawnTime = Time.time + enemyRespawnDelaySeconds;
        }
    }

    private void CheckWinCondition()
    {
        if (hasWon || hasLost || coinsCollected < coinsToWin || enemiesDefeated < enemiesToWin)
        {
            return;
        }

        hasWon = true;
        GameManager.LastOutcome = GameManager.Outcome.Win;
        ShowDialogue($"LEVEL CLEAR! You crafted {coinsToWin} coins and defeated {enemiesToWin} enemies.");
        Invoke(nameof(LoadRestartScene), 4f);
    }

    // Timer pressure: ticks the countdown every frame while the level is still in
    // play, and forces a loss the moment it runs out - a hard failure state distinct
    // from running out of health, so the player can't stall indefinitely.
    private void TickTimer()
    {
        if (hasWon || hasLost)
        {
            return;
        }

        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        if (remainingTime <= 0f)
        {
            TriggerTimeUp();
        }
    }

    private void TriggerTimeUp()
    {
        hasLost = true;
        GameManager.LastOutcome = GameManager.Outcome.Lose;
        ShowDialogue("TIME'S UP! You ran out of time before finishing the objective.");
        Invoke(nameof(LoadRestartScene), 3f);
    }

    private void LoadRestartScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("RestartScene");
    }

    public void EquipGun(GameObject gunPrefab, string weaponName, Color weaponColor)
    {
        int slotIndex = GetSlotIndex(weaponName);
        if (slotIndex < 0)
        {
            return;
        }

        GunSlot slot = gunSlots[slotIndex];
        if (!slot.Acquired)
        {
            slot.Acquired = true;
            if (slot.Prefab == null)
            {
                slot.Prefab = gunPrefab;
            }

            RefreshInventorySlots();
            ShowDialogue($"Acquired {slot.Name}. Chest: {AcquiredCount} unique guns acquired.");
            UpdateChestText();
        }
        else
        {
            ShowDialogue($"{slot.Name} is already in your chest. Unique gun count stays {AcquiredCount}.");
        }

        EquipSlot(slotIndex, false);
    }

    // Clicking a slot (or pressing its number) equips the gun, or unequips it if it
    // is already the equipped one so the player becomes unarmed.
    private void ToggleEquipSlot(int slotIndex)
    {
        if (gunSlots == null || slotIndex < 0 || slotIndex >= gunSlots.Length)
        {
            return;
        }

        if (!gunSlots[slotIndex].Acquired)
        {
            ShowDialogue($"Slot {slotIndex + 1} is empty. Collect that weapon first.");
            return;
        }

        if (slotIndex == equippedGunIndex)
        {
            Dequip();
            return;
        }

        EquipSlot(slotIndex);
    }

    private void EquipSlot(int slotIndex, bool showMessage = true)
    {
        if (gunSlots == null || slotIndex < 0 || slotIndex >= gunSlots.Length || !gunSlots[slotIndex].Acquired)
        {
            ShowDialogue($"No gun in slot {slotIndex + 1}. Press Y to view the chest inventory.");
            return;
        }

        GunSlot slot = gunSlots[slotIndex];
        equippedGunIndex = slotIndex;
        EquipGunModel(slot.Prefab, slot.Name, slot.Color);
        UpdateWeaponText(slot.Name);
        RefreshInventorySlots();

        if (showMessage)
        {
            ShowDialogue($"Equipped {slot.Name}. Press F to shoot.");
        }
    }

    private void Dequip()
    {
        if (equippedGun != null)
        {
            Destroy(equippedGun);
            equippedGun = null;
        }

        equippedGunIndex = -1;
        if (gunController != null)
        {
            gunController.SetEquippedGun(null);
        }

        UpdateWeaponText("None (holstered)");
        RefreshInventorySlots();
        ShowDialogue("Holstered your weapon. Click the slot again or press 1-5 to draw it.");
    }

    private void EquipGunModel(GameObject gunPrefab, string weaponName, Color weaponColor)
    {
        if (player == null)
        {
            return;
        }

        if (rightHand == null)
        {
            rightHand = FindRightHand(player);
        }

        Transform holdTarget = rightHand != null ? rightHand : player;
        if (equippedGun != null)
        {
            Destroy(equippedGun);
        }

        HideWeaponHoldPreview();

        equippedGun = gunPrefab != null ? Instantiate(gunPrefab, holdTarget) : CreateFallbackGun(holdTarget, weaponName);
        equippedGun.name = $"Equipped {weaponName}";
        ApplyEquippedGunHoldTransform(gunPrefab != null);
        DisableGameplayPhysics(equippedGun);
        ApplyColorToRenderers(equippedGun, weaponColor, $"{weaponName} Equipped Color");

        if (gunController == null)
        {
            SetupPlayerWeaponController();
        }

        gunController.SetEquippedGun(equippedGun.transform, weaponColor, equippedGunIndex);
    }

    private void ApplyEquippedGunHoldTransform()
    {
        if (equippedGun == null)
        {
            return;
        }

        ApplyEquippedGunHoldTransform(!equippedGun.name.Contains("Runtime Model"));
    }

    private void ApplyEquippedGunHoldTransform(bool usesPrefabScale)
    {
        if (equippedGun == null)
        {
            return;
        }

        equippedGun.transform.localPosition = equippedGunLocalPosition;
        equippedGun.transform.localRotation = Quaternion.Euler(equippedGunLocalEulerAngles);
        equippedGun.transform.localScale = Vector3.one * (usesPrefabScale ? prefabGunScale : fallbackGunScale);
        if (gunController != null)
        {
            gunController.RefreshEquippedGunRestPose();
        }
    }

    public void SetPrompt(string message)
    {
        if (promptText != null)
        {
            promptText.text = message;
        }
    }

    public void ClearPrompt(string message)
    {
        if (promptText != null && promptText.text == message)
        {
            promptText.text = string.Empty;
        }
    }

    // How long a dialogue line (NPC talk, craft/win/loss messages, etc.) stays
    // on screen before auto-clearing, mirroring ShowCraftStatus's pattern so
    // old lines don't linger indefinitely and get confused with new ones.
    private const float DialogueDisplaySeconds = 5f;

    public void ShowDialogue(string message)
    {
        if (dialogueText != null)
        {
            dialogueText.text = message;
        }

        CancelInvoke(nameof(ClearDialogue));
        Invoke(nameof(ClearDialogue), DialogueDisplaySeconds);
    }

    private void ClearDialogue()
    {
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }
    }

    // A short-lived, colored status line dedicated to coin crafting (success or
    // failure), separate from the general bottom-of-screen dialogue log so a craft
    // result is always clearly visible near the Coins counter, then auto-clears.
    private void ShowCraftStatus(string message, Color color)
    {
        if (craftStatusText == null)
        {
            return;
        }

        craftStatusText.text = message;
        craftStatusText.color = color;

        CancelInvoke(nameof(ClearCraftStatus));
        Invoke(nameof(ClearCraftStatus), 2.5f);
    }

    private void ClearCraftStatus()
    {
        if (craftStatusText != null)
        {
            craftStatusText.text = string.Empty;
        }
    }

    private void BuildUi()
    {
        Canvas canvas = new GameObject("Gameplay HUD Canvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        scoreText = CreateText(canvas.transform, "Coin Status Text", $"Coins: 0/{coinsToWin}", 22, TextAnchor.UpperLeft, new Vector2(20f, -18f), new Vector2(240f, 34f));
        enemyText = CreateText(canvas.transform, "Enemy Status Text", $"Enemies: 0/{enemiesToWin}", 22, TextAnchor.UpperLeft, new Vector2(20f, -52f), new Vector2(260f, 34f));
        weaponText = CreateText(canvas.transform, "Weapon Text", "Weapon: None", 20, TextAnchor.UpperLeft, new Vector2(20f, -86f), new Vector2(340f, 34f));
        chestText = CreateText(canvas.transform, "Chest Status Text", "Chest: 0 unique guns (Press Y)", 20, TextAnchor.UpperLeft, new Vector2(20f, -118f), new Vector2(360f, 34f));
        craftStatusText = CreateText(canvas.transform, "Craft Status Text", string.Empty, 18, TextAnchor.UpperLeft, new Vector2(20f, -150f), new Vector2(420f, 30f));
        timerText = CreateText(canvas.transform, "Timer Text", FormatTime(timeLimitSeconds), 22, TextAnchor.UpperLeft, new Vector2(20f, -184f), new Vector2(260f, 34f));
        promptText = CreateText(canvas.transform, "Interaction Prompt", string.Empty, 24, TextAnchor.LowerCenter, new Vector2(0f, 92f), new Vector2(760f, 42f));
        dialogueText = CreateText(canvas.transform, "Dialogue Text", string.Empty, 18, TextAnchor.LowerCenter, new Vector2(0f, 34f), new Vector2(940f, 64f));
        CreateText(canvas.transform, "Purpose Text", "Press N to talk to an NPC  •  Press E to pull up all information", 18, TextAnchor.LowerRight, new Vector2(-20f, 20f), new Vector2(420f, 36f));
        CreateInventoryPanel(canvas.transform);
        CreateInstructionsPanel(canvas.transform);
        CreateCompass(canvas.transform);
        enemyRespawnText = CreateText(canvas.transform, "Enemy Respawn Timer", "", 16, TextAnchor.UpperCenter, new Vector2(0f, -96f), new Vector2(300f, 22f));
        enemyRespawnText.color = markerColor;
    }

    private void CreateInventoryPanel(Transform canvasTransform)
    {
        int count = gunSlots != null ? gunSlots.Length : GunSlotCount;
        const int perRow = 5;
        const float cell = 96f;
        const float spacing = 12f;
        const float padding = 18f;
        const float titleHeight = 46f;
        const float footerHeight = 36f;
        const float sectionLabelHeight = 26f;
        const int resourceSlotCount = 3;
        const float craftButtonHeight = 40f;
        const float craftButtonGap = 10f;

        int columns = Mathf.Clamp(count, 1, perRow);
        int rows = Mathf.CeilToInt(count / (float)columns);
        float gunGridWidth = columns * cell + (columns - 1) * spacing;
        float gunGridHeight = rows * cell + (rows - 1) * spacing;
        float resourceGridWidth = resourceSlotCount * cell + (resourceSlotCount - 1) * spacing;
        const float resourceGridHeight = cell;

        float gridWidth = Mathf.Max(gunGridWidth, resourceGridWidth);
        float panelWidth = gridWidth + padding * 2f;
        float panelHeight = gunGridHeight + resourceGridHeight + craftButtonGap + craftButtonHeight + padding * 2f + titleHeight + footerHeight + sectionLabelHeight;

        RectTransform panel = CreatePanel(canvasTransform, "Inventory Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelWidth, panelHeight));
        panel.pivot = new Vector2(0.5f, 0.5f);
        inventoryPanel = panel.gameObject;

        Text title = CreateText(panel, "Inventory Title", "Chest Inventory", 24, TextAnchor.UpperCenter, new Vector2(0f, -12f), new Vector2(panelWidth - 24f, 30f));
        title.color = new Color(1f, 0.92f, 0.55f);

        GameObject gridGo = new GameObject("Inventory Grid");
        gridGo.transform.SetParent(panel, false);
        RectTransform gridRect = gridGo.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = new Vector2(0f, -titleHeight);
        gridRect.sizeDelta = new Vector2(gunGridWidth, gunGridHeight);

        GridLayoutGroup layout = gridGo.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(cell, cell);
        layout.spacing = new Vector2(spacing, spacing);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;

        for (int i = 0; i < count; i++)
        {
            CreateInventorySlot(gridRect, i);
        }

        // "Resources" section: static, non-interactive Proton/Electron counters,
        // laid out directly under the gun grid.
        Text resourceLabel = CreateText(panel, "Resource Section Label", "Resources", 16, TextAnchor.UpperCenter,
            new Vector2(0f, -(titleHeight + gunGridHeight + 6f)), new Vector2(panelWidth - 24f, sectionLabelHeight));
        resourceLabel.color = new Color(1f, 0.92f, 0.55f, 0.85f);

        GameObject resourceGridGo = new GameObject("Resource Grid");
        resourceGridGo.transform.SetParent(panel, false);
        RectTransform resourceGridRect = resourceGridGo.AddComponent<RectTransform>();
        resourceGridRect.anchorMin = new Vector2(0.5f, 1f);
        resourceGridRect.anchorMax = new Vector2(0.5f, 1f);
        resourceGridRect.pivot = new Vector2(0.5f, 1f);
        resourceGridRect.anchoredPosition = new Vector2(0f, -(titleHeight + gunGridHeight + sectionLabelHeight));
        resourceGridRect.sizeDelta = new Vector2(resourceGridWidth, resourceGridHeight);

        GridLayoutGroup resourceLayout = resourceGridGo.AddComponent<GridLayoutGroup>();
        resourceLayout.cellSize = new Vector2(cell, cell);
        resourceLayout.spacing = new Vector2(spacing, spacing);
        resourceLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        resourceLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        resourceLayout.childAlignment = TextAnchor.UpperCenter;
        resourceLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        resourceLayout.constraintCount = resourceSlotCount;

        CreateResourceSlot(resourceGridRect, "Proton", protonColor);
        CreateResourceSlot(resourceGridRect, "Electron", electronColor);
        CreateResourceSlot(resourceGridRect, "Coin", coinColor);

        CreateCraftButton(panel, new Vector2(0f, -(titleHeight + gunGridHeight + sectionLabelHeight + resourceGridHeight + craftButtonGap)), new Vector2(gridWidth, craftButtonHeight));

        Text footer = CreateText(panel, "Inventory Footer", $"Click or press 1-{Mathf.Min(count, 9)} to equip  •  click equipped gun to holster  •  Y to close", 14, TextAnchor.LowerCenter, new Vector2(0f, 10f), new Vector2(panelWidth - 24f, 26f));
        footer.color = new Color(1f, 1f, 1f, 0.8f);

        inventoryPanel.SetActive(false);
    }

    // A single always-available "how to play" screen: press E anywhere to pull
    // up every control and the win condition in one place (talking to an NPC
    // uses its own key, N, so the two never collide). The body text is rebuilt
    // each time it opens so it always reflects the live win-condition tuning
    // and current progress rather than a copy that could go stale.
    private void CreateInstructionsPanel(Transform canvasTransform)
    {
        const float panelWidth = 560f;
        const float panelHeight = 520f;
        const float titleHeight = 46f;
        const float footerHeight = 36f;

        RectTransform panel = CreatePanel(canvasTransform, "Instructions Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelWidth, panelHeight));
        panel.pivot = new Vector2(0.5f, 0.5f);
        instructionsPanel = panel.gameObject;

        Text title = CreateText(panel, "Instructions Title", "How To Play", 26, TextAnchor.UpperCenter, new Vector2(0f, -12f), new Vector2(panelWidth - 24f, 32f));
        title.color = new Color(1f, 0.92f, 0.55f);

        instructionsBodyText = CreateText(panel, "Instructions Body", string.Empty, 16, TextAnchor.UpperLeft,
            new Vector2(20f, -titleHeight), new Vector2(panelWidth - 40f, panelHeight - titleHeight - footerHeight - 12f));
        instructionsBodyText.color = new Color(1f, 1f, 1f, 0.92f);

        Text footer = CreateText(panel, "Instructions Footer", "Press E to close", 14, TextAnchor.LowerCenter, new Vector2(0f, 10f), new Vector2(panelWidth - 24f, footerHeight));
        footer.color = new Color(1f, 1f, 1f, 0.8f);

        instructionsPanel.SetActive(false);
    }

    private void ToggleInstructions()
    {
        if (instructionsPanel == null)
        {
            return;
        }

        bool show = !instructionsPanel.activeSelf;
        if (show)
        {
            RefreshInstructionsText();
        }

        instructionsPanel.SetActive(show);
    }

    private void RefreshInstructionsText()
    {
        if (instructionsBodyText == null)
        {
            return;
        }

        instructionsBodyText.text = BuildInstructionsText();
    }

    // Every number here is read straight from the live fields/properties
    // (protonsPerCoin, electronsPerCoin, coinsToWin, enemiesToWin, the
    // current counts, remainingTime) so this screen can never drift out of
    // sync with how the win condition is actually tuned in the Inspector.
    private string BuildInstructionsText()
    {
        int equipSlotCount = Mathf.Min(gunSlots != null ? gunSlots.Length : GunSlotCount, 9);

        return
            "CONTROLS\n" +
            "WASD - Move   |   Shift - Sprint   |   Space - Jump\n" +
            "Left Click / F - Shoot equipped gun\n" +
            $"1-{equipSlotCount} - Equip/holster a gun from your chest\n" +
            "Y - Open/close Chest Inventory\n" +
            $"B - Craft a Coin ({protonsPerCoin} Protons + {electronsPerCoin} Electrons)\n" +
            "U - Cycle compass target (Enemy / Proton / Electron / Vehicle)\n" +
            "I - Enter/exit the nearby vehicle (drives faster than sprinting)\n" +
            "N - Talk to a nearby NPC\n" +
            "Q - Toggle enemy mode between Wander and Chase (shown lower-left)\n" +
            "E - Open/close this screen\n\n" +
            "HOW TO WIN\n" +
            $"- Craft {coinsToWin} Coins ({protonsPerCoin} Protons + {electronsPerCoin} Electrons each)\n" +
            $"- Defeat {enemiesToWin} enemies\n" +
            "- Finish both before the clock hits zero\n\n" +
            "WATCH OUT\n" +
            "- Poison gas zones chip away your health while you stand in them\n" +
            "- A new enemy spawns near you shortly after you defeat the current one\n\n" +
            "CURRENT PROGRESS\n" +
            $"Coins {coinsCollected}/{coinsToWin}   Enemies {enemiesDefeated}/{enemiesToWin}\n" +
            $"Protons {protonsCollected}   Electrons {electronsCollected}\n" +
            $"Time left: {FormatTime(remainingTime)}";
    }

    private void CreateInventorySlot(Transform parent, int slotIndex)
    {
        GunSlot slot = gunSlots[slotIndex];

        GameObject slotGo = new GameObject($"Slot {slotIndex + 1} - {slot.Name}");
        slotGo.transform.SetParent(parent, false);
        Image frame = slotGo.AddComponent<Image>();
        frame.color = slotBorderColor;
        slot.Frame = frame;

        // Click the slot to equip its gun, or click the equipped gun to holster it.
        Button button = slotGo.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.None;
        int clickedIndex = slotIndex;
        button.onClick.AddListener(() => ToggleEquipSlot(clickedIndex));

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(slotGo.transform, false);
        Image fill = fillGo.AddComponent<Image>();
        fill.color = slotFillColor;
        fill.raycastTarget = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(5f, 5f);
        fillRect.offsetMax = new Vector2(-5f, -5f);

        // Static gun icon (a simple pistol silhouette drawn from UI rectangles),
        // tinted with the gun's color. Shown only once the gun is acquired.
        GameObject iconRoot = new GameObject("Gun Icon");
        iconRoot.transform.SetParent(fillGo.transform, false);
        RectTransform iconRect = iconRoot.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        BuildGunIcon(iconRoot.transform, slot.Color);
        iconRoot.SetActive(false);
        slot.IconRoot = iconRoot;

        Text nameLabel = CreateText(slotGo.transform, "Name", slot.Name, 11, TextAnchor.LowerCenter, new Vector2(0f, 4f), new Vector2(90f, 16f));
        nameLabel.color = new Color(1f, 1f, 1f, 0.85f);
        nameLabel.raycastTarget = false;
        nameLabel.enabled = false;
        slot.NameLabel = nameLabel;

        Text number = CreateText(slotGo.transform, "Number", (slotIndex + 1).ToString(), 15, TextAnchor.UpperLeft, new Vector2(6f, -4f), new Vector2(26f, 20f));
        number.color = new Color(1f, 1f, 1f, 0.85f);
        number.raycastTarget = false;
    }

    // Display-only resource slot (Proton/Electron): unlike gun slots these are always
    // visible, never "acquired"-gated, and show a live count instead of a name label.
    private void CreateResourceSlot(Transform parent, string label, Color color)
    {
        GameObject slotGo = new GameObject($"Resource Slot - {label}");
        slotGo.transform.SetParent(parent, false);
        Image frame = slotGo.AddComponent<Image>();
        frame.color = slotBorderColor;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(slotGo.transform, false);
        Image fill = fillGo.AddComponent<Image>();
        fill.color = slotFillColor;
        fill.raycastTarget = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(5f, 5f);
        fillRect.offsetMax = new Vector2(-5f, -5f);

        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(fillGo.transform, false);
        Image icon = iconGo.AddComponent<Image>();
        icon.sprite = GetOrCreateCircleSprite(color);
        icon.color = Color.white;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.6f);
        iconRect.anchorMax = new Vector2(0.5f, 0.6f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(40f, 40f);
        iconRect.anchoredPosition = Vector2.zero;

        Text countText = CreateText(slotGo.transform, "Count", "x0", 15, TextAnchor.LowerCenter, new Vector2(0f, 6f), new Vector2(90f, 18f));
        countText.color = new Color(1f, 1f, 1f, 0.9f);
        countText.raycastTarget = false;

        Text nameLabel = CreateText(slotGo.transform, "Name", label, 11, TextAnchor.UpperCenter, new Vector2(0f, -4f), new Vector2(90f, 14f));
        nameLabel.color = new Color(1f, 1f, 1f, 0.7f);
        nameLabel.raycastTarget = false;

        if (label == "Proton")
        {
            protonCountText = countText;
        }
        else if (label == "Electron")
        {
            electronCountText = countText;
        }
        else
        {
            coinCountText = countText;
        }
    }

    // Clickable button that crafts a Coin from protonsPerCoin protons + electronsPerCoin
    // electrons (same action as pressing B), placed directly under the Resources row.
    private void CreateCraftButton(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonGo = new GameObject("Craft Coin Button");
        buttonGo.transform.SetParent(parent, false);
        Image background = buttonGo.AddComponent<Image>();
        background.color = slotBorderColor;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(CraftCoin);

        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text label = CreateText(buttonGo.transform, "Label", $"Craft Coin ({protonsPerCoin}P + {electronsPerCoin}E) — B", 14, TextAnchor.MiddleCenter, Vector2.zero, size);
        label.raycastTarget = false;
    }

    // Draws a small pistol shape from flat UI rectangles, centered in the slot.
    private static void BuildGunIcon(Transform parent, Color color)
    {
        Color body = color;
        Color grip = color * 0.7f;
        grip.a = 1f;

        AddGunIconPiece(parent, "Slide", new Vector2(2f, 9f), new Vector2(52f, 15f), 0f, body);
        AddGunIconPiece(parent, "Barrel", new Vector2(-30f, 9f), new Vector2(14f, 8f), 0f, body);
        AddGunIconPiece(parent, "Grip", new Vector2(14f, -14f), new Vector2(15f, 26f), 20f, grip);
        AddGunIconPiece(parent, "Trigger Guard", new Vector2(2f, -6f), new Vector2(9f, 12f), 0f, grip);
    }

    private static void AddGunIconPiece(Transform parent, string pieceName, Vector2 anchoredPosition, Vector2 size, float rotationZ, Color color)
    {
        GameObject piece = new GameObject(pieceName);
        piece.transform.SetParent(parent, false);
        Image image = piece.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
    }

    // Procedurally draws a small filled circle into a Texture2D and wraps it in a
    // Sprite, caching the result per color so it's only generated once.
    private static Sprite GetOrCreateCircleSprite(Color color)
    {
        if (circleIconCache.TryGetValue(color, out Sprite cached) && cached != null)
        {
            return cached;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = $"Runtime Circle Icon {color}";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                texture.SetPixel(x, y, dist <= radius ? color : clear);
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = $"Runtime Circle Sprite {color}";

        circleIconCache[color] = sprite;
        return sprite;
    }

    // Procedurally draws a filled, outlined arrow into a Texture2D and wraps it in a
    // Sprite, caching per color. Shape is a classic "shaft + head" arrow (like a
    // stretched → glyph, but pointing up so 0 heading = straight ahead lines up with
    // it unrotated) rather than a plain triangle, so it reads unambiguously as a
    // direction pointer instead of a generic wedge.
    private static Sprite GetOrCreateArrowSprite(Color color)
    {
        if (arrowIconCache.TryGetValue(color, out Sprite cached) && cached != null)
        {
            return cached;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = $"Runtime Arrow Icon {color}";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color outlineColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        // Arrow silhouette, normalized 0..1 (y=0 bottom / tail, y=1 top / tip), then
        // scaled to pixel space. Outer polygon = dark outline silhouette; inner
        // polygon (scaled toward the shape's centroid) = colored fill, leaving a
        // uniform border so the arrow reads against any background.
        Vector2[] outer = BuildArrowPolygon(size);
        Vector2 centroid = PolygonCentroid(outer);

        const float innerScale = 0.78f;
        Vector2[] inner = new Vector2[outer.Length];
        for (int i = 0; i < outer.Length; i++)
        {
            inner[i] = centroid + (outer[i] - centroid) * innerScale;
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                if (IsPointInPolygon(point, inner))
                {
                    texture.SetPixel(x, y, color);
                }
                else if (IsPointInPolygon(point, outer))
                {
                    texture.SetPixel(x, y, outlineColor);
                }
                else
                {
                    texture.SetPixel(x, y, clear);
                }
            }
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = $"Runtime Arrow Sprite {color}";

        arrowIconCache[color] = sprite;
        return sprite;
    }

    // A classic arrow silhouette (thin rectangular shaft + wide triangular head),
    // pointing up, as 7 points in pixel space for a `size` x `size` texture.
    private static Vector2[] BuildArrowPolygon(int size)
    {
        return new[]
        {
            new Vector2(size * 0.36f, size * 0.02f), // shaft bottom-left
            new Vector2(size * 0.64f, size * 0.02f), // shaft bottom-right
            new Vector2(size * 0.64f, size * 0.52f), // shaft top-right
            new Vector2(size * 0.90f, size * 0.52f), // head base-right
            new Vector2(size * 0.50f, size * 0.98f), // head tip
            new Vector2(size * 0.10f, size * 0.52f), // head base-left
            new Vector2(size * 0.36f, size * 0.52f)  // shaft top-left
        };
    }

    private static Vector2 PolygonCentroid(Vector2[] polygon)
    {
        Vector2 sum = Vector2.zero;
        foreach (Vector2 point in polygon)
        {
            sum += point;
        }

        return sum / polygon.Length;
    }

    // Standard even-odd ray casting point-in-polygon test; works for the concave
    // arrow shape (a plain triangle-only test wouldn't handle the shaft notch).
    private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            if ((pi.y > point.y) != (pj.y > point.y) &&
                point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
            {
                inside = !inside;
            }

            j = i;
        }

        return inside;
    }

    private void CreateCompass(Transform canvasTransform)
    {
        RectTransform panel = CreatePanel(canvasTransform, "Top Compass", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(300f, 72f));

        Text strip = CreateText(panel, "Compass Labels", "Nearest Enemy", 18, TextAnchor.MiddleCenter, new Vector2(0f, 26f), new Vector2(280f, 22f));
        strip.color = new Color(1f, 1f, 1f, 0.78f);

        // A solid dark puck behind the arrow so it stays readable against any
        // background showing through the translucent compass panel.
        RectTransform markerBacking = CreateMarkerBacking(panel, new Vector2(0f, -4f), 38f);

        // The actual pointer: a filled, outlined arrow sprite (not a font glyph) so
        // it's chunky and unmistakable at a glance. CompassMarkerBar only rotates
        // this piece - the backing puck stays fixed.
        RectTransform markerArrow = CreateMarkerArrow(markerBacking, markerColor, 32f);

        Text heading = CreateText(panel, "Heading Text", "N 000\u00b0", 14, TextAnchor.MiddleCenter, new Vector2(0f, -30f), new Vector2(120f, 18f));
        heading.color = new Color(1f, 1f, 1f, 0.86f);

        CompassMarkerBar compass = panel.gameObject.AddComponent<CompassMarkerBar>();
        compass.Initialize(player, markerArrow, heading, strip);
    }

    // A solid dark circular puck rendered behind the compass arrow, purely so the
    // arrow has guaranteed contrast no matter what's visible behind the HUD panel.
    private RectTransform CreateMarkerBacking(Transform parent, Vector2 anchoredPosition, float diameter)
    {
        GameObject backingObject = new GameObject("Compass Marker Backing");
        backingObject.transform.SetParent(parent, false);

        Image backing = backingObject.AddComponent<Image>();
        backing.sprite = GetOrCreateCircleSprite(new Color(0.05f, 0.05f, 0.05f, 0.85f));
        backing.raycastTarget = false;

        RectTransform rect = backing.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(diameter, diameter);
        return rect;
    }

    // The compass pointer itself - an outlined arrow sprite centered on the backing
    // puck. Returned RectTransform is what CompassMarkerBar rotates to show heading.
    private RectTransform CreateMarkerArrow(Transform parent, Color color, float size)
    {
        GameObject arrowObject = new GameObject("Compass Marker Arrow");
        arrowObject.transform.SetParent(parent, false);

        Image arrow = arrowObject.AddComponent<Image>();
        arrow.sprite = GetOrCreateArrowSprite(color);
        arrow.raycastTarget = false;

        RectTransform rect = arrow.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        return rect;
    }

    private void SpawnGunPickups()
    {
        Vector3 center = player != null ? player.position : collectibleAreaCenter;
        int gunCount = Mathf.Max(5, gunNames != null ? gunNames.Length : 0);

        for (int i = 0; i < gunCount; i++)
        {
            float offset = i - (gunCount - 1) * 0.5f;
            Vector3 position = center + new Vector3(offset * 3.1f, 0f, 9.5f + Mathf.Abs(offset) * 0.6f);
            position.y = SampleGroundHeight(position) + 1.1f;

            GameObject prefab = gunPrefabs != null && i < gunPrefabs.Length ? gunPrefabs[i] : null;
            string weaponName = GetGunName(i, prefab);
            CreateGunPickup(position, i + 1, prefab, weaponName);
        }
    }

    private void CreateGunPickup(Vector3 position, int index, GameObject prefab, string weaponName)
    {
        GameObject pickup = new GameObject($"Weapon Pickup {index:00} - {weaponName}");
        pickup.transform.position = position;

        SphereCollider trigger = pickup.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.15f;
        AddTriggerBody(pickup);

        if (prefab != null)
        {
            GameObject display = Instantiate(prefab, pickup.transform);
            display.name = $"{weaponName} Display";
            display.transform.localPosition = Vector3.zero;
            display.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            display.transform.localScale = Vector3.one * 0.42f;
            DisableGameplayPhysics(display);
            ApplyColorToRenderers(display, GetGunColor(index - 1), $"{weaponName} Pickup Color");
        }
        else
        {
            GameObject fallback = CreateFallbackGun(pickup.transform, weaponName);
            ApplyColorToRenderers(fallback, GetGunColor(index - 1), $"{weaponName} Pickup Color");
        }

        pickup.AddComponent<GunPickup>().Initialize(this, prefab, weaponName, GetGunColor(index - 1));
    }

    // Scatters protons and electrons together throughout the same central ring area
    // that coins previously occupied, alternating element type around the ring.
    private void SpawnAtoms()
    {
        if (player != null)
        {
            collectibleAreaCenter = player.position + new Vector3(0f, 0.8f, 0f);
        }

        SpawnSingleProton();
        SpawnSingleElectron();
    }

    private void SpawnSingleProton()
    {
        Vector3 position = GetRandomPointNearPlayer(spawnPersonalSpace, atomSpawnRadius);
        position.y = SampleGroundHeight(position) + 1.05f;
        protonSpawnCounter++;
        CreateAtomPickup(position, protonSpawnCounter, "Proton", protonColor, isProton: true);
    }

    private void SpawnSingleElectron()
    {
        Vector3 position = GetRandomPointNearPlayer(spawnPersonalSpace, atomSpawnRadius);
        position.y = SampleGroundHeight(position) + 1.05f;
        electronSpawnCounter++;
        CreateAtomPickup(position, electronSpawnCounter, "Electron", electronColor, isProton: false);
    }

    // Shared "spawn somewhere near the player, but not literally on top of them"
    // helper used by protons, electrons, and enemy respawns alike.
    private Vector3 GetRandomPointNearPlayer(float minRadius, float maxRadius)
    {
        Vector3 center = player != null ? player.position : collectibleAreaCenter;
        float safeMin = Mathf.Min(minRadius, maxRadius);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(safeMin, maxRadius);
        return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private void CreateAtomPickup(Vector3 position, int index, string label, Color color, bool isProton)
    {
        GameObject pickup = new GameObject($"Collectible {label} {index:00}");
        pickup.transform.position = position;

        SphereCollider trigger = pickup.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;
        AddTriggerBody(pickup);

        GameObject display = BuildAtomDisplay(pickup.transform, label);
        ApplyColorToRenderers(display, color, $"{label} Pickup Color");

        if (isProton)
        {
            pickup.AddComponent<ProtonCollectible>().Initialize(this);
        }
        else
        {
            pickup.AddComponent<ElectronCollectible>().Initialize(this);
        }
    }

    // Builds the composite "Atom Pickup" visual: a flattened orbit-ring disc (Coin_A)
    // plus a small core disc (Coin_C), or a procedural sphere-pair fallback if the
    // two model fields haven't been assigned in the Inspector yet.
    private GameObject BuildAtomDisplay(Transform parent, string label)
    {
        if (atomOrbitPrefab != null && atomCorePrefab != null)
        {
            GameObject display = new GameObject($"{label} Display");
            display.transform.SetParent(parent, false);

            GameObject orbit = Instantiate(atomOrbitPrefab, display.transform);
            orbit.name = "Orbit Ring";
            orbit.transform.localPosition = Vector3.zero;
            orbit.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            orbit.transform.localScale = new Vector3(0.5f, 0.5f, 0.06f);

            GameObject core = Instantiate(atomCorePrefab, display.transform);
            core.name = "Core";
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            core.transform.localScale = Vector3.one * 0.32f;

            DisableGameplayPhysics(display);
            return display;
        }

        return CreateFallbackAtom(parent, label);
    }

    // Procedural fallback: two stacked primitives standing in for the orbit ring and
    // core until the KayKit coin models are wired into atomOrbitPrefab/atomCorePrefab.
    private GameObject CreateFallbackAtom(Transform parent, string label)
    {
        GameObject root = new GameObject($"{label} Runtime Model");
        root.transform.SetParent(parent, false);

        GameObject orbit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        orbit.name = "Orbit Ring";
        orbit.transform.SetParent(root.transform, false);
        orbit.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        orbit.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * 0.28f;

        DisableGameplayPhysics(root);
        return root;
    }

    private void SpawnNpc()
    {
        Vector3 center = player != null ? player.position : collectibleAreaCenter;
        Vector3 position = center + new Vector3(7f, 0f, 8f);
        position.y = SampleGroundHeight(position);

        GameObject npc = new GameObject("Guide NPC");
        npc.transform.position = position;

        SphereCollider trigger = npc.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 3f;
        AddTriggerBody(npc);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Guide NPC Body";
        body.transform.SetParent(npc.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.85f, 1f, 0.85f);
        body.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime NPC Teal", new Color(0.1f, 0.62f, 0.58f));

        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        TextMesh label = new GameObject("Guide Label").AddComponent<TextMesh>();
        label.transform.SetParent(npc.transform, false);
        label.transform.localPosition = new Vector3(0f, 2.35f, 0f);
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        label.text = "Guide";
        label.fontSize = 48;
        label.characterSize = 0.08f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;

        npc.AddComponent<NpcDialogue>().Initialize(this);
    }

    // Scatters static "poison gas" hazard zones in a ring around the player spawn,
    // just outside the collectible ring so they read as an obstacle to route around
    // rather than blocking pickups outright.
    private void SpawnPoisonGasZones()
    {
        if (poisonGasZoneCount <= 0)
        {
            return;
        }

        Vector3 center = player != null ? player.position : collectibleAreaCenter;
        float ringDistance = Mathf.Max(collectibleAreaSize.x, collectibleAreaSize.y) * 0.5f + 6f;

        for (int i = 0; i < poisonGasZoneCount; i++)
        {
            float angle = (i / (float)poisonGasZoneCount) * Mathf.PI * 2f + Mathf.PI / poisonGasZoneCount;
            Vector3 position = center + new Vector3(Mathf.Cos(angle) * ringDistance, 0f, Mathf.Sin(angle) * ringDistance);
            position.y = SampleGroundHeight(position);

            CreatePoisonGasZone(position, i + 1);
        }
    }

    private void CreatePoisonGasZone(Vector3 position, int index)
    {
        GameObject zone = new GameObject($"Poison Gas Zone {index:00}");
        zone.transform.position = position;

        SphereCollider trigger = zone.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = poisonGasZoneRadius;
        trigger.center = new Vector3(0f, 1f, 0f);
        AddTriggerBody(zone);

        // Visuals live on a separate child so DisableGameplayPhysics doesn't touch
        // the zone root's own trigger collider (same convention as CreateAtomPickup).
        GameObject display = new GameObject("Gas Display");
        display.transform.SetParent(zone.transform, false);

        GameObject groundStain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        groundStain.name = "Ground Stain";
        groundStain.transform.SetParent(display.transform, false);
        groundStain.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        groundStain.transform.localScale = new Vector3(poisonGasZoneRadius * 2f, 0.02f, poisonGasZoneRadius * 2f);
        Color stainColor = new Color(poisonGasColor.r * 0.6f, poisonGasColor.g * 0.6f, poisonGasColor.b * 0.6f, 1f);
        groundStain.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Poison Ground", stainColor);

        GameObject cloudA = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cloudA.name = "Gas Cloud A";
        cloudA.transform.SetParent(display.transform, false);
        cloudA.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        cloudA.transform.localScale = new Vector3(poisonGasZoneRadius * 1.6f, poisonGasZoneRadius * 1.1f, poisonGasZoneRadius * 1.6f);
        cloudA.GetComponent<Renderer>().sharedMaterial = CreateTransparentRuntimeMaterial("Runtime Poison Gas A", poisonGasColor);

        GameObject cloudB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cloudB.name = "Gas Cloud B";
        cloudB.transform.SetParent(display.transform, false);
        cloudB.transform.localPosition = new Vector3(poisonGasZoneRadius * 0.25f, 0.7f, poisonGasZoneRadius * 0.2f);
        cloudB.transform.localScale = new Vector3(poisonGasZoneRadius * 1.1f, poisonGasZoneRadius * 0.8f, poisonGasZoneRadius * 1.1f);
        cloudB.GetComponent<Renderer>().sharedMaterial = CreateTransparentRuntimeMaterial("Runtime Poison Gas B", poisonGasColor);

        DisableGameplayPhysics(display);

        zone.AddComponent<PoisonGasZone>().Initialize(poisonGasDamagePerTick, display.transform);
    }

    // Ensures exactly one enemy exists in the world at game start. Also cleans out
    // any leftover enemies AutoSetup may have baked directly into the scene, so the
    // "only one enemy at a time" rule holds even before the scene is re-saved.
    private void SpawnInitialEnemy()
    {
        Transform parent = GetOrCreateEnemiesParent();
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }

        SpawnReplacementEnemy();
    }

    private Transform GetOrCreateEnemiesParent()
    {
        if (enemiesParent != null)
        {
            return enemiesParent;
        }

        GameObject existing = GameObject.Find("Enemies");
        enemiesParent = existing != null ? existing.transform : new GameObject("Enemies").transform;
        return enemiesParent;
    }

    // Spawns one enemy between enemySafeZoneRadius and enemySpawnRadius of the
    // player - never inside the safe zone. Called both for the initial enemy
    // and as the delayed replacement once the current one is killed (see
    // AddEnemyDefeat), so exactly one enemy is ever alive at a time.
    private void SpawnReplacementEnemy()
    {
        nextEnemySpawnTime = -1f;

        // The delayed Invoke from AddEnemyDefeat can still fire after the
        // game has already ended (win or time-up) - don't spawn a fresh
        // enemy into a finished game.
        if (hasWon || hasLost)
        {
            return;
        }

        Vector3 position = GetRandomPointNearPlayer(enemySafeZoneRadius, enemySpawnRadius);
        position.y = SampleGroundHeight(position);

        GameObject prefab = null;
        List<GameObject> valid = new List<GameObject>();
        if (enemyPrefabs != null && enemyPrefabs.Length > 0)
        {
            // Pick from any assigned, non-null entries rather than bailing out
            // the moment index 0 happens to be empty.
            foreach (GameObject candidate in enemyPrefabs)
            {
                if (candidate != null)
                {
                    valid.Add(candidate);
                }
            }
        }

        if (valid.Count == 0)
        {
            // enemyPrefabs isn't wired up in the Inspector (e.g. AutoSetup
            // hasn't been re-run) - load the Husky/Wolf models straight from
            // Resources so real models spawn without needing any manual
            // Editor step, same self-sufficient convention used for the
            // gunshot/pickup/enemy-hit audio clips.
            foreach (string resourceName in HuskyWolfResourceNames)
            {
                GameObject loaded = Resources.Load<GameObject>($"Enemies/{resourceName}");
                if (loaded != null)
                {
                    valid.Add(loaded);
                }
            }
        }

        if (valid.Count > 0)
        {
            prefab = valid[Random.Range(0, valid.Count)];
        }

        GameObject enemy;
        if (prefab != null)
        {
            enemy = Instantiate(prefab, position, Quaternion.identity, GetOrCreateEnemiesParent());
            enemy.name = $"{prefab.name} (Runtime {Random.Range(1000, 9999)})";

            BoxCollider box = enemy.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = enemy.AddComponent<BoxCollider>();
            }
            box.size = new Vector3(4f, 4f, 4f);
            box.center = new Vector3(0f, 2f, 0f);
            box.isTrigger = false;

            Rigidbody body = enemy.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = enemy.AddComponent<Rigidbody>();
            }
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Match AutoSetup's convention: model prefabs come in larger than the player.
            enemy.transform.localScale = Vector3.one * 0.5f;
        }
        else
        {
            // No enemyPrefabs wired up yet (e.g. AutoSetup hasn't been re-run in
            // the Editor) - build a simple, always-visible fallback enemy instead
            // of silently spawning nothing.
            enemy = CreateFallbackEnemy(position);
        }

        if (enemy.GetComponent<EnemyDamage>() == null)
        {
            enemy.AddComponent<EnemyDamage>();
        }

        if (enemy.GetComponent<WanderAI>() == null)
        {
            enemy.AddComponent<WanderAI>();
        }
    }

    // A bright red capsule "training dummy" enemy, used whenever no real
    // enemy prefab is wired up. Fully functional (damages the player, dies to
    // gunfire, tracked by the compass) - just not a fancy model.
    private GameObject CreateFallbackEnemy(Vector3 position)
    {
        GameObject enemy = new GameObject("Fallback Enemy (Runtime)");
        enemy.transform.SetParent(GetOrCreateEnemiesParent(), false);
        enemy.transform.position = position;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(enemy.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Enemy Red", new Color(0.85f, 0.15f, 0.1f));
        Object.Destroy(body.GetComponent<Collider>());

        BoxCollider box = enemy.AddComponent<BoxCollider>();
        box.size = new Vector3(4f, 4f, 4f);
        box.center = new Vector3(0f, 2f, 0f);
        box.isTrigger = false;

        Rigidbody rb = enemy.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        return enemy;
    }

    // Exactly one vehicle spawns near the player, using vehiclePrefab if
    // AutoSetup has wired it up, else a procedural fallback car so there's
    // always something to see and drive.
    private void SpawnVehicle()
    {
        Vector3 position = GetRandomPointNearPlayer(spawnPersonalSpace, vehicleSpawnRadius);
        position.y = SampleGroundHeight(position);
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject vehicle;
        if (vehiclePrefab != null)
        {
            vehicle = Instantiate(vehiclePrefab, position, rotation, GetOrCreateVehiclesParent());
            vehicle.name = "Vehicle (Runtime)";

            BoxCollider box = vehicle.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = vehicle.AddComponent<BoxCollider>();
            }
            box.size = new Vector3(3.3f, 3.2f, 5.3f);
            box.center = new Vector3(0f, 1.6f, 0f);
            box.isTrigger = false;

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = vehicle.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;

            vehicle.transform.localScale = Vector3.one * vehicleScale;
        }
        else
        {
            vehicle = CreateFallbackVehicle(position, rotation);
        }

        if (vehicle.GetComponent<VehicleController>() == null)
        {
            vehicle.AddComponent<VehicleController>();
        }
    }

    // A boxy red car built from primitives (body + 4 wheels), used whenever
    // no real vehicle model is wired up. Fully drivable - just not a fancy
    // model. Mirrors CreateFallbackGun/CreateFallbackAtom's convention.
    private GameObject CreateFallbackVehicle(Vector3 position, Quaternion rotation)
    {
        GameObject vehicle = new GameObject("Fallback Vehicle (Runtime)");
        vehicle.transform.SetParent(GetOrCreateVehiclesParent(), false);
        vehicle.transform.position = position;
        vehicle.transform.rotation = rotation;

        GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chassis.name = "Chassis";
        chassis.transform.SetParent(vehicle.transform, false);
        chassis.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        chassis.transform.localScale = new Vector3(2.2f, 1.1f, 4.2f);
        chassis.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Vehicle Red", new Color(0.75f, 0.1f, 0.08f));
        Object.Destroy(chassis.GetComponent<Collider>());

        GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        cabin.transform.SetParent(vehicle.transform, false);
        cabin.transform.localPosition = new Vector3(0f, 1.75f, -0.3f);
        cabin.transform.localScale = new Vector3(1.7f, 0.7f, 2.2f);
        cabin.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Vehicle Glass", new Color(0.55f, 0.75f, 0.85f));
        Object.Destroy(cabin.GetComponent<Collider>());

        Material wheelMaterial = CreateRuntimeMaterial("Runtime Vehicle Wheel", new Color(0.05f, 0.05f, 0.05f));
        Vector3[] wheelOffsets =
        {
            new Vector3(-1.15f, 0.45f, 1.4f),
            new Vector3(1.15f, 0.45f, 1.4f),
            new Vector3(-1.15f, 0.45f, -1.4f),
            new Vector3(1.15f, 0.45f, -1.4f)
        };

        foreach (Vector3 offset in wheelOffsets)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            wheel.transform.SetParent(vehicle.transform, false);
            wheel.transform.localPosition = offset;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
            wheel.GetComponent<Renderer>().sharedMaterial = wheelMaterial;
            Object.Destroy(wheel.GetComponent<Collider>());
        }

        BoxCollider box = vehicle.AddComponent<BoxCollider>();
        box.size = new Vector3(2.4f, 1.9f, 4.6f);
        box.center = new Vector3(0f, 1f, 0f);
        box.isTrigger = false;

        Rigidbody rb = vehicle.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        return vehicle;
    }

    private Transform vehiclesParent;

    private Transform GetOrCreateVehiclesParent()
    {
        if (vehiclesParent != null)
        {
            return vehiclesParent;
        }

        GameObject existing = GameObject.Find("Vehicles");
        vehiclesParent = existing != null ? existing.transform : new GameObject("Vehicles").transform;
        return vehiclesParent;
    }

    private float SampleGroundHeight(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 50f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return collectibleAreaCenter.y;
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Coins: {coinsCollected}/{coinsToWin}";
        }
    }

    private void RefreshStatusTexts()
    {
        UpdateScoreText();
        UpdateEnemyText();
        UpdateTimerText();
        UpdateEnemyRespawnText();
    }

    private void UpdateEnemyRespawnText()
    {
        if (enemyRespawnText == null)
        {
            return;
        }

        if (!hasWon && !hasLost && nextEnemySpawnTime > Time.time)
        {
            float timeLeft = nextEnemySpawnTime - Time.time;
            enemyRespawnText.text = $"Next Enemy in: {timeLeft:F1}s";
        }
        else
        {
            enemyRespawnText.text = string.Empty;
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
        {
            return;
        }

        timerText.text = FormatTime(remainingTime);
        // Warn the player with a red readout once time is getting critical.
        timerText.color = remainingTime <= 30f ? new Color(0.95f, 0.25f, 0.2f) : textColor;
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return $"Time Left: {minutes:00}:{remainder:00}";
    }

    private void UpdateEnemyText()
    {
        if (enemyText != null)
        {
            enemyText.text = $"Enemies: {enemiesDefeated}/{enemiesToWin}";
        }
    }

    private void UpdateWeaponText(string weaponName)
    {
        if (weaponText != null)
        {
            weaponText.text = $"Weapon: {weaponName}";
        }
    }

    private void UpdateChestText()
    {
        if (chestText != null)
        {
            chestText.text = $"Chest: {AcquiredCount}/{GunSlotCount} unique guns (Press Y)";
        }
    }

    private void ToggleInventory()
    {
        if (inventoryPanel == null)
        {
            return;
        }

        bool showInventory = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(showInventory);
        RefreshInventorySlots();

        // Free the mouse while the chest is open so slots can be clicked, then relock.
        Cursor.lockState = showInventory ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = showInventory;

        // Freeze player movement + camera look while browsing so the view doesn't whip
        // around when the mouse is used to click slots.
        if (playerController != null)
        {
            playerController.enabled = !showInventory;
        }
    }

    private void RefreshInventorySlots()
    {
        if (gunSlots == null)
        {
            return;
        }

        for (int i = 0; i < gunSlots.Length; i++)
        {
            GunSlot slot = gunSlots[i];

            if (slot.IconRoot != null)
            {
                slot.IconRoot.SetActive(slot.Acquired);
            }

            if (slot.NameLabel != null)
            {
                slot.NameLabel.enabled = slot.Acquired;
            }

            if (slot.Frame != null)
            {
                slot.Frame.color = i == equippedGunIndex ? slotEquippedBorderColor : slotBorderColor;
            }
        }

        if (protonCountText != null)
        {
            protonCountText.text = $"x{protonsCollected}";
        }

        if (electronCountText != null)
        {
            electronCountText.text = $"x{electronsCollected}";
        }

        if (coinCountText != null)
        {
            coinCountText.text = $"x{coinsCollected}";
        }
    }

    private void SetupPlayerWeaponController()
    {
        if (player == null)
        {
            return;
        }

        rightHand = FindRightHand(player);
        playerController = player.GetComponent<ThirdPersonController>();
        gunController = player.GetComponent<PlayerGunController>();
        if (gunController == null)
        {
            gunController = player.gameObject.AddComponent<PlayerGunController>();
        }
    }

    // Pushes dayNightCycleSeconds (tuned here alongside the other win-condition
    // knobs) into the scene's DayNightManager so its cycle length stays in sync.
    private void SyncDayNightCycle()
    {
        if (dayNightManager == null)
        {
            dayNightManager = FindAnyObjectByType<DayNightManager>();
        }

        if (dayNightManager != null)
        {
            dayNightManager.CycleSeconds = dayNightCycleSeconds;
        }
    }

    private RectTransform CreatePanel(Transform parent, string objectName, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject panelObject = new GameObject(objectName);
        panelObject.transform.SetParent(parent, false);

        Image image = panelObject.AddComponent<Image>();
        image.color = panelColor;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private Text CreateText(Transform parent, string objectName, string value, int size, TextAnchor alignment, Vector2 anchoredPosition, Vector2 rectSize)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        text.fontSize = size;
        text.alignment = alignment;
        text.color = textColor;

        RectTransform rect = text.rectTransform;
        if (alignment == TextAnchor.UpperLeft)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
        }
        else if (alignment == TextAnchor.LowerCenter)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }
        else if (alignment == TextAnchor.LowerRight)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
        }
        else if (alignment == TextAnchor.UpperCenter)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = rectSize;
        return text;
    }

    private string GetGunName(int index, GameObject prefab)
    {
        if (gunNames != null && index < gunNames.Length && !string.IsNullOrWhiteSpace(gunNames[index]))
        {
            return gunNames[index];
        }

        return prefab != null ? prefab.name.Replace("_", " ") : $"Gun {index + 1}";
    }

    private int GetSlotIndex(string weaponName)
    {
        if (gunSlots == null)
        {
            return -1;
        }

        for (int i = 0; i < gunSlots.Length; i++)
        {
            if (gunSlots[i].Name == weaponName)
            {
                return i;
            }
        }

        return -1;
    }

    private Color GetGunColor(int index)
    {
        return gunColors[Mathf.Abs(index) % gunColors.Length];
    }

    private GameObject CreateFallbackGun(Transform parent, string weaponName)
    {
        GameObject root = new GameObject($"{weaponName} Runtime Model");
        root.transform.SetParent(parent, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Gun Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0.12f, 0f, 0f);
        body.transform.localScale = new Vector3(0.3f, 0.08f, 0.06f);
        body.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Gun Dark", new Color(0.08f, 0.08f, 0.09f));

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.name = "Gun Handle";
        handle.transform.SetParent(root.transform, false);
        handle.transform.localPosition = new Vector3(0.0f, -0.1f, 0f);
        handle.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        handle.transform.localScale = new Vector3(0.07f, 0.18f, 0.05f);
        handle.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial("Runtime Gun Handle", new Color(0.18f, 0.12f, 0.07f));

        DisableGameplayPhysics(root);
        return root;
    }

    private static void ApplyColorToRenderers(GameObject root, Color color, string materialName)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = CreateRuntimeMaterial(materialName, color);
        }
    }

    // Different character rigs name the right-hand bone differently
    // (StarterAssets uses "Right_Hand", Mixamo/Unreal rigs use "hand_r").
    private static readonly string[] RightHandBoneNames = { "Right_Hand", "hand_r", "RightHand", "mixamorig:RightHand", "Hand_R" };

    private static Transform FindRightHand(Transform root)
    {
        foreach (string boneName in RightHandBoneNames)
        {
            Transform bone = FindChildRecursive(root, boneName);
            if (bone != null)
            {
                return bone;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void DisableGameplayPhysics(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private static void AddTriggerBody(GameObject root)
    {
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        return material;
    }

    // Same as CreateRuntimeMaterial but configured for alpha blending, for hazard
    // "gas cloud" visuals that need to read as translucent rather than solid.
    private static Material CreateTransparentRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        bool isUrp = shader != null;
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;

        if (isUrp)
        {
            material.SetFloat("_Surface", 1f); // 1 = Transparent
            material.SetFloat("_Blend", 0f);   // 0 = Alpha blend
        }
        else
        {
            material.SetFloat("_Mode", 3f); // Standard shader's Transparent rendering mode
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    private static bool WasInventoryKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.Y);
    }

    private static bool WasCraftKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.B);
    }

    private static bool WasInstructionsKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.E);
    }

    private void OnValidate()
    {
        coinsToWin = Mathf.Max(1, coinsToWin);
        enemiesToWin = Mathf.Max(1, enemiesToWin);
        protonsPerCoin = Mathf.Max(1, protonsPerCoin);
        electronsPerCoin = Mathf.Max(1, electronsPerCoin);
        dayNightCycleSeconds = Mathf.Max(1f, dayNightCycleSeconds);
        timeLimitSeconds = Mathf.Max(10f, timeLimitSeconds);
        poisonGasZoneCount = Mathf.Max(0, poisonGasZoneCount);
        poisonGasZoneRadius = Mathf.Max(1f, poisonGasZoneRadius);
        poisonGasDamagePerTick = Mathf.Max(1, poisonGasDamagePerTick);
        atomSpawnRadius = Mathf.Max(1f, atomSpawnRadius);
        enemySafeZoneRadius = Mathf.Max(1f, enemySafeZoneRadius);
        enemySpawnRadius = Mathf.Max(enemySafeZoneRadius + 1f, enemySpawnRadius); // keep a real ring beyond the safe zone
        enemyRespawnDelaySeconds = Mathf.Max(0f, enemyRespawnDelaySeconds);
        vehicleSpawnRadius = Mathf.Max(1f, vehicleSpawnRadius);
        spawnPersonalSpace = Mathf.Max(0f, spawnPersonalSpace);
        prefabGunScale = Mathf.Max(0.01f, prefabGunScale);
        fallbackGunScale = Mathf.Max(0.01f, fallbackGunScale);
        weaponHoldGizmoSize = Mathf.Max(0.02f, weaponHoldGizmoSize);
        SyncDayNightCycle();
        RefreshStatusTexts();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showWeaponHoldGizmo)
        {
            return;
        }

        Transform holdTarget = GetWeaponHoldTargetForGizmo();
        if (holdTarget == null)
        {
            return;
        }

        Vector3 worldPosition = holdTarget.TransformPoint(equippedGunLocalPosition);
        Quaternion worldRotation = holdTarget.rotation * Quaternion.Euler(equippedGunLocalEulerAngles);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(worldPosition, weaponHoldGizmoSize);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(worldPosition, worldPosition + worldRotation * Vector3.right * weaponHoldGizmoSize * 2f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(worldPosition, worldPosition + worldRotation * Vector3.up * weaponHoldGizmoSize * 2f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(worldPosition, worldPosition + worldRotation * Vector3.forward * weaponHoldGizmoSize * 3f);
    }

    private Transform GetWeaponHoldTargetForGizmo()
    {
        Transform sourcePlayer = player;
        if (sourcePlayer == null)
        {
            ThirdPersonController controller = FindAnyObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                sourcePlayer = controller.transform;
            }
        }

        if (sourcePlayer == null)
        {
            return null;
        }

        Transform hand = FindRightHand(sourcePlayer);
        return hand != null ? hand : sourcePlayer;
    }

    private void UpdateWeaponHoldPreview()
    {
        if (!showWeaponHoldPreview || equippedGun != null)
        {
            HideWeaponHoldPreview();
            return;
        }

        Transform holdTarget = GetWeaponHoldTargetForGizmo();
        if (holdTarget == null)
        {
            HideWeaponHoldPreview();
            return;
        }

        if (weaponHoldPreview == null)
        {
            weaponHoldPreview = CreateFallbackGun(holdTarget, "Weapon Hold Preview");
            weaponHoldPreview.name = "Weapon Hold Preview";
            ApplyColorToRenderers(weaponHoldPreview, weaponHoldPreviewColor, "Runtime Weapon Hold Preview");
        }
        else if (weaponHoldPreview.transform.parent != holdTarget)
        {
            weaponHoldPreview.transform.SetParent(holdTarget, false);
        }

        weaponHoldPreview.SetActive(true);
        weaponHoldPreview.transform.localPosition = equippedGunLocalPosition;
        weaponHoldPreview.transform.localRotation = Quaternion.Euler(equippedGunLocalEulerAngles);
        weaponHoldPreview.transform.localScale = Vector3.one * fallbackGunScale;
    }

    private void HideWeaponHoldPreview()
    {
        if (weaponHoldPreview != null)
        {
            weaponHoldPreview.SetActive(false);
        }
    }

    private static bool WasNumberKeyPressed(int number)
    {
        if (number < 1 || number > 9)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var keyControl = number switch
            {
                1 => Keyboard.current.digit1Key,
                2 => Keyboard.current.digit2Key,
                3 => Keyboard.current.digit3Key,
                4 => Keyboard.current.digit4Key,
                5 => Keyboard.current.digit5Key,
                6 => Keyboard.current.digit6Key,
                7 => Keyboard.current.digit7Key,
                8 => Keyboard.current.digit8Key,
                _ => Keyboard.current.digit9Key
            };

            if (keyControl != null && keyControl.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        return number switch
        {
            1 => Input.GetKeyDown(KeyCode.Alpha1),
            2 => Input.GetKeyDown(KeyCode.Alpha2),
            3 => Input.GetKeyDown(KeyCode.Alpha3),
            4 => Input.GetKeyDown(KeyCode.Alpha4),
            5 => Input.GetKeyDown(KeyCode.Alpha5),
            6 => Input.GetKeyDown(KeyCode.Alpha6),
            7 => Input.GetKeyDown(KeyCode.Alpha7),
            8 => Input.GetKeyDown(KeyCode.Alpha8),
            _ => Input.GetKeyDown(KeyCode.Alpha9)
        };
    }
}
