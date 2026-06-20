using System;
using System.Numerics;
using GorillaAntiCheat.Core.Detectors;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;
using Xunit;

namespace GorillaAntiCheat.Tests
{
    public class NetworkDetectorTests
    {
        private readonly TestWorld _world = new();
        private readonly NetworkDetector _detector = new();
        private readonly double _dt;

        public NetworkDetectorTests() => _dt = _world.Config.TickDeltaSeconds;

        private ViolationCollector Run(PlayerState p, int tick, double now)
        {
            var collector = new ViolationCollector();
            var ctx = new DetectorContext(_world, p, tick, now, _world.Config);
            _detector.Evaluate(in ctx, collector);
            return collector;
        }

        private static Quaternion YawDegrees(float deg)
        {
            float rad = deg * (float)(Math.PI / 180.0);
            return Quaternion.CreateFromAxisAngle(Vector3.UnitY, rad);
        }

        [Fact]
        public void ExcessiveRpcs_RaisesRpcSpam()
        {
            PlayerState p = _world.AddPlayer(1);
            double now = 5.0;
            p.RecordTick(Ticks.Make(0, now, Vector3.Zero));
            // 60 RPCs within the last second.
            for (int i = 0; i < 60; i++)
                p.RecordRpc(now - 0.5 + i * 0.001);

            ViolationCollector c = Run(p, 1, now);
            Assert.True(c.Has(ViolationCode.RpcSpam));
        }

        [Fact]
        public void NormalRpcRate_DoesNotRaiseSpam()
        {
            PlayerState p = _world.AddPlayer(1);
            double now = 5.0;
            p.RecordTick(Ticks.Make(0, now, Vector3.Zero));
            for (int i = 0; i < 10; i++)
                p.RecordRpc(now - 0.5 + i * 0.05);

            ViolationCollector c = Run(p, 1, now);
            Assert.False(c.Has(ViolationCode.RpcSpam));
        }

        [Fact]
        public void InstantRotationFlip_RaisesImpossibleTransition()
        {
            PlayerState p = _world.AddPlayer(1);
            p.RecordTick(Ticks.Make(0, 0, Vector3.Zero, rotation: YawDegrees(0)));
            p.RecordTick(Ticks.Make(1, _dt, Vector3.Zero, rotation: YawDegrees(0)));
            p.RecordTick(Ticks.Make(2, 2 * _dt, Vector3.Zero, rotation: YawDegrees(179)));

            ViolationCollector c = Run(p, 2, 2 * _dt);
            Assert.True(c.Has(ViolationCode.ImpossibleTransition));
        }

        [Fact]
        public void NonInterpolatablePath_RaisesStateDesync()
        {
            PlayerState p = _world.AddPlayer(1);
            p.RecordTick(Ticks.Make(0, 0, new Vector3(0, 0, 0)));
            p.RecordTick(Ticks.Make(1, _dt, new Vector3(1, 0, 0)));
            // Constant-velocity prediction is (2,0,0); actual is (10,0,0).
            p.RecordTick(Ticks.Make(2, 2 * _dt, new Vector3(10, 0, 0)));

            ViolationCollector c = Run(p, 2, 2 * _dt);
            Assert.True(c.Has(ViolationCode.StateDesync));
        }

        [Fact]
        public void LatencySpikeWithTagEvent_RaisesBurstLagAbuse()
        {
            PlayerState p = _world.AddPlayer(1);
            // Establish a low baseline.
            for (int i = 0; i < 20; i++)
                p.RecordTick(Ticks.Make(i, i * _dt, Vector3.Zero, latencyMs: 50f));
            // Spike on the current tick.
            p.RecordTick(Ticks.Make(20, 20 * _dt, Vector3.Zero, latencyMs: 350f));
            _world.AddTagEvent(new TagEvent(1, 2, 20, 20 * _dt, 20 * _dt, Vector3.Zero));

            ViolationCollector c = Run(p, 20, 20 * _dt);
            Assert.True(c.Has(ViolationCode.BurstLagAbuse));
        }

        [Fact]
        public void LatencySpikeWithoutEvent_DoesNotRaiseBurstLagAbuse()
        {
            PlayerState p = _world.AddPlayer(1);
            for (int i = 0; i < 20; i++)
                p.RecordTick(Ticks.Make(i, i * _dt, Vector3.Zero, latencyMs: 50f));
            p.RecordTick(Ticks.Make(20, 20 * _dt, Vector3.Zero, latencyMs: 350f));

            ViolationCollector c = Run(p, 20, 20 * _dt);
            Assert.False(c.Has(ViolationCode.BurstLagAbuse));
        }
    }
}
