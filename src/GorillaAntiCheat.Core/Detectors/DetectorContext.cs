using GorillaAntiCheat.Core.Config;
using GorillaAntiCheat.Core.State;

namespace GorillaAntiCheat.Core.Detectors
{
    /// <summary>
    /// Everything a detector needs to evaluate one player for one tick. Passed by
    /// <c>in</c> to avoid copies; holds references, not ownership.
    /// </summary>
    public readonly struct DetectorContext
    {
        public readonly IAntiCheatWorld World;
        public readonly PlayerState Subject;
        public readonly int CurrentTick;
        public readonly double CurrentTime;
        public readonly AntiCheatConfig Config;

        public DetectorContext(IAntiCheatWorld world, PlayerState subject, int currentTick, double currentTime, AntiCheatConfig config)
        {
            World = world;
            Subject = subject;
            CurrentTick = currentTick;
            CurrentTime = currentTime;
            Config = config;
        }
    }
}
