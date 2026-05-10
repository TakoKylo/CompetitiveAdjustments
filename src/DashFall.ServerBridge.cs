// DashFall.ServerBridge.cs - Server-side message handler for client keybinds
// Receives PPKB/Hello and PPKB/Action messages from clients

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PoncePuck.Keybinds
{
    /// <summary>
    /// Stores which features are enabled on the server for each role
    /// </summary>
    public class ServerFeatures
    {
        public bool SkaterDashEnabled = true;
        public bool SkaterDiveEnabled = true;
        public bool SkaterSlideInfluenceEnabled = true;
        public bool SkaterTwistEnabled = true;
        public bool GoalieDiveEnabled = true;
        public bool GoalieSlideInfluenceEnabled = false;
        public bool GoalieTwistEnabled = false;
        public bool GoalieStandingDashEnabled = true;
        public bool GoalieDashExtendEnabled = true;
        public bool GoalieStancesEnabled = true;
        public bool SprintShoulderTrailEnabled = true;
        
        /// <summary>Pack features into a ushort for network transmission</summary>
        public ushort ToUShort()
        {
            ushort b = 0;
            if (SkaterDashEnabled) b |= 1;
            if (SkaterDiveEnabled) b |= 2;
            if (SkaterSlideInfluenceEnabled) b |= 4;
            if (SkaterTwistEnabled) b |= 8;
            if (GoalieDiveEnabled) b |= 16;
            if (GoalieSlideInfluenceEnabled) b |= 32;
            if (GoalieTwistEnabled) b |= 64;
            if (GoalieStandingDashEnabled) b |= 128;
            if (GoalieDashExtendEnabled) b |= 256;
            if (GoalieStancesEnabled) b |= 512;
            if (SprintShoulderTrailEnabled) b |= 1024;
            return b;
        }
        
        /// <summary>Legacy: Pack features into a byte (for backwards compat)</summary>
        public byte ToByte() => (byte)(ToUShort() & 0xFF);
        
        /// <summary>Unpack features from a ushort</summary>
        public static ServerFeatures FromUShort(ushort b)
        {
            return new ServerFeatures
            {
                SkaterDashEnabled = (b & 1) != 0,
                SkaterDiveEnabled = (b & 2) != 0,
                SkaterSlideInfluenceEnabled = (b & 4) != 0,
                SkaterTwistEnabled = (b & 8) != 0,
                GoalieDiveEnabled = (b & 16) != 0,
                GoalieSlideInfluenceEnabled = (b & 32) != 0,
                GoalieTwistEnabled = (b & 64) != 0,
                GoalieStandingDashEnabled = (b & 128) != 0,
                GoalieDashExtendEnabled = (b & 256) != 0,
                GoalieStancesEnabled = (b & 512) != 0,
                SprintShoulderTrailEnabled = (b & 1024) != 0
            };
        }
        
        /// <summary>Legacy: Unpack features from a byte</summary>
        public static ServerFeatures FromByte(byte b) => FromUShort(b);
    }
    
    public static class ServerBridge
    {
        private static bool _hooked;
        private static GameObject _host;
        private static CustomMessagingManager _cmm;

        // per-client declared binds & held states
        private static readonly Dictionary<ulong, HashSet<string>> _declared =
            new Dictionary<ulong, HashSet<string>>();
        private static readonly Dictionary<ulong, HashSet<string>> _held =
            new Dictionary<ulong, HashSet<string>>();
            
        // Client-side: features received from server
        public static ServerFeatures ReceivedFeatures { get; private set; } = new ServerFeatures();
        public static bool HasReceivedFeatures { get; private set; } = false;
        public static event Action OnFeaturesReceived;

        // event if a mod wants real-time notification
        public static event Action<ulong, string, bool> OnAction; // clientId, action, isDown

        public static void Hook(string ownerTag)
        {
            if (_hooked) return;
            _hooked = true;

            _host = new GameObject("PPKB_ServerBridgeHost_" + ownerTag);
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<Runner>();
        }

        public static void Unhook()
        {
            _hooked = false;
            try
            {
                if (_cmm != null)
                {
                    _cmm.UnregisterNamedMessageHandler("PPKB/Hello");
                    _cmm.UnregisterNamedMessageHandler("PPKB/Action");
                    _cmm.UnregisterNamedMessageHandler("PPKB/Features");
                    _cmm.UnregisterNamedMessageHandler("PPKB/GoalTweaks");
                    _cmm = null;
                }
            }
            catch { }
            try { if (_host != null) UnityEngine.Object.Destroy(_host); } catch { }
            _host = null;
            _declared.Clear();
            _held.Clear();
            OnAction = null;
            HasReceivedFeatures = false;
            ReceivedFeatures = new ServerFeatures();
            DashFallMod.GoalNetTweaks.ClearSyncedTweaks();
            OnFeaturesReceived = null;
        }
        
        /// <summary>
        /// Reset client-side feature state (call when disconnecting)
        /// </summary>
        public static void ResetClientFeatures()
        {
            HasReceivedFeatures = false;
            ReceivedFeatures = new ServerFeatures();
        }

        public static bool IsKnown(ulong clientId)
        {
            return _declared.ContainsKey(clientId);
        }

        public static bool IsBound(ulong clientId, string action)
        {
            HashSet<string> set;
            if (!_declared.TryGetValue(clientId, out set) || set == null) return false;
            string canon = Canon(action);
            return set.Contains(canon);
        }

        public static bool IsActionHeld(ulong clientId, string action)
        {
            HashSet<string> set;
            if (!_held.TryGetValue(clientId, out set) || set == null)
            {
                // Only log occasionally to avoid spam
                return false;
            }
            string canon = Canon(action);
            bool result = set.Contains(canon);
            return result;
        }
        
        // Debug method to check state
        public static void LogState()
        {
            // Debug helper - can be called from console if needed
        }

        private static string Canon(string a)
        {
            if (string.IsNullOrEmpty(a)) return "";
            a = a.Trim().ToLowerInvariant();
            if (a == "dash-left" || a == "dash_l" || a == "dashl") return "dashleft";
            if (a == "dash-right" || a == "dash_r" || a == "dashr") return "dashright";
            if (a == "twist-left" || a == "twist_l" || a == "twistl") return "twistleft";
            if (a == "twist-right" || a == "twist_r" || a == "twistr") return "twistright";
            if (a == "spawn_puck" || a == "spawn-puck" || a == "sp") return "spawnpuck";
            return a;
        }

        // Mono host to poll for NetworkManager & (re)register handlers safely
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                var nm = NetworkManager.Singleton;
                if (nm == null) return;

                // Re-register if needed
                if (_cmm != nm.CustomMessagingManager)
                {
                    TryUnregister();
                    _cmm = nm.CustomMessagingManager;
                    if (_cmm != null)
                    {
                        _cmm.RegisterNamedMessageHandler("PPKB/Hello", OnHello);
                        _cmm.RegisterNamedMessageHandler("PPKB/Action", OnActionMsg);
                        _cmm.RegisterNamedMessageHandler("PPKB/Features", OnFeaturesMsg);
                        _cmm.RegisterNamedMessageHandler("PPKB/GoalTweaks", OnGoalTweaksMsg);
                        nm.OnClientDisconnectCallback += OnClientLeft;
                        if (DashFallMod.ConfigManager.Config.EnableDebugLogs)
                            DashFallMod.ConfigManager.Dbg($"Registered CMM handlers. IsServer={nm.IsServer} IsHost={nm.IsHost} IsClient={nm.IsClient}");
                    }
                }
            }

            private void OnDestroy() { TryUnregister(); }

            private static void TryUnregister()
            {
                try
                {
                    var nm = NetworkManager.Singleton;
                    if (_cmm != null)
                    {
                        _cmm.UnregisterNamedMessageHandler("PPKB/Hello");
                        _cmm.UnregisterNamedMessageHandler("PPKB/Action");
                        _cmm.UnregisterNamedMessageHandler("PPKB/Features");
                        _cmm.UnregisterNamedMessageHandler("PPKB/GoalTweaks");
                    }
                    if (nm != null) nm.OnClientDisconnectCallback -= OnClientLeft;
                }
                catch { }
                _cmm = null;
            }

            private static void OnClientLeft(ulong clientId)
            {
                _declared.Remove(clientId);
                _held.Remove(clientId);
            }
        }
        
        // ---------- Client-side feature message handler ----------
        private static void OnFeaturesMsg(ulong senderId, FastBufferReader reader)
        {
            try
            {
                ushort featureFlags;
                reader.ReadValueSafe(out featureFlags);
                ReceivedFeatures = ServerFeatures.FromUShort(featureFlags);
                HasReceivedFeatures = true;
                OnFeaturesReceived?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[COMPADJUST] OnFeaturesMsg exception: {e}");
            }
        }

        private static void OnGoalTweaksMsg(ulong senderId, FastBufferReader reader)
        {
            try
            {
                bool enabled;
                float thicknessScale;
                float scaleX;
                float scaleY;
                float scaleZ;
                float goalBackOffset;
                bool arenaEnabled;
                float arenaScaleX;
                float arenaScaleY;
                float arenaScaleZ;
                float arenaOffsetX;
                float arenaOffsetY;
                float arenaOffsetZ;
                float arenaRotX;
                float arenaRotY;
                float arenaRotZ;

                reader.ReadValueSafe(out enabled);
                reader.ReadValueSafe(out thicknessScale);
                reader.ReadValueSafe(out scaleX);
                reader.ReadValueSafe(out scaleY);
                reader.ReadValueSafe(out scaleZ);
                reader.ReadValueSafe(out goalBackOffset);
                reader.ReadValueSafe(out arenaEnabled);
                reader.ReadValueSafe(out arenaScaleX);
                reader.ReadValueSafe(out arenaScaleY);
                reader.ReadValueSafe(out arenaScaleZ);
                reader.ReadValueSafe(out arenaOffsetX);
                reader.ReadValueSafe(out arenaOffsetY);
                reader.ReadValueSafe(out arenaOffsetZ);
                reader.ReadValueSafe(out arenaRotX);
                reader.ReadValueSafe(out arenaRotY);
                reader.ReadValueSafe(out arenaRotZ);

                DashFallMod.GoalNetTweaks.SetSyncedTweaks(
                    enabled,
                    thicknessScale,
                    scaleX,
                    scaleY,
                    scaleZ,
                    goalBackOffset,
                    arenaEnabled,
                    arenaScaleX,
                    arenaScaleY,
                    arenaScaleZ,
                    arenaOffsetX,
                    arenaOffsetY,
                    arenaOffsetZ,
                    arenaRotX,
                    arenaRotY,
                    arenaRotZ);
            }
            catch (Exception e)
            {
                Debug.LogError($"[COMPADJUST] OnGoalTweaksMsg exception: {e}");
            }
        }
        
        /// <summary>
        /// Server calls this to send feature flags to a specific client
        /// </summary>
        public static void SendFeaturesToClient(ulong clientId, ServerFeatures features)
        {
            if (_cmm == null) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            
            try
            {
                var writer = new FastBufferWriter(2, Unity.Collections.Allocator.Temp);
                writer.WriteValueSafe(features.ToUShort());
                _cmm.SendNamedMessage("PPKB/Features", clientId, writer);
            }
            catch (Exception e)
            {
                Debug.LogError($"[COMPADJUST] SendFeaturesToClient exception: {e}");
            }
        }

        public static void BroadcastFeaturesToAllClients()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            // Ensure config is loaded
            try
            {
                DashFallMod.ConfigManager.EnsureConfig();
            }
            catch { }

            var cfg = DashFallMod.ConfigManager.Config;
            if (cfg == null)
            {
                Debug.LogError("[COMPADJUST] BroadcastFeaturesToAllClients: Config is null!");
                return;
            }

            var compAdjust = DashFallMod.ConfigManager.CompAdjust;
            if (compAdjust == null)
            {
                Debug.LogError("[COMPADJUST] BroadcastFeaturesToAllClients: CompAdjust is null!");
                return;
            }

            var features = new ServerFeatures
            {
                SkaterDashEnabled = cfg.SkaterDashEnabled,
                SkaterDiveEnabled = cfg.SkaterDiveEnabled,
                SkaterSlideInfluenceEnabled = cfg.EnableSlideInfluence,
                SkaterTwistEnabled = cfg.EnableTwistWhileSliding,
                GoalieDiveEnabled = cfg.GoalieDiveEnabled,
                GoalieSlideInfluenceEnabled = cfg.GoalieSlideInfluenceEnabled,
                GoalieTwistEnabled = cfg.GoalieTwistWhileSlidingEnabled,
                GoalieStandingDashEnabled = cfg.GoalieStandingDashEnabled,
                GoalieDashExtendEnabled = cfg.GoalieDashExtendEnabled,
                GoalieStancesEnabled = cfg.GoalieStancesEnabled,
                SprintShoulderTrailEnabled = compAdjust.SprintShoulderTrailEnabled
            };

            Debug.Log($"[COMPADJUST] Broadcasting features: GoalieDashExtendEnabled={features.GoalieDashExtendEnabled}, GoalieStancesEnabled={features.GoalieStancesEnabled}");

            var players = PlayerManager.Instance?.GetPlayers();
            if (players == null || players.Count == 0)
            {
                CompetitiveAdjustments.ConfigManager.LogWarning("No players to send features to");
                return;
            }

            foreach (var player in players)
            {
                SendFeaturesToClient(player.OwnerClientId, features);
            }
        }

        public static void BroadcastGoalTweaksToAllClients()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || _cmm == null) return;

            var cfg = DashFallMod.ConfigManager.CompAdjust;

            foreach (var player in PlayerManager.Instance.GetPlayers())
            {
                try
                {
                    var writer = new FastBufferWriter(160, Unity.Collections.Allocator.Temp);
                    writer.WriteValueSafe(cfg.EnableGoalNetTweaks);
                    writer.WriteValueSafe(cfg.GoalThicknessScale);
                    writer.WriteValueSafe(cfg.GoalSizeScaleX);
                    writer.WriteValueSafe(cfg.GoalSizeScaleY);
                    writer.WriteValueSafe(cfg.GoalSizeScaleZ);
                    writer.WriteValueSafe(cfg.GoalBackOffset);
                    writer.WriteValueSafe(cfg.EnableArenaTweaks);
                    writer.WriteValueSafe(cfg.ArenaScaleX);
                    writer.WriteValueSafe(cfg.ArenaScaleY);
                    writer.WriteValueSafe(cfg.ArenaScaleZ);
                    writer.WriteValueSafe(cfg.ArenaOffsetX);
                    writer.WriteValueSafe(cfg.ArenaOffsetY);
                    writer.WriteValueSafe(cfg.ArenaOffsetZ);
                    writer.WriteValueSafe(cfg.ArenaRotX);
                    writer.WriteValueSafe(cfg.ArenaRotY);
                    writer.WriteValueSafe(cfg.ArenaRotZ);
                    _cmm.SendNamedMessage("PPKB/GoalTweaks", player.OwnerClientId, writer);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[COMPADJUST] BroadcastGoalTweaksToAllClients exception: {e}");
                }
            }
        }

        // ---------- Message handlers ----------
        private static void OnHello(ulong clientId, FastBufferReader reader)
        {
            try
            {
                int count = 0;
                reader.ReadValueSafe(out count);
                
                var set = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    string a; 
                    reader.ReadValueSafe(out a);
                    if (!string.IsNullOrEmpty(a)) set.Add(Canon(a));
                }
                _declared[clientId] = set;
                // Reset held set when new declaration arrives
                _held[clientId] = new HashSet<string>();
                
                // Send server features to client
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer)
                {
                    BroadcastFeaturesToAllClients();
                    BroadcastGoalTweaksToAllClients();
                }
            }
            catch (Exception e) 
            { 
                Debug.LogError($"[COMPADJUST] OnHello exception: {e}");
            }
        }

        private static void OnActionMsg(ulong clientId, FastBufferReader reader)
        {
            try
            {
                string action; 
                byte phase;
                reader.ReadValueSafe(out action);
                reader.ReadValueSafe(out phase);
                action = Canon(action);

                HashSet<string> held;
                if (!_held.TryGetValue(clientId, out held) || held == null) 
                { 
                    held = new HashSet<string>(); 
                    _held[clientId] = held; 
                }

                bool isDown = (phase == 0);
                if (isDown) 
                    held.Add(action); 
                else 
                    held.Remove(action);

                var evt = OnAction; 
                if (evt != null) 
                    evt(clientId, action, isDown);
            }
            catch (Exception e) 
            { 
                Debug.LogError($"[COMPADJUST] OnActionMsg exception: {e}");
            }
        }
    }
}
