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
        [SerializeField] private Slot[] slots = Array.Empty<Slot>();
        [SerializeField] private Tool[] tools = Array.Empty<Tool>();

        private const float ShoulderRadius = .19f;
        private const float ShoulderReleaseRadius = .25f;
        private const float WalkAwayDistance = 1.1f;
        private TrackedPoseDriver leftPose, rightPose;
        private bool leftArmed, rightArmed;
        private Vector3 openPosition;

        public bool IsOpen => display && display.activeSelf;

        private void Awake()
        {
            leftPose = leftHand.GetComponentInParent<TrackedPoseDriver>(true);
            rightPose = rightHand.GetComponentInParent<TrackedPoseDriver>(true);
        }

        private void OnEnable()
        {
            display.SetActive(false);
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
            foreach (var slot in slots)
            {
                slot.grip.selectEntered.RemoveListener(OnSlotSelected);
                slot.PendingItem = null;
                slot.TakingHand = null;
            }
            foreach (var tool in tools)
                if (tool.item) tool.item.Grab.selectExited.RemoveListener(OnToolReleased);
            if (display) display.SetActive(false);
            leftArmed = rightArmed = false;
        }

        private void Update()
        {
            bool desktopActive = desktop && desktop.IsDesktopActive;
            var head = desktopActive ? desktop.DesktopCamera.transform : rig.Camera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = rig.transform.forward;
            forward.Normalize();

            bool wantsToggle;
            if (desktopActive)
            {
                wantsToggle = Keyboard.current?.bKey.wasPressedThisFrame == true;
            }
            else
            {
                var right = Vector3.Cross(Vector3.up, forward);
                var behindHead = head.position - forward * .28f - Vector3.up * .24f;
                bool leftEntered = EnteredShoulder(HandPosition(leftHand), behindHead - right * .23f,
                    IsTracked(leftPose), ref leftArmed);
                bool rightEntered = EnteredShoulder(HandPosition(rightHand), behindHead + right * .23f,
                    IsTracked(rightPose), ref rightArmed);
                wantsToggle = leftEntered || rightEntered;
            }

            if (wantsToggle)
            {
                if (IsOpen) display.SetActive(false);
                else Open(head.position, forward);
            }
            else if (IsOpen && Vector3.ProjectOnPlane(head.position - openPosition, Vector3.up).sqrMagnitude >
                     WalkAwayDistance * WalkAwayDistance)
            {
                display.SetActive(false);
            }
        }

        private void Open(Vector3 head, Vector3 forward)
        {
            openPosition = head;
            // Anchor the panel in world space so it stays still as the player reaches for it.
            var pose = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(13f, 0f, 0f);
            display.transform.SetPositionAndRotation(head + forward * .60f - Vector3.up * .16f, pose);
            display.SetActive(true);
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
            return (hand == leftHand && IsTracked(leftPose)) || (hand == rightHand && IsTracked(rightPose));
        }

        private void OnToolReleased(SelectExitEventArgs args)
        {
            if (!IsOpen || args.isCanceled || !IsPlayerHand(args.interactorObject)) return;

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
            var item = slot.Item;
            if (!IsOpen || !item || hand.hasSelection || !IsPlayerHand(hand)) return;

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
                    slot.Item = null;
                    RefreshSlot(slot);
                    return;
                }
            }

            item.gameObject.SetActive(false);
            item.transform.SetPositionAndRotation(previousPosition, previousRotation);
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

        private static void StopMotion(Rigidbody body)
        {
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
