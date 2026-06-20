using System.Collections.Generic;

namespace GorillaAntiCheat.Core.Config
{
    /// <summary>
    /// Central, fully tunable configuration for the anti-cheat engine.
    /// Every threshold is calibrated for Gorilla Tag's VR locomotion and is meant to
    /// be overridable from the mod's config file so admins can tune false-positive
    /// rates per lobby without recompiling.
    /// </summary>
    public sealed class AntiCheatConfig
    {
        // ---- Tick / buffer sizing -------------------------------------------------
        /// <summary>Anti-cheat ticks per second (independent of render frame rate).</summary>
        public int TickRateHz { get; set; } = 20;

        /// <summary>Seconds of per-player history retained in ring buffers.</summary>
        public float HistorySeconds { get; set; } = 10f;

        /// <summary>Seconds of replay retained for post-incident review.</summary>
        public float ReplaySeconds { get; set; } = 30f;

        // ---- Join grace -----------------------------------------------------------
        /// <summary>Seconds after a player joins during which scores are suppressed (spawn/teleport-in noise).</summary>
        public float JoinGraceSeconds { get; set; } = 3f;

        // ---- Movement -------------------------------------------------------------
        /// <summary>Calibrated ceiling for sustained VR locomotion speed (m/s).</summary>
        public float MaxHumanSpeed { get; set; } = 12f;

        /// <summary>Number of ticks used to smooth instantaneous velocity (reduces spike false positives).</summary>
        public int VelocitySmoothingTicks { get; set; } = 4;

        /// <summary>Single-tick displacement (m) above which we suspect a teleport.</summary>
        public float TeleportThreshold { get; set; } = 4f;

        /// <summary>Latency (ms) under which a large jump is more suspicious (above it, blame lag-comp).</summary>
        public float LagCompLatencyMs { get; set; } = 120f;

        /// <summary>Upward velocity (m/s) that counts as "hovering" while airborne.</summary>
        public float HoverVelocityThreshold { get; set; } = 0.5f;

        /// <summary>Seconds of continuous airtime without gravity-consistent decay before flagging.</summary>
        public float MaxAirtimeSeconds { get; set; } = 1.5f;

        /// <summary>Gravity magnitude (m/s^2) used to validate airborne velocity decay.</summary>
        public float Gravity { get; set; } = 9.81f;

        /// <summary>Hand speed (m/s) that is implausible as a climbing impulse without surface contact.</summary>
        public float ClimbHandSpeedLimit { get; set; } = 8f;

        /// <summary>Consecutive ticks the rig may register inside geometry before NOCLIP is raised.</summary>
        public int NoclipPersistenceTicks { get; set; } = 3;

        // ---- Tag integrity --------------------------------------------------------
        /// <summary>Max plausible distance (m) from tagger hand to target body at the tag tick.</summary>
        public float MaxTagRange { get; set; } = 0.6f;

        /// <summary>Max plausible reconstructed arm length head→hand (m).</summary>
        public float MaxHumanArmLength { get; set; } = 1.1f;

        /// <summary>Ticks around a tag claim in which a real collision must exist to be considered valid.</summary>
        public int CollisionConfirmWindowTicks { get; set; } = 3;

        /// <summary>Latency spike (ms) near a tag that suggests a lag-switch.</summary>
        public float LagSwitchLatencyMs { get; set; } = 250f;

        /// <summary>Allowed positional rollback (m) between event time and historical buffer before DESYNC.</summary>
        public float AllowedRollback { get; set; } = 2.5f;

        // ---- Network --------------------------------------------------------------
        /// <summary>RPCs/sec from a single player above which we suspect spam.</summary>
        public int RpcPerSecondThreshold { get; set; } = 40;

        /// <summary>Multiplier over a player's latency baseline that counts as a spike.</summary>
        public float LatencySpikeMultiplier { get; set; } = 2.5f;

        /// <summary>Absolute latency delta (ms) over baseline that counts as a spike.</summary>
        public float LatencySpikeAbsoluteMs { get; set; } = 150f;

        /// <summary>Rotation change (degrees) in a single tick that is physically implausible.</summary>
        public float ImpossibleRotationDegrees { get; set; } = 170f;

        // ---- Metadata -------------------------------------------------------------
        /// <summary>Photon custom-property keys considered benign cosmetic/identity data.</summary>
        public HashSet<string> AllowedMetadataKeys { get; } = new HashSet<string>
        {
            "cosmetics", "color", "hat", "nameTag", "platform", "didTutorial", "level"
        };

        // ---- Scoring --------------------------------------------------------------
        public float MovementWeight { get; set; } = 1.0f;
        public float TagWeight { get; set; } = 1.5f;
        public float NetworkWeight { get; set; } = 1.2f;
        public float MetadataWeight { get; set; } = 0.3f;

        /// <summary>Base points added to a category subscore per unit of signal weight.</summary>
        public float PointsPerSignal { get; set; } = 12f;

        /// <summary>Score subtracted per second of clean play.</summary>
        public float DecayPerSecond { get; set; } = 4f;

        /// <summary>Upper clamp for accumulated suspicion score.</summary>
        public float MaxScore { get; set; } = 120f;

        public float HighlightThreshold { get; set; } = 20f;
        public float SuspiciousThreshold { get; set; } = 50f;
        public float StrongWarningThreshold { get; set; } = 80f;

        public int TicksPerSecond => TickRateHz;

        public int HistoryTickCapacity => System.Math.Max(2, (int)(HistorySeconds * TickRateHz));

        public int ReplayTickCapacity => System.Math.Max(2, (int)(ReplaySeconds * TickRateHz));

        public double TickDeltaSeconds => 1.0 / TickRateHz;
    }
}
