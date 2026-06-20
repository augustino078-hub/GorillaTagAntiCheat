using System.Collections.Generic;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Read-only view of the shared anti-cheat world handed to detectors each tick.
    /// Lets a detector inspect other players (e.g. a tag target's body position)
    /// without granting any mutation rights.
    /// </summary>
    public interface IAntiCheatWorld
    {
        bool TryGetPlayer(int playerId, out PlayerState state);

        /// <summary>Tag claims received since the previous tick, awaiting corroboration.</summary>
        IReadOnlyList<TagEvent> PendingTagEvents { get; }
    }
}
