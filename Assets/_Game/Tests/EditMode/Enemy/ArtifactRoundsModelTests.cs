using Game4026.Enemy;
using NUnit.Framework;

namespace Game4026.Tests.Enemy
{
    public sealed class ArtifactRoundsModelTests
    {
        [Test]
        public void Begin_StartsFirstRound()
        {
            var m = new ArtifactRoundsModel(3, 2f);
            Assert.AreEqual(ArtifactRoundAction.StartRound, m.Begin());
            Assert.AreEqual(0, m.Round);
            Assert.AreEqual(ArtifactRoundStage.Fighting, m.Stage);
        }

        [Test]
        public void NoRounds_DoneAtOnce()
        {
            var m = new ArtifactRoundsModel(0, 2f);
            Assert.AreEqual(ArtifactRoundAction.Complete, m.Begin());
            Assert.IsTrue(m.IsComplete);
        }

        [Test]
        public void Restore_BeforeGuardianDown_IsIgnored()
        {
            var m = new ArtifactRoundsModel(3, 2f);
            m.Begin();
            Assert.IsFalse(m.ArtifactRestored(1f));
            Assert.AreEqual(ArtifactRoundStage.Fighting, m.Stage);
        }

        [Test]
        public void GuardianDown_ThenRestore_NextRoundAfterPause()
        {
            var m = new ArtifactRoundsModel(3, 2f);
            m.Begin();
            Assert.IsTrue(m.GuardianDown());
            Assert.IsFalse(m.GuardianDown(), "only once per round");
            Assert.AreEqual(ArtifactRoundAction.None, m.Tick(100f), "waits for the artifact, however long");
            Assert.IsTrue(m.ArtifactRestored(10f));
            Assert.AreEqual(1, m.RestoredCount);
            Assert.AreEqual(ArtifactRoundAction.None, m.Tick(11.9f));
            Assert.AreEqual(ArtifactRoundAction.StartRound, m.Tick(12f));
            Assert.AreEqual(1, m.Round);
            Assert.AreEqual(ArtifactRoundStage.Fighting, m.Stage);
        }

        [Test]
        public void ThirdArtifact_CompletesHall()
        {
            var m = new ArtifactRoundsModel(3, 0f);
            m.Begin();
            for (int i = 0; i < 2; i++)
            {
                m.GuardianDown();
                m.ArtifactRestored(i);
                Assert.AreEqual(ArtifactRoundAction.StartRound, m.Tick(i));
            }
            m.GuardianDown();
            m.ArtifactRestored(5f);
            Assert.AreEqual(ArtifactRoundAction.Complete, m.Tick(5f));
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual(3, m.RestoredCount);
            Assert.AreEqual(ArtifactRoundAction.None, m.Tick(6f));
        }

        [Test]
        public void Stop_ThenBegin_StartsOver()
        {
            var m = new ArtifactRoundsModel(3, 0f);
            m.Begin();
            m.GuardianDown();
            m.ArtifactRestored(0f);
            m.Tick(0f);
            m.Stop();
            Assert.AreEqual(ArtifactRoundStage.Idle, m.Stage);
            Assert.IsFalse(m.GuardianDown());
            m.Begin();
            Assert.AreEqual(0, m.Round);
            Assert.AreEqual(0, m.RestoredCount);
        }
    }
}
