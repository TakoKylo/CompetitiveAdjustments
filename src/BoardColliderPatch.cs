using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CompetitivePuckTweaks.src
{
    [HarmonyPatch(typeof(PlayerBodyV2), "OnNetworkPostSpawn")]
    public class BoardColliderPatch
    {
        private static List<Collider> foundBoardColliders = new List<Collider>();
        
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!PluginCore.config.EnableSoftBoards)
                return;
                
            // Always try to find board colliders (in case they weren't loaded yet)
            if (foundBoardColliders.Count == 0)
            {
                FindBoardColliders();
            }
            
            // Re-apply physics to found colliders (in case config changed)
            if (foundBoardColliders.Count > 0)
            {
                foreach (Collider collider in foundBoardColliders)
                {
                    ApplySoftBoardPhysics(collider);
                }
            }
        }
        
        private static void FindBoardColliders()
        {
            // Find all colliders that are likely board pieces
            Collider[] allColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foundBoardColliders.Clear();
            
            foreach (Collider collider in allColliders)
            {
                string colliderName = collider.name.ToLower();
                
                // Look for colliders that are likely board pieces
                if (colliderName.Contains("collider") &&
                    (colliderName.Contains("left") ||
                     colliderName.Contains("right") ||
                     colliderName.Contains("front") ||
                     colliderName.Contains("back") ||
                     colliderName.Contains("top") ||
                     colliderName.Contains("barrier") ||
                     colliderName.Contains("board") ||
                     colliderName.Contains("wall")))
                {
                    foundBoardColliders.Add(collider);
                    PluginCore.Log($"Found board collider: {collider.name}");
                }
            }
            
            PluginCore.Log($"Found {foundBoardColliders.Count} board colliders to modify");
        }
        
        private static void ApplySoftBoardPhysics(Collider collider)
        {
            // Create or get physics material for the collider
            PhysicsMaterial boardMaterial = collider.material;
            if (boardMaterial == null)
            {
                boardMaterial = new PhysicsMaterial("SoftBoardMaterial");
                collider.material = boardMaterial;
            }
            
            // Apply soft board properties
            boardMaterial.bounciness = PluginCore.config.BoardBounciness;
            boardMaterial.dynamicFriction = PluginCore.config.BoardFriction;
            boardMaterial.staticFriction = PluginCore.config.BoardFriction;
            boardMaterial.bounceCombine = PhysicsMaterialCombine.Average;
            boardMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            
            // If the collider has a rigidbody, make it softer
            Rigidbody rb = collider.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Make the rigidbody softer by reducing its mass and adding damping
                rb.mass = Mathf.Max(0.1f, rb.mass * PluginCore.config.BoardSoftness);
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
                
                // Make it kinematic so it doesn't move but still responds to collisions
                rb.isKinematic = true;
            }
            
            PluginCore.Dbg($"Applied soft board physics to {collider.name} (bounciness: {boardMaterial.bounciness}, friction: {boardMaterial.dynamicFriction})");
        }
    }
}
