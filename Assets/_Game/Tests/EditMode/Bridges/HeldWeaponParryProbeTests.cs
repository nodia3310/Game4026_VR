using Game4026.Bridges;
using Game4026.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Bridges
{
    /// <summary>The probe behind flying-weapon reflects (shape here: a big box, as on a large weapon): held weapon, grown shape, and in the way.</summary>
    public sealed class HeldWeaponParryProbeTests
    {
        private GameObject probeGo, hand, playerWeapon, player;
        private HeldWeaponParryProbe probe;
        private BoxCollider handBox;
        private bool held;

        [SetUp]
        public void SetUp()
        {
            probeGo = new GameObject("Probe");
            probe = probeGo.AddComponent<HeldWeaponParryProbe>();
            held = true;
            probe.IsHeldPlayerWeapon = c => held && c.gameObject == playerWeapon;

            // A 1 m hand 2 m in front of the player, collider off (like the pharaoh's flying hand).
            hand = new GameObject("Hand");
            hand.transform.position = new Vector3(0f, 1.3f, 2f);
            handBox = hand.AddComponent<BoxCollider>();
            handBox.enabled = false;

            playerWeapon = new GameObject("PlayerWeapon");
            playerWeapon.AddComponent<BoxCollider>().size = Vector3.one * 0.05f;

            player = new GameObject("Player");
            var head = new GameObject("Head");
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var body = new GameObject("Body");
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            var target = player.AddComponent<PlayerTarget>();
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty("head").objectReferenceValue = head.transform;
            so.FindProperty("body").objectReferenceValue = body.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            typeof(PlayerTarget).GetProperty("Current").SetValue(null, target); // OnEnable does not run in Edit Mode
        }

        [TearDown]
        public void TearDown()
        {
            typeof(PlayerTarget).GetProperty("Current").SetValue(null, null);
            Object.DestroyImmediate(probeGo);
            Object.DestroyImmediate(hand);
            Object.DestroyImmediate(playerWeapon);
            Object.DestroyImmediate(player);
        }

        private bool WeaponAt(Vector3 position, float padding = 0.13f)
        {
            playerWeapon.transform.position = position;
            Physics.SyncTransforms();
            return probe.TryFind(handBox, padding, out _);
        }

        [Test]
        public void HeldWeaponInGrownShape_InFrontOfPlayer_Found_EvenWithColliderOff()
        {
            Assert.IsTrue(WeaponAt(new Vector3(0f, 1.3f, 1.4f))); // 0.1 m short of the hand face, inside the padding
        }

        [Test]
        public void OutsideGrownShape_NotFound()
        {
            Assert.IsFalse(WeaponAt(new Vector3(0f, 1.3f, 1.2f)));
            Assert.IsFalse(WeaponAt(new Vector3(0f, 1.3f, 1.4f), padding: 0f));
        }

        [Test]
        public void NotHeld_NotFound()
        {
            held = false;
            Assert.IsFalse(WeaponAt(new Vector3(0f, 1.3f, 1.4f)));
        }

        [Test]
        public void HandAlreadyOnThePlayer_WeaponAtTheSide_NotInTheWay()
        {
            hand.transform.position = new Vector3(0f, 1.3f, 0.2f);
            handBox.size = Vector3.one * 2f; // the big hand covers the player and the weapon
            Assert.IsFalse(WeaponAt(new Vector3(0f, 1.3f, -0.4f))); // behind the player's body
            Assert.IsFalse(WeaponAt(new Vector3(0.3f, 1.3f, 0f)));  // held at the side
        }

        [Test]
        public void BigHand_WeaponHeldOutTowardIt_InTheWay()
        {
            hand.transform.position = new Vector3(0f, 1.3f, 1.6f);
            handBox.size = Vector3.one * 2f;
            Assert.IsTrue(WeaponAt(new Vector3(0f, 1.3f, 0.5f)));
        }

        [Test]
        public void SegmentDistance_ClampsToEnds()
        {
            Assert.AreEqual(1f, HeldWeaponParryProbe.DistanceToSegment(new Vector3(1f, 0.5f, 0f), Vector3.zero, Vector3.up), 1e-4f);
            Assert.AreEqual(1f, HeldWeaponParryProbe.DistanceToSegment(new Vector3(0f, 2f, 0f), Vector3.zero, Vector3.up), 1e-4f);
        }
    }
}
