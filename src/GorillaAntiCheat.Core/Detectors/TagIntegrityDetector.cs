using System;
using System.Collections.Generic;
using System.Numerics;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Corroborates each replicated tag claim against the tagger's and target's physics
    /// history. A tag is only "clean" if a full validation chain holds: plausible range,
    /// plausible arm length, a real collision in-window, no lag-switch, and no desync.
    /// </summary>
    public sealed class TagIntegrityDetector : DetectorBase, ITagIntegrityDetector
    {
        public TagIntegrityDetector() : base("TagIntegrity", ViolationCategory.Tag) { }

        public override void Evaluate(in DetectorContext ctx, ViolationCollector collector)
        {
            IReadOnlyList<TagEvent> events = ctx.World.PendingTagEvents;
            for (int i = 0; i < events.Count; i++)
            {
                TagEvent ev = events[i];
                // Each event is evaluated once, from the tagger's perspective.
                if (ev.TaggerId != ctx.Subject.PlayerId)
                    continue;
                EvaluateTag(ctx, collector, ev);
            }
        }

        private static void EvaluateTag(in DetectorContext ctx, ViolationCollector collector, in TagEvent ev)
        {
            AntiCheatConfig cfg = ctx.Config;
            PlayerState tagger = ctx.Subject;

            if (!tagger.TryGetTickNearTime(ev.ClaimedTime, out PlayerTick taggerTick))
                return;

            // ---- Lag-switch: a latency spike coincident with the interaction. -----
            if (taggerTick.LatencyMs > cfg.LagSwitchLatencyMs)
            {
                float sev = Clamp01((taggerTick.LatencyMs - cfg.LagSwitchLatencyMs) / cfg.LagSwitchLatencyMs);
                collector.Add(ViolationCode.LagSwitchTag, ViolationCategory.Tag, sev, ctx.CurrentTick, tagger.PlayerId, taggerTick.LatencyMs);
            }

            // ---- Desync: claimed-time vs received-time position rollback. ----------
            if (tagger.TryGetTickNearTime(ev.ReceivedTime, out PlayerTick receivedTick))
            {
                float rollback = Vector3.Distance(taggerTick.Position, receivedTick.Position);
                if (rollback > cfg.AllowedRollback)
                {
                    float sev = Clamp01((rollback - cfg.AllowedRollback) / cfg.AllowedRollback);
                    collector.Add(ViolationCode.DesyncTag, ViolationCategory.Tag, sev, ctx.CurrentTick, tagger.PlayerId, rollback);
                }
            }

            // ---- Extended reach: reconstructed arm length. ------------------------
            float armLength = Math.Max(
                Vector3.Distance(taggerTick.HeadPosition, taggerTick.LeftHandPosition),
                Vector3.Distance(taggerTick.HeadPosition, taggerTick.RightHandPosition));
            if (armLength > cfg.MaxHumanArmLength)
            {
                float sev = Clamp01((armLength - cfg.MaxHumanArmLength) / cfg.MaxHumanArmLength);
                collector.Add(ViolationCode.ExtendedReach, ViolationCategory.Tag, sev, ctx.CurrentTick, tagger.PlayerId, armLength);
            }

            // Everything below needs the target's reconstructed body position.
            if (!ctx.World.TryGetPlayer(ev.TargetId, out PlayerState target) ||
                !target.TryGetTickNearTime(ev.ClaimedTime, out PlayerTick targetTick))
            {
                return;
            }

            float handToTarget = Math.Min(
                Vector3.Distance(taggerTick.LeftHandPosition, targetTick.Position),
                Vector3.Distance(taggerTick.RightHandPosition, targetTick.Position));

            // ---- Tag aura: hand nowhere near the target at the tag tick. ----------
            if (handToTarget > cfg.MaxTagRange)
            {
                float sev = Clamp01((handToTarget - cfg.MaxTagRange) / cfg.MaxTagRange);
                collector.Add(ViolationCode.TagAura, ViolationCategory.Tag, sev, ctx.CurrentTick, tagger.PlayerId, handToTarget);
            }

            // ---- Fake tag: no real collision recorded in the confirm window. ------
            bool collisionConfirmed = tagger.HasContactNear(ev.ReceivedTick, cfg.CollisionConfirmWindowTicks);
            if (!collisionConfirmed)
            {
                collector.Add(ViolationCode.FakeTag, ViolationCategory.Tag, 0.8f, ctx.CurrentTick, tagger.PlayerId, 0f);
            }

            // ---- Silent tag: state changed with neither contact nor proximity. ----
            // Stronger than a fake tag: the bodies were never even close in the window.
            bool everInRange = WasEverInRange(tagger, target, ev.ReceivedTick, cfg.CollisionConfirmWindowTicks, cfg.MaxTagRange);
            if (!collisionConfirmed && !everInRange)
            {
                collector.Add(ViolationCode.SilentTag, ViolationCategory.Tag, 0.9f, ctx.CurrentTick, tagger.PlayerId, handToTarget);
            }
        }

        private static bool WasEverInRange(PlayerState tagger, PlayerState target, int centerTick, int windowTicks, float range)
        {
            for (int i = 0; i < tagger.Ticks.Count; i++)
            {
                PlayerTick tt = tagger.Ticks[i];
                if (tt.TickIndex < centerTick - windowTicks)
                    continue;
                if (tt.TickIndex > centerTick + windowTicks)
                    break;
                if (!target.TryGetTickNearTime(tt.Timestamp, out PlayerTick targetTick))
                    continue;
                float d = Math.Min(
                    Vector3.Distance(tt.LeftHandPosition, targetTick.Position),
                    Vector3.Distance(tt.RightHandPosition, targetTick.Position));
                if (d <= range)
                    return true;
            }
            return false;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
