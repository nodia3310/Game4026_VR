using System.Collections.Generic;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>Laser bolt flight, player hit by proximity, reflection (no physics needed for these paths).</summary>
    public sealed class EnemyProjectileTests
    {
        private sealed class Receiver : MonoBehaviour, IPlayerDamageReceiver
        {
            public readonly List<float> Hits = new List<float>();
            public void ApplyPlayerDamage(float damage) => Hits.Add(damage);
        }

        private readonly List<Object> created = new List<Object>();
        private EnemyProjectile bolt;
        private Receiver receiver;
        private GameObject player;

        private T Track<T>(T o) where T : Object { created.Add(o); return o; }

        [SetUp]
        public void SetUp()
        {
            bolt = Track(new GameObject("Bolt")).AddComponent<EnemyProjectile>();
            player = Track(new GameObject("Player"));
            player.transform.position = new Vector3(0f, 0f, 10f);
            receiver = player.AddComponent<Receiver>();
            var head = Track(new GameObject("Head"));
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var target = player.AddComponent<PlayerTarget>();
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty("head").objectReferenceValue = head.transform;
            so.FindProperty("body").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            // PlayerTarget.Current is set in OnEnable, which does not run in Edit Mode.
            typeof(PlayerTarget).GetProperty("Current").SetValue(null, target);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(PlayerTarget).GetProperty("Current").SetValue(null, null);
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private int Fly(float seconds, float dt = 0.02f)
        {
            int frames = 0;
            for (float t = 0f; t < seconds - 1e-5f && bolt.IsFlying; t += dt, frames++)
                bolt.Step(dt);
            return frames;
        }

        [Test]
        public void FliesStraight_AtSpeed()
        {
            bolt.Launch(null, new Vector3(0f, 1f, 0f), Vector3.forward, 5f, 10f);
            Fly(0.4f);
            Assert.IsTrue(bolt.IsFlying);
            Assert.AreEqual(2f, bolt.transform.position.z, 0.02f);
        }

        [Test]
        public void ReachesPlayer_DamagesOnce_AndEnds()
        {
            bool? struck = null;
            bolt.Expired += (_, s) => struck = s;
            bolt.Launch(null, new Vector3(0f, 1.3f, 0f), Vector3.forward, 10f, 12f);
            Fly(1.5f);

            Assert.IsFalse(bolt.IsFlying);
            CollectionAssert.AreEqual(new[] { 12f }, receiver.Hits);
            Assert.AreEqual(true, struck);
        }

        [Test]
        public void MissesPlayer_WhenPathIsFarFromBodyToHeadLine()
        {
            bolt.Launch(null, new Vector3(2f, 1.3f, 0f), Vector3.forward, 10f, 12f);
            Fly(1.5f);
            Assert.AreEqual(0, receiver.Hits.Count);
        }

        [Test]
        public void Lifetime_EndsTheBolt()
        {
            bolt.Launch(null, new Vector3(0f, 50f, 0f), Vector3.up, 5f, 10f);
            Fly(4.1f);
            Assert.IsFalse(bolt.IsFlying);
        }

        [Test]
        public void Reflect_TurnsAround_Faster_AndNoLongerHurtsPlayer()
        {
            bolt.Launch(null, new Vector3(0f, 1.3f, 0f), Vector3.forward, 8f, 12f);
            Fly(0.5f); // at z = 4
            bolt.TakeDamage(20f, bolt.transform.position, false); // player's melee weapon

            Assert.IsTrue(bolt.IsReflected);
            Assert.AreEqual(12f, bolt.Speed, 1e-4f);
            Assert.Less(Vector3.Dot(bolt.Direction, Vector3.forward), -0.99f, "No shooter: straight back.");
            Fly(0.5f);
            Assert.Less(bolt.transform.position.z, 4f);
            Assert.AreEqual(0, receiver.Hits.Count);
        }

        [Test]
        public void Reflect_OnlyOnce_AndNotWhenIdle()
        {
            Assert.IsFalse(bolt.Reflect(), "Not flying.");
            bolt.Launch(null, Vector3.zero, Vector3.forward, 8f, 12f);
            Assert.IsTrue(bolt.Reflect());
            Assert.IsFalse(bolt.Reflect());
        }

        [Test]
        public void Arrows_CannotReflect()
        {
            bolt.Launch(null, Vector3.zero, Vector3.forward, 8f, 12f);
            Assert.IsFalse(bolt.TakeUnparryableHit(30f, Vector3.zero));
            Assert.IsFalse(bolt.IsReflected);
        }

        [Test]
        public void SegmentDistance_ParallelAndEndpoints()
        {
            // Parallel vertical segments 1 m apart.
            Assert.AreEqual(1f, EnemyProjectile.SegmentDistance(
                Vector3.zero, Vector3.up, Vector3.right, Vector3.right + Vector3.up), 1e-4f);
            // Collinear, with a 0.5 m gap between the ends.
            Assert.AreEqual(0.5f, EnemyProjectile.SegmentDistance(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1.5f), new Vector3(0f, 0f, 3f)), 1e-4f);
            // A point (zero-length segment) against a segment.
            Assert.AreEqual(2f, EnemyProjectile.SegmentDistance(
                new Vector3(2f, 0.5f, 0f), new Vector3(2f, 0.5f, 0f), Vector3.zero, Vector3.up), 1e-4f);
        }

        [Test]
        public void SegmentDistance_Crossing_IsGap()
        {
            float d = EnemyProjectile.SegmentDistance(
                new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, -1f, 0.3f), new Vector3(0f, 1f, 0.3f));
            Assert.AreEqual(0.3f, d, 1e-4f);
        }
    }
}
