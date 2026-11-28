namespace Game4026.Enemy
{
    /// <summary>
    /// Optional replacement for <see cref="DemoFlow"/>'s single hall encounter (e.g. artifact rounds). While one is
    /// set, the flow starts / stops / resets it instead of the hall encounter and goes to the boss when it is done.
    /// </summary>
    public interface IDemoHallSteps
    {
        /// <summary>The hall fight starts (after the intro countdown).</summary>
        void BeginHall();
        /// <summary>Stop now: the player fell or the hall was skipped. Enemies are cleared.</summary>
        void StopHall();
        /// <summary>Back to the state before <see cref="BeginHall"/> (restart).</summary>
        void ResetHall();
        bool IsHallDone { get; }
        /// <summary>HUD line during the hall.</summary>
        string HallHud { get; }
    }
}
