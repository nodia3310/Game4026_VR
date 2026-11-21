using System.Collections.Generic;
using System.Reflection;
using Game4026.Artifacts;
using Game4026.Contracts;
using Game4026.Interaction.Artifacts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Tests.Interaction.Artifacts
{
    public sealed class ArtifactDisplayTriggerTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private static readonly Vector3 TestOrigin = new Vector3(10000f, 10000f, 10000f);
        private ArtifactDisplayTrigger display;
        private Transform snap;
        private RestorableArtifact artifact;
        private Rigidbody body;
        private XRGrabInteractable grab;

        [SetUp]
        public void SetUp()
        {
            NewObject("Test interaction manager").AddComponent<XRInteractionManager>();
            var displayObject = NewObject("Display");
            displayObject.transform.position = TestOrigin;
            var trigger = displayObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = Vector3.one * 2f;
            display = displayObject.AddComponent<ArtifactDisplayTrigger>();
            snap = NewObject("Snap").transform;
            snap.position = TestOrigin;
            display.Configure("jar-01", ArtifactType.CanopicJar, snap, 0.4f);

            var artifactObject = NewObject("Artifact");
            artifactObject.transform.position = TestOrigin + Vector3.right * 0.05f;
            artifactObject.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
            body = artifactObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            grab = artifactObject.AddComponent<XRGrabInteractable>();
            artifactObject.AddComponent<ArtifactIdentity>().Configure("jar-01", ArtifactType.CanopicJar);
            artifact = artifactObject.AddComponent<RestorableArtifact>();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = objects.Count - 1; i >= 0; i--)
                if (objects[i]) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void SleepingObject_InsideWithoutTriggerCallbacks_RestoresAfterDwell_AndNotifiesOnce()
        {
            var csharpCount = 0;
            var unityCount = 0;
            display.OnArtifactRestored += notification =>
            {
                csharpCount++;
                Assert.That(notification.ArtifactId, Is.EqualTo("jar-01"));
                Assert.That(notification.Type, Is.EqualTo(ArtifactType.CanopicJar));
                Assert.That(artifact.IsRestored, Is.True);
                Assert.That(body.isKinematic, Is.True);
            };
            display.Restored.AddListener(() => unityCount++);
            body.Sleep();

            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.False);
            display.Evaluate(0.2f);
            display.Evaluate(1f);

            Assert.That(display.IsRestored, Is.True);
            Assert.That(grab.enabled, Is.False);
            Assert.That(artifact.transform.parent, Is.EqualTo(snap));
            Assert.That(Vector3.Distance(artifact.transform.position, snap.position), Is.LessThan(0.001f));
            Assert.That(csharpCount, Is.EqualTo(1));
            Assert.That(unityCount, Is.EqualTo(1));
        }

        [TestCase("wrong-id", ArtifactType.CanopicJar)]
        [TestCase("jar-01", ArtifactType.TutankhamunMask)]
        public void MismatchedIdentity_DoesNotRestore(string id, ArtifactType type)
        {
            artifact.Identity.Configure(id, type);
            display.Evaluate(1f);
            Assert.That(display.IsRestored, Is.False);
            Assert.That(artifact.IsRestored, Is.False);
            Assert.That(grab.enabled, Is.True);
        }

        [Test]
        public void ReleaseWhileStillInside_StartsDwell_WithoutAnotherEnterCallback()
        {
            // This fixture simulates XRI's published selection state, not an end-to-end controller input.
            SetSelected(true);
            display.Evaluate(1f);
            Assert.That(display.IsRestored, Is.False);
            SetSelected(false);
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.False);
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.True);
        }

        [Test]
        public void CompoundCollider_OneColliderLeaves_OtherStillInsidePreservesDwell()
        {
            var child = NewObject("Second collider");
            child.transform.SetParent(artifact.transform, false);
            child.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
            Physics.SyncTransforms();
            display.Evaluate(0.2f);
            child.transform.localPosition = Vector3.right * 5f;
            Physics.SyncTransforms();
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.True);
        }

        [Test]
        public void LeavingVolume_ResetsDwell()
        {
            display.Evaluate(0.3f);
            artifact.transform.position = TestOrigin + Vector3.right * 3f;
            Physics.SyncTransforms();
            display.Evaluate(1f);
            artifact.transform.position = TestOrigin;
            Physics.SyncTransforms();
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.False);
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.True);
        }

        [Test]
        public void PositionAndRotationTolerance_BlockUntilSatisfied()
        {
            display.Configure("jar-01", ArtifactType.CanopicJar, snap, 0f, true, 0.02f, true, 10f);
            display.Evaluate(0f);
            Assert.That(display.IsRestored, Is.False);
            artifact.transform.position = TestOrigin;
            artifact.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Physics.SyncTransforms();
            display.Evaluate(0f);
            Assert.That(display.IsRestored, Is.False);
            artifact.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            display.Evaluate(0f);
            Assert.That(display.IsRestored, Is.True);
        }

        [Test]
        public void DisabledTolerance_AllowsAnyPoseInsideVolume()
        {
            display.Configure("jar-01", ArtifactType.CanopicJar, snap, 0f, false, 0f, false, 0f);
            artifact.transform.position = TestOrigin + Vector3.right * 0.7f;
            artifact.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Physics.SyncTransforms();
            display.Evaluate(0f);
            Assert.That(display.IsRestored, Is.True);
        }

        [Test]
        public void DisableAndReenable_ClearsPendingDwell_AndRediscoversExistingOverlap()
        {
            display.Evaluate(0.3f);
            display.enabled = false;
            // Ordinary MonoBehaviours do not receive runtime lifecycle callbacks merely
            // from toggling enabled in EditMode. Simulate those callbacks explicitly here;
            // the PlayMode suite verifies Unity's actual disable/enable dispatch.
            typeof(ArtifactDisplayTrigger).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(display, null);
            display.enabled = true;
            typeof(ArtifactDisplayTrigger).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(display, null);
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.False);
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.True);
        }

        [Test]
        public void DisabledTriggerCollider_ClearsPendingDwell()
        {
            var collider = display.GetComponent<BoxCollider>();
            display.Evaluate(0.3f);
            collider.enabled = false;
            display.Evaluate(1f);
            collider.enabled = true;
            Physics.SyncTransforms();
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.False);
            display.Evaluate(0.2f);
            Assert.That(display.IsRestored, Is.True);
        }

        [Test]
        public void ResetRestoration_RestoresPosePhysicsAndGrab_AndAllowsAnotherNotification()
        {
            var previousPosition = artifact.transform.position;
            var previousScale = artifact.transform.localScale;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            grab.throwOnDetach = true;
            body.linearVelocity = Vector3.right;
            var notifications = 0;
            display.Restored.AddListener(() => notifications++);

            display.Evaluate(0.4f);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousSpeculative));
            Assert.That(grab.throwOnDetach, Is.False);
            display.ResetRestoration();
            Assert.That(display.IsRestored, Is.False);
            Assert.That(artifact.IsRestored, Is.False);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Assert.That(grab.throwOnDetach, Is.True);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.right));
            Assert.That(grab.enabled, Is.True);
            Assert.That(artifact.transform.parent, Is.Null);
            Assert.That(artifact.transform.position, Is.EqualTo(previousPosition));
            Assert.That(artifact.transform.localScale, Is.EqualTo(previousScale));
            Physics.SyncTransforms();
            display.Evaluate(0.4f);
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void OptionalTracker_RejectsWrongPlan_WithoutLeavingObjectLocked()
        {
            display.SetTracker(new ArtifactRestorationTracker(new Dictionary<string, ArtifactType>
            {
                ["different-id"] = ArtifactType.CanopicJar
            }));
            display.Evaluate(1f);
            Assert.That(display.IsRestored, Is.False);
            Assert.That(artifact.IsRestored, Is.False);
            Assert.That(grab.enabled, Is.True);
            Assert.That(body.isKinematic, Is.False);
        }

        [Test]
        public void OptionalTracker_NotifiesAfterPhysicalLock()
        {
            var tracker = new ArtifactRestorationTracker(new Dictionary<string, ArtifactType>
            {
                ["jar-01"] = ArtifactType.CanopicJar
            });
            tracker.OnArtifactRestored += _ => Assert.That(artifact.IsRestored, Is.True);
            display.SetTracker(tracker);
            display.Evaluate(0.4f);
            Assert.That(tracker.IsComplete, Is.True);
            Assert.That(display.IsRestored, Is.True);
        }

        private GameObject NewObject(string name)
        {
            var result = new GameObject(name);
            objects.Add(result);
            return result;
        }

        private void SetSelected(bool selected)
        {
            typeof(XRBaseInteractable).GetProperty("isSelected", BindingFlags.Instance | BindingFlags.Public)
                .GetSetMethod(true).Invoke(grab, new object[] { selected });
        }
    }
}
