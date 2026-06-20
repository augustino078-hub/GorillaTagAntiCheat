using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// Bridges observable Photon traffic into the anti-cheat manager. It listens to
    /// realtime callbacks only — it never sends anything and never trusts payloads:
    ///  * RPC events feed the network spam/cadence analysis (by sender actor).
    ///  * custom-property updates feed the metadata context detector (keys only).
    ///  * join/leave events keep the rig registry fresh.
    /// </summary>
    public sealed class PhotonObserver : MonoBehaviour, IInRoomCallbacks, IOnEventCallback
    {
        /// <summary>PUN's reserved event code for RPC calls.</summary>
        private const byte RpcEventCode = 200;

        private AntiCheatManager _manager = null!;
        private readonly List<string> _keyScratch = new List<string>(8);

        public void Bind(AntiCheatManager manager) => _manager = manager;

        private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);

        private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == RpcEventCode && _manager != null)
                _manager.ReportRpc(photonEvent.Sender);
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (_manager == null || changedProps == null)
                return;

            _keyScratch.Clear();
            foreach (object key in changedProps.Keys)
                _keyScratch.Add(key?.ToString() ?? string.Empty);

            _manager.ReportMetadata(targetPlayer.ActorNumber, _keyScratch);
        }

        public void OnPlayerEnteredRoom(Player newPlayer) => GorillaRigRegistry.Invalidate();

        public void OnPlayerLeftRoom(Player otherPlayer) => GorillaRigRegistry.Invalidate();

        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }

        public void OnMasterClientSwitched(Player newMasterClient) { }
    }
}
