using System.Collections;
using System.Collections.Generic;
using Game4026.Contracts;
using Game4026.Interaction.Artifacts;
using Game4026.Interaction.Grabbing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Tests.Interaction
{
    public class InteractionTestHand : XRBaseInteractor
    {
        public IXRInteractable Target;
        public bool Selecting = true;
        public override bool isSelectActive => Selecting;
        public override void GetValidTargets(List<IXRInteractable> targets)
        {
            targets.Clear();
            if (Target != null) targets.Add(Target);
        }
    }

    public class InteractionPhysicsTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private bool originalBackground;
        private static readonly Vector3 Location = new Vector3(1000f, 1000f, 1000f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.Destroy(objects[i]);
            objects.Clear();
            Application.runInBackground = originalBackground;
            yield return null;
        }

        private GameObject Make(string name, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.position = Location + offset;
            objects.Add(go);
            return go;
        }

        [UnityTest]
        public IEnumerator Grab_FollowsHand_ThenReleaseFallsAndCollidesWithFloor()
        {
            var manager = Make("Test manager", Vector3.zero).AddComponent<XRInteractionManager>();
            var floor = Make("Test floor", new Vector3(0f, -0.1f, 0f));
            floor.AddComponent<BoxCollider>().size = new Vector3(5f, 0.2f, 5f);
            var item = Make("Test item", Vector3.up);
            item.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
            var physical = item.AddComponent<PhysicalGrabbable>();
            physical.Grab.interactionManager = manager;
            var hand = Make("Test hand", Vector3.up).AddComponent<InteractionTestHand>();
            hand.interactionManager = manager;
            hand.Target = physical.Grab;
            var grabbed = 0;
            var released = 0;
            physical.Grabbed += _ => grabbed++;
            physical.Released += _ => released++;
            manager.SelectEnter((IXRSelectInteractor)hand, physical.Grab);
            Assert.That(physical.IsHeld, Is.True);
            Assert.That(physical.ApplyPhysicsSettings(), Is.False);
            for (var i = 0; i < 15; i++)
            {
                hand.transform.position += Vector3.right * 0.01f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(item.transform.position.x, Is.GreaterThan(Location.x + 0.03f));
            hand.Selecting = false;
            manager.SelectExit((IXRSelectInteractor)hand, physical.Grab);
            Assert.That(physical.IsHeld, Is.False);
            Assert.That(grabbed, Is.EqualTo(1));
            Assert.That(released, Is.EqualTo(1));
            var height = item.transform.position.y;
            for (var i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            Assert.That(item.transform.position.y, Is.LessThan(height - 0.2f));
            Assert.That(item.transform.position.y, Is.GreaterThan(Location.y - 0.03f));
        }

        [UnityTest]
        public IEnumerator Display_AutomaticallyRestoresSleepingCompoundArtifact_Once()
        {
            Make("Artifact test manager", Vector3.zero).AddComponent<XRInteractionManager>();
            var item = Make("Sleeping artifact", Vector3.zero);
            item.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
            var secondCollider = Make("Compound collider", Vector3.zero);
            secondCollider.transform.SetParent(item.transform, true);
            secondCollider.AddComponent<SphereCollider>().radius = 0.07f;
            var artifact = item.AddComponent<RestorableArtifact>();
            artifact.Identity.Configure("jar-physics", ArtifactType.CanopicJar);
            var body = item.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.Sleep();
            var display = Make("Display trigger", Vector3.zero);
            display.AddComponent<BoxCollider>().isTrigger = true;
            var socket = display.AddComponent<ArtifactDisplayTrigger>();
            socket.Configure("jar-physics", ArtifactType.CanopicJar, display.transform, 0.4f);
            var notifications = 0;
            socket.OnArtifactRestored += _ => notifications++;
            for (var i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            Assert.That(socket.IsRestored, Is.False);
            socket.enabled = false;
            yield return null;
            socket.enabled = true;
            for (var i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(socket.IsRestored, Is.False, "Dwell must start again after enabling the trigger.");
            for (var i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            Assert.That(socket.IsRestored, Is.True);
            Assert.That(artifact.IsRestored, Is.True);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(item.GetComponent<XRGrabInteractable>().enabled, Is.False);
            Assert.That(notifications, Is.EqualTo(1));
        }
    }
}
