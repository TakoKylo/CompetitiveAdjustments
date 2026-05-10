using HarmonyLib;

namespace CompetitivePuckTweaks.src {
    internal class StickOnBodyCollisions {
        [HarmonyPatch(typeof(PlayerBodyV2), "OnNetworkPostSpawn")]
        public class PlayerBodyV2_OnNetworkPostSpawn_Patch {
            [HarmonyPostfix]
            public static void Postfix(PlayerBodyV2 __instance) {
                if (CompetitiveAdjustments.ConfigManager.Config?.CompAdjust.StickBodyCollision == null || !(bool)CompetitiveAdjustments.ConfigManager.Config?.CompAdjust.StickBodyCollision)
                    return;

                __instance.Rigidbody.mass = float.MaxValue;
            }
        }
    }
}
