using System.Collections.Generic;
using System.Reflection;
using Game4026.Interaction.Melee;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Interaction.Melee
{
    // Tests the physics query behavior without creating/changing a scene asset or requiring XR hardware.
    public class MeleeSweepTests
    {
        private const int TargetLayer = 30;
        private const int WallLayer = 29;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<MeleeHit> hits = new List<MeleeHit>();
        private MeleeWeapon weapon;
        private Transform origin;

        [SetUp]
        public void SetUp()
        {
            var weaponObject = Create("weapon", new Vector3(1000, 1000, 1000));
            weapon = weaponObject.AddComponent<MeleeWeapon>();
            weapon.enabled = false;
            origin = Create("origin", P(-1, 0)).transform;
            Set("obstructionOrigin", origin);
            Set("targetMask", (LayerMask)(1 << TargetLayer));
            Set("blockerMask", (LayerMask)(1 << WallLayer));
            Set("sweepRadius", 0.035f);
            var swing = new MeleeSwingWindow(1.2f, 0.4f, 0.6f);
            swing.Advance(0, 0.02f);
            swing.Advance(3, 0.02f);
            Set("swing", swing);
            weapon.Hit += hits.Add;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; --i)
                if (objects[i]) Object.DestroyImmediate(objects[i]);
            objects.Clear(); hits.Clear();
        }

        [Test]
        public void FastSweep_HitsThinTargetBetweenSamples()
        {
            Box("thin target", P(0.5f, 0), new Vector3(0.01f, 0.2f, 0.2f), TargetLayer);
            Sweep(P(0, 0), P(1, 0));
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Speed, Is.EqualTo(50).Within(0.01f));
        }

        [Test]
        public void InitialOverlap_IsDetectedWithoutRelyingOnSphereCast()
        {
            Box("overlap target", P(0, 0), Vector3.one * 0.1f, TargetLayer);
            Sweep(P(0, 0), P(0.3f, 0));
            Assert.That(hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void WallBeforeTarget_BlocksFastSweep()
        {
            Box("wall", P(0.35f, 0), new Vector3(0.05f, 2, 2), WallLayer);
            Box("target", P(0.7f, 0), Vector3.one * 0.1f, TargetLayer);
            Sweep(P(0, 0), P(1, 0));
            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void BladeAlreadyBeyondWall_CannotHitThroughIt()
        {
            Box("wall", P(0, 0), new Vector3(0.05f, 2, 2), WallLayer);
            Box("target", P(0.6f, 0), Vector3.one * 0.1f, TargetLayer);
            Sweep(P(0.3f, 0), P(0.9f, 0));
            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void MultipleChildColliders_AreOneTargetPerSwing()
        {
            var root = Create("target root", P(0, 0));
            root.AddComponent<MeleeHitTarget>();
            Box("target a", P(0.3f, 0), Vector3.one * 0.1f, TargetLayer).transform.SetParent(root.transform, true);
            Box("target b", P(0.6f, 0), Vector3.one * 0.1f, TargetLayer).transform.SetParent(root.transform, true);
            Sweep(P(0, 0), P(1, 0));
            Sweep(P(0, 0), P(1, 0));
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Target, Is.SameAs(root));
        }

        [Test]
        public void SelfCollider_IsIgnored()
        {
            Box("self", P(0.3f, 0), Vector3.one * 0.1f, TargetLayer).transform.SetParent(weapon.transform, true);
            Sweep(P(0, 0), P(1, 0));
            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void FullQueryBuffer_FailsClosedInsteadOfLosingWalls()
        {
            // 64 overlapping candidates exhaust the fixed buffer; even a valid target must not emit.
            for (int i = 0; i < 64; ++i)
                Box("crowded target " + i, P(0, 0), Vector3.one * 0.1f, TargetLayer);
            Sweep(P(0, 0), P(0.3f, 0));
            Assert.That(hits, Is.Empty);
            Assert.That(weapon.QueryOverflowCount, Is.EqualTo(1));
        }

        [Test]
        public void ObstructionOriginInsideWall_FailsClosed()
        {
            Box("wall containing head", origin.position, Vector3.one * 0.2f, WallLayer);
            Box("target", P(0.6f, 0), Vector3.one * 0.1f, TargetLayer);
            Sweep(P(0.3f, 0), P(0.9f, 0));
            Assert.That(hits, Is.Empty);
        }

        private static Vector3 P(float x, float y) => new Vector3(1000 + x, 1000 + y, 1000);
        private GameObject Create(string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.position = position; objects.Add(go); return go;
        }
        private GameObject Box(string name, Vector3 position, Vector3 size, int layer)
        {
            var go = Create(name, position); go.layer = layer;
            go.AddComponent<BoxCollider>().size = size; return go;
        }
        private void Set(string name, object value) => typeof(MeleeWeapon)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, value);
        private void Sweep(Vector3 from, Vector3 to)
        {
            Physics.SyncTransforms();
            typeof(MeleeWeapon).GetMethod("Sweep", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(weapon, new object[] { from, to, 0.02f });
        }
    }
}
