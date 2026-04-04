using UnityEngine;
using HarmonyLib;

namespace CompetitivePuckTweaks.src
{
    [HarmonyPatch(typeof(Movement), "Start")]
    public class MovementPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Movement __instance, ref float ___turnAcceleration, ref float ___turnBrakeAcceleration, ref float ___turnMaxSpeed, ref float ___turnDrag,
                                    ref float ___maxBackwardsSpeed, ref float ___maxBackwardsSprintSpeed, ref float ___maxForwardsSpeed, ref float ___maxForwardsSprintSpeed)
        {
            if (__instance.PlayerBody.Player.IsReplay.Value) return;
            ___turnDrag = PluginCore.config.TurnDrag;

            if (__instance.PlayerBody.Player.PlayerPosition.Role == PlayerRole.Attacker)
            {
                ___maxBackwardsSpeed = PluginCore.config.MaxBackwardsSpeed;
                ___maxBackwardsSprintSpeed = PluginCore.config.MaxBackwardsSprintSpeed;
                ___maxForwardsSpeed = PluginCore.config.MaxForwardsSpeed;
                ___maxForwardsSprintSpeed = PluginCore.config.MaxForwardsSprintSpeed;
                __instance.gameObject.AddComponent<FloatComponent>();
            }
            
            else if (__instance.PlayerBody.Player.PlayerPosition.Role == PlayerRole.Goalie)
            {
                ___maxBackwardsSpeed = PluginCore.config.GoalieMaxBackwardsSpeed;
                ___maxBackwardsSprintSpeed = PluginCore.config.GoalieMaxBackwardsSprintSpeed;
                ___maxForwardsSpeed = PluginCore.config.GoalieMaxForwardsSpeed;
                ___maxForwardsSprintSpeed = PluginCore.config.GoalieMaxForwardsSprintSpeed;
                ___turnMaxSpeed = PluginCore.config.GoalieTurnMaxSpeed;
                ___turnAcceleration = PluginCore.config.GoalieTurnAcceleration;
                ___turnBrakeAcceleration = PluginCore.config.GoalieTurnBrakeAcceleration;
                ___turnDrag = PluginCore.config.GoalieTurnDrag;
            }
        }
    }

    [HarmonyPatch(typeof(Movement), "FixedUpdate")]
    public class ScalingTurnSpeedPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Movement __instance, ref float ___turnAcceleration, ref float ___turnBrakeAcceleration, ref float ___turnMaxSpeed,
            ref float ___forwardsAcceleration, ref float ___forwardsSprintAcceleration, ref float ___backwardsAcceleration, ref float ___backwardsSprintAcceleration)
        {
            if (__instance.PlayerBody.Player.IsReplay.Value) return;
            if (__instance.PlayerBody.Player.PlayerPosition.Role == PlayerRole.Goalie) return;
            float speed = __instance.Speed;
            ___forwardsAcceleration = Mathf.Max(PluginCore.config.ForwardsAccelerationBase -
                    speed * PluginCore.config.ForwardsAccelerationScaling, PluginCore.config.ForwardsAccelerationMin);
            ___forwardsSprintAcceleration = Mathf.Max(PluginCore.config.ForwardsSprintAccelerationBase -
                    speed * PluginCore.config.ForwardsSprintAccelerationScaling, PluginCore.config.ForwardsSprintAccelerationMin);
            ___backwardsAcceleration = Mathf.Max(PluginCore.config.BackwardsAccelerationBase -
                    speed * PluginCore.config.BackwardsAccelerationScaling, PluginCore.config.BackwardsAccelerationMin);
            ___backwardsSprintAcceleration = Mathf.Max(PluginCore.config.BackwardsSprintAccelerationBase -
                    speed * PluginCore.config.BackwardsSprintAccelerationScaling, PluginCore.config.BackwardsSprintAccelerationMin);

            FloatComponent slideTime = __instance.gameObject.GetComponent<FloatComponent>();
            if (slideTime == null) return;
            if (__instance.PlayerBody.IsSliding.Value)
            {
                ___turnAcceleration = PluginCore.config.TurnAccelerationBase - speed * PluginCore.config.TurnAccelerationScaling;
                ___turnBrakeAcceleration = PluginCore.config.TurnBrakeAccelerationBase - speed * PluginCore.config.TurnBrakeAccelerationScaling;
                slideTime.value = PluginCore.config.PostSlideTurnTime;
            }
            else if (slideTime.value > 0)
            {
                slideTime.value -= Time.fixedDeltaTime;
                ___turnMaxSpeed = PluginCore.config.PostSlideTurnMax;
                ___turnAcceleration = PluginCore.config.PostSlideTurnAcceleration;
                ___turnBrakeAcceleration = PluginCore.config.PostSlideBrakeAcceleration;
            }
            else
            {
                ___turnBrakeAcceleration = PluginCore.config.TurnBrakeAccelerationBase - speed * PluginCore.config.TurnBrakeAccelerationScaling;
                ___turnAcceleration = PluginCore.config.TurnAccelerationBase - speed * PluginCore.config.TurnAccelerationScaling;
                float defaultTurn = PluginCore.config.TurnMaxSpeedBase - speed * PluginCore.config.TurnMaxSpeedScaling;
                if (___turnMaxSpeed - Time.fixedDeltaTime < defaultTurn) ___turnMaxSpeed = defaultTurn;
                else ___turnMaxSpeed -= Time.fixedDeltaTime;
            }


        }
    }

}