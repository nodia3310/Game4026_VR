using System;
using Game4026.Interaction.Melee;
using Game4026.Interaction.TestScene;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using Keyboard = UnityEngine.InputSystem.Keyboard;

namespace Game4026.Interaction.Grabbing
{
    [DisallowMultipleComponent]
    public sealed class FieldBackpack : MonoBehaviour
    {
        [Serializable]
        private sealed class Slot
        {
            public XRSimpleInteractable grip;
            public BoxCollider volume;
            public TMP_Text label;

            [NonSerialized] public PhysicalGrabbable Item;
            [NonSerialized] public PhysicalGrabbable PendingItem;
            [NonSerialized] public IXRSelectInteractor TakingHand;
        }

        [Serializable]
        private sealed class Tool
        {
            public PhysicalGrabbable item;
            public string displayName;
        }

        [SerializeField] private XROrigin rig;
        [SerializeField] private NearFarInteractor leftHand;
        [SerializeField] private NearFarInteractor rightHand;
        [SerializeField] private InteractionDesktopMode desktop;
        [SerializeField] private GameObject display;
        [SerializeField] private bool useFloatingCarousel;
        [SerializeField] private Slot[] slots = Array.Empty<Slot>();
        [SerializeField] private Tool[] tools = Array.Empty<Tool>();

        private const float ShoulderRadius = .19f;
        private const float ShoulderReleaseRadius = .25f;
        private const float WalkAwayDistance = 1.1f;
        private TrackedPoseDriver leftPose, rightPose;
        private bool leftArmed, rightArmed;
        private Vector3 openPosition;
        private BackpackCarousel carousel;
        private XRBaseInteractor releaseLeft, releaseRight;
        private bool bothShouldersArmed, desktopStowing;

        public bool IsOpen => display && display.activeSelf;
        public int StoredCount => carousel?.Count ?? Array.FindAll(slots, slot => slot.Item).Length;
        public PhysicalGrabbable SelectedItem => carousel?.Selected;

        public void ResetContents()
        {
            display.SetActive(false);
            carousel?.ResetContents();
            foreach (var slot in slots)
            {
                if (slot.Item) slot.Item.gameObject.SetActive(true);
                slot.Item = slot.PendingItem = null;
                slot.TakingHand = null;
                RefreshSlot(slot);
            }
            leftArmed = rightArmed = bothShouldersArmed = false;
        }

        private void Awake()
        {
            leftPose = leftHand.GetComponentInParent<TrackedPoseDriver>(true);
            rightPose = rightHand.GetComponentInParent<TrackedPoseDriver>(true);
            if (useFloatingCarousel)
            {
                carousel = new BackpackCarousel(display, slots[0].grip, slots[0].label);
                foreach (var tool in tools)
                    if (tool.item) carousel.View.SetDisplayName(tool.item, tool.displayName);
            }
        }

        private void OnEnable()
        {
            display.SetActive(false);
            if (carousel != null) return;
            foreach (var slot in slots)
            {
                slot.grip.selectEntered.AddListener(OnSlotSelected);
                RefreshSlot(slot);
            }
            foreach (var tool in tools)
                if (tool.item) tool.item.Grab.selectExited.AddListener(OnToolReleased);
        }

        private void OnDisable()
        {
            BindReleaseHand(ref releaseLeft, null);
            BindReleaseHand(ref releaseRight, null);
            carousel?.CancelPending();
            foreach (var slot in slots)
            {
                slot.grip.selectEntered.RemoveListener(OnSlotSelected);
                slot.PendingItem = null;
                slot.TakingHand = null;
            }
            foreach (var tool in tools)
                if (tool.item) tool.item.Grab.selectExited.RemoveListener(OnToolReleased);
            if (display) display.SetActive(false);
            leftArmed = rightArmed = bothShouldersArmed = false;
        }

        private void Update()
        {
            bool desktopActive = desktop && desktop.IsDesktopActive;
            if (carousel != null)
            {
                BindReleaseHand(ref releaseLeft, desktopActive ? desktop.LeftHand : leftHand);
                BindReleaseHand(ref releaseRight, desktopActive ? desktop.RightHand : rightHand);
            }
            var head = desktopActive ? desktop.DesktopCamera.transform : rig.Camera.transform;
            var forward = HorizontalForward(head);

            bool wantsToggle;
            if (desktopActive)
            {
                wantsToggle = Keyboard.current?.bKey.wasPressedThisFrame == true;
                if (wantsToggle && carousel != null && (desktop.LeftHand.hasSelection || desktop.RightHand.hasSelection))
                {
                    // B represents the two-shoulder gesture on a keyboard.
                    desktopStowing = true;
                    try
                    {
                        ReleaseDesktopTool(desktop.LeftHand);
                        ReleaseDesktopTool(desktop.RightHand);
                    }
                    finally { desktopStowing = false; }
                    wantsToggle = false;
                }
            }
            else if (carousel != null)
            {
                wantsToggle = BothShoulderGesture(head, forward);
            }
            else
            {
                bool leftEntered = EnteredShoulder(HandPosition(leftHand), Shoulder(head, forward, true),
                    IsTracked(leftPose), ref leftArmed);
                bool rightEntered = EnteredShoulder(HandPosition(rightHand), Shoulder(head, forward, false),
                    IsTracked(rightPose), ref rightArmed);
                wantsToggle = leftEntered || rightEntered;
            }

            if (wantsToggle)
            {
                if (IsOpen) display.SetActive(false);
                else Open(head.position, forward, desktopActive);
            }
            else if (IsOpen && Vector3.ProjectOnPlane(head.position - openPosition, Vector3.up).sqrMagnitude >
                     WalkAwayDistance * WalkAwayDistance)
            {
                display.SetActive(false);
            }
            else if (IsOpen && carousel != null)
            {
                if (desktopActive)
                {
                    var keyboard = Keyboard.current;
                    float scroll = UnityEngine.InputSystem.Mouse.current?.scroll.ReadValue().y ?? 0f;
                    if (scroll < 0 || keyboard?.rightBracketKey.wasPressedThisFrame == true) StepSelection(1);
                    else if (scroll > 0 || keyboard?.leftBracketKey.wasPressedThisFrame == true) StepSelection(-1);
                }
                else
                {
                    bool leftValid = TryCarouselHandPosition(true, out var leftPosition);
                    bool rightValid = TryCarouselHandPosition(false, out var rightPosition);
                    int left = carousel.ReadSwipe(true, leftPosition, !leftHand.hasSelection && leftValid);
                    int right = carousel.ReadSwipe(false, rightPosition, !rightHand.hasSelection && rightValid);
                    StepSelection(left != 0 ? left : right);
                }
            }
        }

        private void StepSelection(int delta) => carousel?.StepSelection(delta);

        private static void ReleaseDesktopTool(DesktopTestHand hand)
        {
            if (hand.firstInteractableSelected is XRGrabInteractable grab && grab.GetComponent<PhysicalGrabbable>())
                hand.Release();
        }

        private Vector3 HorizontalForward(Transform head)
        {
            var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            return flat.sqrMagnitude < .001f ? rig.transform.forward : flat.normalized;
        }

        private static Vector3 Shoulder(Transform head, Vector3 forward, bool left) =>
            head.position - forward * .28f - Vector3.up * .24f +
            Vector3.Cross(Vector3.up, forward) * (left ? -.23f : .23f);

        private bool TryCarouselHandPosition(bool left, out Vector3 position)
        {
            // Link can report a valid grip pose while the rig's pointer tracking action reads zero.
            var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            if (device.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState state) &&
                (state & InputTrackingState.Position) != 0 &&
                device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 grip))
            {
                position = rig.CameraFloorOffsetObject.transform.TransformPoint(grip);
                return true;
            }
            position = HandPosition(left ? leftHand : rightHand);
            return IsTracked(left ? leftPose : rightPose);
        }

        private enum ShoulderZone { Untracked, Outside, Edge, Behind }

        private ShoulderZone RearZone(Transform head, Vector3 forward, bool left)
        {
            if (!TryCarouselHandPosition(left, out var hand)) return ShoulderZone.Untracked;
            var offset = hand - head.position;
            float side = Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward)) * (left ? -1f : 1f);
            float back = -Vector3.Dot(offset, forward);
            // Both over-the-shoulder and beside-the-neck reaches count, but the hands must be behind the head.
            if (side >= .015f && side <= .48f && back >= .055f && back <= .58f &&
                offset.y >= -.56f && offset.y <= .20f) return ShoulderZone.Behind;
            if (side < -.035f || side > .55f || back < .015f || back > .66f ||
                offset.y < -.64f || offset.y > .27f) return ShoulderZone.Outside;
            return ShoulderZone.Edge;
        }

        private bool BothAtShoulders(Transform head, Vector3 forward) =>
            RearZone(head, forward, true) == ShoulderZone.Behind &&
            RearZone(head, forward, false) == ShoulderZone.Behind;

        private bool BothShoulderGesture(Transform head, Vector3 forward)
        {
            var left = RearZone(head, forward, true);
            var right = RearZone(head, forward, false);
            if (left == ShoulderZone.Outside && right == ShoulderZone.Outside) bothShouldersArmed = true;
            // A missing sample cannot arm or complete the gesture. Resume when both poses are valid.
            if (!bothShouldersArmed || left != ShoulderZone.Behind || right != ShoulderZone.Behind) return false;
            bothShouldersArmed = false;
            return !leftHand.hasSelection && !rightHand.hasSelection;
        }

        private void BindReleaseHand(ref XRBaseInteractor current, XRBaseInteractor next)
        {
            if (ReferenceEquals(current, next)) return;
            if (current) current.selectExited.RemoveListener(OnToolReleased);
            current = next;
            if (current) current.selectExited.AddListener(OnToolReleased);
        }

        private void Open(Vector3 head, Vector3 forward, bool desktopActive)
        {
            openPosition = head;
            // Anchor the panel in world space so it stays still as the player reaches for it.
            var pose = Quaternion.LookRotation(forward, Vector3.up);
            if (carousel == null) pose *= Quaternion.Euler(13f, 0f, 0f);
            display.transform.SetPositionAndRotation(head + forward * .60f - Vector3.up * (carousel == null ? .16f : .045f), pose);
            display.SetActive(true);
            carousel?.Open(desktopActive);
        }

        private static bool EnteredShoulder(Vector3 hand, Vector3 shoulder, bool tracked, ref bool armed)
        {
            if (!tracked)
            {
                armed = false;
                return false;
            }

            float distance = (hand - shoulder).sqrMagnitude;
            if (distance > ShoulderReleaseRadius * ShoulderReleaseRadius) armed = true;
            if (!armed || distance > ShoulderRadius * ShoulderRadius) return false;
            armed = false;
            return true;
        }

        private static bool IsTracked(TrackedPoseDriver pose) =>
            pose && pose.isActiveAndEnabled && pose.trackingStateInput.action is { enabled: true } tracking &&
            ((InputTrackingState)tracking.ReadValue<int>() & InputTrackingState.Position) != 0;

        private static Vector3 HandPosition(IXRSelectInteractor hand)
        {
            if (hand is NearFarInteractor near && near.nearInteractionCaster is InteractionCasterBase caster && caster.castOrigin)
                return caster.castOrigin.position;

            return hand.transform.position;
        }

        private bool IsPlayerHand(IXRSelectInteractor hand)
        {
            if (desktop && desktop.IsDesktopActive)
                return hand == desktop.LeftHand || hand == desktop.RightHand;
            if (ReferenceEquals(hand, leftHand)) return carousel != null ? TryCarouselHandPosition(true, out _) : IsTracked(leftPose);
            if (ReferenceEquals(hand, rightHand)) return carousel != null ? TryCarouselHandPosition(false, out _) : IsTracked(rightPose);
            return false;
        }

        private void OnToolReleased(SelectExitEventArgs args)
        {
            if (args.isCanceled || !IsPlayerHand(args.interactorObject)) return;
            if (carousel != null)
            {
                if (!args.interactableObject.transform.TryGetComponent(out PhysicalGrabbable item)) return;
                var head = rig.Camera.transform;
                bool canStore = desktop && desktop.IsDesktopActive ? desktopStowing :
                    BothAtShoulders(head, HorizontalForward(head));
                if (!canStore) return;
                bothShouldersArmed = false;
                carousel.QueueStore(item);
                return;
            }
            if (!IsOpen) return;
            Vector3 hand = HandPosition(args.interactorObject);
            foreach (var slot in slots)
            {
                if (slot.Item || slot.PendingItem || (slot.volume.ClosestPoint(hand) - hand).sqrMagnitude > .025f * .025f)
                    continue;

                slot.PendingItem = args.interactableObject.transform.GetComponent<PhysicalGrabbable>();
                return;
            }
        }

        private void OnSlotSelected(SelectEnterEventArgs args)
        {
            foreach (var slot in slots)
            {
                if (slot.grip != args.interactableObject) continue;
                slot.TakingHand = args.interactorObject;
                return;
            }
        }

        private void LateUpdate()
        {
            // Commit after XRI finishes dispatching selection events for the frame.
            if (carousel != null)
            {
                carousel.Commit(this);
                return;
            }
            foreach (var slot in slots)
            {
                if (slot.PendingItem)
                {
                    var item = slot.PendingItem;
                    slot.PendingItem = null;
                    if (!item.IsHeld)
                    {
                        StopMotion(item.Body);
                        item.gameObject.SetActive(false);
                        slot.Item = item;
                        RefreshSlot(slot);
                    }
                }

                var hand = slot.TakingHand;
                slot.TakingHand = null;
                if (hand != null && hand.IsSelecting(slot.grip)) TakeFromSlot(slot, hand);
            }
        }

        private void TakeFromSlot(Slot slot, IXRSelectInteractor hand)
        {
            var manager = slot.grip.interactionManager;
            manager.SelectCancel(hand, slot.grip);
            if (TryEquip(slot.Item, hand, manager))
            {
                slot.Item = null;
                RefreshSlot(slot);
            }
        }

        internal bool TryEquip(PhysicalGrabbable item, IXRSelectInteractor hand, XRInteractionManager manager)
        {
            if (!IsOpen || !item || hand.hasSelection || !IsPlayerHand(hand)) return false;
            var grab = item.Grab;
            var previousPosition = item.transform.position;
            var previousRotation = item.transform.rotation;
            item.gameObject.SetActive(true);

            if (manager.IsSelectPossible(hand, grab))
            {
                var handAttach = hand.GetAttachTransform(grab);
                var toolAttach = grab.GetAttachTransform(hand);
                var rotation = handAttach.rotation * Quaternion.Inverse(toolAttach.rotation);
                item.transform.SetPositionAndRotation(handAttach.position + rotation * (previousPosition - toolAttach.position),
                    rotation * previousRotation);
                item.Body.position = item.transform.position;
                item.Body.rotation = item.transform.rotation;
                StopMotion(item.Body);
                if (item.TryGetComponent(out MeleeWeapon weapon)) weapon.ResetSampling();
                Physics.SyncTransforms();

                manager.SelectEnter(hand, grab);
                if (hand.IsSelecting(grab))
                {
                    return true;
                }
            }

            item.gameObject.SetActive(false);
            item.transform.SetPositionAndRotation(previousPosition, previousRotation);
            return false;
        }

        private void RefreshSlot(Slot slot)
        {
            slot.grip.enabled = slot.Item;
            slot.label.text = "EMPTY";
            if (!slot.Item) return;
            foreach (var tool in tools)
            {
                if (tool.item != slot.Item) continue;
                slot.label.text = tool.displayName;
                return;
            }
        }

        internal static void StopMotion(Rigidbody body)
        {
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
