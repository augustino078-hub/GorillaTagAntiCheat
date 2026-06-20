using System.Numerics;
using GorillaAntiCheat.Core.Detectors;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;
using Xunit;

namespace GorillaAntiCheat.Tests
{
    public class TagDetectorTests
    {
        private readonly TestWorld _world = new();
        private readonly TagIntegrityDetector _detector = new();
        private readonly double _dt;

        public TagDetectorTests() => _dt = _world.Config.TickDeltaSeconds;

        /// <summary>Builds a tagger whose hands sit at <paramref name="handOffsetFromHead"/> relative to the head.</summary>
        private PlayerState BuildTagger(int id, Vector3 body, Vector3 handWorldPos, bool contact, float latency)
        {
            PlayerState p = _world.AddPlayer(id);
            for (int i = 0; i < 8; i++)
            {
                p.RecordTick(Ticks.Make(i, i * _dt, body,
                    head: body + new Vector3(0, 1.6f, 0),
                    leftHand: handWorldPos,
                    rightHand: handWorldPos,
                    leftContact: contact,
                    rightContact: contact,
                    latencyMs: latency));
            }
            return p;
        }

        private PlayerState BuildTarget(int id, Vector3 body)
        {
            PlayerState p = _world.AddPlayer(id);
            for (int i = 0; i < 8; i++)
                p.RecordTick(Ticks.Make(i, i * _dt, body));
            return p;
        }

        private ViolationCollector RunForTagger(PlayerState tagger)
        {
            var collector = new ViolationCollector();
            var ctx = new DetectorContext(_world, tagger, 7, 7 * _dt, _world.Config);
            _detector.Evaluate(in ctx, collector);
            return collector;
        }

        [Fact]
        public void LegitimateTag_RaisesNothing()
        {
            // Target within a plausible arm's reach of the tagger's head.
            Vector3 targetBody = new(0f, 1.6f, 0.5f);
            // Hand right on the target body, plausible arm length, contact present, low latency.
            PlayerState tagger = BuildTagger(1, Vector3.Zero, handWorldPos: targetBody, contact: true, latency: 50f);
            BuildTarget(2, targetBody);
            _world.AddTagEvent(new TagEvent(1, 2, 7, 7 * _dt, 7 * _dt, targetBody));

            ViolationCollector c = RunForTagger(tagger);
            Assert.Empty(c.Violations);
        }

        [Fact]
        public void HandFarFromTarget_RaisesTagAura()
        {
            Vector3 targetBody = new(5f, 0, 0);
            // Hand stays near the tagger's own body; target is 5 m away but contact flagged.
            PlayerState tagger = BuildTagger(1, Vector3.Zero, handWorldPos: new Vector3(0, 1f, 0.2f), contact: true, latency: 50f);
            BuildTarget(2, targetBody);
            _world.AddTagEvent(new TagEvent(1, 2, 7, 7 * _dt, 7 * _dt, Vector3.Zero));

            ViolationCollector c = RunForTagger(tagger);
            Assert.True(c.Has(ViolationCode.TagAura));
        }

        [Fact]
        public void OverlongArm_RaisesExtendedReach()
        {
            // Hand 2 m from head => reconstructed arm length 2 m > 1.1 m limit.
            Vector3 hand = new(2f, 1.6f, 0);
            PlayerState tagger = BuildTagger(1, Vector3.Zero, handWorldPos: hand, contact: true, latency: 50f);
            BuildTarget(2, hand); // target at the hand so range/aura is fine
            _world.AddTagEvent(new TagEvent(1, 2, 7, 7 * _dt, 7 * _dt, hand));

            ViolationCollector c = RunForTagger(tagger);
            Assert.True(c.Has(ViolationCode.ExtendedReach));
        }

        [Fact]
        public void NoCollisionButInRange_RaisesFakeTagNotSilent()
        {
            Vector3 targetBody = new(0f, 1.6f, 0.5f);
            PlayerState tagger = BuildTagger(1, Vector3.Zero, handWorldPos: targetBody, contact: false, latency: 50f);
            BuildTarget(2, targetBody);
            _world.AddTagEvent(new TagEvent(1, 2, 7, 7 * _dt, 7 * _dt, targetBody));

            ViolationCollector c = RunForTagger(tagger);
            Assert.True(c.Has(ViolationCode.FakeTag));
            Assert.False(c.Has(ViolationCode.SilentTag));
        }

        [Fact]
        public void NoCollisionAndNeverInRange_RaisesSilentTag()
        {
            Vector3 targetBody = new(5f, 0, 0);
            PlayerState tagger = BuildTagger(1, Vector3.Zero, handWorldPos: new Vector3(0, 1f, 0.2f), contact: false, latency: 50f);
            BuildTarget(2, targetBody);
            _world.AddTagEvent(new TagEvent(1, 2, 7, 7 * _dt, 7 * _dt, Vector3.Zero));

            ViolationCollector c = RunForTagger(tagger);
            Assert.True(c.Has(ViolationCode.SilentTag));
        }

        [Fact]
        public void LatencySpikeAtTag_RaisesLagSwitch()
        {
            Vector3 targetBody = new(0f, 1.6f, 0.5f);
            PlayerState tagger = BuildTagger(1, Vector3.Zero, handWorldPos: targetBody, contact: true, latency: 320f);
            BuildTarget(2, targetBody);
            _world.AddTagEvent(new TagEvent(1, 2, 7, 7 * _dt, 7 * _dt, targetBody));

            ViolationCollector c = RunForTagger(tagger);
            Assert.True(c.Has(ViolationCode.LagSwitchTag));
        }

        [Fact]
        public void PositionRollbackBetweenClaimAndReceive_RaisesDesync()
        {
            Vector3 targetBody = new(0.4f, 0, 0);
            PlayerState tagger = _world.AddPlayer(1);
            // Tagger teleports across its own history: far at claim time, near at receive time.
            // claim time ~ tick 1, receive time ~ tick 7.
            for (int i = 0; i < 8; i++)
            {
                float x = i < 4 ? 10f : 0.4f; // >2.5 m rollback between the two sampled times
                tagger.RecordTick(Ticks.Make(i, i * _dt, new Vector3(x, 0, 0),
                    leftHand: targetBody, rightHand: targetBody, leftContact: true, rightContact: true, latencyMs: 50f));
            }
            BuildTarget(2, targetBody);
            _world.AddTagEvent(new TagEvent(1, 2, 7, claimedTime: 1 * _dt, receivedTime: 7 * _dt, taggerHandPosition: targetBody));

            ViolationCollector c = RunForTagger(tagger);
            Assert.True(c.Has(ViolationCode.DesyncTag));
        }

        [Fact]
        public void TagEventForOtherTagger_IsIgnored()
        {
            Vector3 targetBody = new(5f, 0, 0);
            PlayerState tagger = BuildTagger(1, Vector3.Zero, handWorldPos: Vector3.Zero, contact: false, latency: 50f);
            BuildTarget(2, targetBody);
            // Event names a different tagger id (99) — subject 1 must not be charged.
            _world.AddTagEvent(new TagEvent(99, 2, 7, 7 * _dt, 7 * _dt, Vector3.Zero));

            ViolationCollector c = RunForTagger(tagger);
            Assert.Empty(c.Violations);
        }
    }
}
