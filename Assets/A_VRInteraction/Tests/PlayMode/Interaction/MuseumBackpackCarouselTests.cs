#if UNITY_EDITOR && ENABLE_VR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game4026.Enemy;
using Game4026.Interaction;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.TestScene;
using Game4026.PlayerCombat;
using NUnit.Framework;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using InputUsages = UnityEngine.InputSystem.CommonUsages;
using Object = UnityEngine.Object;

namespace Game4026.Tests.Interaction
{
    public sealed class MuseumBackpackCarouselTests
    {
        private const string DemoScene = "Assets/A_VRInteraction/Scenes/Museum_Demo_VR.unity";
        private const int Tracked = (int)(InputTrackingState.Position | InputTrackingState.Rotation);
        private static readonly Vector3 Eye = Vector3.up * 1.65f;

        private XRSimulatedHMD headset;
        private XRSimulatedController leftDevice, rightDevice;
        private XRSimulatedControllerState leftState, rightState;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior previousInputBackgroundBehavior;
        private bool changedInputSettings;
        private Keyboard keyboard;
        private Mouse mouse;
        private XROrigin rig;
        private InteractionDesktopMode desktop;
        private FieldBackpack backpack;
        private MuseumFloorConfirmation floor;
        private NearFarInteractor leftHand, rightHand;
        private PhysicalGrabbable flashlight, crowbar, axe;
        private bool previousBackground;
        private Transform Space => rig.CameraFloorOffsetObject.transform;

        [UnitySetUp]
        public IEnumerator LoadDemo()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(DemoScene,
                new LoadSceneParameters(LoadSceneMode.Single));

            desktop = Object.FindAnyObjectByType<InteractionDesktopMode>();
            backpack = Object.FindAnyObjectByType<FieldBackpack>();
            floor = Object.FindAnyObjectByType<MuseumFloorConfirmation>();
            rig = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            Assert.That(backpack, Is.Not.Null);
            Assert.That(floor, Is.Not.Null);
            var flow = Object.FindAnyObjectByType<DemoFlow>();
            // Keep the playable scene's restart wiring without starting combat during the interaction test.
            flow.Configure(flow.HallEncounter, flow.BossEncounter, float.MaxValue, 4f, 4f);
            flashlight = Object.FindObjectsByType<PhysicalGrabbable>().Single(item => item.name == "Flashlight");
            crowbar = Object.FindObjectsByType<PhysicalGrabbable>().Single(item => item.name == "Melee weapon - Crowbar");
            axe = Object.FindObjectsByType<PhysicalGrabbable>().Single(item => item.name == "Axe");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (headset != null) InputSystem.RemoveDevice(headset);
            if (leftDevice != null) InputSystem.RemoveDevice(leftDevice);
            if (rightDevice != null) InputSystem.RemoveDevice(rightDevice);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (changedInputSettings)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
                InputSystem.settings.backgroundBehavior = previousInputBackgroundBehavior;
                changedInputSettings = false;
            }
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Museum carousel test cleanup"));
            if (scene.path == DemoScene && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Application.runInBackground = previousBackground;
        }

        [UnityTest]
        public IEnumerator QuestLinkNeckHeightReachSurvivesAnInterruptedPositionSample()
        {
            yield return StartTrackedPlay();
            yield return WithdrawBothHands();
            // Relative hand positions recorded during a two-handed Quest Link attempt.
            var head = rig.Camera.transform;
            var yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(head.forward, Vector3.up));
            yield return MoveHand(leftHand, head.position + yaw * new Vector3(-.122f, -.089f, -.213f));
            rightState.trackingState = (int)InputTrackingState.Rotation;
            InputSystem.QueueStateEvent(rightDevice, rightState);
            yield return null;
            yield return null;
            Assert.That(backpack.IsOpen, Is.False, "An unlocated right hand cannot complete the gesture.");

            rightState.trackingState = Tracked;
            yield return MoveHand(rightHand, head.position + yaw * new Vector3(.085f, -.081f, -.236f));
            yield return Until(() => backpack.IsOpen, "Reacquiring the right hand at neck height should complete the reach.");
            yield return null;
            Assert.That(backpack.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator ShoulderStowAndOpenCarouselPreserveToolsThroughGripAndRestart()
        {
            yield return StartTrackedPlay();

            yield return PickUpWithRightGrip(flashlight, "front drop");
            SetGrip(rightHand, false);
            yield return Until(() => !flashlight.IsHeld, "Releasing in front must drop the flashlight.");
            yield return null;
            Assert.That(backpack.StoredCount, Is.Zero);
            Assert.That(flashlight.gameObject.activeSelf, Is.True);

            yield return PickUpWithRightGrip(flashlight, "single-shoulder drop");
            yield return MoveHand(rightHand, Shoulder(false));
            SetGrip(rightHand, false);
            yield return Until(() => !flashlight.IsHeld, "Releasing behind just one shoulder must drop the flashlight.");
            yield return null;
            Assert.That(backpack.StoredCount, Is.Zero);
            Assert.That(flashlight.gameObject.activeSelf, Is.True);

            yield return StoreAtShoulders(flashlight, 1);
            Assert.That(flashlight.gameObject.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(backpack.IsOpen, Is.False, "Releasing the tool during the gesture must not also open the pack.");

            yield return WithdrawBothHands();
            yield return MoveHand(rightHand, Shoulder(false));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(backpack.IsOpen, Is.False, "One empty hand at its shoulder must not open the pack.");
            yield return MoveHand(leftHand, Shoulder(true));
            yield return Until(() => backpack.IsOpen, "The other empty hand joining at its shoulder must open the pack.");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(backpack.IsOpen, Is.True, "Holding both hands in place must leave the pack open.");
            Assert.That(backpack.SelectedItem, Is.SameAs(flashlight));

            yield return PickUpWithRightGrip(crowbar, "open carousel drop");
            yield return MoveHand(rightHand, FocusGrip().colliders[0].bounds.center);
            SetGrip(rightHand, false);
            yield return Until(() => !crowbar.IsHeld, "Releasing at the open carousel must drop the crowbar.");
            yield return null;
            Assert.That(backpack.StoredCount, Is.EqualTo(1));
            Assert.That(crowbar.gameObject.activeSelf, Is.True);

            yield return StoreAtShoulders(crowbar, 2);
            yield return StoreAtShoulders(axe, 3);
            Assert.That(backpack.IsOpen, Is.True, "Shoulder storage must preserve an open pack.");
            Assert.That(crowbar.gameObject.activeSelf, Is.False);
            Assert.That(axe.gameObject.activeSelf, Is.False);
            Assert.That(backpack.SelectedItem, Is.SameAs(axe));

            yield return WithdrawBothHands();
            yield return MoveHand(leftHand, Shoulder(true));
            yield return MoveHand(rightHand, Shoulder(false));
            yield return Until(() => !backpack.IsOpen, "Returning both empty hands to their shoulders must close the pack.");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(backpack.IsOpen, Is.False, "Holding both hands in place must leave the pack closed.");
            Assert.That(backpack.StoredCount, Is.EqualTo(3));
            yield return WithdrawBothHands();
            yield return MoveHand(leftHand, Shoulder(true));
            yield return MoveHand(rightHand, Shoulder(false));
            yield return Until(() => backpack.IsOpen, "The two-hand gesture must reopen the pack with its stored tools.");

            var view = backpack.GetComponentInChildren<BackpackCarouselView>();
            Assert.That(view, Is.Not.Null);
            AssertPreview(view, flashlight);
            AssertPreview(view, crowbar);
            AssertPreview(view, axe);

            var focus = FocusGrip();
            var center = focus.colliders[0].bounds.center;
            var swipe = rig.Camera.transform.right * .12f;
            yield return MoveHand(rightHand, Front(false));
            yield return SwipeLeftHand(center + swipe, center - swipe, flashlight);
            yield return SwipeLeftHand(center - swipe, center + swipe, axe);
            yield return SwipeLeftHand(center + swipe, center - swipe, flashlight);
            yield return SwipeLeftHand(center + swipe, center - swipe, crowbar);

            yield return MoveHand(leftHand, center);
            SetGrip(leftHand, true);
            yield return Until(() => leftHand.IsSelecting(crowbar.Grab),
                "Gripping the focus must equip the original selected tool into the left hand.");
            Assert.That(backpack.IsOpen, Is.False, "Equipping the crowbar must dismiss the floating panel.");
            Assert.That(backpack.StoredCount, Is.EqualTo(2));
            Assert.That(crowbar.gameObject.activeSelf, Is.True);

            Object.FindAnyObjectByType<TrackedPlayerHurtbox>().ResetHealth();
            yield return Until(() => !backpack.IsOpen && backpack.StoredCount == 0 && !leftHand.hasSelection,
                "The scene restart must close the backpack, empty its contents, and release the equipped tool.");
            Assert.That(backpack.SelectedItem, Is.Null);
            Assert.That(new[] { flashlight, crowbar, axe }.All(item => item.gameObject.activeSelf), Is.True);
        }

        [UnityTest]
        public IEnumerator DesktopKeyboardAndWheelCycleTheStoredTools()
        {
            // Editor batch runs have no focused Game View. Route the injected events to gameplay.
            var inputSettings = InputSystem.settings;
            previousEditorInputBehavior = inputSettings.editorInputBehaviorInPlayMode;
            previousInputBackgroundBehavior = inputSettings.backgroundBehavior;
            changedInputSettings = true;
            inputSettings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            typeof(InteractionDesktopMode).GetMethod("EnableDesktop", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(desktop, null);
            yield return Until(() => floor.IsGameplayReady, "Desktop mode must enter demo gameplay.");
            yield return Press(Key.B);
            Assert.That(backpack.IsOpen, Is.True);
            var view = backpack.GetComponentInChildren<BackpackCarouselView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<MeshFilter>()
                .Where(filter => filter.GetComponentInParent<TMP_Text>() == null), Is.Empty);

            yield return Press(Key.B);
            Assert.That(backpack.IsOpen, Is.False);
            Assert.That(desktop.RightHand.TryGrab(flashlight.Grab), Is.True);
            Assert.That(desktop.LeftHand.TryGrab(crowbar.Grab), Is.True);
            yield return Press(Key.B);
            Assert.That(backpack.IsOpen, Is.False, "Using desktop B to stow tools must leave a closed pack closed.");
            Assert.That(backpack.StoredCount, Is.EqualTo(2), "Desktop B must release and store tools in both hands.");
            Assert.That(desktop.RightHand.hasSelection || desktop.LeftHand.hasSelection, Is.False);
            yield return Press(Key.B);
            Assert.That(backpack.IsOpen, Is.True, "Desktop B must open the stored contents when both hands are empty.");

            var start = backpack.SelectedItem;
            yield return Press(Key.RightBracket);
            Assert.That(backpack.SelectedItem, Is.Not.SameAs(start));
            yield return Press(Key.LeftBracket);
            Assert.That(backpack.SelectedItem, Is.SameAs(start));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f) });
            yield return Until(() => backpack.SelectedItem != start, "Mouse wheel must cycle the carousel.");
            var equipped = backpack.SelectedItem;
            var hand = desktop.ActiveHand;
            hand.transform.position = FocusGrip().transform.position;
            yield return Press(Key.Q);
            Assert.That(hand.IsSelecting(equipped.Grab), Is.True);
            Assert.That(backpack.IsOpen, Is.False);
            Assert.That(backpack.StoredCount, Is.EqualTo(1));
            yield return Press(Key.Q);
            yield return Press(Key.B);
            Assert.That(backpack.IsOpen, Is.True);
            Assert.That(backpack.SelectedItem, Is.SameAs(start), "Reopening must show the remaining tool.");
        }

        private IEnumerator StartTrackedPlay()
        {
            desktop.enabled = false;
            headset = InputSystem.AddDevice<XRSimulatedHMD>();
            leftDevice = InputSystem.AddDevice<XRSimulatedController>();
            rightDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(leftDevice, InputUsages.LeftHand);
            InputSystem.SetDeviceUsage(rightDevice, InputUsages.RightHand);
            // The simulated devices report tracked poses without providing an XRInputSubsystem.
            typeof(XROrigin).GetProperty(nameof(XROrigin.CurrentTrackingOriginMode))
                .SetValue(rig, TrackingOriginModeFlags.Floor);
            var hands = rig.GetComponentsInChildren<NearFarInteractor>(true);
            leftHand = hands.Single(hand => hand.handedness == InteractorHandedness.Left);
            rightHand = hands.Single(hand => hand.handedness == InteractorHandedness.Right);
            InputSystem.QueueStateEvent(headset, new XRSimulatedHMDState
            {
                devicePosition = Eye, centerEyePosition = Eye,
                deviceRotation = Quaternion.identity, centerEyeRotation = Quaternion.identity,
                trackingState = Tracked, isTracked = true
            });
            leftState = HandState(new Vector3(-.28f, 1.14f, .35f));
            rightState = HandState(new Vector3(.28f, 1.14f, .35f));
            InputSystem.QueueStateEvent(leftDevice, leftState);
            InputSystem.QueueStateEvent(rightDevice, rightState);
            yield return Until(() => floor.CanConfirm, "The authored XR floor check must accept tracked poses.");
            InputSystem.QueueStateEvent(rightDevice, rightState.WithButton(ControllerButton.PrimaryButton));
            yield return Until(() => floor.IsGameplayReady, "Right A must confirm the XR floor.");
            InputSystem.QueueStateEvent(rightDevice, rightState.WithButton(ControllerButton.PrimaryButton, false));
            yield return null;
            yield return null;
        }

        private IEnumerator StoreAtShoulders(PhysicalGrabbable item, int count)
        {
            yield return WithdrawBothHands();
            yield return PickUpWithRightGrip(item, "two-shoulder stow");
            yield return MoveHand(leftHand, Shoulder(true));
            yield return MoveHand(rightHand, Shoulder(false));
            yield return new WaitForFixedUpdate();
            SetGrip(rightHand, false);
            yield return Until(() => backpack.StoredCount == count,
                $"Releasing {item.name} with both hands behind their shoulders must store it.");
        }

        private IEnumerator WithdrawBothHands()
        {
            yield return MoveHand(leftHand, Front(true));
            yield return MoveHand(rightHand, Front(false));
        }

        private Vector3 Shoulder(bool left) => rig.Camera.transform.position - Space.forward * .28f
            + Space.right * (left ? -.23f : .23f) - Vector3.up * .24f;

        private Vector3 Front(bool left) => Space.TransformPoint(new Vector3(left ? -.40f : .40f, 1.16f, .38f));

        private IEnumerator PickUpWithRightGrip(PhysicalGrabbable item, string stage)
        {
            // XRI applies detach velocity in LateUpdate. Wait for it and a physics step before staging a dropped tool.
            yield return null;
            yield return new WaitForFixedUpdate();
            // Stage the authored tool clear of the desk so the test starts with a reproducible near-grab.
            var collider = item.Grab.colliders.First(candidate => candidate.enabled && !candidate.isTrigger);
            var pickup = Space.TransformPoint(new Vector3(.50f, 1.12f, .37f));
            Vector3 position = item.transform.position + pickup - collider.bounds.center;
            item.Body.useGravity = false;
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
            item.transform.position = position;
            item.Body.position = position;
            Physics.SyncTransforms();

            yield return MoveHand(rightHand, pickup);
            yield return Until(() => rightHand.IsHovering(item.Grab),
                $"The right hand must hover {item.name} before {stage}.");
            SetGrip(rightHand, true);
            yield return Until(() => rightHand.IsSelecting(item.Grab),
                $"The right Grip must pick up {item.name} before {stage}.");
        }

        private IEnumerator SwipeLeftHand(Vector3 start, Vector3 end, PhysicalGrabbable expectedItem)
        {
            yield return MoveHand(leftHand, Front(true));
            yield return MoveHand(leftHand, start);
            yield return null;
            yield return MoveHand(leftHand, end);
            yield return Until(() => backpack.SelectedItem == expectedItem, $"The swipe must select {expectedItem.name}.");
        }

        private XRSimpleInteractable FocusGrip() =>
            backpack.GetComponentsInChildren<XRSimpleInteractable>().Single();

        private static void AssertPreview(BackpackCarouselView view, PhysicalGrabbable item)
        {
            var anchor = view.transform.Find(item.name + " preview");
            Assert.That(anchor, Is.Not.Null, $"The carousel must show a model for {item.name}.");
            var rendered = anchor.GetComponentsInChildren<MeshFilter>()
                .Where(filter => filter.TryGetComponent(out MeshRenderer renderer) && renderer.enabled)
                .Select(filter => filter.sharedMesh).ToArray();
            var originals = item.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh);
            Assert.That(rendered, Is.Not.Empty);
            Assert.That(rendered, Is.SubsetOf(originals), "The preview must use the authored tool meshes.");
        }

        private IEnumerator MoveHand(NearFarInteractor hand, Vector3 point)
        {
            var caster = (InteractionCasterBase)hand.nearInteractionCaster;
            var origin = caster.effectiveCastOrigin;
            ref var state = ref State(hand);
            state.devicePosition += Space.InverseTransformVector(point - origin.position);
            InputSystem.QueueStateEvent(hand == leftHand ? leftDevice : rightDevice, state);
            return Until(() => Vector3.Distance(origin.position, point) < .025f,
                "The simulated pose must move the actual near-grab caster to the requested point.");
        }

        private void SetGrip(NearFarInteractor hand, bool pressed)
        {
            ref var state = ref State(hand);
            state.grip = pressed ? 1f : 0f;
            state = state.WithButton(ControllerButton.GripButton, pressed);
            InputSystem.QueueStateEvent(hand == leftHand ? leftDevice : rightDevice, state);
        }

        private ref XRSimulatedControllerState State(NearFarInteractor hand)
        {
            if (hand == leftHand) return ref leftState;
            return ref rightState;
        }

        private static XRSimulatedControllerState HandState(Vector3 position) => new()
        {
            devicePosition = position, deviceRotation = Quaternion.identity, trackingState = Tracked, isTracked = true
        };

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static IEnumerator Until(Func<bool> predicate, string failure)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, failure);
        }
    }
}
#endif
