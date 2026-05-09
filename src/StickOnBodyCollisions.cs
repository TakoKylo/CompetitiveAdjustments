using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CompetitivePuckTweaks.src {
    internal class StickOnBodyCollisions {
        private static Dictionary<Rigidbody, Vector3> _linearVelocityBeforeCollision = new Dictionary<Rigidbody, Vector3>();
        private static Dictionary<Rigidbody, Vector3> _angularVelocityBeforeCollision = new Dictionary<Rigidbody, Vector3>();

        [HarmonyPatch(typeof(PlayerBodyV2), "FixedUpdate")]
        public class PlayerBodyV2_FixedUpdate_Patch {
            [HarmonyPostfix]
            public static void Postfix(PlayerBodyV2 __instance) {
                if (CompetitiveAdjustments.ConfigManager.Config?.CompAdjust.StickBodyCollision == null || !(bool)CompetitiveAdjustments.ConfigManager.Config?.CompAdjust.StickBodyCollision)
                    return;

                if (!_linearVelocityBeforeCollision.TryAdd(__instance.Rigidbody, __instance.Rigidbody.linearVelocity))
                    _linearVelocityBeforeCollision[__instance.Rigidbody] = __instance.Rigidbody.linearVelocity;

                if (!_angularVelocityBeforeCollision.TryAdd(__instance.Rigidbody, __instance.Rigidbody.angularVelocity))
                    _angularVelocityBeforeCollision[__instance.Rigidbody] = __instance.Rigidbody.angularVelocity;
            }
        }

        [HarmonyPatch(typeof(PlayerBodyV2), "OnCollisionEnter")]
        public class PlayerBodyV2_OnCollisionEnter_Patch {
            [HarmonyPrefix]
            public static bool Prefix(PlayerBodyV2 __instance, Collision collision) {
                if (CompetitiveAdjustments.ConfigManager.Config?.CompAdjust.StickBodyCollision == null || !(bool)CompetitiveAdjustments.ConfigManager.Config?.CompAdjust.StickBodyCollision)
                    return true;

                if (collision.gameObject.layer == 6) {
                    //__instance.Rigidbody.linearVelocity = _linearVelocityBeforeCollision[__instance.Rigidbody];
                    //__instance.Rigidbody.angularVelocity = _angularVelocityBeforeCollision[__instance.Rigidbody];

                    //collision.rigidbody.linearVelocity = -collision.rigidbody.linearVelocity;
                    //collision.rigidbody.angularVelocity = Vector3.zero;
                }

                return true;
            }
        }

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
