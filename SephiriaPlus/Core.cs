using System.Collections.Generic;
using System.Reflection;
using Mirror;
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
            controllerObject = new GameObject("SephiriaPlusController");
            Object.DontDestroyOnLoad(controllerObject);
            controllerObject.AddComponent<SephiriaPlusController>();
            Debug.Log(LogPrefix + " loaded. The host will refill reroll dice for every player.");
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

    internal sealed class SephiriaPlusController : MonoBehaviour
    {
        private const int RefillTarget = 99;
        private const int TalentPointMultiplier = 10;
        private const short ExtraInventorySlots = 18;
        private const float PollIntervalSeconds = 0.25f;
        private static readonly FieldInfo AddedPassiveField = typeof(TreeShopItemStorage).GetField(
            "addedPassive",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<int, TalentPointState> talentPointStates = new Dictionary<int, TalentPointState>();
        private readonly HashSet<int> expandedInventories = new HashSet<int>();
        private float nextPollTime;

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

                if (player.rerollDice < RefillTarget)
                {
                    player.AddDice(RefillTarget - player.rerollDice);
                }

                GridInventory inventory = player.Inventory;
                int inventoryInstanceId = inventory != null ? inventory.GetInstanceID() : 0;
                if (inventory != null && inventory.isServer && !expandedInventories.Contains(inventoryInstanceId))
                {
                    inventory.AddStorage(ExtraInventorySlots);
                    expandedInventories.Add(inventoryInstanceId);
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
                    normalizedCap -= state.VanillaAddedPoints * (TalentPointMultiplier - 1);
                }
                else if (!isNewState && player.maxPassivePoint == state.LastAppliedCap)
                {
                    normalizedCap -= vanillaAddedPoints * (TalentPointMultiplier - 1);
                }

                int multipliedCap = normalizedCap + vanillaAddedPoints * (TalentPointMultiplier - 1);
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
                    player.NetworkmaxPassivePoint -= state.VanillaAddedPoints * (TalentPointMultiplier - 1);
                }
            }
        }
    }
}
