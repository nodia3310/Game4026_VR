using System.Collections.Generic;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>A flying weapon asks the parry probe each frame and reflects when a held weapon is in its grown shape.</summary>
    public sealed class ProjectileReflectAssistTests
    {
        private sealed class Receiver : MonoBehaviour, IPlayerDamageReceiver
        {
            public int Hits;
            public void ApplyPlayerDamage(float damage) => Hits++;
        }

        private sealed class FakeProbe : IParryProbe
        {
            public bool Answer;
            public int Queries;
            public Collider LastShape;
            public float LastPadding;

            public bool TryFind(Collider shape, float padding, out Vector3 point)
            {
                Queries++;
                LastShape = shape;
                LastPadding = padding;
                point = shape.transform.position;
                return Answer;
            }
        }

        private readonly List<Object> created = new List<Object>();
        private EnemyProjectile bolt;
        private Collider boltCollider;
        private Receiver receiver;
        private FakeProbe probe;

        private T Track<T>(T o) where T : Object { created.Add(o); return o; }

        [SetUp]
        public void SetUp()
        {
            var boltGo = Track(new GameObject("Bolt"));
            boltCollider = boltGo.AddComponent<SphereCollider>();
            bolt = boltGo.AddComponent<EnemyProjectile>();
            var player = Track(new GameObject("Player"));
            player.transform.position = new Vector3(0f, 0f, 10f);
            receiver = player.AddComponent<Receiver>();
            var target = player.AddComponent<PlayerTarget>();
            // PlayerTarget.Current is set in OnEnable, which does not run in Edit Mode.
            typeof(PlayerTarget).GetProperty("Current").SetValue(null, target);
            probe = new FakeProbe();
            ParryProbe.Current = probe;
        }

        [TearDown]
        public void TearDown()
        {
            ParryProbe.Current = null;
            typeof(PlayerTarget).GetProperty("Current").SetValue(null, null);
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private void Fly(float seconds, float dt = 0.02f)
        {
            for (float t = 0f; t < seconds - 1e-5f && bolt.IsFlying; t += dt)
                bolt.Step(dt);
        }

        [Test]
        public void HeldWeaponInZone_ReflectsBeforeReachingPlayer()
        {
            bolt.Launch(null, Vector3.zero, Vector3.forward, 10f, 10f);
            probe.Answer = true;
            bolt.Step(0.02f);

            Assert.IsTrue(bolt.IsReflected);
            Assert.Less(bolt.Direction.z, 0f);
            Assert.AreSame(boltCollider, probe.LastShape);
            Assert.Greater(probe.LastPadding, 0f);
            Fly(3f);
            Assert.AreEqual(0, receiver.Hits);
        }

        [Test]
        public void NoWeaponInZone_FliesOnAndHitsPlayer()
        {
            bolt.Launch(null, Vector3.zero, Vector3.forward, 10f, 10f);
            Fly(2f);

            Assert.IsFalse(bolt.IsReflected);
            Assert.AreEqual(1, receiver.Hits);
            Assert.Greater(probe.Queries, 0);
        }

        [Test]
        public void AssistOff_NeverAsks()
        {
            bolt.ReflectAssist = false;
            bolt.Launch(null, Vector3.zero, Vector3.forward, 10f, 10f);
            probe.Answer = true;
            Fly(2f);

            Assert.AreEqual(0, probe.Queries);
            Assert.AreEqual(1, receiver.Hits);
        }

        [Test]
        public void Reflected_StopsAsking()
        {
            bolt.Launch(null, Vector3.zero, Vector3.forward, 10f, 10f);
            probe.Answer = true;
            bolt.Step(0.02f);
            int queries = probe.Queries;
            bolt.Step(0.02f);
            bolt.Step(0.02f);

            Assert.AreEqual(queries, probe.Queries);
        }
    }
}
