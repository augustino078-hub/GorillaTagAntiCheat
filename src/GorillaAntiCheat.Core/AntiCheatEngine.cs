using System;
using System.Collections.Generic;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Detectors;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.Replay;
using GorillaAntiCheat.Core.Scoring;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Core
{
    /// <summary>
    /// Engine-agnostic orchestrator for the anti-cheat system. The Unity/BepInEx layer
    /// feeds it untrusted Photon-replicated state each tick; it runs every enabled
    /// detector, folds the weighted signals into per-player scores with decay, and
    /// snapshots replays on escalation. It contains no Unity types so it is fully
    /// unit-testable and free of frame-rate coupling.
    /// </summary>
    public sealed class AntiCheatEngine : IAntiCheatWorld
    {
        private readonly AntiCheatConfig _config;
        private readonly List<IDetector> _detectors = new List<IDetector>();
        private readonly Dictionary<int, PlayerState> _players = new Dictionary<int, PlayerState>();
        private readonly ScoringEngine _scoring;
        private readonly ReplayRecorder _replay;
        private readonly ViolationCollector _collector = new ViolationCollector();
        private readonly List<TagEvent> _pendingTagEvents = new List<TagEvent>(16);
        private readonly Dictionary<int, EscalationLevel> _lastLevel = new Dictionary<int, EscalationLevel>();

        private double _lastStepTime = double.NaN;

        public AntiCheatEngine(AntiCheatConfig? config = null)
        {
            _config = config ?? new AntiCheatConfig();
            _scoring = new ScoringEngine(_config);
            _replay = new ReplayRecorder(_config);
            AddDefaultDetectors();
        }

        public AntiCheatConfig Config => _config;

        public int CurrentTick { get; private set; }

        public IReadOnlyList<IDetector> Detectors => _detectors;

        public ScoringEngine Scoring => _scoring;

        public ReplayRecorder Replay => _replay;

        /// <summary>Raised when a player's escalation level changes (either direction).</summary>
        public event Action<int, EscalationLevel, EscalationLevel>? EscalationChanged;

        /// <summary>Raised once when a player first reaches <see cref="EscalationLevel.StrongWarning"/>.</summary>
        public event Action<ReplaySegment>? ReplayCaptured;

        // ---- Detector registry ---------------------------------------------------
        private void AddDefaultDetectors()
        {
            _detectors.Add(new MovementIntegrityDetector());
            _detectors.Add(new TagIntegrityDetector());
            _detectors.Add(new NetworkDetector());
            // Metadata must run last: it only correlates with already-collected signals.
            _detectors.Add(new MetadataDetector());
        }

        public void AddDetector(IDetector detector) => _detectors.Add(detector);

        public void SetDetectorEnabled(string name, bool enabled)
        {
            for (int i = 0; i < _detectors.Count; i++)
            {
                if (string.Equals(_detectors[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    _detectors[i].Enabled = enabled;
            }
        }

        // ---- Player lifecycle -----------------------------------------------------
        public PlayerState RegisterPlayer(int playerId, double now)
        {
            if (!_players.TryGetValue(playerId, out PlayerState state))
            {
                state = new PlayerState(playerId, now, CurrentTick, _config);
                _players[playerId] = state;
                _scoring.GetOrCreate(playerId);
                _replay.Register(playerId);
                _lastLevel[playerId] = EscalationLevel.Ignore;
            }
            return state;
        }

        public void RemovePlayer(int playerId)
        {
            _players.Remove(playerId);
            _scoring.Remove(playerId);
            _replay.Remove(playerId);
            _lastLevel.Remove(playerId);
        }

        // ---- Untrusted input ------------------------------------------------------
        /// <summary>Records a sampled state tick for a player (tick index should be <see cref="CurrentTick"/>).</summary>
        public void RecordState(int playerId, in PlayerTick tick)
        {
            PlayerState state = GetOrRegister(playerId, tick.Timestamp);
            state.RecordTick(tick);
            _replay.Record(playerId, tick);
        }

        public void RecordTagEvent(in TagEvent tagEvent) => _pendingTagEvents.Add(tagEvent);

        public void RecordRpc(int playerId, double time)
        {
            PlayerState state = GetOrRegister(playerId, time);
            state.RecordRpc(time);
        }

        /// <summary>
        /// Records a Photon custom-property change. We only retain how many keys fall
        /// outside the safe whitelist — never the values themselves.
        /// </summary>
        public void RecordMetadata(int playerId, IReadOnlyList<string> keys, double now)
        {
            PlayerState state = GetOrRegister(playerId, now);
            int unknown = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                if (!_config.AllowedMetadataKeys.Contains(keys[i]))
                    unknown++;
            }
            state.RecordMetadata(new MetadataSnapshot(CurrentTick, now, unknown, keys.Count));
        }

        // ---- Tick driver ----------------------------------------------------------
        /// <summary>
        /// Runs all enabled detectors for every known player, updates scores with decay,
        /// captures replays on first escalation, then advances the tick counter.
        /// </summary>
        public void Step(double now)
        {
            double dt = double.IsNaN(_lastStepTime) ? _config.TickDeltaSeconds : now - _lastStepTime;
            if (dt <= 0) dt = _config.TickDeltaSeconds;

            foreach (KeyValuePair<int, PlayerState> kv in _players)
            {
                int playerId = kv.Key;
                PlayerState subject = kv.Value;

                _collector.Reset();
                var ctx = new DetectorContext(this, subject, CurrentTick, now, _config);
                for (int d = 0; d < _detectors.Count; d++)
                {
                    IDetector detector = _detectors[d];
                    if (detector.Enabled)
                        detector.Evaluate(in ctx, _collector);
                }

                bool suppress = subject.IsInJoinGrace(now, _config);
                _scoring.Apply(playerId, _collector.Violations, dt, suppress);

                HandleEscalation(playerId, now);
            }

            _pendingTagEvents.Clear();
            _lastStepTime = now;
            CurrentTick++;
        }

        private void HandleEscalation(int playerId, double now)
        {
            if (!_scoring.TryGet(playerId, out SuspicionScore score))
                return;

            EscalationLevel previous = _lastLevel.TryGetValue(playerId, out EscalationLevel l) ? l : EscalationLevel.Ignore;
            EscalationLevel current = score.Level;
            if (current == previous)
                return;

            _lastLevel[playerId] = current;
            EscalationChanged?.Invoke(playerId, previous, current);

            // Capture a replay only on the rising edge into StrongWarning.
            if (current == EscalationLevel.StrongWarning && previous < EscalationLevel.StrongWarning)
            {
                ReplaySegment? segment = _replay.Capture(playerId, score.Total, "Escalated to StrongWarning");
                if (segment != null)
                    ReplayCaptured?.Invoke(segment);
            }
        }

        private PlayerState GetOrRegister(int playerId, double now)
            => _players.TryGetValue(playerId, out PlayerState state) ? state : RegisterPlayer(playerId, now);

        // ---- IAntiCheatWorld ------------------------------------------------------
        public bool TryGetPlayer(int playerId, out PlayerState state) => _players.TryGetValue(playerId, out state!);

        public IReadOnlyList<TagEvent> PendingTagEvents => _pendingTagEvents;
    }
}
