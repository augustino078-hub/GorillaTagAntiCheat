using BepInEx;
using BepInEx.Configuration;
using GorillaAntiCheat.Core.Config;
using UnityEngine;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// BepInEx entrypoint. Builds the tunable <see cref="AntiCheatConfig"/> from the mod
    /// config file, spawns a persistent <see cref="AntiCheatManager"/>, and wires the
    /// Photon observer. All detectors are individually toggleable from config.
    /// </summary>
    [BepInPlugin(Guid, "Gorilla Tag Anti-Cheat", "0.1.0")]
    public sealed class AntiCheatPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.augustino.gorillatag.anticheat";

        public static AntiCheatManager? Manager { get; private set; }

        private void Awake()
        {
            AntiCheatConfig config = BuildConfig();

            var host = new GameObject("GorillaAntiCheat");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;

            AntiCheatManager manager = host.AddComponent<AntiCheatManager>();
            manager.Configure(config);

            ApplyDetectorToggles(manager);

            PhotonObserver observer = host.AddComponent<PhotonObserver>();
            observer.Bind(manager);

            Manager = manager;
            Logger.LogInfo($"{Guid} initialised at {config.TickRateHz} Hz.");
        }

        private AntiCheatConfig BuildConfig()
        {
            var cfg = new AntiCheatConfig();

            cfg.TickRateHz = Bind("Core", "TickRateHz", cfg.TickRateHz, "Anti-cheat ticks per second.");
            cfg.JoinGraceSeconds = Bind("Core", "JoinGraceSeconds", cfg.JoinGraceSeconds, "Grace window after a player joins.");

            cfg.MaxHumanSpeed = Bind("Movement", "MaxHumanSpeed", cfg.MaxHumanSpeed, "VR locomotion speed ceiling (m/s).");
            cfg.TeleportThreshold = Bind("Movement", "TeleportThreshold", cfg.TeleportThreshold, "Single-tick teleport distance (m).");

            cfg.MaxTagRange = Bind("Tag", "MaxTagRange", cfg.MaxTagRange, "Max hand-to-target distance at a tag (m).");
            cfg.MaxHumanArmLength = Bind("Tag", "MaxArmLength", cfg.MaxHumanArmLength, "Max reconstructed arm length (m).");

            cfg.RpcPerSecondThreshold = Bind("Network", "RpcPerSecond", cfg.RpcPerSecondThreshold, "RPC/sec spam threshold.");

            return cfg;
        }

        private void ApplyDetectorToggles(AntiCheatManager manager)
        {
            manager.Engine.SetDetectorEnabled("Movement", Bind("Detectors", "Movement", true, "Enable movement detector."));
            manager.Engine.SetDetectorEnabled("TagIntegrity", Bind("Detectors", "TagIntegrity", true, "Enable tag integrity detector."));
            manager.Engine.SetDetectorEnabled("Network", Bind("Detectors", "Network", true, "Enable network detector."));
            manager.Engine.SetDetectorEnabled("Metadata", Bind("Detectors", "Metadata", true, "Enable metadata context detector."));
            manager.OverlayEnabled = Bind("Debug", "Overlay", true, "Show the on-screen suspicion overlay.");
        }

        private T Bind<T>(string section, string key, T defaultValue, string description)
        {
            ConfigEntry<T> entry = Config.Bind(section, key, defaultValue, description);
            return entry.Value;
        }
    }
}
