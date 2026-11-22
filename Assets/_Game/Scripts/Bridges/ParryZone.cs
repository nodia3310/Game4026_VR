using System;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>Overlap queries for a collider's shape grown by a padding (works on disabled colliders too).</summary>
    public static class ParryZone
    {
        /// <summary>Fills <paramref name="buffer"/> with colliders inside the grown shape. Triggers are ignored.</summary>
        public static int Overlap(Collider shape, float radiusPadding, float lengthPadding, Collider[] buffer, int mask)
        {
            const QueryTriggerInteraction triggers = QueryTriggerInteraction.Ignore;
            switch (shape)
            {
                case CapsuleCollider capsule:
                    Capsule(capsule, radiusPadding, lengthPadding, out Vector3 a, out Vector3 b, out float radius);
                    return Physics.OverlapCapsuleNonAlloc(a, b, radius, buffer, mask, triggers);
                case BoxCollider box:
                    Box(box, radiusPadding, out Vector3 center, out Vector3 half, out Quaternion rotation);
                    return Physics.OverlapBoxNonAlloc(center, half, buffer, rotation, mask, triggers);
                case SphereCollider sphere:
                    float r = sphere.radius * MaxAbs(sphere.transform.lossyScale) + radiusPadding;
                    return Physics.OverlapSphereNonAlloc(sphere.transform.TransformPoint(sphere.center), r, buffer, mask, triggers);
                default:
                    Bounds bounds = shape.bounds;
                    return Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * radiusPadding, buffer, Quaternion.identity, mask, triggers);
            }
        }

        /// <summary>First collider in the zone that passes <paramref name="accept"/>, with its point closest to the zone centre.</summary>
        public static bool Find(Collider shape, float radiusPadding, float lengthPadding, Collider[] buffer, int mask,
            Predicate<Collider> accept, Transform ignoreRoot, out Collider found, out Vector3 point)
        {
            found = null;
            point = default;
            if (shape == null || accept == null)
                return false;
            int count = Overlap(shape, radiusPadding, lengthPadding, buffer, mask);
            for (int i = 0; i < count; i++)
            {
                Collider other = buffer[i];
                if (other == null || other == shape || (ignoreRoot != null && other.transform.IsChildOf(ignoreRoot)))
                    continue;
                if (!accept(other))
                    continue;
                found = other;
                point = other.ClosestPoint(Center(shape));
                return true;
            }
            return false;
        }

        public static Vector3 Center(Collider shape)
        {
            switch (shape)
            {
                case CapsuleCollider c: return c.transform.TransformPoint(c.center);
                case BoxCollider b: return b.transform.TransformPoint(b.center);
                case SphereCollider s: return s.transform.TransformPoint(s.center);
                default: return shape.bounds.center;
            }
        }

        /// <summary>A capsule grown: segment ends a, b (lengthened at both ends) and radius (padded).</summary>
        public static void Capsule(CapsuleCollider capsule, float radiusPadding, float lengthPadding, out Vector3 a, out Vector3 b, out float radius)
        {
            Transform t = capsule.transform;
            Vector3 scale = Abs(t.lossyScale);
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            float axisScale = capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z;
            float radialScale = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z)
                : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
            float baseRadius = capsule.radius * radialScale;
            float halfSegment = Mathf.Max(0f, capsule.height * axisScale * 0.5f - baseRadius) + lengthPadding;
            Vector3 center = t.TransformPoint(capsule.center);
            Vector3 dir = t.TransformDirection(axis).normalized;
            a = center - dir * halfSegment;
            b = center + dir * halfSegment;
            radius = baseRadius + radiusPadding;
        }

        public static void Box(BoxCollider box, float padding, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            Transform t = box.transform;
            center = t.TransformPoint(box.center);
            halfExtents = Vector3.Scale(box.size * 0.5f, Abs(t.lossyScale)) + Vector3.one * padding;
            rotation = t.rotation;
        }

        /// <summary>Wire outline of the grown shape (gizmos).</summary>
        public static void DrawGizmo(Collider shape, float radiusPadding, float lengthPadding)
        {
            if (shape is CapsuleCollider capsule)
            {
                Capsule(capsule, radiusPadding, lengthPadding, out Vector3 a, out Vector3 b, out float r);
                Gizmos.DrawWireSphere(a, r);
                Gizmos.DrawWireSphere(b, r);
                Vector3 side = Vector3.Cross(b - a, Vector3.up).normalized;
                if (side == Vector3.zero) side = Vector3.right;
                Gizmos.DrawLine(a + side * r, b + side * r);
                Gizmos.DrawLine(a - side * r, b - side * r);
            }
            else if (shape is BoxCollider box)
            {
                Box(box, radiusPadding, out Vector3 c, out Vector3 half, out Quaternion rot);
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(c, rot, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, half * 2f);
                Gizmos.matrix = old;
            }
            else if (shape is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.transform.TransformPoint(sphere.center), sphere.radius * MaxAbs(sphere.transform.lossyScale) + radiusPadding);
            }
            else
            {
                Bounds bounds = shape.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size + Vector3.one * radiusPadding * 2f);
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        private static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
    }
}
