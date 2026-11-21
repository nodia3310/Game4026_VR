#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using Game4026.Bridges;
using Game4026.Contracts;
using Game4026.Enemy;
using Game4026.Interaction;
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
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Tests.Interaction
{
    /// <summary>Integration checks load the authored room, not substitute test objects.</summary>
    public sealed class InteractionSceneTests
    {
        const string Path = "Assets/A_VRInteraction/Scenes/InteractionTest.unity";
        InteractionDesktopMode desktop;
        InteractionTestFeedback feedback;
        Scene loadedScene;
        bool previousBackground;

        [UnitySetUp]
        public IEnumerator LoadRoom()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return LoadSavedRoom(Path);
        }

        private IEnumerator LoadSavedRoom(string scenePath)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            loadedScene = SceneManager.GetSceneByPath(scenePath);
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

        static void Pose(PhysicalGrabbable item, Vector3 position, Quaternion? rotation = null)
        {
            var facing = rotation ?? Quaternion.identity;
            item.transform.SetPositionAndRotation(position, facing);
            item.Body.position = position;
            item.Body.rotation = facing;
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        static IEnumerator Settle(int steps = 12)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        void Grab(DesktopTestHand hand, PhysicalGrabbable item, Vector3 position, Quaternion? rotation = null)
        {
            var facing = rotation ?? Quaternion.identity;
            Pose(item, position, facing);
            hand.transform.SetPositionAndRotation(position, facing);
            Assert.That(hand.TryGrab(item.Grab), Is.True, "Actual XRI select should succeed for " + item.name);
        }

        [UnityTest]
        public IEnumerator AuthoredRoom_HasCompleteReferencesAndCorrectCollisionWiring()
        {
            yield return Settle(50);
            var items = Object.FindObjectsByType<PhysicalGrabbable>();
            Assert.That(items.Length, Is.EqualTo(10));
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
            Assert.That(xrRig.GetComponent<PlayerTarget>().Head, Is.SameAs(Field<Camera>(desktop, "xrCamera").transform));
            Assert.That(PlayerTarget.Current.Head, Is.SameAs(desktop.DesktopCamera.transform));
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
            var artifacts = Object.FindObjectsByType<ArtifactIdentity>();
            Assert.That(artifacts.Select(artifact => artifact.Type), Is.EquivalentTo(new[]
                { ArtifactType.RosettaStone, ArtifactType.CanopicJar, ArtifactType.TutankhamunMask }));
            var displays = Object.FindObjectsByType<ArtifactDisplayTrigger>();
            Assert.That(displays.Length, Is.EqualTo(artifacts.Length));
            foreach (var artifact in artifacts)
            {
                Assert.That(artifact.IsValid, Is.True);
                Assert.That(Field<Transform>(DisplayFor(artifact), "snapPoint"), Is.Not.Null);
            }
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
            Assert.That(arrows[0].GetComponent<ArrowDamageBridge>(), Is.Not.Null);
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
            var artifact = Object.FindObjectsByType<RestorableArtifact>()
                .Single(item => item.Identity.Type == ArtifactType.CanopicJar);
            var physical = artifact.GetComponent<PhysicalGrabbable>();
            var display = DisplayFor(artifact.Identity);
            var director = Object.FindAnyObjectByType<EncounterDirector>();
            var hall = new GameObject("Canopic Jar encounter").AddComponent<EncounterController>();
            hall.Configure(null, null, 1, null, ArtifactType.CanopicJar);
            director.Configure(new[] { hall }, null);
            var snap = Field<Transform>(display, "snapPoint");
            Grab(desktop.RightHand, physical, snap.position + Vector3.up * .02f);
            yield return Settle(30);
            Assert.That(display.IsRestored, Is.False, "Being inside while held must not restore.");
            Assert.That(director.Progress.RestoredCount, Is.Zero);
            Assert.That(hall.IsCleared, Is.False);
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
            Assert.That(director.Progress.RestoredCount, Is.EqualTo(1));
            Assert.That(director.Progress.IsRestored(ArtifactType.CanopicJar), Is.True);
            Assert.That(hall.WasForceCleared, Is.True);
        }

        private static ArtifactDisplayTrigger DisplayFor(ArtifactIdentity identity) =>
            Object.FindObjectsByType<ArtifactDisplayTrigger>()
                .Single(display => Field<string>(display, "expectedArtifactId") == identity.ArtifactId);

        private IEnumerator RestoreArtifact(RestorableArtifact artifact, ArtifactDisplayTrigger display)
        {
            var hand = desktop.RightHand;
            var snap = Field<Transform>(display, "snapPoint");
            Grab(hand, artifact.GetComponent<PhysicalGrabbable>(), snap.position + Vector3.up * .02f);
            yield return Settle();
            hand.Release();
            yield return Settle(35);
            Assert.That(display.IsRestored && artifact.IsRestored, Is.True, artifact.Identity.ArtifactId);
        }

        [UnityTest]
        public IEnumerator ThreePhysicalArtifacts_ClearMatchingHalls_AndSpawnThePharaohOnce()
        {
            var director = Object.FindAnyObjectByType<EncounterDirector>();
            var boss = Field<EncounterController>(director, "bossEncounter");
            Assert.That(boss, Is.Not.Null);
            var artifacts = Object.FindObjectsByType<RestorableArtifact>().ToDictionary(artifact => artifact.Identity.Type);
            var types = new[] { ArtifactType.RosettaStone, ArtifactType.CanopicJar, ArtifactType.TutankhamunMask };
            var halls = types.Select(type =>
            {
                var hall = new GameObject(type + " integration hall").AddComponent<EncounterController>();
                hall.Configure(null, null, 1, null, type);
                return hall;
            }).ToArray();
            director.Configure(halls, boss);

            int accepted = 0;
            int bossStarts = 0;
            director.ArtifactAccepted += _ => accepted++;
            director.BossEncounterStarted += () => bossStarts++;
            for (int i = 0; i < types.Length; i++)
            {
                var artifact = artifacts[types[i]];
                var display = DisplayFor(artifact.Identity);
                yield return RestoreArtifact(artifact, display);
                Assert.That(halls[i].WasForceCleared, Is.True);
                Assert.That(halls.Count(hall => hall.IsCleared), Is.EqualTo(i + 1));
                Assert.That(director.Progress.RestoredCount, Is.EqualTo(i + 1));
                Assert.That(bossStarts, Is.EqualTo(i == types.Length - 1 ? 1 : 0));

                if (i == 0)
                {
                    // Physically restore the same ID again; the bridge forwards it but the director deduplicates it.
                    display.ResetRestoration();
                    yield return RestoreArtifact(artifact, display);
                    Assert.That(accepted, Is.EqualTo(1));
                    Assert.That(director.Progress.RestoredCount, Is.EqualTo(1));
                }
            }

            for (int i = 0; i < 150 && boss.SpawnedCount == 0; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(accepted, Is.EqualTo(3));
            Assert.That(bossStarts, Is.EqualTo(1));
            Assert.That(boss.SpawnedCount, Is.EqualTo(1));
            Assert.That(boss.AliveCount, Is.EqualTo(1));
            var pharaoh = Object.FindObjectsByType<PharaohBoss>().Single();
            Assert.That(pharaoh.GetComponent<EnemyController>().isActiveAndEnabled, Is.True);
            Assert.That(pharaoh.GetComponent<EnemyHealth>().Data.name, Is.EqualTo("EnemyData_Pharaoh"));
            TestContext.WriteLine("Three artifact IDs accepted, one duplicate ignored, three linked halls cleared, one Pharaoh spawned.");
        }

        [UnityTest]
        public IEnumerator Museum_PlayModeConnectsWeaponsPlayerAndTheSavedDisplay()
        {
            yield return LoadSavedRoom("Assets/Scenes/Museum.unity");
            var weapons = Object.FindObjectsByType<MeleeWeapon>();
            Assert.That(weapons, Has.Length.EqualTo(4));
            Assert.That(weapons.All(weapon => weapon.GetComponent<MeleeDamageBridge>()), Is.True);
            Assert.That(Object.FindObjectsByType<DamageBridgeInstaller>(), Has.Length.EqualTo(1));

            var rig = Field<GameObject>(desktop, "xrRig");
            var target = rig.GetComponent<PlayerTarget>();
            var health = rig.GetComponent<PlayerHealth>();
            Assert.That(target.Head, Is.SameAs(Field<Camera>(desktop, "xrCamera").transform));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            var desktopReceiver = PlayerTarget.Current.Damageable;
            Assert.That(desktopReceiver, Is.Not.Null);
            desktopReceiver.ApplyPlayerDamage(10f);
            Assert.That(health.CurrentHealth, Is.EqualTo(90f));

            var artifact = Object.FindObjectsByType<RestorableArtifact>().Single();
            yield return RestoreArtifact(artifact, DisplayFor(artifact.Identity));
            var progress = Object.FindAnyObjectByType<EncounterDirector>().Progress;
            Assert.That(progress.RestoredCount, Is.EqualTo(1));
            Assert.That(progress.IsRestored(ArtifactType.CanopicJar), Is.True);
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
                // Let the held weapon establish a slow-motion sample before starting the swing.
                yield return Settle();
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

        [UnityTest]
        public IEnumerator Melee_ActualHeldSweepDamagesDefaultLayerEnemy()
        {
            var melee = Object.FindAnyObjectByType<ReboundingWeapon>().GetComponent<MeleeWeapon>();
            var physical = melee.GetComponent<PhysicalGrabbable>();
            Assert.That(melee.GetComponent<MeleeDamageBridge>(), Is.Not.Null);

            var enemy = new GameObject("Default-layer enemy health");
            enemy.SetActive(false);
            enemy.transform.position = new Vector3(3.2f, 1.7f, 3.4f);
            enemy.AddComponent<BoxCollider>().size = new Vector3(.2f, .8f, .25f);
            var health = enemy.AddComponent<EnemyHealth>();
            health.Initialize(AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Game/Data/Enemies/EnemyData_Default.asset"));
            enemy.SetActive(true);

            var tip = Field<Transform>(melee, "bladeTip").localPosition;
            Grab(desktop.RightHand, physical, new Vector3(2.6f, 1.7f - tip.y, 3.4f - tip.z));
            yield return Settle();
            for (int i = 0; i < 24 && health.CurrentHealth == health.MaxHealth; i++)
            {
                desktop.RightHand.transform.position += Vector3.right * .045f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(health.CurrentHealth, Is.LessThan(health.MaxHealth));
        }

        private static EnemyHealth FindAnubis(out Collider body)
        {
            var enemy = Object.FindObjectsByType<EnemyHealth>()
                .Single(health => health.Data && health.Data.name == "EnemyData_AnubisGuard");
            body = enemy.transform.Find("Body").GetComponent<Collider>();
            Assert.That(body.enabled && body.gameObject.activeInHierarchy, Is.True);
            Assert.That(body.GetComponentInParent<IDamageable>(), Is.SameAs(enemy));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100f));
            return enemy;
        }

        [UnityTest]
        public IEnumerator Anubis_AuthoredAttackDamagesTheXRPlayersHealth()
        {
            var enemy = FindAnubis(out _);
            var xrRig = Field<GameObject>(desktop, "xrRig");
            var xrTarget = xrRig.GetComponent<PlayerTarget>();
            var playerHealth = xrRig.GetComponent<PlayerHealth>();
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f));

            // Disable the desktop adapter to drive the authored XR target and receiver directly.
            desktop.enabled = false;
            yield return null;
            Assert.That(PlayerTarget.Current, Is.SameAs(xrTarget));
            Assert.That(xrTarget.Damageable, Is.SameAs(playerHealth));
            xrTarget.Head.position = enemy.transform.position + enemy.transform.forward * 1.25f + Vector3.up * 1.4f;
            Assert.That(xrTarget.Body, Is.SameAs(xrTarget.Head));
            var attack = enemy.GetComponent<EnemyAttack>();
            var slash = enemy.Data.Attacks.Single(move => move.name == "Anubis_QuickSlash");
            int confirmedHits = 0;
            attack.AttackResolved += (_, _, hit) => confirmedHits += hit ? 1 : 0;
            Assert.That(attack.Begin(enemy.Data, Time.time, slash), Is.True);
            for (int i = 0; i < 120 && attack.Tick(Time.time, Time.fixedDeltaTime, xrTarget); i++)
                yield return new WaitForFixedUpdate();

            Assert.That(confirmedHits, Is.EqualTo(1));
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth - slash.Damage));
            TestContext.WriteLine($"Anubis Quick Slash reduced XR player health to {playerHealth.CurrentHealth:F0} HP.");
        }

        [UnityTest]
        public IEnumerator Anubis_PhysicalArrowHitsTheWeakPoint_AndCooldownBlocksAnImmediateSecondHit()
        {
            yield return Settle();
            var enemy = FindAnubis(out var body);
            var weakCollider = enemy.GetComponentInChildren<WeakPoint>().GetComponent<Collider>();
            var melee = Object.FindObjectsByType<MeleeWeapon>().Single(weapon => weapon.name == "Melee weapon - Knife");
            EnemyDamageResult result = default;
            int hits = 0;
            enemy.Damaged += (_, damage) => { result = damage; hits++; };
            Collider impact = null;
            float expected = 0f;
            var muzzle = weakCollider.bounds.center + new Vector3(0f, .04f, 1.95f);
            yield return ShootArrow(muzzle, (collision, arrowBridge) =>
            {
                impact = collision.collider;
                expected = arrowBridge.Damage.Evaluate(collision.relativeVelocity.magnitude) * enemy.Data.WeakPointMultiplier;
                melee.GetComponent<MeleeDamageBridge>().ApplyHit(new MeleeHit { Collider = body, Point = body.bounds.center, Speed = 3f });
            });
            Assert.That(impact, Is.SameAs(weakCollider), $"The arrow struck {impact?.name ?? "nothing"}.");
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(result.IsWeakPoint, Is.True);
            Assert.That(enemy.Data.WeakPointMultiplier, Is.EqualTo(2.5f));
            Assert.That(result.AppliedDamage, Is.EqualTo(expected).Within(.01f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth - expected).Within(.01f));
            TestContext.WriteLine($"Anubis weak-point arrow damage: {result.AppliedDamage:F2} HP. Immediate melee hit ignored.");
        }

        [UnityTest]
        public IEnumerator Anubis_TakesOneHitFromAHeldKnife()
        {
            var enemy = FindAnubis(out var body);
            var knife = Object.FindObjectsByType<MeleeWeapon>().Single(weapon => weapon.name == "Melee weapon - Knife");
            Assert.That(knife.GetComponent<MeleeDamageBridge>(), Is.Not.Null);

            var torso = body.bounds.center;
            var tip = Field<Transform>(knife, "bladeTip");
            var facing = Quaternion.LookRotation(Vector3.back);
            desktop.DesktopCamera.transform.position = torso + new Vector3(0f, .6f, 1.3f);
            // The desktop adapter normally syncs the XR obstruction origin in its keyboard update.
            // Batch Mode may have no keyboard device, so sync the simulated head explicitly.
            typeof(InteractionDesktopMode).GetMethod("SyncReferences", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(desktop, null);
            var hand = desktop.RightHand;
            // Approach from behind the enemy's daggers, with the knife tip leading the motion.
            var strikeStart = torso + new Vector3(0f, .17f, .88f);
            Grab(hand, knife.GetComponent<PhysicalGrabbable>(), strikeStart - facing * tip.localPosition, facing);
            yield return Settle();

            int hits = 0;
            enemy.Damaged += (_, _) => hits++;
            Collider impact = null;
            knife.Hit += hit => impact = hit.Collider;
            for (int i = 0; i < 24 && hits == 0; i++)
            {
                hand.transform.position += Vector3.back * .055f;
                yield return new WaitForFixedUpdate();
            }
            yield return Settle(20);
            Assert.That(impact, Is.SameAs(body), $"The knife struck {impact?.name ?? "nothing"}.");
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(enemy.MaxHealth - enemy.CurrentHealth, Is.InRange(5f, 40f));
            TestContext.WriteLine($"Anubis knife damage: {enemy.MaxHealth - enemy.CurrentHealth:F2} HP");
        }

        [UnityTest]
        public IEnumerator Anubis_TakesOneHitFromAPhysicalArrow()
        {
            var enemy = FindAnubis(out var body);
            int hits = 0;
            Collider impact = null;
            enemy.Damaged += (_, _) => hits++;
            yield return ShootArrow(body.bounds.center + new Vector3(0f, .33f, 1.95f), (collision, _) => impact = collision.collider);
            Assert.That(impact, Is.SameAs(body), $"The arrow struck {impact?.name ?? "nothing"}.");
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(enemy.MaxHealth - enemy.CurrentHealth, Is.InRange(10f, 35f));
            TestContext.WriteLine($"Anubis arrow damage: {enemy.MaxHealth - enemy.CurrentHealth:F2} HP");
        }

        private IEnumerator ShootArrow(Vector3 muzzlePosition, System.Action<Collision, ArrowDamageBridge> onImpact)
        {
            var bow = Object.FindAnyObjectByType<BowDrawAim>();
            var item = bow.GetComponent<PhysicalGrabbable>();
            var muzzle = Field<Transform>(bow, "arrowOrigin");
            var facing = Quaternion.LookRotation(Vector3.back);
            var rotation = facing * Quaternion.Inverse(Quaternion.Inverse(item.transform.rotation) * muzzle.rotation);
            var position = muzzlePosition - rotation * item.transform.InverseTransformPoint(muzzle.position);
            Pose(item, position, rotation);

            var left = desktop.LeftHand;
            var right = desktop.RightHand;
            var grip = item.Grab.attachTransform;
            left.transform.SetPositionAndRotation(grip.position, grip.rotation);
            Assert.That(left.TryGrab(item.Grab), Is.True);
            yield return Settle();
            right.transform.SetPositionAndRotation(Field<Transform>(bow, "stringRest").position, facing);
            Assert.That(right.TryGrab(Field<XRBaseInteractable>(bow, "stringInteractable")), Is.True);
            for (int i = 0; i < 18; i++)
            {
                right.transform.position -= muzzle.forward * .025f;
                yield return null;
            }
            Assert.That(bow.NormalizedDraw, Is.GreaterThan(.55f));

            void SubscribeImpact(BowShot _)
            {
                var launched = Object.FindObjectsByType<ArrowProjectile>().Single();
                var bridge = launched.GetComponent<ArrowDamageBridge>();
                Assert.That(bridge, Is.Not.Null, "The physical arrow and its bridge exist when Shot is published.");
                launched.Impact += collision => onImpact(collision, bridge);
            }

            bow.Shot += SubscribeImpact;
            right.Release();
            yield return Settle(30);
            var arrow = Object.FindObjectsByType<ArrowProjectile>().Single();
            bow.Shot -= SubscribeImpact;
            Assert.That(arrow.GetComponent<Rigidbody>().isKinematic, Is.True);
        }

        [UnityTest]
        public IEnumerator Anubis_TakesOneOutboundAxeHit_AndTheAxeCanBeCaught()
        {
            var enemy = FindAnubis(out var body);
            var weapon = Object.FindAnyObjectByType<ReboundingWeapon>();
            var axe = weapon.GetComponent<PhysicalGrabbable>();
            var hand = desktop.RightHand;
            Grab(hand, axe, body.bounds.center + new Vector3(0f, .39f, 1.35f), Quaternion.LookRotation(Vector3.back));
            yield return Settle();

            Collider impact = null;
            bool canCatch = false;
            bool? caught = null;
            int hits = 0;
            enemy.Damaged += (_, _) => hits++;
            weapon.Rebounded += collider => impact = collider;
            weapon.CatchWindowOpened += () => canCatch = true;
            weapon.ReturnEnded += result => caught = result;
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            var queueThrow = typeof(ReboundingWeapon).GetMethod("QueueDesktopThrow", privateInstance);
            Assert.That(queueThrow.Invoke(weapon, new object[] { hand, hand.transform.forward * 9f }), Is.True);
            hand.Release();

            for (int i = 0; i < 110 && !canCatch && !caught.HasValue; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(impact, Is.SameAs(body), $"First outbound collision was {impact?.name ?? "none"}.");
            Assert.That(canCatch, Is.True);
            typeof(InteractionDesktopMode).GetMethod("CatchOrGrab", privateInstance).Invoke(desktop, null);
            Assert.That(caught, Is.True);
            Assert.That(hand.IsSelecting(axe.Grab), Is.True);

            yield return Settle(20);
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(80f));
            TestContext.WriteLine($"Anubis axe damage: {enemy.MaxHealth - enemy.CurrentHealth:F2} HP. Axe caught.");
        }

        [UnityTest]
        public IEnumerator Axe_PhysicalThrowReboundsToMovingHand_RequiresNewGripPress()
        {
            var weapon = Object.FindAnyObjectByType<ReboundingWeapon>();
            var axe = weapon.GetComponent<PhysicalGrabbable>();
            var handObject = new GameObject("Simulated XR controller");
            var volume = handObject.AddComponent<SphereCollider>();
            volume.isTrigger = true;
            volume.radius = 0.12f;
            var hand = handObject.AddComponent<XRDirectInteractor>();
            hand.handedness = InteractorHandedness.Right;
            hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;

            var start = new Vector3(0f, 1.65f, 3.5f);
            Pose(axe, start);
            hand.transform.position = start;
            hand.selectInput.QueueManualState(true, 1f);
            hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, axe.Grab);
            yield return Settle();
            Assert.That(hand.IsSelecting(axe.Grab), Is.True);

            Collider hit = null;
            bool canCatch = false;
            weapon.Rebounded += collider => hit = collider;
            weapon.CatchWindowOpened += () => canCatch = true;
            for (int i = 0; i < 10; i++)
            {
                hand.transform.position += Vector3.forward * .14f;
                yield return new WaitForFixedUpdate();
            }
            hand.selectInput.QueueManualState(false, 0f);
            for (int i = 0; i < 55 && hit == null; i++) yield return new WaitForFixedUpdate();
            Assert.That(hit, Is.Not.Null, "XRI release velocity should send the axe into a solid collider.");
            Assert.That(hit.GetComponentInParent<MeleeHitTarget>()?.name, Is.EqualTo("Range compound target"));

            Assert.That(desktop.LeftHand.TryGrab(axe.Grab), Is.False);
            hand.transform.position += Vector3.right * .35f;
            hand.selectInput.QueueManualState(true, 1f);
            float impactHeight = axe.Body.position.y;
            float apex = impactHeight;
            for (int i = 0; i < 45 && !canCatch; i++)
            {
                yield return new WaitForFixedUpdate();
                apex = Mathf.Max(apex, axe.Body.position.y);
            }
            Assert.That(apex - impactHeight, Is.GreaterThan(.4f));
            Assert.That(canCatch, Is.True);
            Assert.That(axe.IsHeld, Is.False, "Holding Grip from an early press cannot catch the axe.");

            hand.selectInput.QueueManualState(false, 0f);
            yield return null;
            hand.selectInput.QueueManualState(true, 1f);
            yield return null;
            yield return null;
            Assert.That(hand.IsSelecting(axe.Grab), Is.True);
            Assert.That(axe.Body.detectCollisions, Is.True);
            Assert.That(axe.Body.isKinematic, Is.False);
            yield return Settle();
            Assert.That(Vector3.Distance(axe.Grab.attachTransform.position, hand.transform.position), Is.LessThan(.1f));
        }

        [UnityTest]
        public IEnumerator Axe_EnemyTriggersApplyOneHit_AndMissDropsTheWeapon()
        {
            var weapon = Object.FindAnyObjectByType<ReboundingWeapon>();
            var axe = weapon.GetComponent<PhysicalGrabbable>();
            var enemy = new GameObject("Compound enemy hitbox").AddComponent<ReboundDamageReceiver>();
            enemy.transform.position = new Vector3(1.5f, 1.5f, 5.7f);
            for (int i = 0; i < 2; i++)
            {
                var hitbox = new GameObject("Enemy trigger").AddComponent<BoxCollider>();
                hitbox.transform.SetParent(enemy.transform, false);
                hitbox.isTrigger = true;
            }

            var hand = desktop.RightHand;
            Grab(hand, axe, new Vector3(1.5f, 1.6f, 4.5f));
            yield return Settle();
            bool? caught = null;
            weapon.ReturnEnded += result => caught = result;
            var queueThrow = typeof(ReboundingWeapon).GetMethod("QueueDesktopThrow", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueThrow.Invoke(weapon, new object[] { hand, Vector3.forward * 9f }), Is.True);
            hand.Release();

            for (int i = 0; i < 110 && !caught.HasValue; i++) yield return new WaitForFixedUpdate();
            Assert.That(caught, Is.EqualTo(false));
            Assert.That(enemy.Hits, Is.EqualTo(1));
            Assert.That(enemy.Damage, Is.EqualTo(20f));
            Assert.That(axe.IsHeld, Is.False);
            Assert.That(weapon.IsInFlight, Is.False);
            Assert.That(axe.Body.detectCollisions, Is.True);
            float dropHeight = axe.Body.position.y;
            yield return Settle(18);
            Assert.That(axe.Body.position.y, Is.LessThan(dropHeight - .2f));
            Assert.That(hand.TryGrab(axe.Grab), Is.True, "A missed axe can be picked up again.");
        }
    }

    public sealed class ReboundDamageReceiver : MonoBehaviour, IDamageable
    {
        public int Hits { get; private set; }
        public float Damage { get; private set; }

        public void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint)
        {
            Hits++;
            Damage += damage;
        }
    }
}
#endif
