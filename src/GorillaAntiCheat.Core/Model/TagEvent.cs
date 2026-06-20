using System.Numerics;

namespace GorillaAntiCheat.Core.Model
{
    /// <summary>
    /// A replicated tag interaction observed over the network (e.g. a Photon RPC
    /// claiming "player A tagged player B"). Treated as an untrusted claim that the
    /// tag-integrity detector must corroborate against physics history.
    /// </summary>
    public readonly struct TagEvent
    {
        public readonly int TaggerId;
        public readonly int TargetId;

        /// <summary>Anti-cheat tick index at which the claim was received.</summary>
        public readonly int ReceivedTick;

        /// <summary>Network timestamp embedded in the claim (untrusted), in seconds.</summary>
        public readonly double ClaimedTime;

        /// <summary>Local receive time, in seconds.</summary>
        public readonly double ReceivedTime;

        /// <summary>Tagger hand position carried in the claim, if provided.</summary>
        public readonly Vector3 TaggerHandPosition;

        public TagEvent(
            int taggerId,
            int targetId,
            int receivedTick,
            double claimedTime,
            double receivedTime,
            Vector3 taggerHandPosition)
        {
            TaggerId = taggerId;
            TargetId = targetId;
            ReceivedTick = receivedTick;
            ClaimedTime = claimedTime;
            ReceivedTime = receivedTime;
            TaggerHandPosition = taggerHandPosition;
        }
    }
}
