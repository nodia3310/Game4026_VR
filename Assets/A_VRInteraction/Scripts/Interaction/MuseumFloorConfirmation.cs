using System.Collections.Generic;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.TestScene;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Game4026.Interaction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(XRInteractionUpdateOrder.k_LocomotionProviders - 1)]
    public sealed class MuseumFloorConfirmation : MonoBehaviour
    {
        [SerializeField] private XROrigin origin;
        [SerializeField] private TrackedPoseDriver headset, rightController;
        [SerializeField] private InteractionDesktopMode desktop;
        [SerializeField] private FieldBackpack backpack;
        [SerializeField] private GameObject prompt;
        [SerializeField] private TMP_Text instructions;
        [SerializeField] private Transform floorMarker;

        private const float FloorTolerance = .05f;
        private const float StableSeconds = .25f;
        private readonly InputAction confirm = new("Confirm floor", InputActionType.Button,
            "<XRController>{RightHand}/{PrimaryButton}");
        private readonly List<Behaviour> suspendedInputs = new();
        private Transform trackingSpace;
        private float stableSince = -1f;
        private bool confirmed, armed, requested;
        private bool suspendedBackpack;

        /// <summary>Tracking is stable and the floor origin lines up with the virtual marker.</summary>
        public bool CanConfirm { get; private set; }
        /// <summary>The player confirmed the floor, or the desktop rig is active.</summary>
        public bool IsGameplayReady => confirmed || (desktop && desktop.IsDesktopActive);

        private void Awake() => trackingSpace = origin.CameraFloorOffsetObject.transform;

        private void OnEnable()
        {
            if (!confirmed)
            {
                foreach (var provider in origin.GetComponentsInChildren<LocomotionProvider>(true))
                    SuspendInput(provider);
                foreach (var interactor in origin.GetComponentsInChildren<XRBaseInteractor>(true))
                    SuspendInput(interactor);
                confirm.Enable();
            }
            SetBackpack(IsGameplayReady);
            confirm.performed += OnConfirmPressed;
        }

        private void OnDisable()
        {
            confirm.performed -= OnConfirmPressed;
            confirm.Disable();
            ResetReadiness();
            RestoreInputs();
            SetBackpack(true);
            ShowConfirmation(false);
        }

        private void OnDestroy() => confirm.Dispose();

        private void Update()
        {
            if (desktop) desktop.ReturnToXRWhenHeadsetIsTracked();
            // Leave XR inputs locked if the desktop adapter is temporarily driving the player.
            bool ready = IsGameplayReady;
            SetBackpack(ready);
            ShowConfirmation(!ready);
            if (ready)
            {
                ResetReadiness();
                return;
            }

            if (!HasPosition(headset) || !HasPosition(rightController))
            {
                Wait("Wake the headset and right controller.\nWait for position tracking.");
                return;
            }
            if ((origin.CurrentTrackingOriginMode & TrackingOriginModeFlags.Floor) == 0)
            {
                Wait(Application.isEditor
                    ? "Floor tracking unavailable.\nReset Boundary floor, then restart Quest Link.\nFor desktop, stop Play and exit Quest Link."
                    : "Floor tracking unavailable.\nReset Boundary floor in Quest Settings,\nthen reopen the game.");
                return;
            }
            if (!(Mathf.Abs(trackingSpace.position.y - floorMarker.position.y) <= FloorTolerance))
            {
                Wait("Floor and marker heights differ.\nReset Boundary floor level in Quest Settings,\nthen restart this session if needed.");
                return;
            }

            if (stableSince < 0f) stableSince = Time.unscaledTime;
            CanConfirm = Time.unscaledTime - stableSince >= StableSeconds;
            if (!CanConfirm)
            {
                instructions.text = "Checking floor tracking.\nLook down at the yellow floor ring.";
                return;
            }

            instructions.text = "Look at the yellow floor ring.\nLower a controller to compare, if you want.\nFloors match? Press right A to start.\nMismatch? Reset the Quest Boundary floor.";
            if (requested)
            {
                confirmed = true;
                ResetReadiness();
                confirm.Disable();
                RestoreInputs();
                SetBackpack(true);
                ShowConfirmation(false);
                return;
            }
            if (!confirm.IsPressed()) armed = true;
        }

        private void OnConfirmPressed(InputAction.CallbackContext _) => requested = CanConfirm && armed;

        private void Wait(string message)
        {
            ResetReadiness();
            instructions.text = message;
        }

        private void ResetReadiness()
        {
            stableSince = -1f;
            CanConfirm = armed = requested = false;
        }

        private void SuspendInput(Behaviour input)
        {
            if (!input.enabled) return;
            suspendedInputs.Add(input);
            input.enabled = false;
        }

        private void RestoreInputs()
        {
            foreach (var input in suspendedInputs)
                if (input) input.enabled = true;
            suspendedInputs.Clear();
        }

        private void SetBackpack(bool enabled)
        {
            if (!backpack) return;
            if (!enabled && backpack.enabled)
            {
                suspendedBackpack = true;
                backpack.enabled = false;
            }
            else if (enabled && suspendedBackpack)
            {
                suspendedBackpack = false;
                backpack.enabled = true;
            }
        }

        private void ShowConfirmation(bool visible)
        {
            if (prompt) prompt.SetActive(visible);
            if (floorMarker) floorMarker.gameObject.SetActive(visible);
        }

        private static bool HasPosition(TrackedPoseDriver pose)
        {
            if (!pose || !pose.isActiveAndEnabled || pose.trackingStateInput.action is not { enabled: true } action ||
                ((InputTrackingState)action.ReadValue<int>() & InputTrackingState.Position) == 0)
                return false;

            Vector3 position = pose.transform.position;
            return float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);
        }
    }
}
