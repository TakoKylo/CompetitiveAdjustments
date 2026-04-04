using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using Unity.Netcode;

namespace CompetitivePuckTweaks.src
{
    [HarmonyPatch(typeof(Puck), "OnNetworkPostSpawn")]
    public class PuckPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Puck __instance, ref float ___maxSpeed, ref UnityEngine.Vector3 ___stickTensor)
        {
            // Use synced client config (PuckScale) if available, else fall back to server config
            float puckScale = GetSyncedPuckScale();
            __instance.transform.localScale = new UnityEngine.Vector3(puckScale, puckScale, puckScale);
            PluginCore.Log($"Puck scaled to {puckScale}");
            ___maxSpeed = PluginCore.config.PuckMaxSpeed;
            ___stickTensor = new UnityEngine.Vector3(PluginCore.config.PuckStickTensorX,
                PluginCore.config.PuckStickTensorY, PluginCore.config.PuckStickTensorZ);
            __instance.Rigidbody.linearDamping = PluginCore.config.PuckDrag;
            __instance.Rigidbody.mass = PluginCore.config.PuckMass;
            __instance.StickCollider.hasModifiableContacts = true;
            __instance.IceCollider.hasModifiableContacts = true;
            PluginCore.PuckIDs.Add(__instance.StickCollider.GetInstanceID());
            PluginCore.PuckIDs.Add(__instance.IceCollider.GetInstanceID());

            if (PluginCore.config.EnableMidStickCollider)
            {
                foreach (Stick stick in UnityEngine.Object.FindObjectsByType<Stick>(FindObjectsSortMode.None))
                {
                    Physics.IgnoreCollision(__instance.StickCollider, stick.GetComponent<BoxCollider>());
                    Physics.IgnoreCollision(__instance.IceCollider, stick.GetComponent<BoxCollider>());
                }
            }

            // Timing guard: send latest config sync when a puck spawns on server.
            // This heals missed join-time sync and keeps client puck scale consistent.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
            {
                foreach (ulong clientId in nm.ConnectedClientsIds)
                {
                    if (clientId == NetworkManager.ServerClientId) continue;
                    PluginCore.ManualSync(clientId);
                }
            }
            else if (nm != null && nm.IsClient)
            {
                CompetitiveCompanion.PluginCore.RequestConfigSyncFromServer("PuckSpawn");
            }
        }

        /// <summary>
        /// Get puck scale from synced client config (CompetitiveCompanion.PluginCore),
        /// or fall back to server config if client config is not available.
        /// This ensures clients display the correct puck size synced from the server.
        /// </summary>
        private static float GetSyncedPuckScale()
        {
            try
            {
                // Try to get from synced client config, which receives updates from server via CMM
                var companionConfig = CompetitiveCompanion.PluginCore.config;
                if (companionConfig != null && companionConfig.PuckScale > 0.01f)
                    return companionConfig.PuckScale;
            }
            catch { }
            
            // Fall back to server config if client config is not available
            return PluginCore.config.PuckScale;
        }
    }


    [HarmonyPatch(typeof(Puck), "FixedUpdate")]
    public class HeightDragTweak
    {
        [HarmonyPostfix]
        public static void Postfix(Puck __instance)
        {
            if (!PluginCore.config.PuckDragSpeedDependence) return;
            float delta = __instance.Rigidbody.linearVelocity.magnitude - PluginCore.config.PuckNominalSpeed;
            float newDrag = PluginCore.config.PuckDrag * (1 + PluginCore.config.PuckDragFactor * delta * delta * delta);
            __instance.Rigidbody.linearDamping = Mathf.Max(PluginCore.config.PuckDrag, newDrag);

            if (PluginCore.config.PuckHeightDependentDrag)
            {
                if (__instance.Rigidbody.position.y > PluginCore.config.PuckHeightLimit && __instance.Rigidbody.linearVelocity.y > 0)
                {
                    float overheight = __instance.Rigidbody.position.y - PluginCore.config.PuckHeightLimit;
                    float heightDrag = PluginCore.config.PuckHeightDragFactor * overheight;
                    __instance.Rigidbody.AddForce(new UnityEngine.Vector3(0f, -heightDrag, 0f), ForceMode.VelocityChange);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Puck), "OnNetworkDespawn")]
    public class PuckDespawnPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Puck __instance)
        {
            if (PluginCore.PuckIDs.Contains(__instance.StickCollider.GetInstanceID())) { PluginCore.PuckIDs.Remove(__instance.StickCollider.GetInstanceID()); }
            if (PluginCore.PuckIDs.Contains(__instance.IceCollider.GetInstanceID())) { PluginCore.PuckIDs.Remove(__instance.IceCollider.GetInstanceID()); }
        }
    }
}