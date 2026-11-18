using UnityEngine;

namespace Game4026.Interaction.Ranged
{
    /// <summary>Defers release until the frame's selection changes have settled.</summary>
    public sealed class BowReleaseGate
    {
        private bool pending;
        private float power;
        public bool HasPending => pending;
        public void Queue(float normalizedDraw) { pending = true; power = Mathf.Clamp01(normalizedDraw); }
        public void Cancel() { pending = false; }
        public bool TryCommit(bool stillHeldAndTracked, out float normalizedDraw)
        {
            normalizedDraw = power;
            bool shouldFire = pending && stillHeldAndTracked;
            pending = false;
            return shouldFire;
        }
    }

    /// <summary>One pull/release cycle. Cancellation consumes the cycle without shooting.</summary>
    public sealed class BowDrawState
    {
        public bool IsDrawing { get; private set; }
        public float DrawDistance { get; private set; }
        public float NormalizedDraw { get; private set; }

        public void Begin()
        {
            IsDrawing = true;
            DrawDistance = NormalizedDraw = 0f;
        }

        public void Sample(Vector3 handOffset, Vector3 backwardAxis, float maxDraw)
        {
            if (!IsDrawing) return;
            DrawDistance = ProjectDraw(handOffset, backwardAxis, maxDraw);
            NormalizedDraw = maxDraw > 0f ? DrawDistance / maxDraw : 0f;
        }

        public bool TryRelease(float minimumDraw, out float normalizedDraw)
        {
            normalizedDraw = NormalizedDraw;
            bool shoot = IsDrawing && DrawDistance > 0f && DrawDistance >= Mathf.Max(0f, minimumDraw);
            Cancel();
            return shoot;
        }

        public void Cancel()
        {
            IsDrawing = false;
            DrawDistance = NormalizedDraw = 0f;
        }

        public static float ProjectDraw(Vector3 offset, Vector3 backwardAxis, float maximumDraw)
        {
            if (maximumDraw <= 0f || backwardAxis.sqrMagnitude < 0.000001f) return 0f;
            float projected = Vector3.Dot(offset, backwardAxis.normalized);
            if (float.IsNaN(projected) || float.IsInfinity(projected)) return 0f;
            return Mathf.Clamp(projected, 0f, maximumDraw);
        }

        public static Vector3 BallisticPosition(Vector3 origin, Vector3 velocity, Vector3 gravity, float seconds)
            => origin + velocity * seconds + gravity * (0.5f * seconds * seconds);
    }
}
