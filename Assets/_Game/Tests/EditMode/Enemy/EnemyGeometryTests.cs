using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class EnemyGeometryTests
    {
        [Test]
        public void HorizontalDistance_IgnoresHeight()
        {
            Assert.AreEqual(5f, EnemyGeometry.HorizontalDistance(Vector3.zero, new Vector3(3f, 10f, 4f)), 1e-4f);
        }

        [TestCase(0f, 1f, true)]      // straight ahead
        [TestCase(0.6f, 1f, true)]    // ~31°, inside 35° half-angle
        [TestCase(0.8f, 1f, false)]   // ~39°, outside
        [TestCase(0f, -1f, false)]    // behind
        [TestCase(0f, 2.5f, false)]   // too far
        public void IsInAttackCone(float x, float z, bool expected)
        {
            bool inCone = EnemyGeometry.IsInAttackCone(Vector3.zero, Vector3.forward, new Vector3(x, 0f, z), 2f, 70f);
            Assert.AreEqual(expected, inCone);
        }

        [Test]
        public void IsInAttackCone_TargetAboveIsStillHit()
        {
            Assert.IsTrue(EnemyGeometry.IsInAttackCone(Vector3.zero, Vector3.forward, new Vector3(0f, 1.7f, 1f), 2f, 70f));
        }

        [Test]
        public void IsInAttackCone_SamePosition_CountsAsHit()
        {
            Assert.IsTrue(EnemyGeometry.IsInAttackCone(Vector3.zero, Vector3.forward, Vector3.zero, 2f, 70f));
        }
    }
}
