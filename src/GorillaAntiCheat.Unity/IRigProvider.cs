using System.Collections.Generic;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// A single player's sampled transforms + locally-approximated physics flags for one tick.
    /// Produced by an <see cref="IRigProvider"/> from whatever the game exposes (VRRig, etc.).
    /// </summary>
    public struct RigSample
    {
        public int ActorNumber;
        public Vector3 RigPosition;
        public Quaternion Rotation;
        public Vector3 HeadPosition;
        public Vector3 LeftHandPosition;
        public Vector3 RightHandPosition;
        public bool IsGrounded;
        public bool LeftHandContact;
        public bool RightHandContact;
        public bool InsideCollider;
        public float LatencyMs;
    }

    /// <summary>
    /// Abstraction over how player rigs are discovered and sampled. The default
    /// implementation reads Gorilla Tag's VRRig instances, but tests or other games
    /// can supply their own. This is the single seam that contains game-specific code.
    /// </summary>
    public interface IRigProvider
    {
        /// <summary>Fills <paramref name="buffer"/> with one sample per active remote/local player.</summary>
        void SampleAll(List<RigSample> buffer);
    }
}
