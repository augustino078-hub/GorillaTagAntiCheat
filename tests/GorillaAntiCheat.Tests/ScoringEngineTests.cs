using System.Collections.Generic;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.Scoring;
using Xunit;

namespace GorillaAntiCheat.Tests
{
    public class ScoringEngineTests
    {
        private static List<Violation> One(ViolationCategory cat, float weight, int playerId)
            => new() { new Violation("X", cat, weight, 0, playerId, 0f) };

        private static readonly List<Violation> None = new();

        [Fact]
        public void SustainedSignals_EscalateToStrongWarning()
        {
            var cfg = new AntiCheatConfig();
            var engine = new ScoringEngine(cfg);
            double dt = cfg.TickDeltaSeconds;

            for (int i = 0; i < 30; i++)
                engine.Apply(1, One(ViolationCategory.Tag, 1f, 1), dt, suppress: false);

            Assert.True(engine.TryGet(1, out SuspicionScore score));
            Assert.Equal(EscalationLevel.StrongWarning, score.Level);
            Assert.True(score.Total >= cfg.StrongWarningThreshold);
        }

        [Fact]
        public void SingleEvent_DecaysBackToIgnore()
        {
            var cfg = new AntiCheatConfig();
            var engine = new ScoringEngine(cfg);
            double dt = cfg.TickDeltaSeconds;

            // One isolated burst.
            engine.Apply(1, One(ViolationCategory.Movement, 1f, 1), dt, suppress: false);
            // Then many clean ticks (~5 seconds of decay).
            for (int i = 0; i < 100; i++)
                engine.Apply(1, None, dt, suppress: false);

            Assert.True(engine.TryGet(1, out SuspicionScore score));
            Assert.Equal(0f, score.Total);
            Assert.Equal(EscalationLevel.Ignore, score.Level);
        }

        [Fact]
        public void SuppressedTicks_IgnoreSignals()
        {
            var cfg = new AntiCheatConfig();
            var engine = new ScoringEngine(cfg);
            double dt = cfg.TickDeltaSeconds;

            for (int i = 0; i < 30; i++)
                engine.Apply(1, One(ViolationCategory.Tag, 1f, 1), dt, suppress: true);

            Assert.True(engine.TryGet(1, out SuspicionScore score));
            Assert.Equal(0f, score.Total);
        }

        [Fact]
        public void TagWeightedHigherThanMovement()
        {
            var cfg = new AntiCheatConfig();
            var movement = new ScoringEngine(cfg);
            var tag = new ScoringEngine(cfg);
            double dt = cfg.TickDeltaSeconds;

            // Identical signal volume, different category.
            for (int i = 0; i < 5; i++)
            {
                movement.Apply(1, One(ViolationCategory.Movement, 1f, 1), dt, suppress: false);
                tag.Apply(1, One(ViolationCategory.Tag, 1f, 1), dt, suppress: false);
            }

            movement.TryGet(1, out SuspicionScore mScore);
            tag.TryGet(1, out SuspicionScore tScore);
            Assert.True(tScore.Total > mScore.Total);
        }
    }
}
