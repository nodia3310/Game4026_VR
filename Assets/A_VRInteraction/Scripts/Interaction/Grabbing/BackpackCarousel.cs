using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Interaction.Grabbing
{
    internal sealed class BackpackCarousel
    {
        private readonly List<PhysicalGrabbable> items = new();
        private readonly List<PhysicalGrabbable> pending = new(2);
        private readonly XRSimpleInteractable focus;
        private readonly Transform panel;
        private int index;
        private IXRSelectInteractor takingHand;
        private PhysicalGrabbable requestedItem;
        private Swipe leftSwipe, rightSwipe;
        public BackpackCarouselView View { get; }
        public int Count => items.Count;
        public PhysicalGrabbable Selected => Count > 0 ? items[index] : null;

        public BackpackCarousel(GameObject display, XRSimpleInteractable slotTemplate, TMP_Text label)
        {
            panel = display.transform;
            foreach (Transform child in panel) child.gameObject.SetActive(false);
            var target = new GameObject("Backpack selection");
            target.SetActive(false);
            target.layer = slotTemplate.gameObject.layer;
            target.transform.SetParent(panel, false);
            var collider = target.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(.20f, .19f, .16f);
            focus = target.AddComponent<XRSimpleInteractable>();
            focus.interactionManager = slotTemplate.interactionManager;
            focus.interactionLayers = slotTemplate.interactionLayers;
            focus.colliders.Add(collider);
            focus.selectEntered.AddListener(OnSelected);
            target.SetActive(true);
            View = display.AddComponent<BackpackCarouselView>();
            View.Initialize(label);
        }

        public void Open(bool desktop)
        {
            leftSwipe = rightSwipe = default;
            View.DesktopControls = desktop;
            Refresh();
        }

        public void CancelPending()
        {
            pending.Clear();
            takingHand = null;
            requestedItem = null;
        }

        public void ResetContents()
        {
            CancelPending();
            foreach (var item in items)
                if (item) item.gameObject.SetActive(true);
            items.Clear();
            index = 0;
        }

        public void QueueStore(PhysicalGrabbable item)
        {
            if (!pending.Contains(item) && !items.Contains(item)) pending.Add(item);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            takingHand = args.interactorObject;
            requestedItem = Selected;
        }

        public void Commit(FieldBackpack pack)
        {
            bool changed = false;
            foreach (var item in pending)
            {
                // Another hand may have caught it during the same selection pass.
                if (!item || item.IsHeld || !item.gameObject.activeInHierarchy) continue;
                FieldBackpack.StopMotion(item.Body);
                item.gameObject.SetActive(false);
                items.Add(item);
                index = Count - 1;
                changed = true;
            }
            pending.Clear();

            var hand = takingHand;
            var requested = requestedItem;
            takingHand = null;
            requestedItem = null;
            if (hand != null && hand.IsSelecting(focus))
            {
                var manager = focus.interactionManager;
                manager.SelectCancel(hand, focus);
                if (pack.TryEquip(requested, hand, manager))
                {
                    index = items.IndexOf(requested);
                    items.RemoveAt(index);
                    index = Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
                    changed = true;
                }
            }
            if (changed && pack.IsOpen) Refresh();
        }

        public void StepSelection(int delta)
        {
            if (delta == 0 || Count < 2 || takingHand != null) return;
            index = (index + (delta > 0 ? 1 : -1) + Count) % Count;
            Refresh(delta);
        }

        private void Refresh(int direction = 0)
        {
            focus.enabled = Count > 0;
            View.Show(items, index, direction);
        }

        public int ReadSwipe(bool left, Vector3 position, bool available)
        {
            var point = panel.InverseTransformPoint(position);
            ref Swipe swipe = ref (left ? ref leftSwipe : ref rightSwipe);
            bool inside = available && Count > 1 && Mathf.Abs(point.x) <= .32f &&
                          Mathf.Abs(point.y) <= .16f && Mathf.Abs(point.z) <= .18f;
            if (!inside)
            {
                swipe = default;
                return 0;
            }
            if (swipe.Consumed) return 0;
            float now = Time.unscaledTime;
            if (!swipe.Tracking || now - swipe.StartTime > .65f)
            {
                swipe = new Swipe { Tracking = true, Start = point, StartTime = now };
                return 0;
            }
            var travel = point - swipe.Start;
            if (Mathf.Abs(travel.x) < .12f ||
                Mathf.Abs(travel.x) < Mathf.Max(Mathf.Abs(travel.y), Mathf.Abs(travel.z)) * 1.4f) return 0;
            // A hand can linger over the preview after swiping. Re-arm when it leaves the row.
            swipe.Consumed = true;
            return travel.x < 0f ? 1 : -1;
        }

        private struct Swipe
        {
            public Vector3 Start;
            public float StartTime;
            public bool Tracking, Consumed;
        }
    }
}
