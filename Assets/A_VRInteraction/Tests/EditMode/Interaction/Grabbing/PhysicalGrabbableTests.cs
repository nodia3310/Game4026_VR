using Game4026.Interaction.Grabbing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Tests.Interaction
{
    public class PhysicalGrabbableTests
    {
        private GameObject item;

        [TearDown]
        public void TearDown()
        {
            if (item != null) Object.DestroyImmediate(item);
        }

        [Test]
        public void Configure_UsesDynamicPhysicsAndThrowing_ForToolOrArtifact()
        {
            item = new GameObject("Grabbable test");
            item.AddComponent<BoxCollider>();
            var physical = item.AddComponent<PhysicalGrabbable>();
            Assert.That(physical.ApplyPhysicsSettings(), Is.True);
            Assert.That(physical.Body.isKinematic, Is.False);
            Assert.That(physical.Body.useGravity, Is.True);
            Assert.That(physical.Body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Assert.That(physical.Grab.movementType, Is.EqualTo(XRBaseInteractable.MovementType.VelocityTracking));
            Assert.That(physical.Grab.throwOnDetach, Is.True);
            Assert.That(physical.Grab.trackScale, Is.False);
            Assert.That(physical.Grab.addDefaultGrabTransformers, Is.True);
        }

        [Test]
        public void Flashlight_CanSwitchAndReplaceBeamWithoutLosingPowerState()
        {
            item = new GameObject("Flashlight test");
            item.AddComponent<BoxCollider>();
            var flashlight = item.AddComponent<GrabbableFlashlight>();
            var light = item.AddComponent<Light>();
            flashlight.Beam = light;
            flashlight.SetPower(false);
            Assert.That(light.enabled, Is.False);
            flashlight.Toggle();
            Assert.That(flashlight.IsOn, Is.True);
            Assert.That(light.enabled, Is.True);
            flashlight.SetPower(true);
            Assert.That(light.enabled, Is.True);
        }
    }
}
