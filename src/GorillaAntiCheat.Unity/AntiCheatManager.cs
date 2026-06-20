using System.Collections.Generic;
using GorillaAntiCheat.Core;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.Replay;
using GorillaAntiCheat.Core.Scoring;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// Unity entrypoint that drives <see cref="AntiCheatEngine"/> on a fixed,
    /// frame-rate-independent tick. It samples every player's rig once per tick, feeds
    /// the untrusted state into the engine, surfaces escalations, and renders the debug
    /// overlay. It performs no detection itself — all logic lives in the core.
    /// </summary>
    public sealed class AntiCheatManager : MonoBehaviour
    {
        private AntiCheatEngine _engine = null!;
        private IRigProvider _rigProvider = null!;
        private DebugOverlay _overlay = null!;

        private readonly List<RigSample> _sampleBuffer = new List<RigSample>(16);
        private readonly HashSet<int> _knownActors = new HashSet<int>();
        private readonly HashSet<int> _seenThisTick = new HashSet<int>();
        private readonly List<int> _toRemove = new List<int>();

        private double _simTime;
        private double _tickAccumulator;
        private double _tickInterval;

        public AntiCheatEngine Engine => _engine;
        public bool OverlayEnabled { get; set; } = true;

        /// <summary>Initialises the manager. Call once after the component is added.</summary>
        public void Configure(AntiCheatConfig? config = null, IRigProvider? rigProvider = null)
        {
            _engine = new AntiCheatEngine(config);
            _tickInterval = _engine.Config.TickDeltaSeconds;
            _rigProvider = rigProvider ?? new GorillaRigProvider(~0);
            _overlay = new DebugOverlay(_engine);

            _engine.EscalationChanged += OnEscalationChanged;
            _engine.ReplayCaptured += OnReplayCaptured;
        }

        private void Update()
        {
            if (_engine == null)
                return;

            // Accumulate real time and run zero-or-more fixed ticks. This decouples the
            // anti-cheat tick rate from the render frame rate (VR runs 72–120+ fps).
            _tickAccumulator += Time.unscaledDeltaTime;
            int guard = 0;
            while (_tickAccumulator >= _tickInterval && guard++ < 4)
            {
                _tickAccumulator -= _tickInterval;
                _simTime += _tickInterval;
                RunTick(_simTime);
            }
        }

        private void RunTick(double now)
        {
            GorillaRigRegistry.MaybeRefresh();
            _rigProvider.SampleAll(_sampleBuffer);

            _seenThisTick.Clear();
            for (int i = 0; i < _sampleBuffer.Count; i++)
            {
                RigSample s = _sampleBuffer[i];
                _seenThisTick.Add(s.ActorNumber);
                if (_knownActors.Add(s.ActorNumber))
                    _engine.RegisterPlayer(s.ActorNumber, now);

                _engine.RecordState(s.ActorNumber, ToTick(s, now));
            }

            PruneLeftPlayers();

            _engine.Step(now);
        }

        private PlayerTick ToTick(in RigSample s, double now) => new PlayerTick(
            _engine.CurrentTick,
            now,
            s.RigPosition.ToNumerics(),
            s.Rotation.ToNumerics(),
            s.HeadPosition.ToNumerics(),
            s.LeftHandPosition.ToNumerics(),
            s.RightHandPosition.ToNumerics(),
            s.IsGrounded,
            s.LeftHandContact,
            s.RightHandContact,
            s.InsideCollider,
            s.LatencyMs);

        private void PruneLeftPlayers()
        {
            _toRemove.Clear();
            foreach (int actor in _knownActors)
            {
                if (!_seenThisTick.Contains(actor))
                    _toRemove.Add(actor);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                _engine.RemovePlayer(_toRemove[i]);
                _knownActors.Remove(_toRemove[i]);
            }
        }

        // ---- Hooks called by the Photon observer ---------------------------------
        /// <summary>Report an observed tag/infection RPC for corroboration.</summary>
        public void ReportTag(int taggerActor, int targetActor, double claimedTime, Vector3 taggerHand)
        {
            if (_engine == null) return;
            _engine.RecordTagEvent(new TagEvent(taggerActor, targetActor, _engine.CurrentTick, claimedTime, _simTime, taggerHand.ToNumerics()));
        }

        /// <summary>Report that a player sent any RPC (for spam/cadence analysis).</summary>
        public void ReportRpc(int actor)
        {
            if (_engine == null) return;
            _engine.RecordRpc(actor, _simTime);
        }

        /// <summary>Report a Photon custom-property change. Values are never inspected.</summary>
        public void ReportMetadata(int actor, IReadOnlyList<string> keys)
        {
            if (_engine == null) return;
            _engine.RecordMetadata(actor, keys, _simTime);
        }

        // ---- Escalation surface ---------------------------------------------------
        private void OnEscalationChanged(int actor, EscalationLevel from, EscalationLevel to)
        {
            if (to > from && to >= EscalationLevel.Suspicious)
                Debug.LogWarning($"[AntiCheat] Player {actor} escalated {from} -> {to}");
        }

        private void OnReplayCaptured(ReplaySegment segment)
        {
            Debug.LogWarning($"[AntiCheat] Replay captured for player {segment.PlayerId} " +
                             $"(score {segment.ScoreAtCapture:F0}, {segment.Ticks.Length} ticks): {segment.Reason}");
            ReplayWriter.Save(segment);
        }

        private void OnGUI()
        {
            if (OverlayEnabled)
                _overlay?.Draw();
        }

        private void OnDestroy()
        {
            if (_engine == null) return;
            _engine.EscalationChanged -= OnEscalationChanged;
            _engine.ReplayCaptured -= OnReplayCaptured;
        }
    }
}
