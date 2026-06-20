using System;
using System.Numerics;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Validates locomotion against an approximate VR physics model. Every check uses
    /// smoothing / persistence windows so a single noisy tick never produces a signal.
    /// </summary>
    public sealed class MovementIntegrityDetector : DetectorBase, IMovementDetector
    {
        public MovementIntegrityDetector() : base("Movement", ViolationCategory.Movement) { }

        public override void Evaluate(in DetectorContext ctx, ViolationCollector collector)
        {
            PlayerState p = ctx.Subject;
            AntiCheatConfig cfg = ctx.Config;
            if (p.Ticks.Count < 2)
                return;

            PlayerTick current = p.Ticks.FromEnd(0);
            PlayerTick previous = p.Ticks.FromEnd(1);
            double dt = current.Timestamp - previous.Timestamp;
            if (dt <= 0)
                return;

            CheckSpeed(ctx, collector, p, cfg);
            CheckTeleport(ctx, collector, current, previous, cfg);
            CheckAirStall(ctx, collector, p, current, cfg);
            CheckClimb(ctx, collector, current, previous, (float)dt, cfg);
            CheckNoclip(ctx, collector, p, cfg);
        }

        private static void CheckSpeed(in DetectorContext ctx, ViolationCollector collector, PlayerState p, AntiCheatConfig cfg)
        {
            // Rolling average smoothing prevents single-tick spikes from flagging.
            if (!p.TryGetSmoothedVelocity(cfg.VelocitySmoothingTicks, out Vector3 v))
                return;
            float speed = v.Length();
            if (speed > cfg.MaxHumanSpeed)
            {
                float overage = (speed - cfg.MaxHumanSpeed) / cfg.MaxHumanSpeed;
                collector.Add(ViolationCode.SpeedHack, ViolationCategory.Movement,
                    Clamp01(overage), ctx.CurrentTick, p.PlayerId, speed);
            }
        }

        private static void CheckTeleport(in DetectorContext ctx, ViolationCollector collector, in PlayerTick current, in PlayerTick previous, AntiCheatConfig cfg)
        {
            float distance = Vector3.Distance(current.Position, previous.Position);
            // High latency opens a lag-compensation window where large corrective jumps are expected.
            bool isLagCompWindow = current.LatencyMs > cfg.LagCompLatencyMs;
            if (distance > cfg.TeleportThreshold && !isLagCompWindow)
            {
                float overage = (distance - cfg.TeleportThreshold) / cfg.TeleportThreshold;
                collector.Add(ViolationCode.Teleport, ViolationCategory.Movement,
                    Clamp01(overage), ctx.CurrentTick, ctx.Subject.PlayerId, distance);
            }
        }

        private static void CheckAirStall(in DetectorContext ctx, ViolationCollector collector, PlayerState p, in PlayerTick current, AntiCheatConfig cfg)
        {
            if (current.IsGrounded)
                return;

            double airtime = p.AirtimeSeconds(current.Timestamp);
            if (airtime < cfg.MaxAirtimeSeconds)
                return;

            if (!p.TryGetInstantVelocity(out Vector3 v))
                return;

            // After sustained airtime a human should be falling (gravity-consistent, negative Y).
            // A non-negative / hovering vertical velocity indicates flight.
            if (v.Y > -cfg.HoverVelocityThreshold)
            {
                float severity = (float)Math.Min(1.0, airtime / (cfg.MaxAirtimeSeconds * 2.0));
                collector.Add(ViolationCode.AirStall, ViolationCategory.Movement,
                    severity, ctx.CurrentTick, p.PlayerId, (float)airtime);
            }
        }

        private static void CheckClimb(in DetectorContext ctx, ViolationCollector collector, in PlayerTick current, in PlayerTick previous, float dt, AntiCheatConfig cfg)
        {
            float leftHandSpeed = Vector3.Distance(current.LeftHandPosition, previous.LeftHandPosition) / dt;
            float rightHandSpeed = Vector3.Distance(current.RightHandPosition, previous.RightHandPosition) / dt;

            EvaluateHand(ctx, collector, leftHandSpeed, current.LeftHandContact, cfg);
            EvaluateHand(ctx, collector, rightHandSpeed, current.RightHandContact, cfg);
        }

        private static void EvaluateHand(in DetectorContext ctx, ViolationCollector collector, float handSpeed, bool contact, AntiCheatConfig cfg)
        {
            // A large hand impulse with no surface contact cannot produce real climbing force.
            if (handSpeed > cfg.ClimbHandSpeedLimit && !contact)
            {
                float overage = (handSpeed - cfg.ClimbHandSpeedLimit) / cfg.ClimbHandSpeedLimit;
                collector.Add(ViolationCode.InvalidClimb, ViolationCategory.Movement,
                    Clamp01(overage), ctx.CurrentTick, ctx.Subject.PlayerId, handSpeed);
            }
        }

        private static void CheckNoclip(in DetectorContext ctx, ViolationCollector collector, PlayerState p, AntiCheatConfig cfg)
        {
            // Persistence across ticks distinguishes a real noclip from a transient
            // collider-overlap glitch that resolves via a valid exit path.
            if (p.InsideColliderStreak >= cfg.NoclipPersistenceTicks)
            {
                float severity = Clamp01((float)p.InsideColliderStreak / (cfg.NoclipPersistenceTicks * 3f));
                collector.Add(ViolationCode.Noclip, ViolationCategory.Movement,
                    severity, ctx.CurrentTick, p.PlayerId, p.InsideColliderStreak);
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
