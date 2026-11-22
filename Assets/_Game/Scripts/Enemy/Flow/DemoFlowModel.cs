using System;

namespace Game4026.Enemy
{
    public enum DemoPhase { Intro, Hall, BossIntro, Boss, Victory, Defeat }

    /// <summary>What <see cref="DemoFlow"/> must do this frame.</summary>
    public enum DemoAction { None, StartHall, StartBoss, Restart }

    /// <summary>
    /// Engine-free demo run: intro wait → hall waves → short pause → boss → victory.
    /// A run begun with <c>waitForReady</c> holds the intro until <see cref="MarkReady"/> (the player picked up a weapon),
    /// then counts the intro delay from that moment.
    /// The player dying during any fight phase → defeat, then a restart after a delay.
    /// </summary>
    public sealed class DemoFlowModel
    {
        private readonly float introDelay;
        private readonly float bossDelay;
        private readonly float restartDelay;
        private float phaseStart;

        public DemoPhase Phase { get; private set; } = DemoPhase.Intro;
        /// <summary>False while the intro waits for the player; the intro countdown runs only once ready.</summary>
        public bool IsReady { get; private set; } = true;
        public float IntroDelay => introDelay;

        /// <summary>(previous, next)</summary>
        public event Action<DemoPhase, DemoPhase> PhaseChanged;

        public DemoFlowModel(float introDelay, float bossDelay, float restartDelay)
        {
            this.introDelay = Math.Max(0f, introDelay);
            this.bossDelay = Math.Max(0f, bossDelay);
            this.restartDelay = Math.Max(0f, restartDelay);
        }

        public float PhaseElapsed(float now) => now - phaseStart;

        /// <summary>Starts (or restarts) the run. With <paramref name="waitForReady"/> the intro waits for <see cref="MarkReady"/>.</summary>
        public void Begin(float now, bool waitForReady = false)
        {
            IsReady = !waitForReady;
            Set(DemoPhase.Intro, now);
        }

        /// <summary>The player is ready: the intro countdown starts now. Ignored outside a waiting intro.</summary>
        public bool MarkReady(float now)
        {
            if (IsReady || Phase != DemoPhase.Intro)
                return false;
            IsReady = true;
            phaseStart = now;
            return true;
        }

        /// <summary>Seconds left in the intro countdown (0 when waiting or not in the intro).</summary>
        public float IntroRemaining(float now) =>
            Phase == DemoPhase.Intro && IsReady ? Math.Max(0f, introDelay - (now - phaseStart)) : 0f;

        /// <summary>Test shortcut: jump from the intro / hall straight to the boss intro. False in any other phase.</summary>
        public bool SkipToBoss(float now)
        {
            if (Phase != DemoPhase.Intro && Phase != DemoPhase.Hall)
                return false;
            IsReady = true;
            Set(DemoPhase.BossIntro, now);
            return true;
        }

        /// <param name="artifactReady">False holds the cleared hall until the artifact is back on its display.</param>
        public DemoAction Tick(float now, bool hallCleared, bool bossCleared, bool playerDead, bool artifactReady = true)
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
                    if (IsReady && elapsed >= introDelay)
                    {
                        Set(DemoPhase.Hall, now);
                        return DemoAction.StartHall;
                    }
                    break;
                case DemoPhase.Hall:
                    if (hallCleared && artifactReady)
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
