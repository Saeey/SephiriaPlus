using System.Collections;
using System.Collections.Generic;
using System.IO;
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
                   ", checkpointRetry=" + EnableCheckpointRetry + " (" + CheckpointRetryKey + ")";
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
        private bool retryButtonLayoutApplied;
        private float nextPollTime;

        public void Configure(ModConfig loadedConfig)
        {
            config = loadedConfig ?? new ModConfig();
            if (!System.Enum.TryParse(config.CheckpointRetryKey, true, out checkpointRetryKey))
            {
                checkpointRetryKey = Key.F8;
                Debug.LogWarning("[SephiriaPlus] invalid CheckpointRetryKey; using F8.");
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
                TextMeshProUGUI[] labels = retryObject.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (TextMeshProUGUI label in labels)
                {
                    label.text = "重试";
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
            if (retryButton.text != null)
            {
                retryButton.text.text = checkpointCurrent == null ? "无检查点" : retryInProgress ? "载入中" : "重试";
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
            float buttonWidth = Mathf.Max(100f, originalWidth * 0.55f);
            destinyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            returnRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            retryRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            retryRect.position = (destinyRect.position + returnRect.position) * 0.5f;
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
}
