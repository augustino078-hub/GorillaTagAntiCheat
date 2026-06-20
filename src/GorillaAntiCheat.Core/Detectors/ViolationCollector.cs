using System.Collections.Generic;
using GorillaAntiCheat.Core.Model;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Reusable sink detectors write violations into. The backing list is cleared and
    /// reused each tick so detectors never allocate while reporting signals.
    /// </summary>
    public sealed class ViolationCollector
    {
        private readonly List<Violation> _violations = new List<Violation>(32);

        public IReadOnlyList<Violation> Violations => _violations;

        public int Count => _violations.Count;

        public void Add(in Violation violation) => _violations.Add(violation);

        public void Add(string code, ViolationCategory category, float weight, int tick, int playerId, float magnitude = 0f)
            => _violations.Add(new Violation(code, category, weight, tick, playerId, magnitude));

        public void Reset() => _violations.Clear();
    }
}
