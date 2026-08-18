using System;
using System.Collections.Generic;
using HarmonyLib;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{

[Info("FixDeepSeaTransfer", "miyagawayuu", "0.3.0")]
[Description("Prevents PlayerBoat passengers from being stranded, killed, or capsized during Deep Sea transfers.")]
public class FixDeepSeaTransfer : RustPlugin
{
    private static FixDeepSeaTransfer _instance;
    private readonly Dictionary<ulong, int> _deepSeaTransferPlayerScopes =
        new Dictionary<ulong, int>();

    private sealed class DeepSeaPassengerTeleportState
    {
        public BasePlayer Player;
        public Vector3 LocalPosition;
        public bool WasMounted;
    }

    private sealed class DeepSeaVehicleTeleportState
    {
        public BaseEntity Vehicle;
        public readonly List<DeepSeaPassengerTeleportState> Passengers =
            new List<DeepSeaPassengerTeleportState>();
    }

    private void Init()
    {
        _instance = this;
    }

    private void Unload()
    {
        _deepSeaTransferPlayerScopes.Clear();
        _instance = null;
    }

    private static DeepSeaVehicleTeleportState CaptureDeepSeaVehiclePassengers(BaseEntity entity)
    {
        if (!(entity is BaseVehicle) || entity.IsDestroyed)
        {
            return null;
        }

        var state = new DeepSeaVehicleTeleportState
        {
            Vehicle = entity,
        };
        var passengers = new List<BasePlayer>();
        BaseVehicle.GetPassengersForVehicle(entity, passengers);
        foreach (var player in passengers)
        {
            if (player == null || !player.IsConnected || player.IsNpc)
            {
                continue;
            }

            state.Passengers.Add(new DeepSeaPassengerTeleportState
            {
                Player = player,
                LocalPosition = entity.transform.InverseTransformPoint(player.transform.position),
                WasMounted = player.isMounted,
            });
        }

        return state.Passengers.Count > 0 ? state : null;
    }

    private void BeginDeepSeaTransfer(DeepSeaVehicleTeleportState state)
    {
        if (state == null)
        {
            return;
        }

        foreach (var passenger in state.Passengers)
        {
            var player = passenger.Player;
            if (!passenger.WasMounted || player == null)
            {
                continue;
            }

            _deepSeaTransferPlayerScopes.TryGetValue(player.userID, out var depth);
            _deepSeaTransferPlayerScopes[player.userID] = depth + 1;
        }
    }

    private void EndDeepSeaTransfer(DeepSeaVehicleTeleportState state)
    {
        if (state == null)
        {
            return;
        }

        foreach (var passenger in state.Passengers)
        {
            var player = passenger.Player;
            if (!passenger.WasMounted
                || player == null
                || !_deepSeaTransferPlayerScopes.TryGetValue(player.userID, out var depth))
            {
                continue;
            }

            if (depth <= 1)
            {
                _deepSeaTransferPlayerScopes.Remove(player.userID);
            }
            else
            {
                _deepSeaTransferPlayerScopes[player.userID] = depth - 1;
            }
        }
    }

    private bool IsPlayerInDeepSeaTransfer(BasePlayer player)
    {
        return player != null
            && _deepSeaTransferPlayerScopes.TryGetValue(player.userID, out var depth)
            && depth > 0;
    }

    private static void SynchronizeDeepSeaVehiclePassengers(DeepSeaVehicleTeleportState state)
    {
        if (state == null || state.Vehicle == null || state.Vehicle.IsDestroyed)
        {
            return;
        }

        foreach (var passenger in state.Passengers)
        {
            var player = passenger.Player;
            if (player == null || !player.IsConnected || player.IsDead())
            {
                continue;
            }

            var targetPosition = state.Vehicle.transform.TransformPoint(passenger.LocalPosition);
            player.Teleport(targetPosition);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate();
        }
    }

    private static void LevelDeepSeaPlayerBoat(PlayerBoat boat)
    {
        if (boat == null || boat.IsDestroyed)
        {
            return;
        }

        // Deep Sea teleportation preserves the vehicle rotation. A boat that is
        // leaning as it crosses the portal can therefore arrive with the same
        // roll/pitch and immediately capsize. Preserve its heading while making
        // it level before PlayerBoat.Teleport clears its physical momentum.
        var heading = boat.transform.eulerAngles.y;
        boat.transform.rotation = Quaternion.Euler(0f, heading, 0f);
    }

    private static void RecoverStrandedDeepSeaPassengers(
        DeepSeaVehicleTeleportState state,
        bool shouldBeInsideDeepSea)
    {
        if (state == null
            || state.Vehicle == null
            || state.Vehicle.IsDestroyed
            || DeepSeaManager.IsInsideDeepSea(state.Vehicle) != shouldBeInsideDeepSea)
        {
            return;
        }

        foreach (var passenger in state.Passengers)
        {
            var player = passenger.Player;
            if (player == null
                || !player.IsConnected
                || player.IsDead()
                || DeepSeaManager.IsInsideDeepSea(player) == shouldBeInsideDeepSea)
            {
                continue;
            }

            var recoveryPosition = state.Vehicle.transform.TransformPoint(passenger.LocalPosition);
            player.Teleport(recoveryPosition);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate();
            _instance?.PrintWarning(
                "Recovered a DeepSea passenger left behind during "
                + (shouldBeInsideDeepSea ? "entry" : "exit")
                + ": player="
                + player.userID
                + ", vehicle="
                + (state.Vehicle.net != null ? state.Vehicle.net.ID.Value : 0uL));
        }
    }

    [AutoPatch]
    [HarmonyPatch(typeof(DeepSeaManager), "TeleportEntity")]
    private static class TeleportDeepSeaEntityPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BaseEntity entity, Vector3 position)
        {
            if (!(entity is PlayerBoat boat) || boat.IsDestroyed)
            {
                return true;
            }

            var state = CaptureDeepSeaVehiclePassengers(boat);
            LevelDeepSeaPlayerBoat(boat);
            boat.Teleport(position);
            SynchronizeDeepSeaVehiclePassengers(state);
            return false;
        }
    }

    [AutoPatch]
    [HarmonyPatch(typeof(DeepSeaManager), "MoveToDeepSea")]
    private static class MoveToDeepSeaPatch
    {
        [HarmonyPrefix]
        private static void Prefix(BaseEntity entity, ref DeepSeaVehicleTeleportState __state)
        {
            __state = CaptureDeepSeaVehiclePassengers(entity);
            _instance?.BeginDeepSeaTransfer(__state);
        }

        [HarmonyPostfix]
        private static void Postfix(BaseEntity entity, DeepSeaVehicleTeleportState __state)
        {
            RecoverStrandedDeepSeaPassengers(__state, shouldBeInsideDeepSea: true);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, DeepSeaVehicleTeleportState __state)
        {
            _instance?.EndDeepSeaTransfer(__state);
            return __exception;
        }
    }

    [AutoPatch]
    [HarmonyPatch(typeof(DeepSeaManager), "MoveToMainIsland")]
    private static class MoveToMainIslandPatch
    {
        [HarmonyPrefix]
        private static void Prefix(BaseEntity entity, ref DeepSeaVehicleTeleportState __state)
        {
            __state = CaptureDeepSeaVehiclePassengers(entity);
            _instance?.BeginDeepSeaTransfer(__state);
        }

        [HarmonyPostfix]
        private static void Postfix(BaseEntity entity, DeepSeaVehicleTeleportState __state)
        {
            RecoverStrandedDeepSeaPassengers(__state, shouldBeInsideDeepSea: false);

            var instance = _instance;
            if (instance == null || __state == null)
            {
                return;
            }

            instance.timer.Once(0.1f, () =>
            {
                if (_instance == instance)
                {
                    RecoverStrandedDeepSeaPassengers(__state, shouldBeInsideDeepSea: false);
                }
            });
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, DeepSeaVehicleTeleportState __state)
        {
            _instance?.EndDeepSeaTransfer(__state);
            return __exception;
        }
    }

    [AutoPatch]
    [HarmonyPatch(typeof(BaseMountable), "DismountPlayer", typeof(BasePlayer), typeof(bool))]
    private static class DeepSeaTransferDismountPatch
    {
        [HarmonyPrefix]
        private static void Prefix(BasePlayer player, ref bool lite)
        {
            if (_instance?.IsPlayerInDeepSeaTransfer(player) == true)
            {
                lite = true;
            }
        }
    }
}
}
