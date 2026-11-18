using Game4026.Bridges;
using NUnit.Framework;

namespace Game4026.Tests.Bridges
{
    public sealed class ImpactDamageCurveTests
    {
        private static readonly ImpactDamageCurve Curve = new ImpactDamageCurve(8f, 5f, 40f);

        [TestCase(3f, 24f)]
        [TestCase(4f, 32f)]
        [TestCase(0.1f, 5f)]   // below min -> min
        [TestCase(100f, 40f)]  // above max -> max
        public void Evaluate_ScalesAndClamps(float speed, float expected)
        {
            Assert.AreEqual(expected, Curve.Evaluate(speed), 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Evaluate_InvalidSpeed_GivesZero(float speed)
        {
            Assert.AreEqual(0f, Curve.Evaluate(speed));
        }

        [Test]
        public void Evaluate_MaxBelowMin_UsesMin()
        {
            var broken = new ImpactDamageCurve(8f, 20f, 10f);
            Assert.AreEqual(20f, broken.Evaluate(100f), 1e-4f);
        }
    }
}
