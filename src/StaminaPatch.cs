using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace CompetitivePuckTweaks.src {
    public class StaminaPatch {
        private static readonly LockDictionary<ulong, int> _frames = new LockDictionary<ulong, int>();

        [HarmonyPatch(typeof(PlayerBody), nameof(PlayerBody.OnNetworkSpawn))]
        public static class PlayerBody_OnNetworkSpawn_Patch {
            [HarmonyPrefix]
            public static bool Prefix(PlayerBody __instance) {
                if (CompetitiveAdjustments.ConfigManager.Config == null)
                    return true;

                Type playerBodyType = typeof(PlayerBody);

                FieldInfo staminaRegenerationRateField = playerBodyType.GetField("staminaRegenerationRate", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo sprintStaminaDrainRateField = playerBodyType.GetField("sprintStaminaDrainRate", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo jumpStaminaDrainField = playerBodyType.GetField("jumpStaminaDrain", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo twistStaminaDrainField = playerBodyType.GetField("twistStaminaDrain", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo dashStaminaDrainField = playerBodyType.GetField("dashStaminaDrain", BindingFlags.NonPublic | BindingFlags.Instance);

                // Goalie and attacker ship as separate prefabs with their own [SerializeField]
                // stamina values. Overwriting them on goalies forces them to skater values
                // (notably dashStaminaDrain), which makes goalie dashes cost less than base game.
                if (__instance.name.ToLower().Contains("goalie")) {
                    staminaRegenerationRateField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.GoalieStaminaRegenerationRate);
                    sprintStaminaDrainRateField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.GoalieSprintStaminaDrainRate);
                    jumpStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.GoalieJumpStaminaDrain);
                    twistStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.GoalieTwistStaminaDrain);
                    dashStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.GoalieDashStaminaDrain);
                }
                else {
                    staminaRegenerationRateField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.StaminaRegenerationRate);
                    sprintStaminaDrainRateField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.SprintStaminaDrainRate);
                    jumpStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.JumpStaminaDrain);
                    twistStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.TwistStaminaDrain);
                    dashStaminaDrainField?.SetValue(__instance, CompetitiveAdjustments.ConfigManager.Config.CompTweaks.DashStaminaDrain);
                }

                CompetitiveAdjustments.ConfigManager.Dbg($"Adjusted stamina related values on PlayerBody {__instance.name}");

                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerBody), "FixedUpdate")]
        public static class PlayerBody_FixedUpdate_Patch {
            [HarmonyPrefix]
            public static bool Prefix(PlayerBody __instance) {
                if (CompetitiveAdjustments.ConfigManager.Config == null)
                    return true;

                if (!_frames.TryGetValue(__instance.NetworkObjectId, out int frame)) {
                    frame = 0;
                    _frames.Add(__instance.NetworkObjectId, frame);
                }

                if (__instance.IsSprinting.Value) {
                    frame++;
                    if (frame % 2 == 0)
                        __instance.Stamina.Value += Time.fixedDeltaTime * CompetitiveAdjustments.ConfigManager.Config.CompTweaks.SprintStaminaDrainRateOffset * 2;
                }
                else
                    frame = 0;

                _frames[__instance.NetworkObjectId] = frame;

                return true;
            }
        }

        // Clear per-player sprint-frame counters when the game ends so stale
        // NetworkObjectIds from prior matches don't leak across games.
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_SetGameState))]
        public static class GameManager_Server_SetGameState_Patch {
            [HarmonyPostfix]
            public static void Postfix(GamePhase? phase, int? tick, int? period, int? blueScore, int? redScore, bool? isOvertime) {
                if (phase == GamePhase.GameOver)
                    _frames.Clear();
            }
        }
    }
}
