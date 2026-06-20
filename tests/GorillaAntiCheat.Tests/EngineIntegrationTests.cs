using System.Numerics;
using GorillaAntiCheat.Core;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Replay;
using GorillaAntiCheat.Core.Scoring;
using GorillaAntiCheat.Core.State;
using Xunit;

namespace GorillaAntiCheat.Tests
{
    public class EngineIntegrationTests
    {
        private static AntiCheatConfig FastGraceConfig() => new() { JoinGraceSeconds = 0.5f };

        [Fact]
        public void SpeedHacker_EscalatesAndCapturesReplay()
        {
            var engine = new AntiCheatEngine(FastGraceConfig());
            double dt = engine.Config.TickDeltaSeconds;

            ReplaySegment? captured = null;
            engine.ReplayCaptured += seg => captured = seg;

            engine.RegisterPlayer(1, 0);
            double t = 0;
            for (int i = 0; i < 200; i++)
            {
                // 1 m per tick == 20 m/s, well above the 12 m/s ceiling.
                engine.RecordState(1, Ticks.Make(engine.CurrentTick, t, new Vector3(i * 1.0f, 0, 0)));
                engine.Step(t);
                t += dt;
            }

            Assert.True(engine.Scoring.TryGet(1, out SuspicionScore score));
            Assert.Equal(EscalationLevel.StrongWarning, score.Level);
            Assert.NotNull(captured);
            Assert.Equal(1, captured!.PlayerId);
            Assert.True(captured.Ticks.Length > 0);
        }

        [Fact]
        public void CleanPlayer_StaysIgnored()
        {
            var engine = new AntiCheatEngine(FastGraceConfig());
            double dt = engine.Config.TickDeltaSeconds;

            engine.RegisterPlayer(1, 0);
            double t = 0;
            for (int i = 0; i < 200; i++)
            {
                // 0.2 m per tick == 4 m/s, grounded, hands by the body.
                engine.RecordState(1, Ticks.Make(engine.CurrentTick, t, new Vector3(i * 0.2f, 0, 0)));
                engine.Step(t);
                t += dt;
            }

            Assert.True(engine.Scoring.TryGet(1, out SuspicionScore score));
            Assert.Equal(0f, score.Total);
            Assert.Equal(EscalationLevel.Ignore, score.Level);
        }

        [Fact]
        public void JoinGrace_SuppressesEarlyFlags()
        {
            // A player who speed-hacks only during the grace window must not be flagged.
            var engine = new AntiCheatEngine(new AntiCheatConfig { JoinGraceSeconds = 3f });
            double dt = engine.Config.TickDeltaSeconds;

            engine.RegisterPlayer(1, 0);
            double t = 0;
            for (int i = 0; i < 40; i++) // 40 ticks == 2 s < 3 s grace
            {
                engine.RecordState(1, Ticks.Make(engine.CurrentTick, t, new Vector3(i * 1.0f, 0, 0)));
                engine.Step(t);
                t += dt;
            }

            Assert.True(engine.Scoring.TryGet(1, out SuspicionScore score));
            Assert.Equal(0f, score.Total);
        }

        [Fact]
        public void RecordMetadata_CountsOnlyNonWhitelistedKeys()
        {
            var engine = new AntiCheatEngine();
            engine.RegisterPlayer(1, 0);
            engine.RecordMetadata(1, new[] { "color", "hat", "speed_multiplier" }, now: 1.0);

            Assert.True(engine.TryGetPlayer(1, out PlayerState p));
            Assert.True(p.Metadata.TryGetNewest(out var snapshot));
            Assert.Equal(1, snapshot.UnknownKeyCount); // only "speed_multiplier"
            Assert.Equal(3, snapshot.TotalKeyCount);
        }

        [Fact]
        public void DisablingDetector_StopsItsSignals()
        {
            var engine = new AntiCheatEngine(FastGraceConfig());
            engine.SetDetectorEnabled("Movement", false);
            double dt = engine.Config.TickDeltaSeconds;

            engine.RegisterPlayer(1, 0);
            double t = 0;
            for (int i = 0; i < 200; i++)
            {
                engine.RecordState(1, Ticks.Make(engine.CurrentTick, t, new Vector3(i * 1.0f, 0, 0)));
                engine.Step(t);
                t += dt;
            }

            // With the movement detector off, the speed hack produces no score.
            Assert.True(engine.Scoring.TryGet(1, out SuspicionScore score));
            Assert.Equal(0f, score.Total);
        }
    }
}
