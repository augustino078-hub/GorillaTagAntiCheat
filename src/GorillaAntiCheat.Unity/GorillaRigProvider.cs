using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// Gorilla Tag specific <see cref="IRigProvider"/>. Reads each networked player's
    /// VRRig transforms (head + both hands) and approximates grounded / contact / noclip
    /// state locally, since the server provides no authority.
    ///
    /// NOTE: VRRig member names have drifted across Gorilla Tag versions. The accessors
    /// are centralised in <see cref="TrySample"/> so they can be adjusted in exactly one
    /// place when the game updates. Everything downstream consumes the neutral
    /// <see cref="RigSample"/> struct.
    /// </summary>
    public sealed class GorillaRigProvider : IRigProvider
    {
        private readonly float _groundRayLength;
        private readonly LayerMask _solidMask;

        public GorillaRigProvider(LayerMask solidMask, float groundRayLength = 0.25f)
        {
            _solidMask = solidMask;
            _groundRayLength = groundRayLength;
        }

        public void SampleAll(List<RigSample> buffer)
        {
            buffer.Clear();
            // VRRig.AllRigsList in modern GT; fall back to scanning if unavailable.
            IReadOnlyList<VRRig> rigs = GorillaRigRegistry.ActiveRigs;
            for (int i = 0; i < rigs.Count; i++)
            {
                if (TrySample(rigs[i], out RigSample sample))
                    buffer.Add(sample);
            }
        }

        private bool TrySample(VRRig rig, out RigSample sample)
        {
            sample = default;
            if (rig == null)
                return false;

            PhotonView view = rig.photonView;
            if (view == null || view.Owner == null)
                return false; // skip rigs without a network owner

            Transform head = rig.headMesh != null ? rig.headMesh.transform : rig.transform;
            Transform left = rig.leftHandTransform != null ? rig.leftHandTransform : rig.transform;
            Transform right = rig.rightHandTransform != null ? rig.rightHandTransform : rig.transform;
            Vector3 rigPos = rig.transform.position;

            sample.ActorNumber = view.Owner.ActorNumber;
            sample.RigPosition = rigPos;
            sample.Rotation = rig.transform.rotation;
            sample.HeadPosition = head.position;
            sample.LeftHandPosition = left.position;
            sample.RightHandPosition = right.position;
            sample.IsGrounded = Physics.Raycast(rigPos + Vector3.up * 0.1f, Vector3.down, _groundRayLength + 0.1f, _solidMask);
            sample.LeftHandContact = IsTouchingSolid(left.position);
            sample.RightHandContact = IsTouchingSolid(right.position);
            sample.InsideCollider = Physics.CheckSphere(head.position, 0.08f, _solidMask);
            sample.LatencyMs = EstimateLatencyMs(view);
            return true;
        }

        private bool IsTouchingSolid(Vector3 worldPos)
            => Physics.CheckSphere(worldPos, 0.12f, _solidMask);

        private static float EstimateLatencyMs(PhotonView view)
        {
            // Photon only exposes the local client's round-trip time directly. For the
            // local player this is exact; for remotes we use it as a shared-link proxy,
            // which the engine then normalises against each player's own EMA baseline.
            return PhotonNetwork.GetPing();
        }
    }
}
