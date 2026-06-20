using Photon.Pun;
using Photon.Realtime;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// Resolves an actor number to the display name Gorilla Tag shows on its leaderboard.
    /// GTag publishes each player's (sanitised) name as their Photon <c>NickName</c>, which
    /// the scoreboard reads — so resolving through the current room gives the same name the
    /// leaderboard displays. Falls back to the actor number if the player has gone.
    /// </summary>
    public static class GorillaNames
    {
        public static string Resolve(int actorNumber)
        {
            Room? room = PhotonNetwork.CurrentRoom;
            if (room != null)
            {
                Player player = room.GetPlayer(actorNumber);
                if (player != null && !string.IsNullOrEmpty(player.NickName))
                    return player.NickName;
            }
            return $"Player {actorNumber}";
        }
    }
}
