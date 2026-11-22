#if UNITY_EDITOR && ENABLE_VR
using System;
using System.Collections;
using System.Linq;
using Game4026.Interaction;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.TestScene;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using Object = UnityEngine.Object;

namespace Game4026.Tests.Interaction
{
    public sealed class MuseumFloorPickupTests
    {
        const int Tracked = (int)(InputTrackingState.Position | InputTrackingState.Rotation);
        XRSimulatedHMD headset;
        XRSimulatedController left, right;
        XRSimulatedControllerState rightState;
        bool previousBackground;

        [UnityTest]
        public IEnumerator PhysicalCrouchAndGripLiftADroppedMuseumFlashlight()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            headset = InputSystem.AddDevice<XRSimulatedHMD>();
            left = InputSystem.AddDevice<XRSimulatedController>();
            right = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/A_VRInteraction/Scenes/Museum_test.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var desktop = Object.FindAnyObjectByType<InteractionDesktopMode>();
            if (desktop) desktop.enabled = false;
            var rig = Object.FindAnyObjectByType<XROrigin>();
            // A simulated HMD supplies device poses but has no OpenXR subsystem to report the floor.
            typeof(XROrigin).GetProperty(nameof(XROrigin.CurrentTrackingOriginMode))
                .SetValue(rig, TrackingOriginModeFlags.Floor);
            var space = rig.CameraFloorOffsetObject.transform;
            var confirmation = Object.FindAnyObjectByType<MuseumFloorConfirmation>();
            Assert.That(confirmation, Is.Not.Null, "Museum_test must include its floor confirmation.");
            var standingEye = Vector3.up * 1.48f;
            SendHead(standingEye);
            InputSystem.QueueStateEvent(left, Hand(new Vector3(-.22f, 1.04f, .25f)));
            rightState = Hand(new Vector3(.22f, 1.04f, .25f));
            InputSystem.QueueStateEvent(right, rightState);
            yield return WaitFor(() => confirmation.CanConfirm, "The tracked floor must be available at startup.");
            InputSystem.QueueStateEvent(right, rightState.WithButton(ControllerButton.PrimaryButton));
            yield return WaitFor(() => confirmation.IsGameplayReady, "Right A must confirm the authored museum floor.");
            InputSystem.QueueStateEvent(right, rightState.WithButton(ControllerButton.PrimaryButton, false));
            yield return null;
            Assert.That(rig.CameraInOriginSpaceHeight, Is.EqualTo(standingEye.y).Within(.03f));

            var flashlight = Object.FindObjectsByType<GrabbableFlashlight>().Single().GetComponent<PhysicalGrabbable>();
            // Drop the existing desk tool above a clear patch of the authored floor at the spawn point.
            var drop = space.TransformPoint(new Vector3(.23f, .70f, .40f));
            Assert.That(Physics.Raycast(drop, Vector3.down, out var floor, 1f, ~0,
                QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(space.InverseTransformPoint(floor.point).y, Is.EqualTo(0f).Within(.06f),
                "This pickup should start on the walkable floor at tracking-space height zero.");
            flashlight.Body.position = drop;
            flashlight.Body.linearVelocity = Vector3.zero;
            flashlight.Body.angularVelocity = Vector3.zero;
            flashlight.Body.WakeUp();
            yield return WaitFor(() => Mathf.Abs(flashlight.Body.position.y - floor.point.y) < .16f &&
                flashlight.Body.linearVelocity.sqrMagnitude < .0025f, "The dropped flashlight must settle on the museum floor.");
            var settledHeight = flashlight.Body.position.y;

            var hand = rig.GetComponentsInChildren<NearFarInteractor>()
                .Single(interactor => interactor.handedness == InteractorHandedness.Right);
            var caster = (InteractionCasterBase)hand.nearInteractionCaster;
            SetGrip(true);
            yield return null;
            yield return null;
            Assert.That(flashlight.IsHeld, Is.False, "A standing hand above the floor item is out of near-grab range.");
            SetGrip(false);
            yield return null;
            yield return null;

            var grip = flashlight.Grab.attachTransform;
            var handToGrip = space.InverseTransformVector(grip.position - caster.effectiveCastOrigin.position);
            rightState.devicePosition += handToGrip;
            InputSystem.QueueStateEvent(right, rightState);
            SendHead(new Vector3(0f, .76f, .20f));
            yield return WaitFor(() => Vector3.Distance(caster.effectiveCastOrigin.position, grip.position) < .05f,
                "Lowering the tracked hand must bring its actual near caster to the resting grip.");
            Assert.That(rig.CameraInOriginSpaceHeight, Is.EqualTo(.76f).Within(.03f));
            SetGrip(true);
            yield return WaitFor(() => flashlight.Grab.interactorsSelecting.Contains(hand),
                "The authored right-hand Grip must pick up the same flashlight from the floor.");

            rightState.devicePosition += Vector3.up * .42f;
            InputSystem.QueueStateEvent(right, rightState);
            yield return WaitFor(() => flashlight.Body.position.y > settledHeight + .25f,
                "The floor item must physically follow the raised hand.");
            SetGrip(false);
            yield return WaitFor(() => !flashlight.IsHeld, "Releasing Grip must release the floor item.");
        }

        void SetGrip(bool pressed)
        {
            rightState.grip = pressed ? 1f : 0f;
            InputSystem.QueueStateEvent(right, rightState.WithButton(ControllerButton.GripButton, pressed));
        }

        void SendHead(Vector3 position)
        {
            InputSystem.QueueStateEvent(headset, new XRSimulatedHMDState
            {
                devicePosition = position, centerEyePosition = position,
                deviceRotation = Quaternion.identity, centerEyeRotation = Quaternion.identity,
                trackingState = Tracked, isTracked = true
            });
        }

        static XRSimulatedControllerState Hand(Vector3 position) => new()
        {
            devicePosition = position, deviceRotation = Quaternion.identity, trackingState = Tracked, isTracked = true
        };

        static IEnumerator WaitFor(Func<bool> ready, string failure)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, failure);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (headset != null) InputSystem.RemoveDevice(headset);
            if (right != null) InputSystem.RemoveDevice(right);
            if (left != null) InputSystem.RemoveDevice(left);
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Museum floor pickup cleanup"));
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Application.runInBackground = previousBackground;
        }
    }
}
#endif
