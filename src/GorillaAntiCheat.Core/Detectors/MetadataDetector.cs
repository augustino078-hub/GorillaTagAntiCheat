using System.Collections.Generic;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Treats Photon custom properties as untrusted metadata. It NEVER infers cheating
    /// from a property on its own — unknown keys only ever become a weak <i>context</i>
    /// signal, and only when real physics/network violations already exist this tick.
    /// This detector must run last so the collector already holds the other signals.
    /// </summary>
    public sealed class MetadataDetector : DetectorBase, IMetadataDetector
    {
        public MetadataDetector() : base("Metadata", ViolationCategory.Metadata) { }

        public override void Evaluate(in DetectorContext ctx, ViolationCollector collector)
        {
            PlayerState p = ctx.Subject;
            if (!p.Metadata.TryGetNewest(out MetadataSnapshot snapshot))
                return;
            if (snapshot.UnknownKeyCount <= 0)
                return;

            // Correlation requirement: only meaningful alongside real violations.
            int realViolations = CountRealViolations(collector.Violations, p.PlayerId);
            if (realViolations <= 0)
                return;

            // Weak, capped context contribution; the category multiplier keeps it small.
            float weight = snapshot.UnknownKeyCount >= 3 ? 1f : snapshot.UnknownKeyCount / 3f;
            collector.Add(ViolationCode.UnexpectedMetadata, ViolationCategory.Metadata,
                weight, ctx.CurrentTick, p.PlayerId, snapshot.UnknownKeyCount);
        }

        private static int CountRealViolations(IReadOnlyList<Violation> violations, int playerId)
        {
            int count = 0;
            for (int i = 0; i < violations.Count; i++)
            {
                Violation v = violations[i];
                if (v.PlayerId == playerId && v.Category != ViolationCategory.Metadata)
                    count++;
            }
            return count;
        }
    }
}
