#if UNITY_EDITOR && ENABLE_VR
using System.Collections;
using System.Linq;
using System.Reflection;
using Game4026.Enemy;
using Game4026.Interaction;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.TestScene;
using NUnit.Framework;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using InputUsages = UnityEngine.InputSystem.CommonUsages;

namespace Game4026.Tests.Interaction
{
    public sealed class MuseumFloorStartupTests
    {
        private const int Tracked = (int)(InputTrackingState.Position | InputTrackingState.Rotation);
        private static readonly Vector3 Eye = new(0f, 1.42f, 0f);
        private static readonly Vector3 RightHand = new(.27f, .97f, .34f);
        private XRSimulatedHMD headset;
        private XRSimulatedController left, right;
        private XRSimulatedControllerState leftState, rightState;
        private MuseumFloorConfirmation opening;
        private InteractionDesktopMode desktop;
        private XROrigin rig;
        private bool previousBackground;
        private Transform Space => rig.CameraFloorOffsetObject.transform;

        [UnitySetUp]
        public IEnumerator LoadMuseum()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            headset = InputSystem.AddDevice<XRSimulatedHMD>();
            left = InputSystem.AddDevice<XRSimulatedController>();
            right = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(left, InputUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, InputUsages.RightHand);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/A_VRInteraction/Scenes/Museum_test.unity", new LoadSceneParameters(LoadSceneMode.Single));
            desktop = Object.FindAnyObjectByType<InteractionDesktopMode>();
            desktop.enabled = false;
            rig = Object.FindAnyObjectByType<XROrigin>();
            opening = Object.FindAnyObjectByType<MuseumFloorConfirmation>();
            Assert.That(opening, Is.Not.Null);
            Assert.That(rig.RequestedTrackingOriginMode, Is.EqualTo(XROrigin.TrackingOriginMode.Floor));
            Assert.That(rig.CameraYOffset, Is.Zero);
            Assert.That(Space.localPosition.y, Is.Zero.Within(.01f));
            Assert.That(rig.GetComponent<MuseumEyeHeight>(), Is.Null);

            // The injected Input System devices report poses but do not create an XRInputSubsystem.
            ReportTrackingOrigin(TrackingOriginModeFlags.Floor);
            leftState = Hand(new Vector3(-.27f, .97f, .34f));
            rightState = Hand(RightHand);
            InputSystem.QueueStateEvent(left, leftState);
            InputSystem.QueueStateEvent(right, rightState);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (headset != null) InputSystem.RemoveDevice(headset);
            if (left != null) InputSystem.RemoveDevice(left);
            if (right != null) InputSystem.RemoveDevice(right);
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Museum floor test cleanup"));
            if (old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
            Application.runInBackground = previousBackground;
        }

        [UnityTest]
        public IEnumerator OpeningLocksTheMuseumUntilRightAAndPreservesThePhysicalPoses()
        {
            var marker = GameObject.Find("Virtual Floor Marker");
            var panel = GameObject.Find("Floor Check Prompt");
            var backpack = Object.FindAnyObjectByType<FieldBackpack>();
            var providers = rig.GetComponentsInChildren<LocomotionProvider>(true);
            var hands = rig.GetComponentsInChildren<NearFarInteractor>(true);
            var rightPose = hands.Single(hand => hand.handedness == InteractorHandedness.Right)
                .GetComponentInParent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            var attack = Object.FindAnyObjectByType<EnemyAttack>();
            int telegraphs = 0;
            attack.AttackTelegraph += (_, _) => telegraphs++;

            Assert.That(marker, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Vector3 spawn = rig.transform.position;
            Vector3 nearAttack = attack.transform.position + attack.transform.forward * 1.25f + Vector3.up * Eye.y;
            var eye = Space.InverseTransformPoint(nearAttack);
            Head(eye);
            leftState.primary2DAxis = Vector2.right;
            InputSystem.QueueStateEvent(left, leftState.WithButton(ControllerButton.GripButton));
            yield return Until(() => opening.CanConfirm);
            Assert.That(opening.IsGameplayReady, Is.False);
            Assert.That(providers.Any(p => p.enabled), Is.False);
            Assert.That(hands.Any(h => h.enabled), Is.False);
            Assert.That(backpack.enabled, Is.False);
            Assert.That(telegraphs, Is.Zero);
            Assert.That(Vector3.Distance(rig.transform.position, spawn), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(Space.InverseTransformPoint(rig.Camera.transform.position), eye), Is.LessThan(.02f));
            Assert.That(Vector3.Distance(Space.InverseTransformPoint(rightPose.transform.position), RightHand), Is.LessThan(.02f));
            AssertPrompt("Press right A");

            leftState.primary2DAxis = Vector2.zero;
            InputSystem.QueueStateEvent(left, leftState.WithButton(ControllerButton.GripButton, false));
            PressRightA(true);
            yield return Until(() => opening.IsGameplayReady, "Right A must confirm the ready XR floor.");
            Assert.That(marker.activeSelf, Is.False);
            Assert.That(panel.activeSelf, Is.False);
            Assert.That(providers.Any(p => p.isActiveAndEnabled), Is.True);
            Assert.That(hands.All(h => h.isActiveAndEnabled), Is.True);
            Assert.That(backpack.isActiveAndEnabled, Is.True);
            Assert.That(Space.localPosition.y, Is.Zero.Within(.01f));
            Assert.That(Vector3.Distance(Space.InverseTransformPoint(rig.Camera.transform.position), eye), Is.LessThan(.02f));
            Assert.That(Vector3.Distance(Space.InverseTransformPoint(rightPose.transform.position), RightHand), Is.LessThan(.02f));
            yield return Until(() => telegraphs == 1);
        }

        [UnityTest]
        public IEnumerator TrackingMustSettleAndAHeldButtonMustBeReleased()
        {
            PressRightA(true);
            yield return null;
            Assert.That(opening.CanConfirm, Is.False);
            Head(Eye);
            yield return Until(() => opening.CanConfirm);
            Assert.That(opening.IsGameplayReady, Is.False);

            InputSystem.QueueStateEvent(headset, new XRSimulatedHMDState());
            yield return Until(() => !opening.CanConfirm);
            AssertPrompt("Wait for position tracking");
            Head(Eye);
            yield return Until(() => opening.CanConfirm);
            Assert.That(opening.IsGameplayReady, Is.False);
            PressRightA(false);
            yield return Until(() => !right.primaryButton.isPressed);
            yield return null;
            PressRightA(true);
            yield return Until(() => opening.IsGameplayReady);
            Assert.That(rig.CameraInOriginSpaceHeight, Is.EqualTo(Eye.y).Within(.02f));
        }

        [UnityTest]
        public IEnumerator UnavailableFloorAndHeightMismatchStayLockedAndExplainRecovery()
        {
            ReportTrackingOrigin(TrackingOriginModeFlags.Device);
            Head(Eye);
            yield return Until(() => PromptText().Contains("Floor tracking unavailable"));
            PressRightA(true);
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(opening.IsGameplayReady || opening.CanConfirm, Is.False);
            AssertPrompt("restart Quest Link");

            ReportTrackingOrigin(TrackingOriginModeFlags.Floor);
            Space.localPosition += Vector3.up * .27f;
            PressRightA(false);
            yield return Until(() => PromptText().Contains("heights differ"));
            Assert.That(opening.IsGameplayReady || opening.CanConfirm, Is.False);
            Assert.That(Space.localPosition.y, Is.EqualTo(.27f).Within(.01f));
            Space.localPosition -= Vector3.up * .27f;
            yield return Until(() => opening.CanConfirm);
            PressRightA(true);
            yield return Until(() => opening.IsGameplayReady);
        }

        [UnityTest]
        public IEnumerator DesktopUsesItsOwnFloorAndTheGateRespectsOtherDisabledBehaviours()
        {
            opening.enabled = false;
            var provider = rig.GetComponentsInChildren<LocomotionProvider>(true).First();
            provider.enabled = false;
            opening.enabled = true;
            desktop.enabled = true;
            typeof(InteractionDesktopMode).GetMethod("EnableDesktop", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(desktop, null);
            yield return Until(() => opening.IsGameplayReady, "Desktop preview must bypass only the physical floor check.");
            yield return null;
            Assert.That(desktop.IsDesktopActive, Is.True);
            Assert.That(desktop.DesktopCamera.transform.localPosition.y, Is.EqualTo(1.65f).Within(.01f));
            TestContext.WriteLine($"Museum desktop camera height over its rig: {desktop.DesktopCamera.transform.localPosition.y:F2} m.");
            Assert.That(GameObject.Find("Virtual Floor Marker"), Is.Null);
            Assert.That(provider.enabled, Is.False);

            desktop.enabled = false;
            yield return Until(() => !opening.IsGameplayReady, "Switching from desktop must reenter the XR floor check.");
            ReportTrackingOrigin(TrackingOriginModeFlags.Floor);
            Head(Eye);
            InputSystem.QueueStateEvent(right, rightState.WithButton(ControllerButton.PrimaryButton, false));
            yield return Until(() => opening.CanConfirm, "Restored XR must accept tracked poses at its floor.");
            PressRightA(true);
            yield return Until(() => opening.IsGameplayReady, "Restored XR must accept a fresh confirmation.");
            Assert.That(provider.enabled, Is.False);
            var backpack = Object.FindAnyObjectByType<FieldBackpack>();
            backpack.enabled = false;
            yield return null;
            Assert.That(backpack.enabled, Is.False);
        }

        private void ReportTrackingOrigin(TrackingOriginModeFlags mode) =>
            typeof(XROrigin).GetProperty(nameof(XROrigin.CurrentTrackingOriginMode)).SetValue(rig, mode);

        private void Head(Vector3 position) => InputSystem.QueueStateEvent(headset, new XRSimulatedHMDState
        {
            centerEyePosition = position, devicePosition = position, centerEyeRotation = Quaternion.identity,
            deviceRotation = Quaternion.identity, trackingState = Tracked, isTracked = true
        });

        private static XRSimulatedControllerState Hand(Vector3 position) => new()
        {
            devicePosition = position, deviceRotation = Quaternion.identity, trackingState = Tracked, isTracked = true
        };

        private void PressRightA(bool pressed) =>
            InputSystem.QueueStateEvent(right, rightState.WithButton(ControllerButton.PrimaryButton, pressed));

        private static TMP_Text Prompt() => GameObject.Find("Floor Check Prompt")
            .GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Instructions");

        private static string PromptText() => Prompt().text;

        private static void AssertPrompt(string expected)
        {
            var prompt = Prompt();
            prompt.ForceMeshUpdate();
            Assert.That(prompt.text, Does.Contain(expected));
            Assert.That(prompt.isTextOverflowing, Is.False, prompt.text);
        }

        private static IEnumerator Until(System.Func<bool> predicate, string failure = null)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, failure);
        }
    }
}
#endif
