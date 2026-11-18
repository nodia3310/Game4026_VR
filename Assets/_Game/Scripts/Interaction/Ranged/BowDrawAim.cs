using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Interaction.Ranged
{
    public readonly struct BowShot
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Velocity;
        public readonly float NormalizedDraw;
        public BowShot(Vector3 origin, Vector3 velocity, float draw)
        { Origin = origin; Velocity = velocity; NormalizedDraw = draw; }
    }

    /// <summary>Hold bow with one hand and select string with the other. Forward is arrow direction.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class BowDrawAim : MonoBehaviour
    {
        [SerializeField] private XRBaseInteractable stringInteractable;
        [SerializeField] private Transform stringRest;
        [SerializeField] private Transform arrowOrigin;
        [SerializeField] private Transform stringVisual;
        [SerializeField, Min(0.01f)] private float maximumDraw = 0.6f;
        [SerializeField, Min(0f)] private float minimumDraw = 0.08f;
        [SerializeField, Min(0f)] private float minimumSpeed = 4f;
        [SerializeField, Min(0.01f)] private float maximumSpeed = 25f;
        [SerializeField] private bool requireTrackedControllers = true;
        [SerializeField, Min(0.01f)] private float maximumHandJump = 0.4f;
        [SerializeField] private ArrowProjectile arrowPrefab;
        [SerializeField] private Collider[] additionalIgnoredColliders = Array.Empty<Collider>();
        [SerializeField] private LineRenderer trajectoryLine;
        [SerializeField, Range(2, 64)] private int trajectoryPoints = 24;
        [SerializeField, Min(0.01f)] private float trajectoryTimeStep = 0.05f;
        [SerializeField] private UnityEvent<float> onShotPower = new UnityEvent<float>();
        public event Action<BowShot> Shot;
        public float NormalizedDraw => state.NormalizedDraw;
        public bool IsDrawing => state.IsDrawing;
        private readonly BowDrawState state = new BowDrawState();
        private XRGrabInteractable bow;
        private IXRSelectInteractor pullingHand;
        private Transform pullAttach;
        private Vector3 lastLocalHand;
        private bool externalTrackingValid = true;
        private Collider[] bowColliders;
        private readonly BowReleaseGate releaseGate = new BowReleaseGate();
        private IXRSelectInteractor pendingPullingHand;
        private IXRSelectInteractor pendingHoldingHand;

        private void Awake()
        {
            bow = GetComponent<XRGrabInteractable>();
            bowColliders = GetComponentsInChildren<Collider>(true);
        }
        private void OnEnable()
        {
            if (bow == null) bow = GetComponent<XRGrabInteractable>();
            bow.selectExited.AddListener(OnBowReleased);
            if (stringInteractable != null)
            {
                stringInteractable.selectEntered.AddListener(OnStringGrabbed);
                stringInteractable.selectExited.AddListener(OnStringReleased);
            }
            if (trajectoryLine != null) trajectoryLine.useWorldSpace = true;
            ResetVisuals();
        }
        private void OnDisable()
        {
            if (bow != null) bow.selectExited.RemoveListener(OnBowReleased);
            if (stringInteractable != null)
            {
                stringInteractable.selectEntered.RemoveListener(OnStringGrabbed);
                stringInteractable.selectExited.RemoveListener(OnStringReleased);
            }
            CancelDraw();
        }
        private void OnApplicationPause(bool paused) { if (paused) CancelDraw(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelDraw(); }

        /// <summary>Optional tracking adapter hook; false cancels and cannot fire on recovery.</summary>
        public void SetTrackingValid(bool valid)
        {
            externalTrackingValid = valid;
            if (!valid) CancelDraw();
        }

        private bool IsTracked(IXRSelectInteractor hand)
        {
            if (hand == null || !externalTrackingValid || !hand.transform.gameObject.activeInHierarchy) return false;
            if (!requireTrackedControllers) return true;
            if (hand.handedness == InteractorHandedness.None) return false;
            XRNode node = hand.handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;
            var device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        }

        private bool BowHeldAndTracked()
            => bow != null && bow.isSelected && IsTracked(bow.interactorsSelecting[0]);

        private void OnStringGrabbed(SelectEnterEventArgs args)
        {
            if (state.IsDrawing || stringRest == null || arrowOrigin == null || !BowHeldAndTracked()) return;
            // Reject the same interactor and also near/ray interactors belonging to the same hand.
            var holder = bow.interactorsSelecting[0];
            if (ReferenceEquals(holder, args.interactorObject)
                || (holder.handedness != InteractorHandedness.None && holder.handedness == args.interactorObject.handedness)
                || !IsTracked(args.interactorObject)) return;
            pullingHand = args.interactorObject;
            pullAttach = pullingHand.GetAttachTransform(stringInteractable);
            if (pullAttach == null) { pullingHand = null; return; }
            lastLocalHand = transform.InverseTransformPoint(pullAttach.position);
            state.Begin();
        }

        private void OnBowReleased(SelectExitEventArgs args) => CancelDraw();
        private void OnStringReleased(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(args.interactorObject, pullingHand)) return;
            bool canShoot = !args.isCanceled && BowHeldAndTracked() && IsTracked(pullingHand);
            if (canShoot) canShoot = UpdatePull();
            float power = 0f;
            if (canShoot) canShoot = state.TryRelease(minimumDraw, out power);
            var releasedHand = pullingHand;
            var holdingHand = canShoot ? bow.interactorsSelecting[0] : null;
            CancelDraw();
            if (!canShoot) return;
            // XRI may dispatch string release before bow release in the same frame.
            // Commit in LateUpdate only after all selection events have settled.
            releaseGate.Queue(power);
            pendingPullingHand = releasedHand;
            pendingHoldingHand = holdingHand;
        }

        private void CommitPendingShot()
        {
            if (!releaseGate.HasPending) return;
            bool canShoot = BowHeldAndTracked() && IsTracked(pendingPullingHand)
                && ReferenceEquals(bow.interactorsSelecting[0], pendingHoldingHand);
            canShoot = releaseGate.TryCommit(canShoot, out float power);
            pendingPullingHand = pendingHoldingHand = null;
            if (!canShoot || arrowOrigin == null) return;
            Vector3 velocity = arrowOrigin.forward * Mathf.Lerp(minimumSpeed, Mathf.Max(minimumSpeed, maximumSpeed), power);
            var shot = new BowShot(arrowOrigin.position, velocity, power);
            if (arrowPrefab != null)
            {
                var arrow = Instantiate(arrowPrefab, shot.Origin, Quaternion.LookRotation(velocity.normalized, arrowOrigin.up));
                arrow.Launch(shot.Velocity, bowColliders, additionalIgnoredColliders);
            }
            Shot?.Invoke(shot);
            onShotPower.Invoke(power);
        }

        private bool UpdatePull()
        {
            if (!state.IsDrawing || pullAttach == null || !BowHeldAndTracked() || !IsTracked(pullingHand)) return false;
            Vector3 localHand = transform.InverseTransformPoint(pullAttach.position);
            if ((localHand - lastLocalHand).magnitude > maximumHandJump) return false;
            lastLocalHand = localHand;
            state.Sample(pullAttach.position - stringRest.position, -arrowOrigin.forward, maximumDraw);
            return true;
        }

        private void LateUpdate()
        {
            CommitPendingShot();
            if (!state.IsDrawing) return;
            if (!UpdatePull()) { CancelDraw(); return; }
            if (stringVisual != null) stringVisual.position = stringRest.position - arrowOrigin.forward * state.DrawDistance;
            if (trajectoryLine == null) return;
            trajectoryLine.enabled = true;
            int count = Mathf.Clamp(trajectoryPoints, 2, 64);
            trajectoryLine.positionCount = count;
            Vector3 velocity = arrowOrigin.forward * Mathf.Lerp(minimumSpeed, Mathf.Max(minimumSpeed, maximumSpeed), state.NormalizedDraw);
            for (int i = 0; i < count; i++)
                trajectoryLine.SetPosition(i, BowDrawState.BallisticPosition(arrowOrigin.position, velocity, Physics.gravity, i * trajectoryTimeStep));
        }

        public void CancelDraw()
        {
            state.Cancel();
            releaseGate.Cancel();
            pendingPullingHand = pendingHoldingHand = null;
            pullingHand = null;
            pullAttach = null;
            ResetVisuals();
        }

        private void ResetVisuals()
        {
            if (trajectoryLine != null) trajectoryLine.enabled = false;
            if (stringVisual != null && stringRest != null) stringVisual.position = stringRest.position;
        }
        private void OnValidate()
        {
            maximumDraw = Mathf.Max(0.01f, maximumDraw);
            minimumDraw = Mathf.Clamp(minimumDraw, 0f, maximumDraw);
            maximumSpeed = Mathf.Max(0.01f, minimumSpeed, maximumSpeed);
            maximumHandJump = Mathf.Max(0.01f, maximumHandJump);
        }
    }
}
