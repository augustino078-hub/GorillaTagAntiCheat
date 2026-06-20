namespace GorillaAntiCheat.Core.Model
{
    /// <summary>High-level family a violation belongs to. Drives weighting in the scoring engine.</summary>
    public enum ViolationCategory
    {
        Movement = 0,
        Tag = 1,
        Network = 2,
        Metadata = 3,
    }

    /// <summary>
    /// Stable string identifiers for every signal the detectors can raise.
    /// These are intentionally <b>signals</b>, never verdicts: a single violation
    /// must never be treated as proof of cheating.
    /// </summary>
    public static class ViolationCode
    {
        // Movement
        public const string SpeedHack = "SPEED_HACK";
        public const string Teleport = "TELEPORT";
        public const string AirStall = "AIR_STALL";
        public const string InvalidClimb = "INVALID_CLIMB";
        public const string Noclip = "NOCLIP";

        // Tag integrity
        public const string TagAura = "TAG_AURA";
        public const string ExtendedReach = "EXTENDED_REACH";
        public const string FakeTag = "FAKE_TAG";
        public const string SilentTag = "SILENT_TAG";
        public const string LagSwitchTag = "LAG_SWITCH_TAG";
        public const string DesyncTag = "DESYNC_TAG";

        // Network
        public const string RpcSpam = "RPC_SPAM";
        public const string BurstLagAbuse = "BURST_LAG_ABUSE";
        public const string StateDesync = "STATE_DESYNC";
        public const string ImpossibleTransition = "IMPOSSIBLE_TRANSITION";

        // Metadata (context only)
        public const string UnexpectedMetadata = "UNEXPECTED_METADATA";
    }

    /// <summary>
    /// A single structured detection signal produced by a detector for one tick.
    /// Detectors only ever <i>contribute</i> weighted violations; they never ban.
    /// </summary>
    public readonly struct Violation
    {
        public readonly string Code;
        public readonly ViolationCategory Category;

        /// <summary>Severity weight in [0,1]-ish range, later scaled by category multiplier.</summary>
        public readonly float Weight;

        /// <summary>Tick at which the signal was raised.</summary>
        public readonly int Tick;

        /// <summary>Player the signal is attributed to.</summary>
        public readonly int PlayerId;

        /// <summary>Optional measured magnitude that triggered the signal (for overlay/replay).</summary>
        public readonly float Magnitude;

        public Violation(string code, ViolationCategory category, float weight, int tick, int playerId, float magnitude)
        {
            Code = code;
            Category = category;
            Weight = weight;
            Tick = tick;
            PlayerId = playerId;
            Magnitude = magnitude;
        }
    }
}
