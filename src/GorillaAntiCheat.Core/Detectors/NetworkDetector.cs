using System;
using System.Collections.Generic;
using System.Numerics;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Analyzes only observable Photon behaviour: RPC cadence, latency dynamics, and
    /// the smoothness of replicated state. Never inspects payloads — purely structural.
    /// </summary>
    public sealed class NetworkDetector : DetectorBase, INetworkDetector
    {
        public NetworkDetector() : base("Network", ViolationCategory.Network) { }

        public override void Evaluate(in DetectorContext ctx, ViolationCollector collector)
        {
            PlayerState p = ctx.Subject;
            AntiCheatConfig cfg = ctx.Config;

            CheckRpcSpam(ctx, collector, p, cfg);

            bool latencySpike = IsLatencySpike(p, cfg, out float currentLatency);
            CheckBurstLagAbuse(ctx, collector, p, cfg, latencySpike, currentLatency);
            CheckStateConsistency(ctx, collector, p, cfg);
        }

        private static void CheckRpcSpam(in DetectorContext ctx, ViolationCollector collector, PlayerState p, AntiCheatConfig cfg)
        {
            int rpcPerSecond = p.RpcCountWithin(ctx.CurrentTime, 1.0);
            if (rpcPerSecond > cfg.RpcPerSecondThreshold)
            {
                float sev = Clamp01((rpcPerSecond - cfg.RpcPerSecondThreshold) / (float)cfg.RpcPerSecondThreshold);
                collector.Add(ViolationCode.RpcSpam, ViolationCategory.Network, sev, ctx.CurrentTick, p.PlayerId, rpcPerSecond);
            }
        }

        private static bool IsLatencySpike(PlayerState p, AntiCheatConfig cfg, out float currentLatency)
        {
            currentLatency = 0f;
            if (!p.TryGetCurrent(out PlayerTick current))
                return false;
            currentLatency = current.LatencyMs;
            float baseline = p.LatencyBaselineMs;
            if (baseline <= 0f)
                return false;
            return currentLatency > baseline * cfg.LatencySpikeMultiplier
                && currentLatency - baseline > cfg.LatencySpikeAbsoluteMs;
        }

        private static void CheckBurstLagAbuse(in DetectorContext ctx, ViolationCollector collector, PlayerState p, AntiCheatConfig cfg, bool latencySpike, float currentLatency)
        {
            if (!latencySpike)
                return;

            // High-impact event == a tag interaction landing during the spike.
            if (HasHighImpactEvent(ctx, p.PlayerId))
            {
                float sev = Clamp01((currentLatency - p.LatencyBaselineMs) / Math.Max(1f, p.LatencyBaselineMs));
                collector.Add(ViolationCode.BurstLagAbuse, ViolationCategory.Network, Clamp01(0.5f + sev), ctx.CurrentTick, p.PlayerId, currentLatency);
            }
        }

        private static bool HasHighImpactEvent(in DetectorContext ctx, int playerId)
        {
            IReadOnlyList<TagEvent> events = ctx.World.PendingTagEvents;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].TaggerId == playerId || events[i].TargetId == playerId)
                    return true;
            }
            return false;
        }

        private static void CheckStateConsistency(in DetectorContext ctx, ViolationCollector collector, PlayerState p, AntiCheatConfig cfg)
        {
            if (p.Ticks.Count < 3)
                return;

            PlayerTick t0 = p.Ticks.FromEnd(2);
            PlayerTick t1 = p.Ticks.FromEnd(1);
            PlayerTick t2 = p.Ticks.FromEnd(0);

            // Impossible rotation: near-instant flips between ticks.
            float rotDelta = QuaternionAngleDegrees(t1.Rotation, t2.Rotation);
            if (rotDelta > cfg.ImpossibleRotationDegrees)
            {
                collector.Add(ViolationCode.ImpossibleTransition, ViolationCategory.Network,
                    Clamp01(rotDelta / 180f), ctx.CurrentTick, p.PlayerId, rotDelta);
            }

            // State desync: the replicated path has no plausible interpolation. We compare
            // the actual position with a constant-velocity prediction from the prior two ticks.
            Vector3 predicted = t1.Position + (t1.Position - t0.Position);
            float error = Vector3.Distance(predicted, t2.Position);
            // Tolerance scales with how far the player legitimately moved this tick.
            float legitStep = Vector3.Distance(t1.Position, t0.Position);
            float tolerance = cfg.TeleportThreshold * 0.5f + legitStep;
            if (error > tolerance)
            {
                collector.Add(ViolationCode.StateDesync, ViolationCategory.Network,
                    Clamp01((error - tolerance) / cfg.TeleportThreshold), ctx.CurrentTick, p.PlayerId, error);
            }
        }

        private static float QuaternionAngleDegrees(Quaternion a, Quaternion b)
        {
            float dot = Math.Abs(Quaternion.Dot(a, b));
            if (dot > 1f) dot = 1f;
            return (float)(2.0 * Math.Acos(dot) * (180.0 / Math.PI));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
