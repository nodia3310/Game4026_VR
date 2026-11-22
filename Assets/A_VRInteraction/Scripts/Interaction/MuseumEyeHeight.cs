using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace Game4026.Interaction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(XRInteractionUpdateOrder.k_InteractionManager - 1)]
    [RequireComponent(typeof(XROrigin))]
    public sealed class MuseumEyeHeight : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float standingEyeHeight = 1.65f;
        [SerializeField, Min(0f)] private float crouchDepth = 0.5f;
        [SerializeField] private InputAction crouchAction = new InputAction(
            "Toggle Crouch", InputActionType.Button, "<XRController>{LeftHand}/{PrimaryButton}");

        private const float StableSeconds = 0.3f;
        private const float HeightTolerance = 0.04f;
        private const float CrouchDuration = 0.25f;
        private const float MinimumCrouchedEyeHeight = 0.3f;

        private XROrigin origin;
        private Transform trackingSpace;
        private float sampleSince = -1f;
        private float sampleHeight;
        private float floorCorrection;
        private bool calibrated;
        private bool isCrouched;
        private float crouchProgress;

        private void Awake()
        {
            origin = GetComponent<XROrigin>();
            trackingSpace = origin.CameraFloorOffsetObject.transform;
        }

        private void OnEnable() => crouchAction.Enable();

        private void Update()
        {
            var mode = origin.CurrentTrackingOriginMode;
            if (mode == TrackingOriginModeFlags.Floor)
            {
                if (!calibrated) SampleFloorHeight();
            }
            else
            {
                sampleSince = -1f;
                if (mode != TrackingOriginModeFlags.Device && mode != TrackingOriginModeFlags.Unbounded)
                    return;
            }

            if (crouchAction.WasPressedThisFrame())
                isCrouched = !isCrouched;

            crouchProgress = Mathf.MoveTowards(crouchProgress, isCrouched ? 1f : 0f,
                Time.unscaledDeltaTime / CrouchDuration);
            float standingOffset = mode == TrackingOriginModeFlags.Floor ? floorCorrection : origin.CameraYOffset;
            float requestedDrop = Mathf.SmoothStep(0f, crouchDepth, crouchProgress);
            float trackedEyeHeight = trackingSpace.InverseTransformPoint(origin.Camera.transform.position).y;
            float availableDrop = Mathf.Max(0f, trackedEyeHeight + standingOffset - MinimumCrouchedEyeHeight);
            float height = standingOffset - Mathf.Min(requestedDrop, availableDrop);

            // One offset moves the camera, tracked hands and grip queries together before XRI runs.
            if (!Mathf.Approximately(trackingSpace.localPosition.y, height))
                SetTrackingSpaceHeight(height);
        }

        private void SampleFloorHeight()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked ||
                (head.TryGetFeatureValue(CommonUsages.userPresence, out bool worn) && !worn))
            {
                sampleSince = -1f;
                return;
            }

            float eyeHeight = head.TryGetFeatureValue(CommonUsages.centerEyePosition, out var eyePosition)
                ? eyePosition.y
                : trackingSpace.InverseTransformPoint(origin.Camera.transform.position).y;
            ObserveEyeHeight(eyeHeight, Time.unscaledTime);
        }

        private void ObserveEyeHeight(float height, float now)
        {
            if (!float.IsFinite(height) || height < -0.25f || height > 3f)
            {
                sampleSince = -1f;
                return;
            }

            if (sampleSince < 0f || Mathf.Abs(height - sampleHeight) > HeightTolerance)
            {
                sampleSince = now;
                sampleHeight = height;
                return;
            }

            if (now - sampleSince < StableSeconds) return;

            // Calibrate once at startup so later real crouches and standing movements remain tracked.
            floorCorrection = Mathf.Max(0f, standingEyeHeight - height);
            calibrated = true;
        }

        private void OnDisable()
        {
            crouchAction.Disable();
            var mode = origin.CurrentTrackingOriginMode;
            if (mode == TrackingOriginModeFlags.Floor)
                SetTrackingSpaceHeight(0f);
            else if (mode == TrackingOriginModeFlags.Device || mode == TrackingOriginModeFlags.Unbounded)
                SetTrackingSpaceHeight(origin.CameraYOffset);

            isCrouched = false;
            crouchProgress = 0f;
            calibrated = false;
            floorCorrection = 0f;
            sampleSince = -1f;
        }

        private void OnDestroy() => crouchAction.Dispose();

        private void SetTrackingSpaceHeight(float height)
        {
            Vector3 position = trackingSpace.localPosition;
            position.y = height;
            trackingSpace.localPosition = position;
        }
    }
}
