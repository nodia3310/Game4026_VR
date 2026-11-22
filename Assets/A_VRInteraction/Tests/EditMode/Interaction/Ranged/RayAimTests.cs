using System.Collections.Generic;
using System.Reflection;
using Game4026.Interaction.Ranged;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Interaction.Ranged
{
    public class RayAimTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private GameObject Create(string name, Vector3 position)
        {
            var go = new GameObject(name);
            objects.Add(go);
            go.layer = 31;
            go.transform.position = position;
            return go;
        }

        [Test]
        public void RayIgnoresWeaponCollidersAndStopsAtNearestWall()
        {
            Vector3 origin = new Vector3(1000, 1000, 1000);
            var weapon = Create("ray-test-weapon", origin);
            var aim = weapon.AddComponent<RayAim>();
            typeof(RayAim).GetField("hitMask", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(aim, (LayerMask)(1 << 31));
            var self = Create("weapon-child", origin + Vector3.forward * 2f);
            self.transform.SetParent(weapon.transform, true);
            self.AddComponent<BoxCollider>();
            var wall = Create("wall", origin + Vector3.forward * 4f).AddComponent<BoxCollider>();
            Create("behind-wall", origin + Vector3.forward * 6f).AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            var result = aim.QueryAim();
            Assert.That(result.HasHit, Is.True);
            Assert.That(result.Collider, Is.EqualTo(wall));
            Assert.That(result.Point.z, Is.EqualTo(origin.z + 3.5f).Within(0.001f));
        }

        [Test]
        public void DefaultTriggerPolicyIgnoresTriggers()
        {
            Vector3 origin = new Vector3(1100, 1000, 1000);
            var aim = Create("ray-test-weapon", origin).AddComponent<RayAim>();
            typeof(RayAim).GetField("hitMask", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(aim, (LayerMask)(1 << 31));
            Create("trigger", origin + Vector3.forward * 2f).AddComponent<BoxCollider>().isTrigger = true;
            var wall = Create("solid", origin + Vector3.forward * 4f).AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Assert.That(aim.QueryAim().Collider, Is.EqualTo(wall));
        }
    }
}
