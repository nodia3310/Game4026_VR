using Game4026.Bridges;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Bridges
{
    /// <summary>
    /// Tests the attach helpers with explicit lists (never scene-wide searches),
    /// so running tests cannot add components to whatever scene is open in the Editor.
    /// </summary>
    public sealed class DamageBridgeInstallerTests
    {
        private GameObject weaponGo;
        private GameObject arrowGo;

        [TearDown]
        public void TearDown()
        {
            if (weaponGo) Object.DestroyImmediate(weaponGo);
            if (arrowGo) Object.DestroyImmediate(arrowGo);
        }

        [Test]
        public void AttachMelee_AddsBridgeWithCurve_AndIsIdempotent()
        {
            weaponGo = new GameObject("Weapon");
            var weapon = weaponGo.AddComponent<MeleeWeapon>(); // RequireComponent pulls in XRGrabInteractable
            var curve = new ImpactDamageCurve(10f, 1f, 50f);

            Assert.AreEqual(1, DamageBridgeInstaller.AttachMelee(new[] { weapon }, curve));
            Assert.AreEqual(0, DamageBridgeInstaller.AttachMelee(new[] { weapon }, curve), "Second pass must not add another bridge.");

            Assert.IsTrue(weaponGo.TryGetComponent(out MeleeDamageBridge bridge));
            Assert.AreEqual(10f, bridge.Damage.DamagePerSpeed);
            Assert.AreEqual(1, weaponGo.GetComponents<MeleeDamageBridge>().Length);
        }

        [Test]
        public void AttachArrows_AddsBridgeWithCurve_AndIsIdempotent()
        {
            arrowGo = new GameObject("Arrow");
            var arrow = arrowGo.AddComponent<ArrowProjectile>(); // RequireComponent pulls in Rigidbody
            var curve = new ImpactDamageCurve(2f, 3f, 30f);

            Assert.AreEqual(1, DamageBridgeInstaller.AttachArrows(new[] { arrow }, curve));
            Assert.AreEqual(0, DamageBridgeInstaller.AttachArrows(new[] { arrow }, curve));

            Assert.IsTrue(arrowGo.TryGetComponent(out ArrowDamageBridge bridge));
            Assert.AreEqual(2f, bridge.Damage.DamagePerSpeed);
        }

        [Test]
        public void Attach_SkipsNullEntries()
        {
            Assert.AreEqual(0, DamageBridgeInstaller.AttachMelee(new MeleeWeapon[] { null }, ImpactDamageCurve.MeleeDefault));
            Assert.AreEqual(0, DamageBridgeInstaller.AttachArrows(new ArrowProjectile[] { null }, ImpactDamageCurve.ArrowDefault));
        }
    }
}
