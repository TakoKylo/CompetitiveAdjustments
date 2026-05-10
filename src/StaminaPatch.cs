using HarmonyLib;
using System;
using System.Reflection;

namespace CompetitivePuckTweaks.src {
    public class StaminaPatch {
        [HarmonyPatch(typeof(PlayerBody), nameof(PlayerBody.OnNetworkSpawn))]
        public class PlayerBody_OnNetworkSpawn_Patch {
            [HarmonyPrefix]
            public static bool Prefix(PlayerBody __instance) {
                if (CompetitiveAdjustments.ConfigManager.Config == null)
                    return true;

                Type playerBodyType = typeof(PlayerBody);

                FieldInfo staminaRegenerationRateField = playerBodyType.GetField("staminaRegenerationRate", BindingFlags.NonPublic | BindingFlags.Instance);
                staminaRegenerationRateField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config?.CompTweaks.StaminaRegenerationRate);

                FieldInfo sprintStaminaDrainRateField = playerBodyType.GetField("sprintStaminaDrainRate", BindingFlags.NonPublic | BindingFlags.Instance);
                sprintStaminaDrainRateField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config?.CompTweaks.SprintStaminaDrainRate);

                FieldInfo jumpStaminaDrainField = playerBodyType.GetField("jumpStaminaDrain", BindingFlags.NonPublic | BindingFlags.Instance);
                jumpStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config?.CompTweaks.JumpStaminaDrain);

                FieldInfo twistStaminaDrainField = playerBodyType.GetField("twistStaminaDrain", BindingFlags.NonPublic | BindingFlags.Instance);
                twistStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config?.CompTweaks.TwistStaminaDrain);

                FieldInfo dashStaminaDrainField = playerBodyType.GetField("dashStaminaDrain", BindingFlags.NonPublic | BindingFlags.Instance);
                dashStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config?.CompTweaks.DashStaminaDrain);

                CompetitiveAdjustments.ConfigManager.Dbg($"Adjusted stamina related values on PlayerBody {__instance.name}");

                return true;
            }
        }
    }
}
