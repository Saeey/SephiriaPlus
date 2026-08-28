using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;

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
                   ", inventory=" + EnableExtraInventorySlots + " (+" + ExtraInventorySlots + ")";
        }
    }

    internal sealed class SephiriaPlusController : MonoBehaviour
    {
        private const float PollIntervalSeconds = 0.25f;
        private static readonly FieldInfo AddedPassiveField = typeof(TreeShopItemStorage).GetField(
            "addedPassive",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<int, TalentPointState> talentPointStates = new Dictionary<int, TalentPointState>();
        private readonly HashSet<int> expandedInventories = new HashSet<int>();
        private ModConfig config = new ModConfig();
        private float nextPollTime;

        public void Configure(ModConfig loadedConfig)
        {
            config = loadedConfig ?? new ModConfig();
        }

        private sealed class TalentPointState
        {
            public int VanillaAddedPoints;
            public int LastAppliedCap;
        }

        private void Update()
        {
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
            foreach (PlayerAvatar player in players)
            {
                if (player == null || !player.isServer)
                {
                    continue;
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
