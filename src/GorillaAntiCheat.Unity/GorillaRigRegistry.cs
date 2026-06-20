using System.Collections.Generic;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// Caches the set of active <see cref="VRRig"/> instances so the per-tick sampler
    /// never calls the expensive <c>FindObjectsOfType</c> on the hot path. The cache is
    /// refreshed on a slow cadence and whenever players join/leave.
    /// </summary>
    public static class GorillaRigRegistry
    {
        private static readonly List<VRRig> _rigs = new List<VRRig>(16);
        private static float _nextRefreshTime;
        private const float RefreshIntervalSeconds = 2f;

        public static IReadOnlyList<VRRig> ActiveRigs => _rigs;

        /// <summary>Forces an immediate rescan (call on player join/leave events).</summary>
        public static void Invalidate() => _nextRefreshTime = 0f;

        /// <summary>Refreshes the cache if the slow cadence elapsed. Call once per tick.</summary>
        public static void MaybeRefresh()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;
            _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;

            _rigs.Clear();
            VRRig[] found = Object.FindObjectsOfType<VRRig>();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    _rigs.Add(found[i]);
            }
        }
    }
}
