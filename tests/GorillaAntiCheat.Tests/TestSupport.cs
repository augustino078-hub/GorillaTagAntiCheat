using System.Collections.Generic;
using System.Numerics;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Detectors;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Tests
{
    /// <summary>Minimal in-memory world so individual detectors can be tested in isolation.</summary>
    internal sealed class TestWorld : IAntiCheatWorld
    {
        private readonly Dictionary<int, PlayerState> _players = new();
        private readonly List<TagEvent> _tagEvents = new();

        public AntiCheatConfig Config { get; } = new AntiCheatConfig();

        public PlayerState AddPlayer(int id, double joinTime = -100.0)
        {
            var state = new PlayerState(id, joinTime, 0, Config);
            _players[id] = state;
            return state;
        }

        public void AddTagEvent(in TagEvent ev) => _tagEvents.Add(ev);

        public bool TryGetPlayer(int playerId, out PlayerState state) => _players.TryGetValue(playerId, out state!);

        public IReadOnlyList<TagEvent> PendingTagEvents => _tagEvents;
    }

    /// <summary>Builds <see cref="PlayerTick"/> values with sensible "normal player" defaults.</summary>
    internal static class Ticks
    {
        public static PlayerTick Make(
            int tickIndex,
            double time,
            Vector3 position,
            Vector3? head = null,
            Vector3? leftHand = null,
            Vector3? rightHand = null,
            bool grounded = true,
            bool leftContact = false,
            bool rightContact = false,
            bool insideCollider = false,
            float latencyMs = 50f,
            Quaternion? rotation = null)
        {
            Vector3 h = head ?? position + new Vector3(0f, 1.6f, 0f);
            Vector3 lh = leftHand ?? position + new Vector3(-0.3f, 1.0f, 0.2f);
            Vector3 rh = rightHand ?? position + new Vector3(0.3f, 1.0f, 0.2f);
            return new PlayerTick(tickIndex, time, position, rotation ?? Quaternion.Identity,
                h, lh, rh, grounded, leftContact, rightContact, insideCollider, latencyMs);
        }
    }

    internal static class CollectorExtensions
    {
        public static bool Has(this ViolationCollector c, string code)
        {
            for (int i = 0; i < c.Violations.Count; i++)
                if (c.Violations[i].Code == code)
                    return true;
            return false;
        }
    }
}
