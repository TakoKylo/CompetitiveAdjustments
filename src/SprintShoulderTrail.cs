using System.Reflection;
using HarmonyLib;
using UnityEngine;
using DashFallMod.Client;

namespace DashFallMod
{
    [HarmonyPatch(typeof(PlayerBodyV2), "OnNetworkPostSpawn")]
    public static class SprintShoulderTrailPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerBodyV2 __instance)
        {
            if (__instance == null) return;
            if (Application.isBatchMode) return;
            if (__instance.GetComponent<SprintShoulderTrail>() != null) return;

            __instance.gameObject.AddComponent<SprintShoulderTrail>();
        }
    }

    public class SprintShoulderTrail : MonoBehaviour
    {
        private static readonly Color TRAIL_WHITE = new Color(1f, 1f, 1f, 1f);

        private static readonly FieldInfo TorsoBoneField =
            typeof(PlayerMesh).GetField("torsoBone", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Material _trailMaterial;

        private PlayerBodyV2 _body;
        private TrailRenderer _leftTrail;
        private TrailRenderer _rightTrail;
        private bool _wasEmitting;
        private Transform _shoulderParent;

        private static float TrailTime => Mathf.Clamp(DashFallConfigLoader.ClientConfig.SprintShoulderTrailTime, 0.05f, 3f);
        private static float TrailWidth => Mathf.Clamp(DashFallConfigLoader.ClientConfig.SprintShoulderTrailWidth, 0.01f, 0.5f);
        private static Color TrailStartColor => ParseHexColorOrDefault(DashFallConfigLoader.ClientConfig.SprintShoulderTrailStartColorHex, TRAIL_WHITE);
        private static Color TrailEndColor => ParseHexColorOrDefault(DashFallConfigLoader.ClientConfig.SprintShoulderTrailEndColorHex, TRAIL_WHITE);
        private static float TrailStartAlpha => Mathf.Clamp01(DashFallConfigLoader.ClientConfig.SprintShoulderTrailStartAlpha);
        private static float TrailEndAlpha => Mathf.Clamp01(DashFallConfigLoader.ClientConfig.SprintShoulderTrailEndAlpha);

        private static Color ParseHexColorOrDefault(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            string normalized = hex.Trim();
            if (!normalized.StartsWith("#")) normalized = "#" + normalized;
            return ColorUtility.TryParseHtmlString(normalized, out var parsed) ? parsed : fallback;
        }

        private void Awake()
        {
            _body = GetComponent<PlayerBodyV2>();
            if (_body == null) return;

            if (_body.Player == null)
            {
                enabled = false;
                return;
            }

            _shoulderParent = ResolveShoulderParent(_body);
            if (_shoulderParent == null)
            {
                enabled = false;
                return;
            }

            EnsureTrailRenderers();
        }

        private void LateUpdate()
        {
            if (_body == null || _shoulderParent == null)
            {
                enabled = false;
                return;
            }

            bool clientEnabled = DashFallConfigLoader.ClientConfig.EnableSprintShoulderTrail;
            bool serverEnabled = !PoncePuck.Keybinds.ServerBridge.HasReceivedFeatures ||
                                 PoncePuck.Keybinds.ServerBridge.ReceivedFeatures.SprintShoulderTrailEnabled;

            bool emitting = clientEnabled && serverEnabled &&
                            _body.IsSprinting.Value && _body.IsGrounded && !_body.IsSliding.Value;

            if (emitting != _wasEmitting)
            {
                if (emitting)
                {
                    EnsureTrailRenderers();
                    _leftTrail.emitting = true;
                    _rightTrail.emitting = true;
                }
                else
                {
                    DetachAndFade(_leftTrail);
                    DetachAndFade(_rightTrail);
                    _leftTrail = null;
                    _rightTrail = null;
                }

                _wasEmitting = emitting;
            }
        }

        private void EnsureTrailRenderers()
        {
            if (_leftTrail == null)
                _leftTrail = CreateShoulderTrail("SprintTrail_Left", _shoulderParent, new Vector3(-0.22f, 0.08f, 0f));
            if (_rightTrail == null)
                _rightTrail = CreateShoulderTrail("SprintTrail_Right", _shoulderParent, new Vector3(0.22f, 0.08f, 0f));
        }

        private static void DetachAndFade(TrailRenderer trail)
        {
            if (trail == null) return;
            trail.emitting = false;
            trail.transform.SetParent(null, true);

            var autoDestroy = trail.gameObject.AddComponent<SprintTrailAutoDestroy>();
            autoDestroy.Lifetime = TrailTime + 0.1f;
        }

        private static Transform ResolveShoulderParent(PlayerBodyV2 body)
        {
            var mesh = body.GetComponentInChildren<PlayerMesh>();
            if (mesh == null) return body.transform;

            if (TorsoBoneField != null)
            {
                try
                {
                    var torso = TorsoBoneField.GetValue(mesh) as Transform;
                    if (torso != null) return torso;
                }
                catch { }
            }

            return mesh.transform;
        }

        private static TrailRenderer CreateShoulderTrail(string name, Transform parent, Vector3 localOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = TrailTime;
            trail.startWidth = TrailWidth;
            trail.endWidth = 0.01f;
            trail.minVertexDistance = 0.02f;
            trail.alignment = LineAlignment.View;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            trail.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            trail.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            trail.autodestruct = false;

            var gradient = new Gradient();
            var startColor = TrailStartColor;
            var endColor = TrailEndColor;
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(startColor, 0.2f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(TrailStartAlpha, 0f),
                    new GradientAlphaKey(TrailStartAlpha, 0.2f),
                    new GradientAlphaKey(TrailEndAlpha, 1f)
                }
            );
            trail.colorGradient = gradient;

            if (_trailMaterial == null)
            {
                _trailMaterial = new Material(Shader.Find("Sprites/Default"));
                _trailMaterial.color = TRAIL_WHITE;
            }
            trail.material = _trailMaterial;
            trail.emitting = false;

            return trail;
        }

        private sealed class SprintTrailAutoDestroy : MonoBehaviour
        {
            public float Lifetime = 0.5f;

            private void Start()
            {
                Destroy(gameObject, Lifetime);
            }
        }
    }
}
