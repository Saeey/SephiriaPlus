using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirror;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SephiriaPlus
{
    /// <summary>
    /// Sephiria built-in AddOn loader entry point.
    /// </summary>
    public sealed class Core : HorayModBase
    {
        private const string LogPrefix = "[SephiriaPlus]";
        private GameObject controllerObject;

        protected override void OnModLoaded()
        {
            ModConfig config = ModConfig.Load();
            controllerObject = new GameObject("SephiriaPlusController");
            Object.DontDestroyOnLoad(controllerObject);
            SephiriaPlusController controller = controllerObject.AddComponent<SephiriaPlusController>();
            controller.Configure(config);
            Debug.Log(LogPrefix + " loaded with config: " + config.ToLogString());
        }

        protected override void OnModUnloaded()
        {
            if (controllerObject != null)
            {
                Object.Destroy(controllerObject);
                controllerObject = null;
            }

            Debug.Log(LogPrefix + " unloaded.");
        }
    }

    internal sealed class ModConfig
    {
        private const string LogPrefix = "[SephiriaPlus]";

        public bool EnableInfiniteReroll = true;
        public int RerollDiceTarget = 99;
        public bool EnableTalentPointMultiplier = true;
        public int TalentPointMultiplier = 10;
        public bool EnableExtraInventorySlots = true;
        public int ExtraInventorySlots = 18;
        public bool EnableCheckpointRetry = true;
        public string CheckpointRetryKey = "F8";
        public bool EnableArtifactSchoolFilter = true;
        public bool EnableHiddenRoomReveal = true;
        public bool EnableDpsMeter = true;
        public string DpsMeterToggleKey = "F7";

        public static ModConfig Load()
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string configPath = Path.Combine(assemblyDirectory ?? string.Empty, "config.json");
            try
            {
                if (!File.Exists(configPath))
                {
                    Debug.LogWarning(LogPrefix + " config.json was not found; using defaults.");
                    return new ModConfig();
                }

                ModConfig config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(configPath));
                if (config == null)
                {
                    Debug.LogWarning(LogPrefix + " config.json was empty; using defaults.");
                    return new ModConfig();
                }

                config.RerollDiceTarget = Mathf.Clamp(config.RerollDiceTarget, 0, 9999);
                config.TalentPointMultiplier = Mathf.Clamp(config.TalentPointMultiplier, 1, 100);
                config.ExtraInventorySlots = Mathf.Clamp(config.ExtraInventorySlots, 0, short.MaxValue);
                return config;
            }
            catch (System.Exception exception)
            {
                Debug.LogError(LogPrefix + " failed to read config.json; using defaults. " + exception);
                return new ModConfig();
            }
        }

        public string ToLogString()
        {
            return "reroll=" + EnableInfiniteReroll + " (target " + RerollDiceTarget + ")" +
                   ", talent=" + EnableTalentPointMultiplier + " (x" + TalentPointMultiplier + ")" +
                   ", inventory=" + EnableExtraInventorySlots + " (+" + ExtraInventorySlots + ")" +
                   ", checkpointRetry=" + EnableCheckpointRetry + " (" + CheckpointRetryKey + ")" +
                   ", artifactFilter=" + EnableArtifactSchoolFilter +
                   ", hiddenRooms=" + EnableHiddenRoomReveal +
                   ", dpsMeter=" + EnableDpsMeter + " (" + DpsMeterToggleKey + ")";
        }
    }

    internal sealed class SephiriaPlusController : MonoBehaviour
    {
        private const float PollIntervalSeconds = 0.25f;
        private static readonly FieldInfo AddedPassiveField = typeof(TreeShopItemStorage).GetField(
            "addedPassive",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentSaveField = typeof(SaveManager).GetField(
            "current",
            BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentRunSaveField = typeof(SaveManager).GetField(
            "currentRun",
            BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MiraclePoolsField = typeof(MiracleDatabase).GetField(
            "miracles",
            BindingFlags.Static | BindingFlags.NonPublic);
        private readonly Dictionary<int, TalentPointState> talentPointStates = new Dictionary<int, TalentPointState>();
        private readonly HashSet<int> expandedInventories = new HashSet<int>();
        private ModConfig config = new ModConfig();
        private SaveData checkpointCurrent;
        private SaveData checkpointRun;
        private string checkpointFloorGuid = string.Empty;
        private string observedFloorGuid = string.Empty;
        private float checkpointCaptureTime = -1f;
        private Key checkpointRetryKey = Key.F8;
        private bool retryInProgress;
        private UI_GameOverLabel retryButtonOwner;
        private GameObject retryButtonObject;
        private UI_HorayButton retryButton;
        private TextMeshProUGUI[] retryButtonLabels = new TextMeshProUGUI[0];
        private bool retryButtonLayoutApplied;
        private UI_MiraclePanel artifactFilterOwner;
        private GameObject artifactFilterContainer;
        private readonly List<GameObject> artifactFilterButtons = new List<GameObject>();
        private List<Miracle> originalTierOneMiracles;
        private string selectedArtifactCategory = string.Empty;
        private Key dpsMeterToggleKey = Key.F7;
        private bool dpsMeterVisible = true;
        private DpsRoomScope currentDpsRoom;
        private bool dpsRoomActive;
        private bool previousBattleState;
        private int dpsRoomSequence;
        private float dpsCombatStartTime = -1f;
        private float dpsCombatEndTime = -1f;
        private readonly Dictionary<uint, float> dpsDamageBaselines = new Dictionary<uint, float>();
        private readonly Dictionary<uint, DpsPlayerRow> dpsRows = new Dictionary<uint, DpsPlayerRow>();
        private GUIStyle overlayTitleStyle;
        private GUIStyle overlayRowStyle;
        private GUIStyle hiddenRoomStyle;
        private Texture2D overlayBackground;
        private float nextPollTime;

        public void Configure(ModConfig loadedConfig)
        {
            config = loadedConfig ?? new ModConfig();
            if (!System.Enum.TryParse(config.CheckpointRetryKey, true, out checkpointRetryKey))
            {
                checkpointRetryKey = Key.F8;
                Debug.LogWarning("[SephiriaPlus] invalid CheckpointRetryKey; using F8.");
            }
            if (!System.Enum.TryParse(config.DpsMeterToggleKey, true, out dpsMeterToggleKey))
            {
                dpsMeterToggleKey = Key.F7;
                Debug.LogWarning("[SephiriaPlus] invalid DpsMeterToggleKey; using F7.");
            }
        }

        private sealed class TalentPointState
        {
            public int VanillaAddedPoints;
            public int LastAppliedCap;
        }

        private void Update()
        {
            HandleCheckpointRetryInput();
            HandleArtifactFilterUI();
            HandleDpsMeterInput();

            if (Time.unscaledTime < nextPollTime)
            {
                return;
            }

            nextPollTime = Time.unscaledTime + PollIntervalSeconds;

            // Host-authoritative mode: a normal client never changes dice. The host
            // updates every server-side PlayerAvatar and Mirror synchronizes the
            // values to their owning clients.
            if (!NetworkServer.active)
            {
                return;
            }

            PlayerAvatar[] players = Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            PlayerAvatar hostPlayer = null;
            foreach (PlayerAvatar player in players)
            {
                if (player == null || !player.isServer)
                {
                    continue;
                }

                if (hostPlayer == null || player.isOwned)
                {
                    hostPlayer = player;
                }

                if (config.EnableInfiniteReroll && player.rerollDice < config.RerollDiceTarget)
                {
                    player.AddDice(config.RerollDiceTarget - player.rerollDice);
                }

                GridInventory inventory = player.Inventory;
                int inventoryInstanceId = inventory != null ? inventory.GetInstanceID() : 0;
                if (config.EnableExtraInventorySlots && config.ExtraInventorySlots > 0 &&
                    inventory != null && inventory.isServer && !expandedInventories.Contains(inventoryInstanceId))
                {
                    inventory.AddStorage((short)config.ExtraInventorySlots);
                    expandedInventories.Add(inventoryInstanceId);
                }

                if (!config.EnableTalentPointMultiplier)
                {
                    continue;
                }

                TreeShopItemStorage storage = player.GetComponent<TreeShopItemStorage>();
                if (storage == null || AddedPassiveField == null)
                {
                    continue;
                }

                int vanillaAddedPoints = (int)AddedPassiveField.GetValue(storage);
                int instanceId = player.GetInstanceID();
                bool isNewState = false;
                if (!talentPointStates.TryGetValue(instanceId, out TalentPointState state))
                {
                    isNewState = true;
                    state = new TalentPointState
                    {
                        VanillaAddedPoints = vanillaAddedPoints,
                        LastAppliedCap = player.maxPassivePoint
                    };
                    talentPointStates.Add(instanceId, state);
                }

                int normalizedCap = player.maxPassivePoint;
                if (!isNewState && vanillaAddedPoints != state.VanillaAddedPoints)
                {
                    // TreeShopItemStorage.Unlock subtracts only its vanilla amount.
                    // Remove our previous bonus before applying the new multiplier.
                    normalizedCap -= state.VanillaAddedPoints * (config.TalentPointMultiplier - 1);
                }
                else if (!isNewState && player.maxPassivePoint == state.LastAppliedCap)
                {
                    normalizedCap -= vanillaAddedPoints * (config.TalentPointMultiplier - 1);
                }

                int multipliedCap = normalizedCap + vanillaAddedPoints * (config.TalentPointMultiplier - 1);
                if (player.maxPassivePoint != multipliedCap)
                {
                    player.NetworkmaxPassivePoint = multipliedCap;
                }

                state.VanillaAddedPoints = vanillaAddedPoints;
                state.LastAppliedCap = multipliedCap;
            }

            UpdateCheckpoint(hostPlayer);
            UpdateDpsTimer(hostPlayer, players);
        }

        private void HandleDpsMeterInput()
        {
            if (config.EnableDpsMeter && Keyboard.current != null &&
                Keyboard.current[dpsMeterToggleKey].wasPressedThisFrame)
            {
                dpsMeterVisible = !dpsMeterVisible;
            }
        }

        private void UpdateDpsTimer(PlayerAvatar hostPlayer, PlayerAvatar[] players)
        {
            if (!config.EnableDpsMeter || hostPlayer == null)
            {
                return;
            }

            bool inBattle = hostPlayer.IsInBattle;
            bool battleStarted = inBattle && !previousBattleState;
            previousBattleState = inBattle;
            if (!inBattle)
            {
                EndDpsRoom();
                return;
            }

            DpsRoomScope room = FindCurrentDpsRoom(hostPlayer);
            if (room == null)
            {
                EndDpsRoom();
                return;
            }
            if (!dpsRoomActive || battleStarted || !room.IsSameRoom(currentDpsRoom))
            {
                BeginDpsRoom(room, players);
            }

            foreach (PlayerAvatar player in players)
            {
                if (player == null)
                {
                    continue;
                }
                uint key = player.netId != 0 ? player.netId : unchecked((uint)player.GetInstanceID());
                float currentDamage = player.dealsStatistics_LastLocation.Values.Sum();
                if (!dpsDamageBaselines.TryGetValue(key, out float previousDamage))
                {
                    dpsDamageBaselines[key] = currentDamage;
                    continue;
                }
                dpsDamageBaselines[key] = currentDamage;
                float delta = currentDamage >= previousDamage ? currentDamage - previousDamage : currentDamage;
                Vector3 position = player.transform.position;
                if (delta <= 0f || !currentDpsRoom.AllowsPlayer(player.currentFloorGuid, position.x, position.y))
                {
                    continue;
                }
                if (!dpsRows.TryGetValue(key, out DpsPlayerRow row))
                {
                    row = new DpsPlayerRow();
                    dpsRows.Add(key, row);
                }
                row.Name = string.IsNullOrWhiteSpace(player.Name) ? player.playerNameSource : player.Name;
                row.Damage += delta;
            }
        }

        private void BeginDpsRoom(DpsRoomScope room, PlayerAvatar[] players)
        {
            currentDpsRoom = room;
            dpsRoomActive = true;
            dpsRoomSequence++;
            dpsCombatStartTime = Time.unscaledTime;
            dpsCombatEndTime = -1f;
            dpsRows.Clear();
            dpsDamageBaselines.Clear();
            foreach (PlayerAvatar player in players)
            {
                if (player == null)
                {
                    continue;
                }
                uint key = player.netId != 0 ? player.netId : unchecked((uint)player.GetInstanceID());
                dpsDamageBaselines[key] = player.dealsStatistics_LastLocation.Values.Sum();
            }
            Debug.Log("[SephiriaPlus] DPS room #" + dpsRoomSequence + " started.");
        }

        private void EndDpsRoom()
        {
            if (!dpsRoomActive)
            {
                return;
            }
            dpsCombatEndTime = Time.unscaledTime;
            dpsRoomActive = false;
            Debug.Log("[SephiriaPlus] DPS room #" + dpsRoomSequence + " ended.");
        }

        private DpsRoomScope FindCurrentDpsRoom(PlayerAvatar player)
        {
            string floorGuid = player.currentFloorGuid;
            Vector3 position = player.transform.position;
            if (string.IsNullOrEmpty(floorGuid))
            {
                return null;
            }

            DpsRoomScope selected = null;
            foreach (BossSpawner spawner in Object.FindObjectsByType<BossSpawner>(FindObjectsSortMode.None))
            {
                if (spawner == null || !spawner.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector2 origin = spawner.transform.position;
                selected = DpsRoomScope.SelectContaining(selected,
                    DpsRoomScope.Create(floorGuid, spawner.GetInstanceID(),
                        origin + spawner.playerPreventArea_lb, origin + spawner.playerPreventArea_rt),
                    position.x, position.y);
            }
            if (selected != null)
            {
                return selected;
            }

            foreach (RandomEnemyPhaseSpawner spawner in Object.FindObjectsByType<RandomEnemyPhaseSpawner>(FindObjectsSortMode.None))
            {
                if (spawner != null && spawner.gameObject.activeInHierarchy)
                {
                    selected = DpsRoomScope.SelectContaining(selected,
                        DpsRoomScope.Create(floorGuid, spawner.GetInstanceID(),
                            spawner.NetworkdetectArea_lb, spawner.NetworkdetectArea_rt),
                        position.x, position.y);
                }
            }
            foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
            {
                if (spawner == null || !spawner.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector2 origin = spawner.transform.position;
                selected = DpsRoomScope.SelectContaining(selected,
                    DpsRoomScope.Create(floorGuid, spawner.GetInstanceID(),
                        origin + spawner.NetworkplayerPreventArea_lb, origin + spawner.NetworkplayerPreventArea_rt),
                    position.x, position.y);
            }
            foreach (CommonEnemySpawner spawner in Object.FindObjectsByType<CommonEnemySpawner>(FindObjectsSortMode.None))
            {
                if (spawner == null || !spawner.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector2 origin = spawner.transform.position;
                selected = DpsRoomScope.SelectContaining(selected,
                    DpsRoomScope.Create(floorGuid, spawner.GetInstanceID(),
                        origin + spawner.NetworkplayerPreventArea_lb, origin + spawner.NetworkplayerPreventArea_rt),
                    position.x, position.y);
            }
            return selected;
        }

        private void OnGUI()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            EnsureOverlayStyles();
            if (config.EnableHiddenRoomReveal)
            {
                DrawHiddenRoomMarkers();
            }
            if (config.EnableDpsMeter && dpsMeterVisible)
            {
                DrawDpsMeter();
            }
        }

        private void EnsureOverlayStyles()
        {
            if (overlayBackground == null)
            {
                overlayBackground = new Texture2D(1, 1);
                overlayBackground.SetPixel(0, 0, new Color(0.055f, 0.045f, 0.075f, 0.88f));
                overlayBackground.Apply();
            }
            if (overlayTitleStyle == null)
            {
                overlayTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(1f, 0.82f, 0.45f) }
                };
            }
            if (overlayRowStyle == null)
            {
                overlayRowStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }
            if (hiddenRoomStyle == null)
            {
                hiddenRoomStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.3f, 0.72f) }
                };
            }
        }

        private void DrawHiddenRoomMarkers()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            HiddenRoomTriggerCollider[] entrances =
                Object.FindObjectsByType<HiddenRoomTriggerCollider>(FindObjectsSortMode.None);
            foreach (HiddenRoomTriggerCollider entrance in entrances)
            {
                if (entrance == null || !entrance.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 screen = camera.WorldToScreenPoint(entrance.transform.position);
                if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                    screen.y < 0f || screen.y > Screen.height)
                {
                    continue;
                }
                float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 4f) * 0.2f;
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, pulse);
                GUI.Label(new Rect(screen.x - 90f, Screen.height - screen.y - 58f, 180f, 42f),
                    "▼ 隐藏房间", hiddenRoomStyle);
                GUI.color = oldColor;
            }
        }

        private void DrawDpsMeter()
        {
            DpsPlayerRow[] rows = dpsRows.Values.OrderByDescending(row => row.Damage).ToArray();
            float teamDamage = rows.Sum(row => row.Damage);
            float duration = dpsCombatStartTime < 0f
                ? 0f
                : Mathf.Max(0.1f, (dpsRoomActive ? Time.unscaledTime : dpsCombatEndTime) - dpsCombatStartTime);
            float width = 400f;
            float height = 76f + Mathf.Max(1, rows.Length) * 28f;
            Rect panel = new Rect(Screen.width - width - 24f, 24f, width, height);
            GUI.DrawTexture(panel, overlayBackground);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, width - 28f, 28f),
                "DPS统计  房间#" + dpsRoomSequence + "  " + duration.ToString("0.0") + "秒  [" +
                dpsMeterToggleKey + "]", overlayTitleStyle);

            float y = panel.y + 40f;
            if (rows.Length == 0)
            {
                GUI.Label(new Rect(panel.x + 14f, y, width - 28f, 26f),
                    dpsRoomActive ? "等待本房间伤害数据" : "进入战斗房间后开始统计", overlayRowStyle);
            }
            for (int i = 0; i < rows.Length; i++)
            {
                DpsPlayerRow rowData = rows[i];
                float damage = rowData.Damage;
                float dps = duration > 0f ? damage / duration : 0f;
                float share = teamDamage > 0f ? damage / teamDamage * 100f : 0f;
                string row = (i + 1) + ". " + rowData.Name + "   " +
                             Mathf.FloorToInt(damage) + "  |  " + Mathf.FloorToInt(dps) + " DPS  |  " +
                             share.ToString("0.0") + "%";
                GUI.Label(new Rect(panel.x + 14f, y, width - 28f, 26f), row, overlayRowStyle);
                y += 28f;
            }
        }

        private void HandleArtifactFilterUI()
        {
            if (!config.EnableArtifactSchoolFilter || !NetworkServer.active || UIManager.Instance == null)
            {
                return;
            }

            UI_MiraclePanel panel = UIManager.Instance.GetElement<UI_MiraclePanel>();
            if (panel == null || !panel.IsOpened)
            {
                if (artifactFilterContainer != null)
                {
                    artifactFilterContainer.SetActive(false);
                }
                return;
            }

            if (artifactFilterContainer == null || artifactFilterOwner != panel)
            {
                CreateArtifactFilterUI(panel);
            }

            if (artifactFilterContainer != null)
            {
                artifactFilterContainer.SetActive(true);
            }
        }

        private void CreateArtifactFilterUI(UI_MiraclePanel panel)
        {
            if (panel.rerollButton == null)
            {
                return;
            }

            if (artifactFilterContainer != null)
            {
                Object.Destroy(artifactFilterContainer);
            }
            artifactFilterButtons.Clear();

            EnsureOriginalMiraclePool();
            if (originalTierOneMiracles == null)
            {
                return;
            }

            List<string> categories = originalTierOneMiracles
                .Where(miracle => miracle != null && miracle.categories != null)
                .SelectMany(miracle => miracle.categories)
                .Where(category => !string.IsNullOrWhiteSpace(category) && ItemDatabase.FindItemCategory(category) != null)
                .Distinct()
                .OrderBy(category => ItemDatabase.FindItemCategory(category).Name)
                .ToList();

            artifactFilterContainer = new GameObject(
                "SephiriaPlus_ArtifactSchoolFilter",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            artifactFilterContainer.transform.SetParent(panel.transform, false);
            RectTransform containerRect = artifactFilterContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.anchoredPosition = new Vector2(0f, -36f);
            containerRect.sizeDelta = new Vector2(1500f, 116f);

            GridLayoutGroup grid = artifactFilterContainer.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(140f, 50f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 10;
            grid.childAlignment = TextAnchor.UpperCenter;

            CreateArtifactFilterButton(panel.rerollButton, string.Empty, "无", null);
            foreach (string category in categories)
            {
                ItemCategoryEntity entity = ItemDatabase.FindItemCategory(category);
                CreateArtifactFilterButton(panel.rerollButton, category, entity.Name, entity.categoryIcon);
            }

            RefreshArtifactFilterButtonLabels();
            artifactFilterOwner = panel;
            artifactFilterContainer.transform.SetAsLastSibling();
            Debug.Log("[SephiriaPlus] artifact school filter created with " + categories.Count + " categories.");
        }

        private void CreateArtifactFilterButton(GameObject source, string category, string displayName, Sprite iconSprite)
        {
            GameObject buttonObject = Object.Instantiate(source, artifactFilterContainer.transform);
            buttonObject.name = "SephiriaPlus_ArtifactFilter_" + (string.IsNullOrEmpty(category) ? "None" : category);
            buttonObject.SetActive(true);

            UI_HorayButton button = buttonObject.GetComponent<UI_HorayButton>();
            if (button == null)
            {
                button = buttonObject.GetComponentInChildren<UI_HorayButton>(true);
            }
            if (button == null)
            {
                Object.Destroy(buttonObject);
                return;
            }

            button.onClick.RemoveAllListeners();
            string capturedCategory = category;
            button.onClick.AddListener(() => SelectArtifactCategory(capturedCategory));
            foreach (MonoBehaviour behaviour in buttonObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != button &&
                    behaviour.GetType().Name.IndexOf("Localiz", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    behaviour.enabled = false;
                }
            }

            TextMeshProUGUI[] labels = buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                label.name = "SephiriaPlus_FilterLabel_" + displayName;
                label.fontSize = Mathf.Min(label.fontSize, 24f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 24f;
                label.text = displayName;
            }

            if (iconSprite != null)
            {
                GameObject iconObject = new GameObject("SephiriaPlus_FilterIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(8f, 0f);
                iconRect.sizeDelta = new Vector2(26f, 26f);
                Image icon = iconObject.GetComponent<Image>();
                icon.sprite = iconSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            ArtifactFilterButtonMarker marker = buttonObject.AddComponent<ArtifactFilterButtonMarker>();
            marker.Category = category;
            marker.DisplayName = displayName;
            marker.Labels = labels;
            artifactFilterButtons.Add(buttonObject);
        }

        private void SelectArtifactCategory(string category)
        {
            selectedArtifactCategory = category ?? string.Empty;
            ApplyArtifactCategoryFilter();
            RefreshArtifactFilterButtonLabels();
            Debug.Log("[SephiriaPlus] artifact school filter selected: " +
                      (string.IsNullOrEmpty(selectedArtifactCategory) ? "None" : selectedArtifactCategory) + ".");
        }

        private void EnsureOriginalMiraclePool()
        {
            if (originalTierOneMiracles != null || MiraclePoolsField == null)
            {
                return;
            }

            Dictionary<Miracle.ETier, List<Miracle>> pools =
                MiraclePoolsField.GetValue(null) as Dictionary<Miracle.ETier, List<Miracle>>;
            if (pools != null && pools.TryGetValue(Miracle.ETier.Tier1, out List<Miracle> tierOne))
            {
                originalTierOneMiracles = new List<Miracle>(tierOne);
            }
        }

        private void ApplyArtifactCategoryFilter()
        {
            EnsureOriginalMiraclePool();
            Dictionary<Miracle.ETier, List<Miracle>> pools = MiraclePoolsField != null
                ? MiraclePoolsField.GetValue(null) as Dictionary<Miracle.ETier, List<Miracle>>
                : null;
            if (pools == null || originalTierOneMiracles == null)
            {
                return;
            }

            List<Miracle> filtered = string.IsNullOrEmpty(selectedArtifactCategory)
                ? new List<Miracle>(originalTierOneMiracles)
                : originalTierOneMiracles.Where(miracle => miracle != null && miracle.categories != null &&
                    miracle.categories.Contains(selectedArtifactCategory)).ToList();
            if (filtered.Count == 0)
            {
                Debug.LogWarning("[SephiriaPlus] selected artifact school has no available miracles; using the normal pool.");
                filtered = new List<Miracle>(originalTierOneMiracles);
            }
            pools[Miracle.ETier.Tier1] = filtered;
        }

        private void RefreshArtifactFilterButtonLabels()
        {
            foreach (GameObject buttonObject in artifactFilterButtons)
            {
                if (buttonObject == null)
                {
                    continue;
                }
                ArtifactFilterButtonMarker marker = buttonObject.GetComponent<ArtifactFilterButtonMarker>();
                if (marker == null)
                {
                    continue;
                }
                string text = marker.Category == selectedArtifactCategory
                    ? "[" + marker.DisplayName + "]"
                    : marker.DisplayName;
                foreach (TextMeshProUGUI label in marker.Labels)
                {
                    if (label != null)
                    {
                        label.text = text;
                    }
                }
            }
        }

        private void UpdateCheckpoint(PlayerAvatar hostPlayer)
        {
            if (!config.EnableCheckpointRetry || retryInProgress || hostPlayer == null ||
                SaveManager.Current == null || SaveManager.CurrentRun == null ||
                !SaveManager.CurrentRun.GetBool("RunStarted", false))
            {
                return;
            }

            string floorGuid = hostPlayer.currentFloorGuid;
            if (string.IsNullOrWhiteSpace(floorGuid))
            {
                return;
            }

            if (floorGuid != observedFloorGuid)
            {
                observedFloorGuid = floorGuid;
                checkpointCaptureTime = Time.unscaledTime + 0.75f;
            }

            if (checkpointCaptureTime < 0f || Time.unscaledTime < checkpointCaptureTime)
            {
                return;
            }

            checkpointCaptureTime = -1f;
            checkpointCurrent = SaveManager.Current.Copy();
            checkpointRun = SaveManager.CurrentRun.Copy();
            checkpointCurrent.enableSave = true;
            checkpointRun.enableSave = true;
            checkpointFloorGuid = floorGuid;
            Debug.Log("[SephiriaPlus] checkpoint captured at floor " + checkpointFloorGuid + ".");
        }

        private void HandleCheckpointRetryInput()
        {
            if (!config.EnableCheckpointRetry)
            {
                return;
            }

            UI_GameOverLabel gameOver = UIManager.Instance != null
                ? UIManager.Instance.GetElement<UI_GameOverLabel>()
                : null;
            EnsureRetryButton(gameOver);

            if (gameOver == null || !gameOver.IsOpened || retryInProgress || !NetworkServer.active ||
                checkpointCurrent == null || checkpointRun == null || Keyboard.current == null ||
                !Keyboard.current[checkpointRetryKey].wasPressedThisFrame)
            {
                return;
            }

            RequestCheckpointRetry();
        }

        private void EnsureRetryButton(UI_GameOverLabel gameOver)
        {
            if (gameOver == null)
            {
                return;
            }

            if (retryButton == null || retryButtonOwner != gameOver)
            {
                GameObject source = gameOver.treeShopButton != null
                    ? gameOver.treeShopButton
                    : gameOver.button != null ? gameOver.button.gameObject : null;
                if (source == null || source.transform.parent == null)
                {
                    return;
                }

                GameObject retryObject = Object.Instantiate(source, source.transform.parent);
                retryObject.name = "SephiriaPlus_RetryCheckpointButton";
                if (gameOver.button != null && gameOver.button.transform.parent == retryObject.transform.parent)
                {
                    retryObject.transform.SetSiblingIndex(gameOver.button.transform.GetSiblingIndex());
                }

                retryButton = retryObject.GetComponent<UI_HorayButton>();
                if (retryButton == null)
                {
                    retryButton = retryObject.GetComponentInChildren<UI_HorayButton>(true);
                }

                if (retryButton == null)
                {
                    Object.Destroy(retryObject);
                    return;
                }

                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(RequestCheckpointRetry);
                retryButtonLabels = retryObject.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (MonoBehaviour behaviour in retryObject.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != retryButton &&
                        behaviour.GetType().Name.IndexOf("Localiz", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        behaviour.enabled = false;
                    }
                }

                retryButtonOwner = gameOver;
                retryButtonObject = retryObject;
                retryButtonLayoutApplied = false;
                retryObject.SetActive(false);
                Debug.Log("[SephiriaPlus] retry button added to the game-over screen.");
            }

            bool buttonsVisible = gameOver.IsOpened;
            if (buttonsVisible && !retryButtonLayoutApplied)
            {
                ApplyRetryButtonLayout(gameOver);
            }
            bool canRetry = buttonsVisible && NetworkServer.active &&
                            checkpointCurrent != null && checkpointRun != null && !retryInProgress;
            retryButtonObject.SetActive(buttonsVisible);
            retryButton.interactable = canRetry;
            string buttonText = checkpointCurrent == null ? "无检查点" : retryInProgress ? "载入中" : "重试";
            foreach (TextMeshProUGUI label in retryButtonLabels)
            {
                if (label != null)
                {
                    label.text = buttonText;
                }
            }
        }

        private void ApplyRetryButtonLayout(UI_GameOverLabel gameOver)
        {
            RectTransform destinyRect = gameOver.treeShopButton != null
                ? gameOver.treeShopButton.GetComponent<RectTransform>()
                : null;
            RectTransform returnRect = gameOver.button != null
                ? gameOver.button.GetComponent<RectTransform>()
                : null;
            RectTransform retryRect = retryButtonObject != null
                ? retryButtonObject.GetComponent<RectTransform>()
                : null;
            if (destinyRect == null || returnRect == null || retryRect == null)
            {
                Debug.LogWarning("[SephiriaPlus] retry button layout is waiting for game-over button RectTransforms.");
                return;
            }

            float originalWidth = Mathf.Min(destinyRect.rect.width, returnRect.rect.width);
            float buttonWidth = Mathf.Max(100f, originalWidth * 0.48f);
            destinyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            returnRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            retryRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            Vector3 destinyPosition = destinyRect.position;
            Vector3 returnPosition = returnRect.position;
            Vector3 retryPosition = (destinyPosition + returnPosition) * 0.5f;
            Vector3 buttonDirection = (returnPosition - destinyPosition).normalized;
            float worldButtonWidth = buttonWidth * Mathf.Abs(destinyRect.lossyScale.x);
            float visibleGap = worldButtonWidth * 0.16f;
            float centerSpacing = worldButtonWidth + visibleGap;
            destinyRect.position = retryPosition - buttonDirection * centerSpacing;
            returnRect.position = retryPosition + buttonDirection * centerSpacing;
            retryRect.position = retryPosition;
            retryRect.SetAsLastSibling();
            retryButtonLayoutApplied = true;
            Debug.Log("[SephiriaPlus] retry button world-position layout applied at " + retryRect.position +
                      " between " + destinyRect.position + " and " + returnRect.position + ".");
        }

        private void RequestCheckpointRetry()
        {
            if (!config.EnableCheckpointRetry || retryInProgress || !NetworkServer.active ||
                checkpointCurrent == null || checkpointRun == null)
            {
                return;
            }

            StartCoroutine(RestoreCheckpointCoroutine());
        }

        private IEnumerator RestoreCheckpointCoroutine()
        {
            retryInProgress = true;
            HorayNetworkManager networkManager = NetworkManager.singleton as HorayNetworkManager;
            if (networkManager == null || CurrentSaveField == null || CurrentRunSaveField == null)
            {
                Debug.LogError("[SephiriaPlus] checkpoint retry is unavailable because required game fields were not found.");
                retryInProgress = false;
                yield break;
            }

            SaveData previousRun = SaveManager.CurrentRun;
            int chapter = checkpointRun.GetInt("CurrentGame", -1);
            Debug.Log("[SephiriaPlus] restoring checkpoint at floor " + checkpointFloorGuid + ".");
            networkManager.RestartGame(false, chapter);

            float timeout = Time.unscaledTime + 5f;
            while (ReferenceEquals(SaveManager.CurrentRun, previousRun) && Time.unscaledTime < timeout)
            {
                yield return null;
            }

            if (ReferenceEquals(SaveManager.CurrentRun, previousRun))
            {
                Debug.LogError("[SephiriaPlus] checkpoint retry timed out while waiting for a new run save.");
                retryInProgress = false;
                yield break;
            }

            SaveData restoredCurrent = checkpointCurrent.Copy();
            SaveData restoredRun = checkpointRun.Copy();
            restoredCurrent.enableSave = true;
            restoredRun.enableSave = true;
            CurrentSaveField.SetValue(null, restoredCurrent);
            CurrentRunSaveField.SetValue(null, restoredRun);
            SaveManager.Save(true, true);

            yield return new WaitForSecondsRealtime(1.5f);
            retryInProgress = false;
            Debug.Log("[SephiriaPlus] checkpoint restored. Press " + checkpointRetryKey + " after another defeat to retry again.");
        }

        private void OnDestroy()
        {
            if (artifactFilterContainer != null)
            {
                Object.Destroy(artifactFilterContainer);
            }

            if (originalTierOneMiracles != null && MiraclePoolsField != null)
            {
                Dictionary<Miracle.ETier, List<Miracle>> pools =
                    MiraclePoolsField.GetValue(null) as Dictionary<Miracle.ETier, List<Miracle>>;
                if (pools != null)
                {
                    pools[Miracle.ETier.Tier1] = new List<Miracle>(originalTierOneMiracles);
                }
            }

            if (!NetworkServer.active)
            {
                return;
            }

            PlayerAvatar[] players = Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            foreach (PlayerAvatar player in players)
            {
                if (player != null && player.isServer &&
                    talentPointStates.TryGetValue(player.GetInstanceID(), out TalentPointState state))
                {
                    player.NetworkmaxPassivePoint -= state.VanillaAddedPoints * (config.TalentPointMultiplier - 1);
                }
            }
        }
    }

    internal sealed class ArtifactFilterButtonMarker : MonoBehaviour
    {
        public string Category;
        public string DisplayName;
        public TextMeshProUGUI[] Labels;
    }

    internal sealed class DpsPlayerRow
    {
        public string Name = string.Empty;
        public float Damage;
    }

    internal sealed class DpsRoomScope
    {
        private readonly string floorGuid;
        private readonly int spawnerId;
        private readonly Rect bounds;

        private DpsRoomScope(string floor, int id, Rect roomBounds)
        {
            floorGuid = floor;
            spawnerId = id;
            bounds = roomBounds;
        }

        public static DpsRoomScope Create(string floor, int id, Vector2 lower, Vector2 upper)
        {
            if (string.IsNullOrEmpty(floor) || id == 0 || upper.x <= lower.x || upper.y <= lower.y)
            {
                return null;
            }
            return new DpsRoomScope(floor, id,
                Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y));
        }

        public bool Contains(float x, float y)
        {
            return x >= bounds.xMin && x <= bounds.xMax && y >= bounds.yMin && y <= bounds.yMax;
        }

        public bool AllowsPlayer(string playerFloor, float x, float y)
        {
            return string.Equals(floorGuid, playerFloor, System.StringComparison.Ordinal) && Contains(x, y);
        }

        public bool IsSameRoom(DpsRoomScope other)
        {
            return other != null && spawnerId == other.spawnerId &&
                   string.Equals(floorGuid, other.floorGuid, System.StringComparison.Ordinal);
        }

        public static DpsRoomScope SelectContaining(
            DpsRoomScope current,
            DpsRoomScope candidate,
            float x,
            float y)
        {
            if (candidate == null || !candidate.Contains(x, y))
            {
                return current;
            }
            if (current == null)
            {
                return candidate;
            }
            float currentArea = current.bounds.width * current.bounds.height;
            float candidateArea = candidate.bounds.width * candidate.bounds.height;
            return candidateArea < currentArea ? candidate : current;
        }
    }
}
