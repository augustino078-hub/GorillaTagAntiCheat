using System.Collections.Generic;
using GorillaAntiCheat.Core.Buffers;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;

namespace GorillaAntiCheat.Core.Replay
{
    /// <summary>A flattened copy of a player's recent history, captured when they escalate.</summary>
    public sealed class ReplaySegment
    {
        public ReplaySegment(int playerId, PlayerTick[] ticks, float scoreAtCapture, string reason)
        {
            PlayerId = playerId;
            Ticks = ticks;
            ScoreAtCapture = scoreAtCapture;
            Reason = reason;
        }

        public int PlayerId { get; }
        public PlayerTick[] Ticks { get; }
        public float ScoreAtCapture { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Maintains a rolling per-player replay buffer (last N seconds) so that when a
    /// player escalates we can snapshot exactly what led up to it for human review.
    /// Recording is allocation-free; only the rare <see cref="Capture"/> allocates.
    /// </summary>
    public sealed class ReplayRecorder
    {
        private readonly AntiCheatConfig _config;
        private readonly Dictionary<int, RingBuffer<PlayerTick>> _buffers = new Dictionary<int, RingBuffer<PlayerTick>>();

        public ReplayRecorder(AntiCheatConfig config) => _config = config;

        public void Register(int playerId)
        {
            if (!_buffers.ContainsKey(playerId))
                _buffers[playerId] = new RingBuffer<PlayerTick>(_config.ReplayTickCapacity);
        }

        public void Remove(int playerId) => _buffers.Remove(playerId);

        public void Record(int playerId, in PlayerTick tick)
        {
            if (!_buffers.TryGetValue(playerId, out RingBuffer<PlayerTick> buffer))
            {
                buffer = new RingBuffer<PlayerTick>(_config.ReplayTickCapacity);
                _buffers[playerId] = buffer;
            }
            buffer.Add(tick);
        }

        public ReplaySegment? Capture(int playerId, float scoreAtCapture, string reason)
        {
            if (!_buffers.TryGetValue(playerId, out RingBuffer<PlayerTick> buffer) || buffer.Count == 0)
                return null;

            var ticks = new PlayerTick[buffer.Count];
            for (int i = 0; i < buffer.Count; i++)
                ticks[i] = buffer[i];
            return new ReplaySegment(playerId, ticks, scoreAtCapture, reason);
        }
    }
}
