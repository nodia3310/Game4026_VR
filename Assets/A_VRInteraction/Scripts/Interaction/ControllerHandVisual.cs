using Game4026.Interaction.Grabbing;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Interaction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    [RequireComponent(typeof(SkinnedMeshRenderer), typeof(OVRSkeleton))]
    public sealed class ControllerHandVisual : MonoBehaviour
    {
        [SerializeField] private GameObject controllerModel;
        [SerializeField] private Transform gripAnchor;
        [SerializeField] private NearFarInteractor grabInteractor;

        private const float MaxGripShift = 0.025f;
        private const float MaxGripTilt = 35f;
        private const float IndexCurlSeconds = 0.08f;

        private SkinnedMeshRenderer handMesh;
        private OVRSkeleton handSkeleton;
        private Renderer[] controllerRenderers;
        private Transform palm;
        private Transform middleFinger;
        private Transform ringFinger;
        private Transform indexKnuckle;
        private Transform indexMiddleJoint;
        private Transform indexEndJoint;
        private Transform indexTip;
        private Transform littleKnuckle;
        private bool controllerHidden;
        private float indexCurlWeight;

        private void Awake()
        {
            handMesh = GetComponent<SkinnedMeshRenderer>();
            handSkeleton = GetComponent<OVRSkeleton>();
            controllerRenderers = controllerModel != null
                ? controllerModel.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
        }

        private void LateUpdate()
        {
            // The Meta prefab enables its renderer after receiving valid hand data.
            bool handVisible = handMesh.enabled && handMesh.sharedMesh != null;
            SetControllerHidden(handVisible);
            if (gripAnchor == null) return;

            PhysicalGrabbable item = null;
            if (grabInteractor != null && grabInteractor.firstInteractableSelected is { } selected)
                selected.transform.TryGetComponent(out item);
            bool gripLocked = item != null && item.KeepsHandOnGrip;

            if (!handVisible || !handSkeleton.IsDataValid || !TryGetGripPosition(out var gripPosition))
            {
                if (!gripLocked) gripAnchor.localPosition = Vector3.zero;
                indexCurlWeight = 0f;
                return;
            }

            // Keep the selected weapon's grip fixed relative to the controller while fingers animate.
            if (!gripLocked) gripAnchor.position = gripPosition;
            if (item == null)
            {
                indexCurlWeight = 0f;
                return;
            }

            Vector3 visibleGrip = gripPosition;
            if (item.TryGetHandGrip(out var handleCenter, out var handleAxis))
            {
                AlignHeldHand(item, gripPosition, handleCenter, handleAxis);
                visibleGrip = handleCenter;
            }

            indexCurlWeight = Mathf.MoveTowards(indexCurlWeight, 1f, Time.unscaledDeltaTime / IndexCurlSeconds);
            PoseHeldIndex(visibleGrip, indexCurlWeight);
        }

        private bool TryGetGripPosition(out Vector3 position)
        {
            if (palm == null || middleFinger == null || ringFinger == null || indexTip == null)
            {
                foreach (var bone in handSkeleton.Bones)
                {
                    switch (bone.Id)
                    {
                        case OVRSkeleton.BoneId.XRHand_Palm: palm = bone.Transform; break;
                        case OVRSkeleton.BoneId.XRHand_MiddleIntermediate: middleFinger = bone.Transform; break;
                        case OVRSkeleton.BoneId.XRHand_RingIntermediate: ringFinger = bone.Transform; break;
                        case OVRSkeleton.BoneId.XRHand_IndexProximal: indexKnuckle = bone.Transform; break;
                        case OVRSkeleton.BoneId.XRHand_IndexIntermediate: indexMiddleJoint = bone.Transform; break;
                        case OVRSkeleton.BoneId.XRHand_IndexDistal: indexEndJoint = bone.Transform; break;
                        case OVRSkeleton.BoneId.XRHand_IndexTip: indexTip = bone.Transform; break;
                        case OVRSkeleton.BoneId.XRHand_LittleProximal: littleKnuckle = bone.Transform; break;
                    }
                }
            }

            position = default;
            if (palm == null || middleFinger == null || ringFinger == null) return false;

            // The center of a handle sits in the space between the palm and curled fingers.
            position = Vector3.Lerp(palm.position, (middleFinger.position + ringFinger.position) * 0.5f, 0.5f);
            return true;
        }

        private void AlignHeldHand(PhysicalGrabbable item, Vector3 trackedGrip, Vector3 handleCenter, Vector3 handleAxis)
        {
            var rotation = Quaternion.identity;
            var visibleGrip = trackedGrip + Vector3.ClampMagnitude(handleCenter - trackedGrip, MaxGripShift);
            if (item.KeepsHandOnGrip)
            {
                // Use the weapon's resolved physics pose when contact stops it short of the controller.
                var targetGrip = grabInteractor.GetAttachTransform(item.Grab);
                var actualGrip = item.Grab.GetAttachTransform(grabInteractor);
                rotation = actualGrip.rotation * Quaternion.Inverse(targetGrip.rotation);
                visibleGrip = handleCenter;
            }

            if (indexKnuckle != null && littleKnuckle != null)
            {
                Vector3 handAxis = rotation * (indexKnuckle.position - littleKnuckle.position);
                if (handAxis.sqrMagnitude > 0.000001f)
                {
                    // Choose the end nearest the thumb to avoid a 180-degree flip.
                    if (Vector3.Dot(handAxis, handleAxis) < 0f) handleAxis = -handleAxis;
                    var tilt = Quaternion.RotateTowards(Quaternion.identity,
                        Quaternion.FromToRotation(handAxis, handleAxis), MaxGripTilt);
                    rotation = tilt * rotation;
                }
            }

            transform.SetPositionAndRotation(visibleGrip + rotation * (transform.position - trackedGrip),
                rotation * transform.rotation);
        }

        private void PoseHeldIndex(Vector3 gripCenter, float weight)
        {
            if (indexKnuckle == null || littleKnuckle == null ||
                indexMiddleJoint == null || indexEndJoint == null || indexTip == null) return;

            Vector3 acrossKnuckles = (indexKnuckle.position - littleKnuckle.position).normalized;
            Vector3 knuckleCenter = (indexKnuckle.position + littleKnuckle.position) * 0.5f;
            Vector3 forward = Vector3.ProjectOnPlane(knuckleCenter - palm.position, acrossKnuckles).normalized;
            Vector3 inward = Vector3.Cross(acrossKnuckles, forward);
            if (inward == Vector3.zero) return;

            // Mirror the bend toward the handle from the plane of the palm on both hands.
            Vector3 towardHandle = gripCenter - palm.position;
            float handleSide = Vector3.Dot(inward, towardHandle);
            if (Mathf.Abs(handleSide) < 0.002f)
                handleSide = Vector3.Dot(inward, middleFinger.position - palm.position);
            if (handleSide < 0f) acrossKnuckles = -acrossKnuckles;

            // Aim all three skinning segments from the same palm frame so a straight
            // Capsense index cannot cancel the grip pose at an individual joint.
            AimIndexSegment(indexKnuckle, indexMiddleJoint, Quaternion.AngleAxis(32f, acrossKnuckles) * forward, weight);
            AimIndexSegment(indexMiddleJoint, indexEndJoint, Quaternion.AngleAxis(88f, acrossKnuckles) * forward, weight);
            AimIndexSegment(indexEndJoint, indexTip, Quaternion.AngleAxis(118f, acrossKnuckles) * forward, weight);
        }

        private static void AimIndexSegment(Transform joint, Transform next, Vector3 direction, float weight)
        {
            Vector3 current = next.position - joint.position;
            if (current.sqrMagnitude < 0.000001f) return;
            Quaternion turn = Quaternion.FromToRotation(current, direction);
            joint.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * joint.rotation;
        }

        private void OnDisable()
        {
            SetControllerHidden(false);
            if (gripAnchor != null) gripAnchor.localPosition = Vector3.zero;
            indexCurlWeight = 0f;
        }

        private void SetControllerHidden(bool hidden)
        {
            if (controllerHidden == hidden) return;

            foreach (var controllerRenderer in controllerRenderers)
                controllerRenderer.forceRenderingOff = hidden;

            controllerHidden = hidden;
        }
    }
}
