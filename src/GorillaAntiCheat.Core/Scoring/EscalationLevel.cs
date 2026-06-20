namespace GorillaAntiCheat.Core.Scoring
{
    /// <summary>
    /// Coarse action tiers derived from the suspicion score. The engine only ever
    /// reaches <see cref="StrongWarning"/> — it logs a replay and warns; it never bans.
    /// </summary>
    public enum EscalationLevel
    {
        /// <summary>0–HighlightThreshold: ignore.</summary>
        Ignore = 0,

        /// <summary>HighlightThreshold–SuspiciousThreshold: highlight in overlay.</summary>
        Highlight = 1,

        /// <summary>SuspiciousThreshold–StrongWarningThreshold: flag as suspicious.</summary>
        Suspicious = 2,

        /// <summary>StrongWarningThreshold+: strong warning + replay log.</summary>
        StrongWarning = 3,
    }
}
