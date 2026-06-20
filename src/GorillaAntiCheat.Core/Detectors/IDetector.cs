using GorillaAntiCheat.Core.Model;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Base contract for every detector. Detectors are pure analyzers: they read
    /// history and emit weighted <see cref="Violation"/> signals into the collector.
    /// They must never ban, kick, or otherwise act — only contribute to the score.
    /// </summary>
    public interface IDetector
    {
        string Name { get; }

        ViolationCategory Category { get; }

        /// <summary>Detectors are individually toggleable to support a modular plugin setup.</summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Evaluate the subject for the current tick and append any signals to
        /// <paramref name="collector"/>. Implementations must be allocation-free and
        /// avoid LINQ on this hot path.
        /// </summary>
        void Evaluate(in DetectorContext ctx, ViolationCollector collector);
    }

    /// <summary>Detects movement/locomotion exploits (speed, teleport, flight, climb, noclip).</summary>
    public interface IMovementDetector : IDetector { }

    /// <summary>Detects tag-interaction exploits (aura, reach, fake/silent tags, lag-switch, desync).</summary>
    public interface ITagIntegrityDetector : IDetector { }

    /// <summary>Detects observable Photon network abuse (RPC spam, burst-lag abuse, desync).</summary>
    public interface INetworkDetector : IDetector { }

    /// <summary>Provides context-only signals from untrusted Photon custom properties.</summary>
    public interface IMetadataDetector : IDetector { }
}
