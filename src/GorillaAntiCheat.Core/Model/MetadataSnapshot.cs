namespace GorillaAntiCheat.Core.Model
{
    /// <summary>
    /// Compact, allocation-free record of a Photon custom-property change.
    /// We deliberately do <b>not</b> retain the raw property values: properties are
    /// untrusted metadata, so we only track structural facts (how many keys fell
    /// outside the safe whitelist) that may later corroborate real physics signals.
    /// </summary>
    public readonly struct MetadataSnapshot
    {
        public readonly int Tick;
        public readonly double Timestamp;

        /// <summary>Number of keys present that are not on the safe whitelist.</summary>
        public readonly int UnknownKeyCount;

        /// <summary>Total number of keys in this property set.</summary>
        public readonly int TotalKeyCount;

        public MetadataSnapshot(int tick, double timestamp, int unknownKeyCount, int totalKeyCount)
        {
            Tick = tick;
            Timestamp = timestamp;
            UnknownKeyCount = unknownKeyCount;
            TotalKeyCount = totalKeyCount;
        }
    }
}
