using System.Numerics;
using GorillaAntiCheat.Core.Buffers;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;

namespace GorillaAntiCheat.Core.State
{
    /// <summary>
    /// All retained, untrusted history for a single player plus cheap derived
    /// accessors used by the detectors. Backed entirely by pre-allocated ring
    /// buffers so the per-tick path performs no heap allocations.
    /// </summary>
    public sealed class PlayerState
    {
        private readonly RingBuffer<PlayerTick> _ticks;
        private readonly RingBuffer<MetadataSnapshot> _metadata;
        private readonly RingBuffer<double> _rpcTimestamps;

        public PlayerState(int playerId, double joinTime, int joinTick, AntiCheatConfig config)
        {
            PlayerId = playerId;
            JoinTime = joinTime;
            JoinTick = joinTick;
            _ticks = new RingBuffer<PlayerTick>(config.HistoryTickCapacity);
            _metadata = new RingBuffer<MetadataSnapshot>(32);
            _rpcTimestamps = new RingBuffer<double>(256);
        }

        public int PlayerId { get; }

        public double JoinTime { get; }

        public int JoinTick { get; }

        public RingBuffer<PlayerTick> Ticks => _ticks;

        public RingBuffer<MetadataSnapshot> Metadata => _metadata;

        /// <summary>EMA baseline of this player's latency, used for spike detection.</summary>
        public float LatencyBaselineMs { get; private set; }

        /// <summary>Consecutive ticks the rig has been reported inside geometry (for NOCLIP persistence).</summary>
        public int InsideColliderStreak { get; private set; }

        /// <summary>Timestamp the rig was last grounded, for airtime computation.</summary>
        public double LastGroundedTime { get; private set; }

        public void RecordTick(in PlayerTick tick)
        {
            _ticks.Add(tick);

            if (tick.IsGrounded)
                LastGroundedTime = tick.Timestamp;

            InsideColliderStreak = tick.InsideCollider ? InsideColliderStreak + 1 : 0;

            // Latency baseline as an exponential moving average (slow to react so spikes stand out).
            if (LatencyBaselineMs <= 0f)
                LatencyBaselineMs = tick.LatencyMs;
            else
                LatencyBaselineMs += (tick.LatencyMs - LatencyBaselineMs) * 0.05f;
        }

        public void RecordMetadata(in MetadataSnapshot snapshot) => _metadata.Add(snapshot);

        public void RecordRpc(double time) => _rpcTimestamps.Add(time);

        public bool TryGetCurrent(out PlayerTick tick) => _ticks.TryGetNewest(out tick);

        public bool IsInJoinGrace(double now, AntiCheatConfig config) => now - JoinTime < config.JoinGraceSeconds;

        /// <summary>Counts RPCs received within the last <paramref name="window"/> seconds of <paramref name="now"/>.</summary>
        public int RpcCountWithin(double now, double window)
        {
            int count = 0;
            for (int i = _rpcTimestamps.Count - 1; i >= 0; i--)
            {
                if (now - _rpcTimestamps[i] <= window)
                    count++;
                else
                    break; // timestamps are monotonic, oldest first
            }
            return count;
        }

        /// <summary>Instantaneous velocity between the two newest ticks (m/s).</summary>
        public bool TryGetInstantVelocity(out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (_ticks.Count < 2)
                return false;
            PlayerTick a = _ticks.FromEnd(1);
            PlayerTick b = _ticks.FromEnd(0);
            double dt = b.Timestamp - a.Timestamp;
            if (dt <= 0)
                return false;
            velocity = (b.Position - a.Position) / (float)dt;
            return true;
        }

        /// <summary>Rolling-average velocity over the last <paramref name="samples"/> tick deltas (m/s).</summary>
        public bool TryGetSmoothedVelocity(int samples, out Vector3 velocity)
        {
            velocity = Vector3.Zero;
            if (_ticks.Count < 2)
                return false;

            int deltas = System.Math.Min(samples, _ticks.Count - 1);
            Vector3 sum = Vector3.Zero;
            int used = 0;
            for (int i = 0; i < deltas; i++)
            {
                PlayerTick newer = _ticks.FromEnd(i);
                PlayerTick older = _ticks.FromEnd(i + 1);
                double dt = newer.Timestamp - older.Timestamp;
                if (dt <= 0)
                    continue;
                sum += (newer.Position - older.Position) / (float)dt;
                used++;
            }
            if (used == 0)
                return false;
            velocity = sum / used;
            return true;
        }

        /// <summary>Continuous seconds since the rig was last grounded.</summary>
        public double AirtimeSeconds(double now) => now - LastGroundedTime;

        /// <summary>
        /// True if a hand contact (collision proxy) exists within +/- window ticks of <paramref name="centerTick"/>.
        /// Used to corroborate tag claims against real physics.
        /// </summary>
        public bool HasContactNear(int centerTick, int windowTicks)
        {
            for (int i = 0; i < _ticks.Count; i++)
            {
                PlayerTick t = _ticks[i];
                if (t.TickIndex < centerTick - windowTicks)
                    continue;
                if (t.TickIndex > centerTick + windowTicks)
                    break;
                if (t.AnyHandContact)
                    return true;
            }
            return false;
        }

        /// <summary>Returns the historical tick nearest to <paramref name="targetTime"/>.</summary>
        public bool TryGetTickNearTime(double targetTime, out PlayerTick result)
        {
            result = default;
            if (_ticks.Count == 0)
                return false;

            double bestDelta = double.MaxValue;
            for (int i = _ticks.Count - 1; i >= 0; i--)
            {
                PlayerTick t = _ticks[i];
                double delta = System.Math.Abs(t.Timestamp - targetTime);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    result = t;
                }
                else if (t.Timestamp < targetTime)
                {
                    // moving further into the past only increases the delta
                    break;
                }
            }
            return true;
        }
    }
}
