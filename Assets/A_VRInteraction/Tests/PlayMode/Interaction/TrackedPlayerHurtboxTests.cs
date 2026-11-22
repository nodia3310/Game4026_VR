#if UNITY_EDITOR && ENABLE_VR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game4026.Contracts;
using Game4026.Enemy;
using Game4026.Interaction;
using Game4026.Interaction.TestScene;
using Game4026.PlayerCombat;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using InputUsages = UnityEngine.InputSystem.CommonUsages;

namespace Game4026.Tests.Interaction
{
    public sealed class TrackedPlayerHurtboxTests
    {
        private const string Root = "Assets/A_VRInteraction/";
        private const int Tracked = (int)(InputTrackingState.Position | InputTrackingState.Rotation);
        private static readonly string[] Postures = { "SideLean", "ForwardBend", "BackLean", "Crouch" };

        private XRSimulatedHMD headset;
        private XRSimulatedController left, right;
        private XRSimulatedControllerState leftState, rightState;
        private XROrigin rig;
        private PlayerTarget target;
        private PlayerHealth health;
        private PlayerDamageVignette vignette;
        private DodgePractice practice;
        private EnemyAttack attack;
        private EnemyData data;
        private AttackDefinition slash;
        private Vector3 authoredEnemyPosition;
        private Quaternion authoredEnemyRotation;
        private bool previousBackground;
        private int resolved;
        private bool legacyHit;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            resolved = 0;
            legacyHit = false;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return LoadVRScene("Scenes/Museum_test.unity");
            vignette = Object.FindAnyObjectByType<PlayerDamageVignette>();
            Assert.That(vignette, Is.Not.Null);
            practice = Object.FindAnyObjectByType<DodgePractice>();
            Assert.That(practice, Is.Not.Null);
            practice.enabled = false;
            headset = InputSystem.AddDevice<XRSimulatedHMD>();
            left = InputSystem.AddDevice<XRSimulatedController>();
            right = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(left, InputUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, InputUsages.RightHand);

            var enemy = Object.FindObjectsByType<EnemyController>()
                .Single(candidate => candidate.GetComponent<EnemyHealth>().Data.name == "EnemyData_AnubisGuard");
            enemy.enabled = false;
            data = enemy.GetComponent<EnemyHealth>().Data;
            attack = enemy.GetComponent<EnemyAttack>();
            slash = data.Attacks.Single(move => move.name == "Anubis_QuickSlash");
            authoredEnemyPosition = attack.transform.position;
            authoredEnemyRotation = attack.transform.rotation;
            attack.transform.SetPositionAndRotation(rig.transform.position + Vector3.forward * 1.4f,
                Quaternion.LookRotation(Vector3.back));
            attack.AttackResolved += (_, _, hit) => { resolved++; legacyHit = hit; };
            yield return Pose(new Vector3(0f, 1.65f, 1.4f));
            var opening = Object.FindAnyObjectByType<MuseumFloorConfirmation>();
            Assert.That(opening, Is.Not.Null);
            yield return Await(() => opening.CanConfirm, "The museum floor check did not settle.");
            InputSystem.QueueStateEvent(right, rightState.WithButton(ControllerButton.PrimaryButton));
            yield return Await(() => opening.IsGameplayReady, "Right A did not begin museum gameplay.");
            InputSystem.QueueStateEvent(right, rightState.WithButton(ControllerButton.PrimaryButton, false));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (headset != null) InputSystem.RemoveDevice(headset);
            if (left != null) InputSystem.RemoveDevice(left);
            if (right != null) InputSystem.RemoveDevice(right);
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Hurtbox test cleanup"));
            if (old.IsValid() && old.isLoaded)
                yield return SceneManager.UnloadSceneAsync(old);
            Application.runInBackground = previousBackground;
        }

        [UnityTest]
        public IEnumerator Anubis_AuthoredAttackDamagesTheXRPlayersHealth()
        {
            var leftHaptics = RecordHaptics(InteractorHandedness.Left);
            var rightHaptics = RecordHaptics(InteractorHandedness.Right);
            float before = health.CurrentHealth;
            BeginWarning();
            Assert.That(vignette.Strength, Is.Zero);
            Assert.That(leftHaptics.Pulses, Is.Empty);
            Assert.That(rightHaptics.Pulses, Is.Empty);
            Strike();
            Assert.That(vignette.Strength, Is.EqualTo(1f));
            Assert.That(leftHaptics.Pulses, Is.EqualTo(new[] { (.45f, .10f, 0f) }));
            Assert.That(rightHaptics.Pulses, Is.EqualTo(leftHaptics.Pulses));
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(before - slash.Damage));
            Assert.That(resolved, Is.EqualTo(1));

            yield return Await(() => vignette.Strength == 0f, "The first hit's vignette did not clear.");
            yield return Pose(new Vector3(0f, 1.02f, 1.4f));
            BeginWarning();
            Strike();
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(before - slash.Damage));
            Assert.That(vignette.Strength, Is.Zero);
            Assert.That(leftHaptics.Pulses, Has.Count.EqualTo(1));
            Assert.That(rightHaptics.Pulses, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DamageVignetteFadesAndRetriggersOnlyWhenHealthDecreases()
        {
            var leftHaptics = RecordHaptics(InteractorHandedness.Left);
            var rightHaptics = RecordHaptics(InteractorHandedness.Right);
            foreach (float damage in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.Epsilon })
                health.ApplyPlayerDamage(damage);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            Assert.That(vignette.Strength, Is.Zero);
            Assert.That(leftHaptics.Pulses, Is.Empty);
            Assert.That(rightHaptics.Pulses, Is.Empty);

            health.ApplyPlayerDamage(8f);
            Assert.That(vignette.Strength, Is.EqualTo(1f));
            Assert.That(leftHaptics.Pulses, Has.Count.EqualTo(1));
            Assert.That(rightHaptics.Pulses, Has.Count.EqualTo(1));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(vignette.Strength, Is.GreaterThan(0f).And.LessThan(1f));
            health.ApplyPlayerDamage(8f);
            Assert.That(vignette.Strength, Is.EqualTo(1f));
            Assert.That(leftHaptics.Pulses, Has.Count.EqualTo(2));
            Assert.That(rightHaptics.Pulses, Has.Count.EqualTo(2));

            health.ResetHealth();
            Assert.That(vignette.Strength, Is.Zero);
            health.ApplyPlayerDamage(health.MaxHealth + 1f);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(vignette.Strength, Is.EqualTo(1f));
            yield return new WaitForSecondsRealtime(.65f);
            health.ApplyPlayerDamage(8f);
            Assert.That(vignette.Strength, Is.Zero);
            Assert.That(leftHaptics.Pulses, Has.Count.EqualTo(3));
            Assert.That(rightHaptics.Pulses, Has.Count.EqualTo(3));

            health.ResetHealth();
            health.ApplyPlayerDamage(8f);
            vignette.enabled = false;
            health.ApplyPlayerDamage(8f);
            Assert.That(vignette.Strength, Is.Zero);
            vignette.enabled = true;
            Assert.That(vignette.Strength, Is.Zero);
            health.ApplyPlayerDamage(8f);
            Assert.That(vignette.Strength, Is.EqualTo(1f));
        }

        [Test]
        public void DamageHapticsPlayOnEitherAvailableController()
        {
            var leftHaptics = RecordHaptics(InteractorHandedness.Left);
            var rightHaptics = RecordHaptics(InteractorHandedness.Right);
            leftHaptics.Available = false;
            health.ApplyPlayerDamage(8f);
            Assert.That(vignette.Strength, Is.EqualTo(1f));
            Assert.That(leftHaptics.Pulses, Is.Empty);
            Assert.That(rightHaptics.Pulses, Has.Count.EqualTo(1));

            leftHaptics.Available = true;
            rightHaptics.Available = false;
            health.ApplyPlayerDamage(8f);
            Assert.That(leftHaptics.Pulses, Has.Count.EqualTo(1));
            Assert.That(rightHaptics.Pulses, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MuseumDemoDamageShowsTheVignetteAndPulsesBothControllers()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Root + "Scenes/Museum_Demo_VR.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            Object.FindAnyObjectByType<InteractionDesktopMode>().enabled = false;
            rig = Object.FindAnyObjectByType<XROrigin>();
            health = Object.FindAnyObjectByType<PlayerHealth>();
            vignette = Object.FindAnyObjectByType<PlayerDamageVignette>();
            var leftHaptics = RecordHaptics(InteractorHandedness.Left);
            var rightHaptics = RecordHaptics(InteractorHandedness.Right);

            health.ApplyPlayerDamage(8f);

            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 8f));
            Assert.That(vignette.Strength, Is.EqualTo(1f));
            Assert.That(leftHaptics.Pulses, Is.EqualTo(new[] { (.45f, .10f, 0f) }));
            Assert.That(rightHaptics.Pulses, Is.EqualTo(leftHaptics.Pulses));
        }

        [UnityTest]
        public IEnumerator Anubis_OverlappingShoulderHitsWhenTheLegacyPointMisses()
        {
            yield return Pose(new Vector3(1.28f, 1.65f, 1.40f));
            BeginWarning();
            Strike();
            yield return null;
            Assert.That(legacyHit, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - slash.Damage));
        }

        [UnityTest]
        public IEnumerator MovingOnlyHandsAndLosingAControllerDoNotDodge()
        {
            BeginWarning();
            var chest = target.Body.position;
            var straight = target.Body.rotation;
            Vector3 midpoint = (leftState.devicePosition + rightState.devicePosition) * .5f;
            Quaternion twist = Quaternion.AngleAxis(45f, Vector3.up);
            leftState.devicePosition = midpoint + twist * (leftState.devicePosition - midpoint);
            rightState.devicePosition = midpoint + twist * (rightState.devicePosition - midpoint);
            QueueHands();
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(target.Body.rotation, straight), Is.GreaterThan(15f));
            Assert.That(Vector3.Distance(target.Body.position, chest), Is.LessThan(.02f));

            leftState.devicePosition += new Vector3(-.9f, .45f, .1f);
            rightState.devicePosition += new Vector3(.9f, -.45f, -.1f);
            rightState.trackingState = 0;
            rightState.isTracked = false;
            QueueHands();
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(target.Body.position, chest), Is.LessThan(.02f));
            Assert.That(Quaternion.Angle(target.Body.rotation, straight), Is.LessThan(2f));
            Strike();
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - slash.Damage));
        }

        [UnityTest]
        public IEnumerator AreaSlamStillHitsACrouchedPlayerInTheHorizontalRadius()
        {
            yield return Pose(new Vector3(0f, 1.02f, 1.4f));
            var slam = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
                "Assets/_Game/Data/Enemies/Attacks/Oracle_Stomp.asset");
            BeginWarning(move: slam);
            Strike();
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - slam.Damage));
        }

        [UnityTest]
        public IEnumerator LossOfHeadTrackingKeepsLastRoomPositionAndRecoversAStandingVolume()
        {
            yield return Pose(new Vector3(.3f, 1.02f, 1.4f));
            var lastEye = target.Head.position;
            InputSystem.QueueStateEvent(headset, new XRSimulatedHMDState());
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(target.Head.position, lastEye), Is.LessThan(.02f));
            yield return new WaitForSecondsRealtime(.24f);
            Assert.That(target.Head.position.y, Is.EqualTo(rig.transform.position.y + 1.65f).Within(.02f));
            Assert.That(target.Head.position.x, Is.EqualTo(lastEye.x).Within(.02f));
            BeginWarning();
            Strike();
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - slash.Damage));

            yield return Pose(new Vector3(.3f, 1.02f, 1.4f));
            BeginWarning();
            Strike();
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - slash.Damage));
        }

        [UnityTest]
        public IEnumerator LeftPrimaryButtonLeavesTheVolumesAtTheTrackedStandingHeight()
        {
            BeginWarning();
            var space = rig.CameraFloorOffsetObject.transform;
            Vector3 floorOffset = space.localPosition;
            Vector3 head = space.InverseTransformPoint(target.Head.position);
            InputSystem.QueueStateEvent(left, leftState.WithButton(ControllerButton.PrimaryButton));
            yield return new WaitForSecondsRealtime(.38f);
            Assert.That(Vector3.Distance(space.localPosition, floorOffset), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(space.InverseTransformPoint(target.Head.position), head), Is.LessThan(.02f));
            Strike();
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - slash.Damage));
        }

        [UnityTest]
        public IEnumerator LeftStickMovesTheVolumesWithTheXROrigin()
        {
            Vector3 oldRoot = rig.transform.position;
            Vector3 oldEye = target.Head.position;
            leftState.primary2DAxis = Vector2.left;
            QueueHands();
            yield return Await(() => Vector3.ProjectOnPlane(rig.transform.position - oldRoot, Vector3.up).magnitude > .12f,
                "XRI did not move the rig in response to the thumbstick.");
            leftState.primary2DAxis = Vector2.zero;
            QueueHands();
            yield return null;
            yield return null;
            Vector3 rootMotion = rig.transform.position - oldRoot;
            Assert.That(Vector3.ProjectOnPlane(rootMotion, Vector3.up).magnitude, Is.GreaterThan(.12f));
            Assert.That((target.Head.position - oldEye - rootMotion).magnitude, Is.LessThan(.03f));
        }

        [UnityTest]
        public IEnumerator ProjectileDamageIsKeptWhenTwoLegacyMeleeHitsAreReevaluatedInTheSameFrame()
        {
            yield return Pose(new Vector3(0f, 1.02f, 1.4f));
            var bolt = new GameObject("Test bolt").AddComponent<EnemyProjectile>();
            Vector3 eye = target.Head.position;
            bolt.Launch(null, eye - Vector3.right * .8f, Vector3.right, 10f, slash.Damage);
            bolt.Step(.08f);
            Assert.That(bolt.IsFlying, Is.False);

            var second = new GameObject("Second attacker").AddComponent<EnemyAttack>();
            second.transform.SetPositionAndRotation(attack.transform.position, attack.transform.rotation);
            target.GetComponent<TrackedPlayerHurtbox>().TrackEnemy(second);
            int secondChecks = 0;
            second.AttackResolved += (_, _, hit) => secondChecks += hit ? 1 : 0;

            BeginWarning(attack);
            BeginWarning(second);
            Strike(attack);
            Assert.That(legacyHit, Is.True, "The old horizontal cone accepts the crouch.");
            Strike(second);
            Assert.That(secondChecks, Is.EqualTo(1));
            yield return null;
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - slash.Damage),
                "The bolt still deals damage, and each melee attack misses.");
        }

        [UnityTest]
        public IEnumerator MuseumPracticeHitsStandingAndLetsPlayersDodgeAfterTheTelegraph(
            [ValueSource(nameof(Postures))] string posture)
        {
            attack.transform.SetPositionAndRotation(authoredEnemyPosition, authoredEnemyRotation);
            yield return Pose(new Vector3(0f, 1.65f, 2.5f));
            int warnings = 0;
            attack.AttackTelegraph += (_, _) => warnings++;
            practice.enabled = true;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(warnings, Is.Zero, "The spawn area outside the station stays quiet.");
            var lane = practice.GetComponentInChildren<LineRenderer>(true);
            Assert.That(lane, Is.Not.Null);
            Assert.That(lane.enabled, Is.False);

            var standing = new Vector3(0f, 1.65f, posture == "BackLean" ? 1.68f : 1.4f);
            yield return Pose(standing);
            yield return Await(() => warnings == 1, "Anubis did not warn when the player approached.");
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(vignette.Strength, Is.Zero);
            Assert.That(lane.enabled, Is.True);
            yield return Await(() => resolved == 1, "The first strike did not resolve.");
            Assert.That(health.CurrentHealth, Is.EqualTo(92f));
            Assert.That(lane.enabled, Is.False);

            yield return Await(() => warnings == 2, "The practice strike did not repeat.");
            float strengthAtRepeatWarning = vignette.Strength;
            var (dodged, pitch, roll) = posture switch
            {
                "SideLean" => (standing + Vector3.right * .38f, 0f, 25f),
                "ForwardBend" => (new Vector3(0f, 1.02f, 1.15f), 63f, 0f),
                "BackLean" => (standing + new Vector3(0f, -.17f, .28f), -40f, 0f),
                "Crouch" => (new Vector3(0f, 1.05f, 1.4f), 0f, 0f),
                _ => throw new System.ArgumentOutOfRangeException(nameof(posture))
            };
            yield return Pose(dodged, pitch, roll);
            yield return Await(() => resolved == 2, "The repeat strike did not resolve.");
            Assert.That(health.CurrentHealth, Is.EqualTo(92f), posture);
            Assert.That(vignette.Strength, Is.LessThanOrEqualTo(strengthAtRepeatWarning),
                $"The {posture} dodge did not retrigger the vignette.");
            yield return null;
            Assert.That(practice.GetComponent<TextMesh>().text, Does.Contain("Player HP: 92"));
            TestContext.WriteLine($"Museum_test: standing 100 → 92 HP; {posture} after the telegraph kept 92 HP.");
        }

        [UnityTest]
        public IEnumerator InteractionTestAndPrefabLibraryLoadWithTheReusablePlayerPrefab()
        {
            foreach (string path in new[] { "Scenes/InteractionTest.unity", "Prefabs/Testing/PrefabLibraryTest.unity" })
            {
                yield return LoadVRScene(path);
                Assert.That(target.Damageable, Is.TypeOf<TrackedPlayerHurtbox>(), path);
                Assert.That(health.CurrentHealth, Is.EqualTo(100f), path);
                Assert.That(target.transform.IsChildOf(rig.transform), Is.True, path);
                Assert.That(rig.GetComponent<MuseumEyeHeight>(), Is.Null, path);
                Assert.That(target.Head, Is.Not.SameAs(target.Body), path);
            }
        }

        private IEnumerator LoadVRScene(string scene)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Root + scene,
                new LoadSceneParameters(LoadSceneMode.Single));
            var desktop = Object.FindAnyObjectByType<InteractionDesktopMode>();
            if (desktop) desktop.enabled = false;
            yield return null;
            rig = Object.FindAnyObjectByType<XROrigin>();
            Assert.That(rig, Is.Not.Null);
            // Supply the floor origin a real OpenXR subsystem would report in the headset.
            typeof(XROrigin).GetProperty(nameof(XROrigin.CurrentTrackingOriginMode))
                .SetValue(rig, TrackingOriginModeFlags.Floor);
            yield return null;
            target = rig.GetComponentInChildren<PlayerTarget>();
            health = rig.GetComponentInChildren<PlayerHealth>();
            Assert.That(target, Is.SameAs(PlayerTarget.Current));
            Assert.That(health, Is.Not.Null);
        }

        private IEnumerator Pose(Vector3 enemyLocalEye, float pitch = 0f, float roll = 0f)
        {
            var space = rig.CameraFloorOffsetObject.transform;
            Vector3 eye = attack.transform.TransformPoint(enemyLocalEye);
            Quaternion look = Quaternion.LookRotation(-attack.transform.forward) * Quaternion.Euler(pitch, 0f, roll);
            Quaternion localLook = Quaternion.Inverse(space.rotation) * look;
            Vector3 localEye = space.InverseTransformPoint(eye);
            InputSystem.QueueStateEvent(headset, new XRSimulatedHMDState
            {
                centerEyePosition = localEye, centerEyeRotation = localLook,
                devicePosition = localEye, deviceRotation = localLook, trackingState = Tracked, isTracked = true
            });
            leftState = HandState(space.InverseTransformPoint(eye + look * new Vector3(-.25f, -.36f, .23f)));
            rightState = HandState(space.InverseTransformPoint(eye + look * new Vector3(.25f, -.36f, .23f)));
            QueueHands();
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(target.Head.position, eye), Is.LessThan(.03f), "HMD drives the target.");
        }

        private static XRSimulatedControllerState HandState(Vector3 position) => new()
        {
            devicePosition = position, deviceRotation = Quaternion.identity, trackingState = Tracked, isTracked = true
        };

        private void QueueHands()
        {
            InputSystem.QueueStateEvent(left, leftState);
            InputSystem.QueueStateEvent(right, rightState);
        }

        private static IEnumerator Await(System.Func<bool> condition, string failure)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        private void BeginWarning(EnemyAttack source = null, AttackDefinition move = null)
        {
            source ??= attack;
            move ??= slash;
            source.ResetState();
            AttackStepTiming timing = default;
            void RecordWarning(EnemyAttack _, AttackStepTiming value) => timing = value;
            source.AttackTelegraph += RecordWarning;
            Assert.That(source.Begin(data, Time.time, move), Is.True);
            source.AttackTelegraph -= RecordWarning;
            Assert.That(timing.HoldDuration, Is.GreaterThan(0f));
            float before = health.CurrentHealth;
            source.Tick(Time.time, timing.HitDelay - .04f, target);
            Assert.That(health.CurrentHealth, Is.EqualTo(before), "The attack is still winding up.");
        }

        private void Strike(EnemyAttack source = null) => (source ?? attack).Tick(Time.time, .05f, target);

        private HapticRecorder RecordHaptics(InteractorHandedness side)
        {
            var hand = rig.GetComponentsInChildren<NearFarInteractor>(true).Single(item => item.handedness == side);
            var haptics = hand.GetComponentInParent<HapticImpulsePlayer>(true);
            Assert.That(haptics, Is.Not.Null);
            var action = haptics.hapticOutput.inputActionReference.action;
            Assert.That(action.bindings.Any(binding => binding.path.Contains($"{{{side}Hand}}")), Is.True);

            var recorder = new HapticRecorder();
            var output = new XRInputHapticImpulseProvider(
                inputSourceMode: XRInputHapticImpulseProvider.InputSourceMode.ObjectReference);
            output.SetObjectReference(recorder);
            haptics.hapticOutput = output;
            return recorder;
        }

        private sealed class HapticRecorder : IXRHapticImpulseProvider, IXRHapticImpulseChannelGroup, IXRHapticImpulseChannel
        {
            public bool Available { get; set; } = true;
            public List<(float amplitude, float duration, float frequency)> Pulses { get; } = new();
            public int channelCount => Available ? 1 : 0;
            public IXRHapticImpulseChannelGroup GetChannelGroup() => this;
            public IXRHapticImpulseChannel GetChannel(int channel = 0) => Available && channel == 0 ? this : null;

            public bool SendHapticImpulse(float amplitude, float duration, float frequency = 0f)
            {
                Pulses.Add((amplitude, duration, frequency));
                return true;
            }
        }
    }
}
#endif
