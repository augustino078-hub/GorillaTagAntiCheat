using GorillaAntiCheat.Core.Model;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>Convenience base that wires up the common <see cref="IDetector"/> members.</summary>
    public abstract class DetectorBase : IDetector
    {
        protected DetectorBase(string name, ViolationCategory category)
        {
            Name = name;
            Category = category;
        }

        public string Name { get; }

        public ViolationCategory Category { get; }

        public bool Enabled { get; set; } = true;

        public abstract void Evaluate(in DetectorContext ctx, ViolationCollector collector);
    }
}
