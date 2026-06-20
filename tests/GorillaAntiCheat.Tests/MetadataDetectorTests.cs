using GorillaAntiCheat.Core.Detectors;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;
using Xunit;

namespace GorillaAntiCheat.Tests
{
    public class MetadataDetectorTests
    {
        private readonly TestWorld _world = new();
        private readonly MetadataDetector _detector = new();

        private ViolationCollector RunWith(PlayerState p, params Violation[] seeded)
        {
            var collector = new ViolationCollector();
            foreach (Violation v in seeded)
                collector.Add(v);
            var ctx = new DetectorContext(_world, p, 0, 0, _world.Config);
            _detector.Evaluate(in ctx, collector);
            return collector;
        }

        private static Violation MovementSignal(int playerId)
            => new(ViolationCode.SpeedHack, ViolationCategory.Movement, 1f, 0, playerId, 0f);

        [Fact]
        public void UnknownKeysAloneAreNotEnough()
        {
            PlayerState p = _world.AddPlayer(1);
            p.RecordMetadata(new MetadataSnapshot(0, 0, unknownKeyCount: 2, totalKeyCount: 5));

            ViolationCollector c = RunWith(p); // no real violations
            Assert.False(c.Has(ViolationCode.UnexpectedMetadata));
        }

        [Fact]
        public void UnknownKeysPlusRealViolation_AddsContextSignal()
        {
            PlayerState p = _world.AddPlayer(1);
            p.RecordMetadata(new MetadataSnapshot(0, 0, unknownKeyCount: 2, totalKeyCount: 5));

            ViolationCollector c = RunWith(p, MovementSignal(1));
            Assert.True(c.Has(ViolationCode.UnexpectedMetadata));
        }

        [Fact]
        public void OnlyWhitelistedKeys_NeverAddsContextSignal()
        {
            PlayerState p = _world.AddPlayer(1);
            // unknownKeyCount == 0 models a property set fully within the safe whitelist.
            p.RecordMetadata(new MetadataSnapshot(0, 0, unknownKeyCount: 0, totalKeyCount: 4));

            ViolationCollector c = RunWith(p, MovementSignal(1));
            Assert.False(c.Has(ViolationCode.UnexpectedMetadata));
        }

        [Fact]
        public void RealViolationForDifferentPlayer_DoesNotCorrelate()
        {
            PlayerState p = _world.AddPlayer(1);
            p.RecordMetadata(new MetadataSnapshot(0, 0, unknownKeyCount: 2, totalKeyCount: 5));

            // The only real violation belongs to player 2, so player 1 must not be charged.
            ViolationCollector c = RunWith(p, MovementSignal(2));
            Assert.False(c.Has(ViolationCode.UnexpectedMetadata));
        }
    }
}
