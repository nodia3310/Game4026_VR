using Game4026.Enemy;
using NUnit.Framework;

namespace Game4026.Tests.Enemy
{
    public sealed class DemoFlowModelTests
    {
        private static DemoFlowModel Started()
        {
            var m = new DemoFlowModel(introDelay: 3f, bossDelay: 4f, restartDelay: 5f);
            m.Begin(0f);
            return m;
        }

        [Test]
        public void Intro_StartsHallAfterDelay()
        {
            var m = Started();
            Assert.AreEqual(DemoAction.None, m.Tick(2.9f, false, false, false));
            Assert.AreEqual(DemoAction.StartHall, m.Tick(3f, false, false, false));
            Assert.AreEqual(DemoPhase.Hall, m.Phase);
        }

        [Test]
        public void HallCleared_WaitsThenStartsBoss_ThenVictory()
        {
            var m = Started();
            m.Tick(3f, false, false, false);
            Assert.AreEqual(DemoAction.None, m.Tick(10f, true, false, false));
            Assert.AreEqual(DemoPhase.BossIntro, m.Phase);
            Assert.AreEqual(DemoAction.None, m.Tick(13.9f, true, false, false));
            Assert.AreEqual(DemoAction.StartBoss, m.Tick(14f, true, false, false));
            Assert.AreEqual(DemoPhase.Boss, m.Phase);
            m.Tick(20f, true, true, false);
            Assert.AreEqual(DemoPhase.Victory, m.Phase);
        }

        [Test]
        public void HallClearedDoesNotSkipIntro()
        {
            var m = Started();
            Assert.AreEqual(DemoAction.None, m.Tick(1f, true, false, false));
            Assert.AreEqual(DemoPhase.Intro, m.Phase);
        }

        [Test]
        public void PlayerDeath_Defeat_ThenRestartAfterDelay()
        {
            var m = Started();
            m.Tick(3f, false, false, false);
            m.Tick(8f, false, false, true);
            Assert.AreEqual(DemoPhase.Defeat, m.Phase);
            Assert.AreEqual(DemoAction.None, m.Tick(12.9f, false, false, true));
            Assert.AreEqual(DemoAction.Restart, m.Tick(13f, false, false, true));
            m.Begin(13f);
            Assert.AreEqual(DemoPhase.Intro, m.Phase);
        }

        [Test]
        public void DeathDuringBoss_IsDefeat_DeathAfterVictory_IsIgnored()
        {
            var m = Started();
            m.Tick(3f, false, false, false);
            m.Tick(4f, true, false, false);
            m.Tick(8f, true, false, false);
            Assert.AreEqual(DemoPhase.Boss, m.Phase);
            m.Tick(9f, true, false, true);
            Assert.AreEqual(DemoPhase.Defeat, m.Phase);

            var won = Started();
            won.Tick(3f, false, false, false);
            won.Tick(4f, true, false, false);
            won.Tick(8f, true, false, false);
            won.Tick(9f, true, true, false);
            won.Tick(10f, true, true, true);
            Assert.AreEqual(DemoPhase.Victory, won.Phase);
        }

        [Test]
        public void SkipToBoss_FromIntroOrHall_GoesToBossIntro()
        {
            var m = Started();
            Assert.IsTrue(m.SkipToBoss(1f));
            Assert.AreEqual(DemoPhase.BossIntro, m.Phase);
            Assert.AreEqual(DemoAction.StartBoss, m.Tick(5f, true, false, false));

            var hall = Started();
            hall.Tick(3f, false, false, false);
            Assert.IsTrue(hall.SkipToBoss(4f));
            Assert.AreEqual(DemoPhase.BossIntro, hall.Phase);
        }

        [Test]
        public void SkipToBoss_IgnoredDuringBossOrAfter()
        {
            var m = Started();
            m.SkipToBoss(0f);
            m.Tick(4f, true, false, false);
            Assert.AreEqual(DemoPhase.Boss, m.Phase);
            Assert.IsFalse(m.SkipToBoss(5f));
            Assert.AreEqual(DemoPhase.Boss, m.Phase);
        }

        [Test]
        public void PhaseChanged_Raised()
        {
            var m = new DemoFlowModel(0f, 0f, 0f);
            int count = 0;
            m.PhaseChanged += (_, __) => count++;
            m.Begin(0f);
            m.Tick(0f, false, false, false);
            Assert.AreEqual(2, count);
        }
    }
}
