using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;

namespace Game4026.Tests.Enemy
{
    public sealed class ArtifactProgressTests
    {
        private static ArtifactRestoredEvent E(string id, ArtifactType type) => new ArtifactRestoredEvent(id, type);

        [Test]
        public void AllThreeTypes_UnlockBoss()
        {
            var p = new ArtifactProgress();
            p.TryAdd(E("a", ArtifactType.RosettaStone));
            p.TryAdd(E("b", ArtifactType.CanopicJar));
            Assert.IsFalse(p.AllRestored);

            p.TryAdd(E("c", ArtifactType.TutankhamunMask));
            Assert.IsTrue(p.AllRestored);
        }

        [Test]
        public void DuplicateId_IsIgnored()
        {
            var p = new ArtifactProgress();
            Assert.IsTrue(p.TryAdd(E("a", ArtifactType.RosettaStone)));
            Assert.IsFalse(p.TryAdd(E("a", ArtifactType.RosettaStone)));
            Assert.AreEqual(1, p.RestoredCount);
        }

        [Test]
        public void SameTypeTwice_DoesNotCountAsAll()
        {
            var p = new ArtifactProgress();
            p.TryAdd(E("a", ArtifactType.RosettaStone));
            p.TryAdd(E("b", ArtifactType.RosettaStone));
            p.TryAdd(E("c", ArtifactType.CanopicJar));
            Assert.IsFalse(p.AllRestored);
        }

        [Test]
        public void Null_IsIgnored()
        {
            Assert.IsFalse(new ArtifactProgress().TryAdd(null));
        }

        [Test]
        public void Reset_ForgetsEverything()
        {
            var p = new ArtifactProgress();
            p.TryAdd(E("a", ArtifactType.RosettaStone));
            p.Reset();

            Assert.IsFalse(p.IsRestored(ArtifactType.RosettaStone));
            Assert.IsTrue(p.TryAdd(E("a", ArtifactType.RosettaStone)), "Same ID is accepted again in a new run.");
        }
    }
}
