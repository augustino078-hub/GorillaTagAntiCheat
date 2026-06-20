using System.Numerics;

namespace GorillaAntiCheat.Core.Model
{
    /// <summary>
    /// Immutable snapshot of a single player's replicated state for one anti-cheat tick.
    /// This is a value type so historical buffers stay allocation-free.
    /// All data here is treated as <b>untrusted</b>: it is reconstructed from Photon
    /// replication and local physics approximation, never from a trusted server.
    /// </summary>
    public readonly struct PlayerTick
    {
        public readonly int TickIndex;

        /// <summary>Monotonic time of this tick in seconds.</summary>
        public readonly double Timestamp;

        /// <summary>Rig / body root position.</summary>
        public readonly Vector3 Position;

        public readonly Quaternion Rotation;

        public readonly Vector3 HeadPosition;

        public readonly Vector3 LeftHandPosition;

        public readonly Vector3 RightHandPosition;

        /// <summary>Locally approximated grounded state of the rig.</summary>
        public readonly bool IsGrounded;

        /// <summary>Whether the left hand is in contact with a climbable/solid surface.</summary>
        public readonly bool LeftHandContact;

        public readonly bool RightHandContact;

        /// <summary>Locally approximated "rig is inside solid geometry" flag.</summary>
        public readonly bool InsideCollider;

        /// <summary>Observed round-trip latency for this player in milliseconds.</summary>
        public readonly float LatencyMs;

        public PlayerTick(
            int tickIndex,
            double timestamp,
            Vector3 position,
            Quaternion rotation,
            Vector3 headPosition,
            Vector3 leftHandPosition,
            Vector3 rightHandPosition,
            bool isGrounded,
            bool leftHandContact,
            bool rightHandContact,
            bool insideCollider,
            float latencyMs)
        {
            TickIndex = tickIndex;
            Timestamp = timestamp;
            Position = position;
            Rotation = rotation;
            HeadPosition = headPosition;
            LeftHandPosition = leftHandPosition;
            RightHandPosition = rightHandPosition;
            IsGrounded = isGrounded;
            LeftHandContact = leftHandContact;
            RightHandContact = rightHandContact;
            InsideCollider = insideCollider;
            LatencyMs = latencyMs;
        }

        public bool AnyHandContact => LeftHandContact || RightHandContact;
    }
}
