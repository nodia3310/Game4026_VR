using Game4026.Enemy;
using NUnit.Framework;

namespace Game4026.Tests.Enemy
{
    public sealed class DemoLightingTests
    {
        private const float Bright = 2f, Flicker = 1f, Dark = 2f, FadeUp = 3f;

        private static float At(float t) => DemoLighting.SequenceLevel(t, Bright, Flicker, Dark, FadeUp);

        [Test]
        public void StartsBright_FlickersDown()
        {
            Assert.AreEqual(Bright, At(-0.1f), 1e-4f);
            bool sawDropOut = false;
            for (float t = 0f; t < Flicker; t += 0.01f)
            {
                float v = At(t);
                Assert.LessOrEqual(v, Bright + 1e-4f);
                if (v < 0.25f)
                    sawDropOut = true;
            }
            Assert.IsTrue(sawDropOut, "the lights stutter off during the flicker");
        }

        [Test]
        public void FullyDark_ThenEasesUpToFightLevel()
        {
            Assert.AreEqual(0f, At(Flicker + 0.01f));
            Assert.AreEqual(0f, At(Flicker + Dark - 0.01f));
            float mid = At(Flicker + Dark + FadeUp * 0.5f);
            Assert.Greater(mid, 0.2f);
            Assert.Less(mid, 0.8f);
            Assert.AreEqual(1f, At(Flicker + Dark + FadeUp), 1e-4f);
            Assert.AreEqual(1f, At(Flicker + Dark + FadeUp + 5f), 1e-4f);
        }

        [Test]
        public void Lightmaps_OffWhileDark_BackAfterStutter()
        {
            Assert.IsTrue(DemoLighting.SequenceLightmaps(-0.1f, Flicker, Dark));
            bool sawOff = false;
            for (float t = 0f; t < Flicker; t += 0.01f)
                sawOff |= !DemoLighting.SequenceLightmaps(t, Flicker, Dark);
            Assert.IsTrue(sawOff, "they flicker with the lights");
            Assert.IsFalse(DemoLighting.SequenceLightmaps(Flicker + 0.5f, Flicker, Dark));
            Assert.IsFalse(DemoLighting.SequenceLightmaps(Flicker + Dark - 0.01f, Flicker, Dark));
            Assert.IsTrue(DemoLighting.SequenceLightmaps(Flicker + Dark + 0.04f, Flicker, Dark), "first stutter on");
            Assert.IsFalse(DemoLighting.SequenceLightmaps(Flicker + Dark + 0.15f, Flicker, Dark), "stutter off");
            Assert.IsTrue(DemoLighting.SequenceLightmaps(Flicker + Dark + 1f, Flicker, Dark));
        }
    }
}
