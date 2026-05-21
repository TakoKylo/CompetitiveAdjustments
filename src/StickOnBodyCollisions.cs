using HarmonyLib;
using UnityEngine;

namespace CompetitivePuckTweaks.src {
    public class StickOnBodyCollisions {
        private const float STICK_FORCE_SOUND_THRESHOLD = 17.5f;
        private const int STICK_LAYER = 6;

        // Property rather than `static readonly` so flipping the master flag
        // (EnableCompAdjust) or the per-feature flag (StickBodyCollision) at
        // runtime takes effect on the next patched call.  A field initializer
        // would freeze the value at class-init time, before the user has had
        // a chance to load their config edits.
        private static bool _disablePatch =>
            CompetitiveAdjustments.ConfigManager.CompAdjustEffective?.StickBodyCollision != true;

        [HarmonyPatch(typeof(PlayerBodyV2), "OnNetworkPostSpawn")]
        public class PlayerBodyV2_OnNetworkPostSpawn_Patch {
            [HarmonyPostfix]
            public static void Postfix(PlayerBodyV2 __instance) {
                if (_disablePatch)
                    return;

                __instance.Rigidbody.mass = float.MaxValue;
            }
        }

        [HarmonyPatch(typeof(PlayerBodyV2), "Server_OnCollisionDeferred")]
        public class PlayerBodyV2_Server_OnCollisionDeferred_Patch {
            [HarmonyPrefix]
            public static bool Prefix(GameObject gameObject, float force) {
                if (_disablePatch)
                    return true;

                if (gameObject.layer == STICK_LAYER && force < STICK_FORCE_SOUND_THRESHOLD)
                    return false;

                return true;
            }
        }
    }
}
