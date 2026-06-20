using System.Numerics;
using GorillaAntiCheat.Core.Detectors;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;
using Xunit;

namespace GorillaAntiCheat.Tests
{
    public class MovementDetectorTests
    {
        private readonly TestWorld _world = new();
        private readonly MovementIntegrityDetector _detector = new();

        private ViolationCollector Run(PlayerState p, int tick, double now)
        {
            var collector = new ViolationCollector();
            var ctx = new DetectorContext(_world, p, tick, now, _world.Config);
            _detector.Evaluate(in ctx, collector);
            return collector;
        }

        [Fact]
        public void NormalWalking_RaisesNothing()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            for (int i = 0; i < 10; i++)
            {
                // 0.2 m / tick == 4 m/s, comfortably under the 12 m/s ceiling.
                p.RecordTick(Ticks.Make(i, i * dt, new Vector3(i * 0.2f, 0, 0)));
            }
            ViolationCollector c = Run(p, 9, 9 * dt);
            Assert.Empty(c.Violations);
        }

        [Fact]
        public void OverspeedSustained_RaisesSpeedHack()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            for (int i = 0; i < 10; i++)
            {
                // 1.0 m / tick == 20 m/s.
                p.RecordTick(Ticks.Make(i, i * dt, new Vector3(i * 1.0f, 0, 0)));
            }
            ViolationCollector c = Run(p, 9, 9 * dt);
            Assert.True(c.Has(ViolationCode.SpeedHack));
        }

        [Fact]
        public void SingleTickJump_RaisesTeleport()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            for (int i = 0; i < 5; i++)
                p.RecordTick(Ticks.Make(i, i * dt, new Vector3(i * 0.1f, 0, 0)));
            // Big low-latency jump (no lag-comp excuse).
            p.RecordTick(Ticks.Make(5, 5 * dt, new Vector3(20f, 0, 0), latencyMs: 40f));

            ViolationCollector c = Run(p, 5, 5 * dt);
            Assert.True(c.Has(ViolationCode.Teleport));
        }

        [Fact]
        public void HighLatencyJump_DoesNotRaiseTeleport()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            for (int i = 0; i < 5; i++)
                p.RecordTick(Ticks.Make(i, i * dt, new Vector3(i * 0.1f, 0, 0)));
            // Same jump but during a latency window above the lag-comp threshold.
            p.RecordTick(Ticks.Make(5, 5 * dt, new Vector3(20f, 0, 0), latencyMs: 300f));

            ViolationCollector c = Run(p, 5, 5 * dt);
            Assert.False(c.Has(ViolationCode.Teleport));
        }

        [Fact]
        public void SustainedHover_RaisesAirStall()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            // Grounded at t=0 to seed LastGroundedTime.
            p.RecordTick(Ticks.Make(0, 0, new Vector3(0, 5, 0), grounded: true));
            int tick = 1;
            double t = dt;
            // Airborne at constant altitude for > MaxAirtimeSeconds (no gravity decay).
            while (t < _world.Config.MaxAirtimeSeconds + 0.5)
            {
                p.RecordTick(Ticks.Make(tick, t, new Vector3(0, 5, 0), grounded: false));
                tick++;
                t += dt;
            }
            ViolationCollector c = Run(p, tick - 1, t - dt);
            Assert.True(c.Has(ViolationCode.AirStall));
        }

        [Fact]
        public void FastHandWithoutContact_RaisesInvalidClimb()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            Vector3 body = new(0, 0, 0);
            p.RecordTick(Ticks.Make(0, 0, body, leftHand: body + new Vector3(-0.3f, 1f, 0)));
            // Left hand jumps 0.6 m in one tick (12 m/s) with no surface contact.
            p.RecordTick(Ticks.Make(1, dt, body, leftHand: body + new Vector3(-0.3f, 1.6f, 0), leftContact: false));

            ViolationCollector c = Run(p, 1, dt);
            Assert.True(c.Has(ViolationCode.InvalidClimb));
        }

        [Fact]
        public void FastHandWithContact_DoesNotRaiseInvalidClimb()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            Vector3 body = new(0, 0, 0);
            p.RecordTick(Ticks.Make(0, 0, body, leftHand: body + new Vector3(-0.3f, 1f, 0)));
            p.RecordTick(Ticks.Make(1, dt, body, leftHand: body + new Vector3(-0.3f, 1.6f, 0), leftContact: true));

            ViolationCollector c = Run(p, 1, dt);
            Assert.False(c.Has(ViolationCode.InvalidClimb));
        }

        [Fact]
        public void PersistentInsideGeometry_RaisesNoclip()
        {
            PlayerState p = _world.AddPlayer(1);
            double dt = _world.Config.TickDeltaSeconds;
            for (int i = 0; i < 5; i++)
                p.RecordTick(Ticks.Make(i, i * dt, new Vector3(0, 0, 0), insideCollider: true));

            ViolationCollector c = Run(p, 4, 4 * dt);
            Assert.True(c.Has(ViolationCode.Noclip));
        }
    }
}
