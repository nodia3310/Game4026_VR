using Game4026.Enemy;
using NUnit.Framework;

namespace Game4026.Tests.Enemy
{
    /// <summary>Which background track each demo phase plays.</summary>
    public sealed class DemoMusicTests
    {
        [TestCase(DemoPhase.Intro, DemoMusic.Track.None)]
        [TestCase(DemoPhase.Hall, DemoMusic.Track.Hall)]
        [TestCase(DemoPhase.BossIntro, DemoMusic.Track.Boss)]
        [TestCase(DemoPhase.Boss, DemoMusic.Track.Boss)]
        [TestCase(DemoPhase.Victory, DemoMusic.Track.None)]
        [TestCase(DemoPhase.Defeat, DemoMusic.Track.None)]
        public void TrackFor_Phase(DemoPhase phase, DemoMusic.Track expected)
        {
            Assert.AreEqual(expected, DemoMusic.TrackFor(phase));
        }
    }
}
