using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;

namespace GorillaAntiCheat.Core.Scoring
{
    /// <summary>
    /// Per-player accumulated suspicion broken down by category so the overlay can show
    /// exactly which signal families are driving the score. No single tick can push the
    /// total to a verdict — the multi-signal weighting and decay enforce that.
    /// </summary>
    public sealed class SuspicionScore
    {
        public int PlayerId { get; }

        public float Movement { get; private set; }
        public float Tag { get; private set; }
        public float Network { get; private set; }
        public float Metadata { get; private set; }

        public SuspicionScore(int playerId) => PlayerId = playerId;

        public float Total { get; private set; }

        public EscalationLevel Level { get; private set; } = EscalationLevel.Ignore;

        internal void Decay(double dtSeconds, AntiCheatConfig cfg)
        {
            float amount = (float)(cfg.DecayPerSecond * dtSeconds);
            Movement = Sub(Movement, amount);
            Tag = Sub(Tag, amount);
            Network = Sub(Network, amount);
            Metadata = Sub(Metadata, amount);
        }

        internal void AddSignal(in Violation v, AntiCheatConfig cfg)
        {
            float points = v.Weight * cfg.PointsPerSignal;
            switch (v.Category)
            {
                case ViolationCategory.Movement: Movement = Clamp(Movement + points, cfg); break;
                case ViolationCategory.Tag: Tag = Clamp(Tag + points, cfg); break;
                case ViolationCategory.Network: Network = Clamp(Network + points, cfg); break;
                case ViolationCategory.Metadata: Metadata = Clamp(Metadata + points, cfg); break;
            }
        }

        internal void Recompute(AntiCheatConfig cfg)
        {
            float total =
                Movement * cfg.MovementWeight +
                Tag * cfg.TagWeight +
                Network * cfg.NetworkWeight +
                Metadata * cfg.MetadataWeight;
            if (total < 0f) total = 0f;
            if (total > cfg.MaxScore) total = cfg.MaxScore;
            Total = total;
            Level = Classify(total, cfg);
        }

        private static EscalationLevel Classify(float total, AntiCheatConfig cfg)
        {
            if (total >= cfg.StrongWarningThreshold) return EscalationLevel.StrongWarning;
            if (total >= cfg.SuspiciousThreshold) return EscalationLevel.Suspicious;
            if (total >= cfg.HighlightThreshold) return EscalationLevel.Highlight;
            return EscalationLevel.Ignore;
        }

        private static float Sub(float value, float amount) => value - amount < 0f ? 0f : value - amount;

        private static float Clamp(float value, AntiCheatConfig cfg) => value > cfg.MaxScore ? cfg.MaxScore : value;
    }
}
