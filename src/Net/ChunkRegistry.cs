using System.Collections.Generic;
using UnityEngine;

namespace DashFallMod.Net
{
    /// <summary>
    /// Per-object chunk offset table.  Every networked object is treated as
    /// living inside a 32 x 32 m chunk on X/Z (Y is not chunked).  Position
    /// quantisation encodes the world position MINUS the chunk origin, so the
    /// wire value is always within +/-50 m of zero -- comfortably inside the
    /// +/-50 m range a signed 16-bit short x 655f precision affords.  Decoding
    /// adds the chunk origin back, so the round-trip is exact whenever the
    /// chunk offset is an integer multiple of (1 / precision).  Chunks are
    /// sized to a clean multiple of 655 (32 x 655 = 20960, integer) so the
    /// round-trip is bit-exact regardless of where in the chunk the object
    /// sits.
    /// </summary>
    public static class ChunkRegistry
    {
        // 32 x 655 = 20960, integer -- chunkSize x ActivePrecision must be an
        // integer for exact round-trip across chunk handoffs.
        public const float ChunkSizeMeters = 32f;

        // Sentinel tickId meaning "no pending switch".  The vanilla server's
        // tick counter wraps from ushort.MaxValue -> 0, so ushort.MaxValue
        // (0xFFFF) is never a real tickId -- safe sentinel.  Also used in the
        // wire format for "apply this chunk immediately, no deferred switch"
        // (bulk snapshot to late joiners).
        public const ushort NoSwitchTickId = ushort.MaxValue;

        public static float ActivePrecision = 655f;
        public static bool ChunksActive;

        // Set per server-tick by ChunkSyncServer.GatherPrefix.  Read by the
        // encode helpers below.
        public static ushort CurrentEncodeTickId;
        // Set per receive-tick by ChunkSyncClient.RpcPrefix.  Read by the
        // decode helpers below.
        public static ushort CurrentDecodeTickId;

        private static readonly Dictionary<ushort, ChunkSlot> _slots
            = new Dictionary<ushort, ChunkSlot>();

        public static bool TryGet(ushort id, out ChunkSlot slot) => _slots.TryGetValue(id, out slot);
        public static void Set(ushort id, ChunkSlot slot)         => _slots[id] = slot;
        public static void Remove(ushort id)                      => _slots.Remove(id);
        public static void Clear()                                => _slots.Clear();

        public static IEnumerable<KeyValuePair<ushort, ChunkSlot>> Snapshot() => _slots;
        public static int Count => _slots.Count;

        // Scratch buffer reused across promotion sweeps to avoid per-tick
        // garbage from materialising the keys.
        private static readonly List<ushort> _promoteScratch = new List<ushort>();

        /// <summary>
        /// Promote any slot whose pending switch tick has arrived, clearing
        /// HasPending so Current is authoritative. Without this sweep, a slot
        /// that takes longer than ~half the ushort tick range (~18 min at 30Hz)
        /// without a follow-up announce wraps around and its old PendingTickId
        /// starts looking "in the future" again, flipping ResolveAt back to the
        /// stale Current chunk. Cheap: skip-fast when HasPending=false.
        /// </summary>
        public static void PromoteAllIfDue(ushort tick)
        {
            if (_slots.Count == 0) return;

            _promoteScratch.Clear();
            foreach (var kvp in _slots)
            {
                var slot = kvp.Value;
                if (!slot.HasPending) continue;
                // TickGE(tick, PendingTickId): pending tick has arrived.
                if ((ushort)(tick - slot.PendingTickId) < 32768)
                    _promoteScratch.Add(kvp.Key);
            }

            for (int i = 0; i < _promoteScratch.Count; i++)
            {
                ushort id = _promoteScratch[i];
                var slot = _slots[id];
                slot.Current = slot.Pending;
                slot.HasPending = false;
                slot.Pending = default;
                slot.PendingTickId = 0;
                _slots[id] = slot;
            }
        }

        /// <summary>
        /// Apply an incoming chunk announce (or initial-state entry) to the
        /// per-id slot.  If <paramref name="switchTickId"/> equals
        /// <see cref="NoSwitchTickId"/> the chunk takes effect immediately --
        /// used by the late-join bulk snapshot.  Otherwise we install it as
        /// a deferred Pending; any existing Pending is promoted to Current
        /// first (its switch tick has already passed by definition -- the
        /// server only emits a new transition after promoting the previous).
        /// </summary>
        public static void ApplyAnnounce(ushort id, ChunkCoord chunk, ushort switchTickId)
        {
            _slots.TryGetValue(id, out var slot);

            if (switchTickId == NoSwitchTickId)
            {
                slot.Current = chunk;
                slot.HasPending = false;
                slot.Pending = default;
                slot.PendingTickId = 0;
            }
            else
            {
                if (slot.HasPending)
                    slot.Current = slot.Pending;

                slot.Pending = chunk;
                slot.PendingTickId = switchTickId;
                slot.HasPending = true;
            }

            _slots[id] = slot;
        }

        public static short EncodeX(float worldX, ushort id)
        {
            if (!ChunksActive)
                return (short)(worldX * ActivePrecision);

            float offset = 0f;
            if (_slots.TryGetValue(id, out var slot))
                offset = slot.ResolveAt(CurrentEncodeTickId).X * ChunkSizeMeters;
            return (short)((worldX - offset) * ActivePrecision);
        }

        public static short EncodeY(float worldY)
        {
            return (short)(worldY * ActivePrecision);
        }

        public static short EncodeZ(float worldZ, ushort id)
        {
            if (!ChunksActive)
                return (short)(worldZ * ActivePrecision);

            float offset = 0f;
            if (_slots.TryGetValue(id, out var slot))
                offset = slot.ResolveAt(CurrentEncodeTickId).Z * ChunkSizeMeters;
            return (short)((worldZ - offset) * ActivePrecision);
        }

        public static float DecodeX(float encoded, ushort id)
        {
            float val = encoded / ActivePrecision;
            if (!ChunksActive) return val;

            if (_slots.TryGetValue(id, out var slot))
                val += slot.ResolveAt(CurrentDecodeTickId).X * ChunkSizeMeters;
            return val;
        }

        public static float DecodeY(float encoded)
        {
            return encoded / ActivePrecision;
        }

        public static float DecodeZ(float encoded, ushort id)
        {
            float val = encoded / ActivePrecision;
            if (!ChunksActive) return val;

            if (_slots.TryGetValue(id, out var slot))
                val += slot.ResolveAt(CurrentDecodeTickId).Z * ChunkSizeMeters;
            return val;
        }
    }

    /// <summary>
    /// Integer 2D chunk index.  +/-127 chunks at 32 m gives a +/-4 km world span.
    /// </summary>
    public struct ChunkCoord
    {
        public sbyte X;
        public sbyte Z;

        public ChunkCoord(sbyte x, sbyte z) { X = x; Z = z; }

        public override string ToString() => $"({X},{Z})";

        public override bool Equals(object obj)
            => obj is ChunkCoord c && c.X == X && c.Z == Z;
        public override int GetHashCode() => (X << 8) | (byte)Z;

        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.X == b.X && a.Z == b.Z;
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !(a == b);
    }

    /// <summary>
    /// Per-object resolution state.  Current is the chunk used for tickIds
    /// strictly before PendingTickId; Pending is used for tickIds at or after
    /// PendingTickId (when HasPending is true).  Modular arithmetic on the
    /// ushort tickId makes the comparison wrap-safe -- same trick the vanilla
    /// skipLateTicks check uses.
    /// </summary>
    public struct ChunkSlot
    {
        public ChunkCoord Current;
        public ChunkCoord Pending;
        public ushort     PendingTickId;
        public bool       HasPending;

        public ChunkCoord ResolveAt(ushort tickId)
        {
            if (!HasPending) return Current;
            return ((ushort)(tickId - PendingTickId) < 32768) ? Pending : Current;
        }
    }
}
