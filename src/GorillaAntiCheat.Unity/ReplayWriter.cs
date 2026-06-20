using System.Globalization;
using System.IO;
using System.Text;
using GorillaAntiCheat.Core.Model;
using GorillaAntiCheat.Core.Replay;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// Persists a captured <see cref="ReplaySegment"/> to disk as CSV for human review.
    /// Replays are evidence for moderators, never an automated punishment trigger.
    /// </summary>
    public static class ReplayWriter
    {
        public static string OutputDirectory { get; set; } =
            Path.Combine(Application.persistentDataPath, "AntiCheatReplays");

        public static string Save(ReplaySegment segment)
        {
            Directory.CreateDirectory(OutputDirectory);
            string file = Path.Combine(OutputDirectory,
                $"player{segment.PlayerId}_{System.DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();
            sb.AppendLine($"# player={segment.PlayerId} score={segment.ScoreAtCapture:F1} reason={segment.Reason}");
            sb.AppendLine("tick,time,posX,posY,posZ,grounded,lContact,rContact,inside,latencyMs");
            for (int i = 0; i < segment.Ticks.Length; i++)
            {
                PlayerTick t = segment.Ticks[i];
                sb.Append(t.TickIndex).Append(',')
                  .Append(F(t.Timestamp)).Append(',')
                  .Append(F(t.Position.X)).Append(',')
                  .Append(F(t.Position.Y)).Append(',')
                  .Append(F(t.Position.Z)).Append(',')
                  .Append(t.IsGrounded ? 1 : 0).Append(',')
                  .Append(t.LeftHandContact ? 1 : 0).Append(',')
                  .Append(t.RightHandContact ? 1 : 0).Append(',')
                  .Append(t.InsideCollider ? 1 : 0).Append(',')
                  .Append(F(t.LatencyMs)).AppendLine();
            }

            File.WriteAllText(file, sb.ToString());
            return file;
        }

        private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
