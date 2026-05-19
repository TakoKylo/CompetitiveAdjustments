using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DashFallMod.Net
{
    /// <summary>
    /// Server-side half of the chunked-position sync system.  Tracks every
    /// SynchronizedObject's world position and, when one crosses a per-axis
    /// hysteresis threshold, announces a deferred chunk switch to all
    /// clients via the OWPMOD/Chunks CMM reliable message.
    /// </summary>
    public static class ChunkSyncServer
    {
        public const string CmmName = "OWPMOD/Chunks";

        // Hysteresis: trip a new pending switch when the object is further
        // than this from its current chunk's center on the relevant axis.
        // Greater than chunkSize/2 (16 m) so the player must overshoot the
        // nominal chunk wall by 4 m before we flip; combined with the new
        // chunk's center being 32 m away (player lands at 12 m from new
        // center after flipping), gives an 8 m deadband and no flapping.
        private const float FlipOutMeters = 20f;

        // Defer the actual switch by this many server ticks.  Long enough
        // for the reliable announce to traverse normal network latency.
        // Server keeps encoding with OLD chunk during the defer window;
        // the wire range (+/-50 m) easily contains the further drift.
        private const int DeferTicks = 50;

        private const string HarmonyId = "compadjust.chunksync.server";

        private static Harmony _harmony;
        private static bool _enabled;
        private static readonly List<SynchronizedObject> _tracked = new List<SynchronizedObject>();

        private static FieldInfo _serverTickIdField;

        public static void Enable()
        {
            if (_enabled) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                CompetitiveAdjustments.ConfigManager.Log("ChunkSyncServer.Enable: not the server, skipping.");
                return;
            }

            _enabled = true;

            EventManager.AddEventListener("Event_Everyone_OnSynchronizedObjectSpawned", OnSpawnedEvent);
            EventManager.AddEventListener("Event_Everyone_OnSynchronizedObjectDespawned", OnDespawnedEvent);

            var mgr = NetworkBehaviourSingleton<SynchronizedObjectManager>.Instance;
            if (mgr != null)
            {
                var listField = AccessTools.Field(typeof(SynchronizedObjectManager), "synchronizedObjects");
                if (listField?.GetValue(mgr) is System.Collections.IEnumerable list)
                {
                    foreach (var item in list)
                        if (item is SynchronizedObject so && !_tracked.Contains(so))
                        {
                            _tracked.Add(so);
                            InitializeSlotFor(so);
                        }
                }
            }

            _serverTickIdField = AccessTools.Field(typeof(SynchronizedObjectManager), "serverLastSentTickId");

            try
            {
                if (_harmony == null) _harmony = new Harmony(HarmonyId);

                var gather = AccessTools.Method(typeof(SynchronizedObjectManager), "Server_GatherSynchronizedObjectData");
                if (gather != null)
                    _harmony.Patch(gather, prefix: new HarmonyMethod(typeof(ChunkSyncServer), nameof(GatherPrefix)));
                else
                    Debug.LogWarning("[COMPADJUST] ChunkSyncServer: Server_GatherSynchronizedObjectData not found -- hysteresis sweep disabled.");

                var forceSync = AccessTools.Method(typeof(SynchronizedObjectManager), "Server_ForceSynchronizeClientId");
                if (forceSync != null)
                    _harmony.Patch(forceSync, prefix: new HarmonyMethod(typeof(ChunkSyncServer), nameof(ForceSyncPrefix)));
                else
                    Debug.LogWarning("[COMPADJUST] ChunkSyncServer: Server_ForceSynchronizeClientId not found -- late-join snapshot disabled.");

                CompetitiveAdjustments.ConfigManager.Log("ChunkSyncServer enabled (" + _tracked.Count + " already-spawned objects).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[COMPADJUST] ChunkSyncServer patch install failed: " + ex.Message);
            }
        }

        public static void Disable()
        {
            if (!_enabled) return;
            _enabled = false;

            EventManager.RemoveEventListener("Event_Everyone_OnSynchronizedObjectSpawned", OnSpawnedEvent);
            EventManager.RemoveEventListener("Event_Everyone_OnSynchronizedObjectDespawned", OnDespawnedEvent);
            _tracked.Clear();

            if (_harmony != null)
            {
                try { _harmony.UnpatchSelf(); }
                catch (Exception ex) { Debug.LogWarning("[COMPADJUST] ChunkSyncServer UnpatchSelf failed: " + ex.Message); }
                _harmony = null;
            }
        }

        private static void OnSpawnedEvent(Dictionary<string, object> msg)
        {
            if (!_enabled) return;
            if (!(msg["synchronizedObject"] is SynchronizedObject obj)) return;
            if (_tracked.Contains(obj)) return;
            _tracked.Add(obj);
            InitializeSlotFor(obj);
        }

        private static void OnDespawnedEvent(Dictionary<string, object> msg)
        {
            if (!_enabled) return;
            if (!(msg["synchronizedObject"] is SynchronizedObject obj)) return;
            _tracked.Remove(obj);
            ChunkRegistry.Remove((ushort)obj.NetworkObjectId);
        }

        private static void InitializeSlotFor(SynchronizedObject obj)
        {
            ushort id = (ushort)obj.NetworkObjectId;
            ChunkCoord chunk = WorldToChunk(obj.transform.position);
            ChunkRegistry.ApplyAnnounce(id, chunk, ChunkRegistry.NoSwitchTickId);
            BroadcastInstant(id, chunk);
        }

        public static void GatherPrefix()
        {
            if (!_enabled) return;

            ushort tick = GetCurrentTickId();
            ChunkRegistry.CurrentEncodeTickId = tick;
            // Clear any stale Pending whose switch tick has arrived. Keeps
            // ResolveAt accurate across the ushort tick wrap (~18 min at 30Hz)
            // even for objects that haven't moved enough to re-announce.
            ChunkRegistry.PromoteAllIfDue(tick);

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                var obj = _tracked[i];
                if (obj == null)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                ushort id = (ushort)obj.NetworkObjectId;
                Vector3 worldPos = obj.transform.position;

                if (!ChunkRegistry.TryGet(id, out var slot))
                {
                    InitializeSlotFor(obj);
                    continue;
                }

                ChunkCoord active = slot.ResolveAt(tick);
                ChunkCoord target = HysteresisCheck(worldPos, active);

                if (target == active) continue;
                if (slot.HasPending && slot.Pending == target && !TickGE(tick, slot.PendingTickId)) continue;

                // Natural ushort wrap (modulo 2^16). The sentinel ushort.MaxValue
                // means "no pending switch"; if our wrap happens to land on it,
                // bump forward by one tick so the announce is never confused with
                // the sentinel.
                ushort switchTick = (ushort)(tick + DeferTicks);
                if (switchTick == ChunkRegistry.NoSwitchTickId) switchTick = 0;
                ChunkRegistry.ApplyAnnounce(id, target, switchTick);
                BroadcastSwitch(id, target, switchTick);
            }
        }

        public static void ForceSyncPrefix(ulong clientId)
        {
            if (!_enabled) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (nm.IsHost && clientId == nm.LocalClientId) return;
            SendBulkTo(clientId);
        }

        /// <summary>
        /// Send the full slot table to a specific client as a reliable bulk
        /// snapshot.  Called both at scene-sync-complete (via ForceSyncPrefix)
        /// and on demand when the client's <see cref="ChunkSyncClient"/>
        /// finishes registering its handler and requests a fresh copy.
        /// </summary>
        public static void SendBulkTo(ulong clientId)
        {
            if (!_enabled) return;
            if (ChunkRegistry.Count == 0) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            var cmm = nm.CustomMessagingManager;
            if (cmm == null) return;

            try
            {
                int est = 3 + ChunkRegistry.Count * 6 + 64;
                var writer = new FastBufferWriter(est, Allocator.Temp, est * 4);
                try
                {
                    writer.WriteValueSafe((byte)1);
                    writer.WriteValueSafe((ushort)ChunkRegistry.Count);
                    foreach (var kvp in ChunkRegistry.Snapshot())
                    {
                        writer.WriteValueSafe(kvp.Key);
                        writer.WriteValueSafe(kvp.Value.Current.X);
                        writer.WriteValueSafe(kvp.Value.Current.Z);
                        writer.WriteValueSafe(ChunkRegistry.NoSwitchTickId);
                    }
                    cmm.SendNamedMessage(CmmName, clientId, writer, NetworkDelivery.Reliable);
                }
                finally { writer.Dispose(); }
                CompetitiveAdjustments.ConfigManager.Log("ChunkSyncServer: sent bulk snapshot (" + ChunkRegistry.Count
                          + " slots) to client " + clientId + ".");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[COMPADJUST] ChunkSyncServer.SendBulkTo failed: " + ex.Message);
            }
        }

        private static void BroadcastInstant(ushort id, ChunkCoord chunk)
        {
            BroadcastSingle(id, chunk, ChunkRegistry.NoSwitchTickId);
        }

        private static void BroadcastSwitch(ushort id, ChunkCoord chunk, ushort switchTickId)
        {
            BroadcastSingle(id, chunk, switchTickId);
        }

        private static void BroadcastSingle(ushort id, ChunkCoord chunk, ushort switchTickId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            var cmm = nm.CustomMessagingManager;
            if (cmm == null) return;

            int remote = nm.IsHost ? nm.ConnectedClientsIds.Count - 1 : nm.ConnectedClientsIds.Count;
            if (remote <= 0) return;

            try
            {
                var writer = new FastBufferWriter(8, Allocator.Temp, 32);
                try
                {
                    writer.WriteValueSafe((byte)0);
                    writer.WriteValueSafe(id);
                    writer.WriteValueSafe(chunk.X);
                    writer.WriteValueSafe(chunk.Z);
                    writer.WriteValueSafe(switchTickId);
                    cmm.SendNamedMessageToAll(CmmName, writer, NetworkDelivery.Reliable);
                }
                finally { writer.Dispose(); }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[COMPADJUST] ChunkSyncServer.BroadcastSingle failed: " + ex.Message);
            }
        }

        private static ChunkCoord WorldToChunk(Vector3 pos)
        {
            int cx = Mathf.RoundToInt(pos.x / ChunkRegistry.ChunkSizeMeters);
            int cz = Mathf.RoundToInt(pos.z / ChunkRegistry.ChunkSizeMeters);
            cx = Mathf.Clamp(cx, sbyte.MinValue, sbyte.MaxValue);
            cz = Mathf.Clamp(cz, sbyte.MinValue, sbyte.MaxValue);
            return new ChunkCoord((sbyte)cx, (sbyte)cz);
        }

        private static ChunkCoord HysteresisCheck(Vector3 worldPos, ChunkCoord current)
        {
            float cx = current.X * ChunkRegistry.ChunkSizeMeters;
            float cz = current.Z * ChunkRegistry.ChunkSizeMeters;
            float dx = worldPos.x - cx;
            float dz = worldPos.z - cz;

            int nx = current.X;
            int nz = current.Z;
            if      (dx >  FlipOutMeters) nx = Mathf.Clamp(current.X + 1, sbyte.MinValue, sbyte.MaxValue);
            else if (dx < -FlipOutMeters) nx = Mathf.Clamp(current.X - 1, sbyte.MinValue, sbyte.MaxValue);
            if      (dz >  FlipOutMeters) nz = Mathf.Clamp(current.Z + 1, sbyte.MinValue, sbyte.MaxValue);
            else if (dz < -FlipOutMeters) nz = Mathf.Clamp(current.Z - 1, sbyte.MinValue, sbyte.MaxValue);
            return new ChunkCoord((sbyte)nx, (sbyte)nz);
        }

        private static ushort GetCurrentTickId()
        {
            var mgr = NetworkBehaviourSingleton<SynchronizedObjectManager>.Instance;
            if (mgr == null || _serverTickIdField == null) return 0;
            try { return (ushort)_serverTickIdField.GetValue(mgr); }
            catch { return 0; }
        }

        private static bool TickGE(ushort a, ushort b) => (ushort)(a - b) < 32768;
    }
}
