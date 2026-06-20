using System.Collections.Generic;
using GorillaAntiCheat.Core;
using GorillaAntiCheat.Core.Scoring;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// IMGUI debug overlay: live suspicion score, per-category breakdown, and a tick
    /// viewer. Read-only — it renders engine state and never influences detection.
    /// </summary>
    public sealed class DebugOverlay
    {
        private readonly AntiCheatEngine _engine;
        private GUIStyle? _header;
        private GUIStyle? _row;

        public DebugOverlay(AntiCheatEngine engine) => _engine = engine;

        public void Draw()
        {
            EnsureStyles();

            const float width = 360f;
            float height = 70f + _engine.Scoring.Scores.Count * 58f;
            GUILayout.BeginArea(new Rect(12, 12, width, height), GUI.skin.box);

            GUILayout.Label($"Anti-Cheat  |  tick {_engine.CurrentTick}", _header);

            foreach (KeyValuePair<int, SuspicionScore> kv in _engine.Scoring.Scores)
            {
                SuspicionScore s = kv.Value;
                GUI.color = ColorFor(s.Level);
                GUILayout.Label($"Player {kv.Key}   {s.Level}   score {s.Total:F0}", _row);
                GUI.color = Color.white;
                GUILayout.Label(
                    $"   move {s.Movement:F0}   tag {s.Tag:F0}   net {s.Network:F0}   meta {s.Metadata:F0}",
                    _row);
            }

            GUILayout.EndArea();
        }

        private static Color ColorFor(EscalationLevel level) => level switch
        {
            EscalationLevel.StrongWarning => Color.red,
            EscalationLevel.Suspicious => new Color(1f, 0.55f, 0f),
            EscalationLevel.Highlight => Color.yellow,
            _ => Color.green,
        };

        private void EnsureStyles()
        {
            _header ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            _row ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };
        }
    }
}
