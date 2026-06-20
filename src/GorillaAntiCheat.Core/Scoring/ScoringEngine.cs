using System.Collections.Generic;
using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.Model;

namespace GorillaAntiCheat.Core.Scoring
{
    /// <summary>
    /// Weighted anomaly engine. Applies per-tick decay, folds the tick's weighted
    /// signals into per-category subscores, and recomputes the escalation level.
    /// Decay-first ordering guarantees a lone event cannot survive as a false positive.
    /// </summary>
    public sealed class ScoringEngine
    {
        private readonly AntiCheatConfig _config;
        private readonly Dictionary<int, SuspicionScore> _scores = new Dictionary<int, SuspicionScore>();

        public ScoringEngine(AntiCheatConfig config) => _config = config;

        public IReadOnlyDictionary<int, SuspicionScore> Scores => _scores;

        public SuspicionScore GetOrCreate(int playerId)
        {
            if (!_scores.TryGetValue(playerId, out SuspicionScore score))
            {
                score = new SuspicionScore(playerId);
                _scores[playerId] = score;
            }
            return score;
        }

        public bool TryGet(int playerId, out SuspicionScore score) => _scores.TryGetValue(playerId, out score!);

        public void Remove(int playerId) => _scores.Remove(playerId);

        /// <summary>
        /// Decays a player's score for the elapsed tick and applies this tick's signals.
        /// When <paramref name="suppress"/> is true (e.g. join grace) decay still runs but
        /// new signals are ignored.
        /// </summary>
        public void Apply(int playerId, IReadOnlyList<Violation> violations, double dtSeconds, bool suppress)
        {
            SuspicionScore score = GetOrCreate(playerId);
            score.Decay(dtSeconds, _config);

            if (!suppress)
            {
                for (int i = 0; i < violations.Count; i++)
                {
                    Violation v = violations[i];
                    if (v.PlayerId == playerId)
                        score.AddSignal(v, _config);
                }
            }

            score.Recompute(_config);
        }
    }
}
