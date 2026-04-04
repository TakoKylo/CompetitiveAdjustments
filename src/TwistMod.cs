// File: TwistMod.cs
// Enables twist functionality while sliding by patching the game's TwistLeft/TwistRight methods

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using DashfallCfg = CompetitiveAdjustments.DashfallConfig;

namespace DashFallMod
{
    public static class TwistMod
    {
        // Twist path marking (don't swallow twists coming from bridge)
        [ThreadStatic] internal static bool TwistFromBridge;

        // Cooldown tracking for slide twist (matches the effective time of double-tap in base game)
        internal static readonly Dictionary<ulong, float> LastSlideTwistAt = new Dictionary<ulong, float>();
        internal const float SlideTwistCooldown = 0.5f;

        private static readonly FieldInfo F_TwistVelocity = AccessTools.Field(typeof(PlayerBodyV2), "twistVelocity");
        private static readonly FieldInfo F_TwistStaminaDrain = AccessTools.Field(typeof(PlayerBodyV2), "twistStaminaDrain");

        private static DashfallCfg Config => ConfigManager.Config;

        public static void ProcessTwistFromBridge(PlayerBodyV2 body, bool left)
        {
            // Mod bind should ONLY do slide twist, not jump twist
            if (!body.IsSliding.Value) return;

            // Block twist during power carve (both lateral inputs active = carving)
            var player = body.Player;
            if (player != null)
            {
                var pi = player.PlayerInput;
                if (pi != null && pi.LateralLeftInput.ServerValue && pi.LateralRightInput.ServerValue)
                    return;
            }

            TwistFromBridge = true;
            try
            {
                if (left) body.TwistLeft();
                else body.TwistRight();
            }
            finally
            {
                TwistFromBridge = false;
            }
        }

        // Shared prefix logic for both TwistLeft and TwistRight patches.
        // Returns true to run vanilla, false to skip it (we handled it ourselves).
        internal static bool ProcessSlideTwistPrefix(PlayerBodyV2 __instance, bool left)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return true;

            var player = __instance.Player;
            if (player == null) return false;

            // Not sliding — let vanilla handle it (normal jump twist)
            if (!__instance.IsSliding.Value) return true;

            // Block twist during power carve
            var pi = player.PlayerInput;
            if (pi != null && pi.LateralLeftInput.ServerValue && pi.LateralRightInput.ServerValue)
                return false;

            bool isGoalie = player.Role == PlayerRole.Goalie;
            bool twistEnabled = isGoalie ? Config.GoalieTwistWhileSlidingEnabled : Config.EnableTwistWhileSliding;
            if (!twistEnabled) return true;

            string bindName = left ? "twistleft" : "twistright";
            bool hasTwistBind = PoncePuck.Keybinds.ServerBridge.IsBound(player.OwnerClientId, bindName);
            if (hasTwistBind && !TwistFromBridge)
                return false;

            var id = player.NetworkObjectId;
            float now = Time.time;
            if (LastSlideTwistAt.TryGetValue(id, out var lastTwist) && (now - lastTwist) < SlideTwistCooldown)
                return false;

            float twistVelocity = F_TwistVelocity != null ? (float)F_TwistVelocity.GetValue(__instance) : 5f;
            float twistStaminaDrain = F_TwistStaminaDrain != null ? (float)F_TwistStaminaDrain.GetValue(__instance) : 0.125f;

            if (__instance.Stamina.Value < twistStaminaDrain) return false;

            LastSlideTwistAt[id] = now;

            float scaledVelocity = twistVelocity * Config.SlideTwistForceScale;
            float direction = left ? -1f : 1f;
            __instance.Rigidbody.AddTorque(__instance.transform.up * (direction * scaledVelocity), ForceMode.VelocityChange);
            __instance.Stamina.Value = Mathf.Max(0f, __instance.Stamina.Value - twistStaminaDrain);

            ConfigManager.Dbg($"Slide Twist{(left ? "Left" : "Right")} applied: velocity={scaledVelocity:F2}");
            return false;
        }

        internal static void CleanupPlayer(ulong networkObjectId)
        {
            LastSlideTwistAt.Remove(networkObjectId);
        }
    }

    [HarmonyPatch(typeof(PlayerBodyV2), nameof(PlayerBodyV2.TwistLeft))]
    public static class TwistLeft_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerBodyV2 __instance) =>
            TwistMod.ProcessSlideTwistPrefix(__instance, left: true);
    }

    [HarmonyPatch(typeof(PlayerBodyV2), nameof(PlayerBodyV2.TwistRight))]
    public static class TwistRight_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerBodyV2 __instance) =>
            TwistMod.ProcessSlideTwistPrefix(__instance, left: false);
    }
}
