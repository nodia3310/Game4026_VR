using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Interaction.Ranged
{
    public readonly struct RayAimResult
    {
        public readonly Collider Collider;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public bool HasHit => Collider != null;
        public RayAimResult(Collider collider, Vector3 point, Vector3 normal, Vector3 direction)
        { Collider = collider; Point = point; Normal = normal; Direction = direction; }
    }

    /// <summary>A world-space aim ray. Reports hits without requiring an enemy implementation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class RayAim : MonoBehaviour
    {
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private Transform ignoreHierarchy;
        [SerializeField, Min(0.01f)] private float maximumDistance = 30f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private bool requireHeld = true;
        [SerializeField] private bool fireOnActivate = true;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private UnityEvent<Vector3> onHitPoint = new UnityEvent<Vector3>();
        public event Action<RayAimResult> Fired;
        public RayAimResult LastAim { get; private set; }
        private XRGrabInteractable grab;
        private RaycastHit[] hits = new RaycastHit[32];

        private void Awake() { grab = GetComponent<XRGrabInteractable>(); }
        private void OnEnable()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            grab.activated.AddListener(OnActivated);
            if (aimLine != null) { aimLine.useWorldSpace = true; aimLine.positionCount = 2; }
        }
        private void OnDisable()
        {
            if (grab != null) grab.activated.RemoveListener(OnActivated);
            if (aimLine != null) aimLine.enabled = false;
        }
        private void LateUpdate()
        {
            bool visible = !requireHeld || grab.isSelected;
            if (aimLine != null) aimLine.enabled = visible;
            if (!visible) return;
            LastAim = QueryAim();
            if (aimLine == null) return;
            aimLine.SetPosition(0, (rayOrigin != null ? rayOrigin : transform).position);
            aimLine.SetPosition(1, LastAim.Point);
        }
        private void OnActivated(ActivateEventArgs args)
        {
            if (!fireOnActivate) return;
            for (int i = 0; i < grab.interactorsSelecting.Count; i++)
            {
                if (!ReferenceEquals(grab.interactorsSelecting[i], args.interactorObject)) continue;
                Fire();
                return;
            }
        }

        public RayAimResult QueryAim()
        {
            Transform source = rayOrigin != null ? rayOrigin : transform;
            Transform ignored = ignoreHierarchy != null ? ignoreHierarchy : transform;
            float distance = Mathf.Max(0.01f, maximumDistance);
            var ray = new Ray(source.position, source.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, distance, hitMask, triggerInteraction);
            // A full buffer is potentially truncated and unsorted. Keep the enlarged buffer
            // so dense scenes only allocate when reaching a new high-water mark.
            while (count == hits.Length)
            {
                Array.Resize(ref hits, hits.Length * 2);
                count = Physics.RaycastNonAlloc(ray, hits, distance, hitMask, triggerInteraction);
            }
            float closest = float.PositiveInfinity;
            RaycastHit best = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(ignored) || hit.distance >= closest) continue;
                closest = hit.distance;
                best = hit;
            }
            return best.collider != null
                ? new RayAimResult(best.collider, best.point, best.normal, ray.direction)
                : new RayAimResult(null, ray.GetPoint(distance), Vector3.zero, ray.direction);
        }

        public void Fire()
        {
            if (!isActiveAndEnabled || (requireHeld && !grab.isSelected)) return;
            LastAim = QueryAim();
            Fired?.Invoke(LastAim);
            if (LastAim.HasHit) onHitPoint.Invoke(LastAim.Point);
        }
    }
}
