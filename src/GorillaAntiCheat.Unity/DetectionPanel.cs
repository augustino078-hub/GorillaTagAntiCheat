using System;
using System.Collections.Generic;
using GorillaAntiCheat.Core;
using GorillaAntiCheat.Core.Scoring;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// In-game detections UI. Lists every tracked player with their current escalation
    /// level, suspicion score, and the per-category breakdown driving it, so you can see
    /// at a glance who is being flagged and why. Read-only — it never influences scoring.
    /// Toggle it with the configured key (Left Alt by default).
    /// </summary>
    public sealed class DetectionPanel
    {
        private readonly AntiCheatEngine _engine;
        private readonly Func<int, string> _resolveName;
        private GUIStyle? _title;
        private GUIStyle? _player;
        private GUIStyle? _detail;
        private GUIStyle? _empty;
        private Vector2 _scroll;

        public DetectionPanel(AntiCheatEngine engine, Func<int, string>? resolveName = null)
        {
            _engine = engine;
            _resolveName = resolveName ?? (actor => $"Player {actor}");
        }

        public void Draw()
        {
            EnsureStyles();

            const float width = 420f;
            const float height = 380f;
            var area = new Rect(16, 16, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 10, width - 24, height - 20));

            int flagged = CountFlagged();
            GUILayout.Label($"ANTI-CHEAT DETECTIONS   ({flagged} flagged)", _title);

            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_engine.Scoring.Scores.Count == 0)
            {
                GUILayout.Label("No players tracked.", _empty);
            }
            else
            {
                foreach (KeyValuePair<int, SuspicionScore> kv in _engine.Scoring.Scores)
                    DrawPlayer(kv.Key, kv.Value);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawPlayer(int actor, SuspicionScore s)
        {
            GUI.color = ColorFor(s.Level);
            GUILayout.Label($"{_resolveName(actor)}   —   {LevelText(s.Level)}   ({s.Total:F0})", _player);
            GUI.color = Color.white;
            GUILayout.Label(
                $"    movement {s.Movement:F0}    tag {s.Tag:F0}    network {s.Network:F0}    metadata {s.Metadata:F0}",
                _detail);
        }

        private int CountFlagged()
        {
            int n = 0;
            foreach (KeyValuePair<int, SuspicionScore> kv in _engine.Scoring.Scores)
                if (kv.Value.Level >= EscalationLevel.Suspicious)
                    n++;
            return n;
        }

        private static string LevelText(EscalationLevel level) => level switch
        {
            EscalationLevel.StrongWarning => "STRONG WARNING",
            EscalationLevel.Suspicious => "SUSPICIOUS",
            EscalationLevel.Highlight => "watch",
            _ => "clean",
        };

        private static Color ColorFor(EscalationLevel level) => level switch
        {
            EscalationLevel.StrongWarning => Color.red,
            EscalationLevel.Suspicious => new Color(1f, 0.55f, 0f),
            EscalationLevel.Highlight => Color.yellow,
            _ => new Color(0.6f, 1f, 0.6f),
        };

        private void EnsureStyles()
        {
            _title ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };
            _player ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            _detail ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _empty ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic };
        }
    }
}
