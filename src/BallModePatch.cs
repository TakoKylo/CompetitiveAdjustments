using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace CompetitiveAdjustments
{
    public static class BallModeHelper
    {
        private static readonly HashSet<int> _modifiedPucks = new HashSet<int>();

        public static bool IsBallModeEnabled =>
            ConfigManager.Config?.CompAdjust?.BallMode == true;

        public static void TransformPuckToBall(Puck puck)
        {
            if (puck == null) return;
            int id = puck.GetInstanceID();
            if (_modifiedPucks.Contains(id)) return;

            // Find puck material (skip shadows, prefer textured)
            Material puckMaterial = null;
            var originalRenderers = puck.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in originalRenderers)
            {
                if (renderer?.sharedMaterial == null) continue;
                if (renderer.sharedMaterial.name.Contains("Shadow")) continue;
                if (renderer.sharedMaterial.mainTexture != null)
                {
                    puckMaterial = renderer.sharedMaterial;
                    break;
                }
            }
            if (puckMaterial == null)
            {
                foreach (var renderer in originalRenderers)
                {
                    if (renderer?.sharedMaterial != null)
                    {
                        puckMaterial = renderer.sharedMaterial;
                        break;
                    }
                }
            }

            // Create or reuse sphere child
            Transform existing = puck.transform.Find("BallMesh");
            GameObject sphere;
            if (existing != null)
            {
                sphere = existing.gameObject;
            }
            else
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "BallMesh";
                sphere.transform.SetParent(puck.transform, false);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localRotation = Quaternion.identity;
                // 0.5 multiplier: unit sphere diameter=1, puck visual ~0.5 local units
                sphere.transform.localScale = Vector3.one * 0.5f;

                // Remove the primitive's own collider
                var primitiveCol = sphere.GetComponent<Collider>();
                if (primitiveCol != null) UnityEngine.Object.Destroy(primitiveCol);

                // Apply puck material to sphere
                if (puckMaterial != null)
                {
                    var mr = sphere.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.material = new Material(puckMaterial);
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }

            // Hide original puck mesh renderers (not the ball)
            var sphereRenderer = sphere.GetComponent<MeshRenderer>();
            foreach (var renderer in originalRenderers)
            {
                if (renderer != sphereRenderer && renderer.gameObject != sphere)
                    renderer.enabled = false;
            }

            // Ensure sphere renderer is visible
            if (sphereRenderer != null)
                sphereRenderer.enabled = true;

            // Server: add a SphereCollider alongside existing colliders.
            // We never remove StickCollider/IceCollider — those are Puck internals.
            // The sphere prevents the disc from lying flat on ice and gives ball bounces.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                if (puck.GetComponent<SphereCollider>() == null)
                {
                    var sc = puck.gameObject.AddComponent<SphereCollider>();
                    sc.radius = 0.25f;
                    sc.isTrigger = false;
                }
            }

            _modifiedPucks.Add(id);
        }

        public static void RestorePuckFromBall(Puck puck)
        {
            if (puck == null) return;
            int id = puck.GetInstanceID();
            if (!_modifiedPucks.Contains(id)) return;

            // Remove ball mesh
            var ballMesh = puck.transform.Find("BallMesh");
            if (ballMesh != null)
            {
                var br = ballMesh.GetComponent<MeshRenderer>();
                if (br != null) br.enabled = false;
                UnityEngine.Object.DestroyImmediate(ballMesh.gameObject);
            }

            // Restore original renderers
            foreach (var renderer in puck.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer != null)
                    renderer.enabled = true;
            }

            // Server: remove the sphere collider that was added for ball mode
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                var sc = puck.GetComponent<SphereCollider>();
                if (sc != null) UnityEngine.Object.DestroyImmediate(sc);
            }

            _modifiedPucks.Remove(id);
        }

        public static void RefreshAllPucks()
        {
            try
            {
                if (PuckManager.Instance == null) return;
                var pucks = PuckManager.Instance.GetPucks();
                if (pucks == null) return;

                if (IsBallModeEnabled)
                {
                    foreach (var puck in pucks)
                    {
                        if (puck != null) TransformPuckToBall(puck);
                    }
                }
                else
                {
                    foreach (var puck in pucks)
                    {
                        if (puck != null) RestorePuckFromBall(puck);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[COMPADJUST] BallMode refresh failed: {ex.Message}");
            }
        }

        public static void OnPuckDespawned(Puck puck)
        {
            if (puck != null)
                _modifiedPucks.Remove(puck.GetInstanceID());
        }
    }
}
