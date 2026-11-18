#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using Game4026.Interaction.Artifacts;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using Game4026.Interaction.TestScene;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Tests.Interaction
{
    /// <summary>Integration checks load the authored room, not substitute test objects.</summary>
    public sealed class InteractionSceneTests
    {
        const string Path = "Assets/_Game/Scenes/InteractionTest.unity";
        InteractionDesktopMode desktop;
        InteractionTestFeedback feedback;
        Scene loadedScene;
        bool previousBackground;

        [UnitySetUp]
        public IEnumerator LoadRoom()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Path, new LoadSceneParameters(LoadSceneMode.Single));
            loadedScene = SceneManager.GetSceneByPath(Path);
            for (int i = 0; i < 5; i++) yield return null;
            desktop = Object.FindAnyObjectByType<InteractionDesktopMode>();
            Assert.That(desktop, Is.Not.Null);
            // Automation is deterministic even when a headset happens to be connected.
            if (!desktop.IsDesktopActive)
                typeof(InteractionDesktopMode).GetMethod("EnableDesktop", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(desktop, null);
            feedback = Object.FindAnyObjectByType<InteractionTestFeedback>();
            Assert.That(desktop.LeftHand, Is.Not.Null);
            Assert.That(desktop.RightHand, Is.Not.Null);
            Object.FindAnyObjectByType<BowDrawAim>().SetTrackingValid(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadRoom()
        {
            if (desktop)
            {
                desktop.LeftHand?.Release();
                desktop.RightHand?.Release();
            }
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var empty = SceneManager.CreateScene("Interaction scene test cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }
            Application.runInBackground = previousBackground;
            yield return null;
        }

        static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }

        static void Pose(PhysicalGrabbable item, Vector3 position)
        {
            item.transform.SetPositionAndRotation(position, Quaternion.identity);
            item.Body.position = position;
            item.Body.rotation = Quaternion.identity;
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        static IEnumerator Settle(int steps = 12)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        void Grab(DesktopTestHand hand, PhysicalGrabbable item, Vector3 position)
        {
            Pose(item, position);
            hand.transform.SetPositionAndRotation(position, Quaternion.identity);
            Assert.That(hand.TryGrab(item.Grab), Is.True, "Actual XRI select should succeed for " + item.name);
        }

        [UnityTest]
        public IEnumerator AuthoredRoom_HasCompleteReferencesAndCorrectCollisionWiring()
        {
            yield return Settle(50);
            var items = Object.FindObjectsByType<PhysicalGrabbable>();
            Assert.That(items.Length, Is.EqualTo(8));
            foreach (var item in items)
            {
                Assert.That(item.Body, Is.Not.Null);
                Assert.That(item.Body.position.y, Is.GreaterThan(.75f), item.name+" must remain on its authored bench/pedestal.");
                Assert.That(item.Grab.colliders.Count, Is.GreaterThan(0));
                Assert.That(item.Grab.colliders.All(c => c && !c.isTrigger), Is.True);
                Assert.That(item.Grab.movementType, Is.EqualTo(XRBaseInteractable.MovementType.VelocityTracking));
            }
            var bow = Object.FindAnyObjectByType<BowDrawAim>();
            var xrRig = Field<GameObject>(desktop, "xrRig");
            var nearCasters = xrRig.GetComponentsInChildren<Component>(true)
                .Where(c => c && c.GetType().Name == "SphereInteractionCaster").ToArray();
            Assert.That(nearCasters.Length, Is.EqualTo(2));
            foreach (var caster in nearCasters)
            {
                var settings = new SerializedObject(caster);
                Assert.That(settings.FindProperty("m_PhysicsTriggerInteraction").intValue,
                    Is.EqualTo((int)QueryTriggerInteraction.Collide), "A real XR hand must detect the bow-string trigger.");
                int mask = settings.FindProperty("m_PhysicsLayerMask").intValue;
                Assert.That(mask & (1 << LayerMask.NameToLayer("InteractionTestProp")), Is.Not.Zero);
                Assert.That(mask & (1 << LayerMask.NameToLayer("InteractionTestArtifact")), Is.Not.Zero);
            }
            var bowGrab = bow.GetComponent<XRGrabInteractable>();
            var bowString = Field<XRBaseInteractable>(bow, "stringInteractable");
            Assert.That(bowString, Is.Not.Null);
            Assert.That(bowString.GetComponent<Collider>().isTrigger, Is.True);
            Assert.That(bowGrab.colliders.Contains(bowString.GetComponent<Collider>()), Is.False);
            Assert.That(Field<Transform>(bow, "stringRest"), Is.Not.SameAs(Field<Transform>(bow, "stringVisual")));
            Assert.That(Field<ArrowProjectile>(bow, "arrowPrefab"), Is.Not.Null);
            Assert.That(Vector3.Dot(Field<Transform>(bow, "arrowOrigin").forward, bow.transform.forward), Is.GreaterThan(.999f));
            var melee = Object.FindAnyObjectByType<MeleeWeapon>();
            var modelWeapons = Object.FindObjectsByType<MeleeWeapon>();
            Assert.That(modelWeapons.Length, Is.EqualTo(4));
            foreach (var weapon in modelWeapons)
            {
                Assert.That(weapon.transform.Find("Imported Model"), Is.Not.Null, weapon.name);
                Assert.That(weapon.transform.localScale, Is.EqualTo(Vector3.one));
                var length = Vector3.Distance(Field<Transform>(weapon, "bladeBase").position, Field<Transform>(weapon, "bladeTip").position);
                Assert.That(length, Is.GreaterThan(.1f).And.LessThan(.9f));
                Assert.That(weapon.GetComponentsInChildren<Renderer>().Any(r => r is MeshRenderer), Is.True);
                foreach (string endpoint in new[]{"bladeBase","bladeTip"})
                {
                    var point=Field<Transform>(weapon,endpoint).position;
                    float gap=weapon.GetComponentsInChildren<Collider>().Min(c=>Vector3.Distance(c.ClosestPoint(point),point));
                    Assert.That(gap, Is.LessThan(.05f), weapon.name+" strike endpoint must lie on its visible collider geometry.");
                }
            }
            var flashlightModel=Object.FindAnyObjectByType<GrabbableFlashlight>();
            var lensRenderer=flashlightModel.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="flashlight1");
            Assert.That(Vector3.Distance(flashlightModel.Beam.transform.position,lensRenderer.bounds.center),Is.LessThan(.03f),"Beam must originate at the imported lens.");
            foreach (string reference in new[] { "bladeBase", "bladeTip", "rigMotionReference", "obstructionOrigin", "ignoredPlayerRoot" })
                Assert.That(Field<Transform>(melee, reference), Is.Not.Null, reference);
            Assert.That(Field<LayerMask>(melee, "targetMask").value & (1 << LayerMask.NameToLayer("InteractionTestTarget")), Is.Not.Zero);
            Assert.That(Field<LayerMask>(melee, "blockerMask").value & 1, Is.Not.Zero, "Default-layer walls must occlude.");
            var targets = Object.FindObjectsByType<MeleeHitTarget>();
            Assert.That(targets.Length, Is.EqualTo(3));
            Assert.That(targets.All(t => t.GetComponentsInChildren<Collider>().Length > 1), Is.True);
            var artifact = Object.FindAnyObjectByType<ArtifactIdentity>();
            Assert.That(artifact.IsValid, Is.True);
            Assert.That(artifact.ArtifactId, Is.EqualTo("interaction-jar-01"));
            var display = Object.FindAnyObjectByType<ArtifactDisplayTrigger>();
            Assert.That(Field<Transform>(display, "snapPoint"), Is.Not.Null);
            Assert.That(Field<string>(display, "expectedArtifactId"), Is.EqualTo(artifact.ArtifactId));
            foreach (var component in loadedScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)))
            {
                Assert.That(component, Is.Not.Null, "No missing script components.");
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceEntityIdValue != EntityId.None)
                        Assert.That(property.objectReferenceValue, Is.Not.Null, component.name + "/" + property.propertyPath);
            }
            var pipeline = QualitySettings.renderPipeline;
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(pipeline.name, Does.Contain("InteractionTest"));
            Assert.That(pipeline.GetType().GetProperty("additionalLightsRenderingMode").GetValue(pipeline).ToString(), Is.EqualTo("PerPixel"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Flashlight_ActualGrabFollowsHand_ActivateToggles_ReleaseFalls()
        {
            var light = Object.FindAnyObjectByType<GrabbableFlashlight>();
            var physical = light.GetComponent<PhysicalGrabbable>();
            var hand = desktop.RightHand;
            Grab(hand, physical, new Vector3(1.5f, 1.7f, 4f));
            yield return Settle();
            float initialX = physical.Body.position.x;
            for (int i = 0; i < 15; i++)
            {
                hand.transform.position += Vector3.right * .012f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(physical.Body.position.x, Is.GreaterThan(initialX + .06f));
            bool power = light.IsOn;
            hand.ActivateSelected();
            Assert.That(light.IsOn, Is.EqualTo(!power));
            Assert.That(light.Beam.enabled, Is.EqualTo(light.IsOn));
            hand.ActivateSelected();
            Assert.That(light.IsOn, Is.EqualTo(power));
            hand.Release();
            Assert.That(physical.IsHeld, Is.False);
            float height = physical.Body.position.y;
            yield return Settle(40);
            Assert.That(physical.Body.position.y, Is.LessThan(height - .3f));
            Assert.That(physical.Body.position.y, Is.GreaterThan(0f), "The saved room floor catches the released body.");
        }

        [UnityTest]
        public IEnumerator Ray_ActualHeldActivateHitsSavedTarget_AndStopsAtSavedWall()
        {
            var ray = Object.FindAnyObjectByType<RayAim>();
            Grab(desktop.RightHand, ray.GetComponent<PhysicalGrabbable>(), new Vector3(0f, 1.6f, 4.5f));
            yield return Settle();
            desktop.RightHand.ActivateSelected();
            Assert.That(ray.LastAim.HasHit, Is.True);
            Assert.That(ray.LastAim.Collider.GetComponentInParent<MeleeHitTarget>().name, Is.EqualTo("Range compound target"));
            Assert.That(feedback.RayShotCount, Is.EqualTo(1));
            desktop.RightHand.Release();
            Grab(desktop.RightHand, ray.GetComponent<PhysicalGrabbable>(), new Vector3(-4.3f, 1.3f, 3.4f));
            yield return Settle();
            desktop.RightHand.ActivateSelected();
            Assert.That(ray.LastAim.Collider.name, Is.EqualTo("Occlusion test wall"));
            Assert.That(feedback.RayShotCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Bow_TwoActualHandsPullAndRelease_EmitsOnePhysicalArrow()
        {
            var bow = Object.FindAnyObjectByType<BowDrawAim>();
            Grab(desktop.LeftHand, bow.GetComponent<PhysicalGrabbable>(), new Vector3(0f, 1.7f, 3f));
            yield return Settle();
            var rest = Field<Transform>(bow, "stringRest");
            var bowString = Field<XRBaseInteractable>(bow, "stringInteractable");
            desktop.RightHand.transform.SetPositionAndRotation(rest.position, Quaternion.identity);
            Assert.That(desktop.RightHand.TryGrab(bowString), Is.True);
            Assert.That(bow.IsDrawing, Is.True);
            for (int i = 0; i < 18; i++)
            {
                desktop.RightHand.transform.position -= Vector3.forward * .025f;
                yield return null;
            }
            Assert.That(bow.NormalizedDraw, Is.GreaterThan(.55f));
            desktop.RightHand.Release();
            yield return null;
            yield return null;
            Assert.That(feedback.BowShotCount, Is.EqualTo(1));
            var arrows = Object.FindObjectsByType<ArrowProjectile>();
            Assert.That(arrows.Length, Is.EqualTo(1));
            var projectileBody = arrows[0].GetComponent<Rigidbody>();
            Assert.That(projectileBody, Is.Not.Null);
            Assert.That(projectileBody.useGravity, Is.True);
            yield return Settle(30);
            Assert.That(projectileBody.isKinematic, Is.True, "The launched physical arrow should hit the saved range target and freeze.");
            Assert.That(feedback.BowShotCount, Is.EqualTo(1), "Release must not emit duplicates.");
        }

        [UnityTest]
        public IEnumerator Artifact_HeldInsideDoesNotRestore_ReleasedInsideLocksOnce()
        {
            var artifact = Object.FindAnyObjectByType<RestorableArtifact>();
            var physical = artifact.GetComponent<PhysicalGrabbable>();
            var display = Object.FindAnyObjectByType<ArtifactDisplayTrigger>();
            var snap = Field<Transform>(display, "snapPoint");
            Grab(desktop.RightHand, physical, snap.position + Vector3.up * .02f);
            yield return Settle(30);
            Assert.That(display.IsRestored, Is.False, "Being inside while held must not restore.");
            desktop.RightHand.Release();
            yield return Settle(40);
            Assert.That(display.IsRestored, Is.True);
            Assert.That(artifact.IsRestored, Is.True);
            Assert.That(physical.Body.isKinematic, Is.True);
            Assert.That(physical.Grab.enabled, Is.False);
            Assert.That(Vector3.Distance(artifact.transform.position, snap.position), Is.LessThan(.001f));
            Assert.That(artifact.transform.parent, Is.SameAs(snap));
            Assert.That(feedback.RestorationCount, Is.EqualTo(1));
            yield return Settle(30);
            Assert.That(feedback.RestorationCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Melee_ActualHeldSweepHitsCompoundTargetOnce_WithPhysicalEstimates()
        {
            foreach (var melee in Object.FindObjectsByType<MeleeWeapon>().OrderBy(w => w.name))
            {
                var physical = melee.GetComponent<PhysicalGrabbable>();
                var midpoint = (Field<Transform>(melee,"bladeBase").localPosition + Field<Transform>(melee,"bladeTip").localPosition) * .5f;
                var tip = Field<Transform>(melee,"bladeTip").localPosition;
                int before = feedback.MeleeHitCount;
                // Sweep the tip across the target's front face. Placing the blade midpoint
                // at the face embeds the longer head in the solid target and tests repeated rebounds.
                Grab(desktop.RightHand, physical, new Vector3(-3.5f, 1.3f-midpoint.y, 1.91f-tip.z));
                yield return Settle();
                melee.ResetSampling();
                for (int i = 0; i < 19; i++)
                {
                    desktop.RightHand.transform.position += Vector3.right * .045f;
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(feedback.MeleeHitCount, Is.EqualTo(before+1), melee.name+": one notification per swing against the compound target.");
                Assert.That(feedback.LastSpeed, Is.GreaterThan(1.2f), melee.name);
                Assert.That(feedback.LastEnergy, Is.GreaterThan(0f), melee.name);
                Assert.That(feedback.LastForce, Is.GreaterThan(0f), melee.name);
                Assert.That(melee.QueryOverflowCount, Is.Zero);
                desktop.RightHand.Release();
                yield return null;
                Pose(physical,new Vector3(-5f,1f,7f));
            }
        }
    }
}
#endif
