using System;

namespace Game4026.Enemy
{
    public enum DemoPhase { Intro, Hall, BossIntro, Boss, Victory, Defeat }

    /// <summary>What <see cref="DemoFlow"/> must do this frame.</summary>
    public enum DemoAction { None, StartHall, StartBoss, Restart }

    /// <summary>
    /// Engine-free demo run: intro wait → hall waves → short pause → boss → victory.
    /// The player dying during any fight phase → defeat, then a restart after a delay.
    /// </summary>
    public sealed class DemoFlowModel
    {
        private readonly float introDelay;
        private readonly float bossDelay;
        private readonly float restartDelay;
        private float phaseStart;

        public DemoPhase Phase { get; private set; } = DemoPhase.Intro;

        /// <summary>(previous, next)</summary>
        public event Action<DemoPhase, DemoPhase> PhaseChanged;

        public DemoFlowModel(float introDelay, float bossDelay, float restartDelay)
        {
            this.introDelay = Math.Max(0f, introDelay);
            this.bossDelay = Math.Max(0f, bossDelay);
            this.restartDelay = Math.Max(0f, restartDelay);
        }

        public float PhaseElapsed(float now) => now - phaseStart;

        /// <summary>Starts (or restarts) the run.</summary>
        public void Begin(float now) => Set(DemoPhase.Intro, now);

        public DemoAction Tick(float now, bool hallCleared, bool bossCleared, bool playerDead)
        {
            float elapsed = now - phaseStart;
            if (playerDead && IsFighting)
            {
                Set(DemoPhase.Defeat, now);
                return DemoAction.None;
            }

            switch (Phase)
            {
                case DemoPhase.Intro:
                    if (elapsed >= introDelay)
                    {
                        Set(DemoPhase.Hall, now);
                        return DemoAction.StartHall;
                    }
                    break;
                case DemoPhase.Hall:
                    if (hallCleared)
                        Set(DemoPhase.BossIntro, now);
                    break;
                case DemoPhase.BossIntro:
                    if (elapsed >= bossDelay)
                    {
                        Set(DemoPhase.Boss, now);
                        return DemoAction.StartBoss;
                    }
                    break;
                case DemoPhase.Boss:
                    if (bossCleared)
                        Set(DemoPhase.Victory, now);
                    break;
                case DemoPhase.Defeat:
                    if (elapsed >= restartDelay)
                        return DemoAction.Restart;
                    break;
            }
            return DemoAction.None;
        }

        private bool IsFighting => Phase == DemoPhase.Intro || Phase == DemoPhase.Hall ||
                                   Phase == DemoPhase.BossIntro || Phase == DemoPhase.Boss;

        private void Set(DemoPhase next, float now)
        {
            DemoPhase previous = Phase;
            Phase = next;
            phaseStart = now;
            PhaseChanged?.Invoke(previous, next);
        }
    }
}
