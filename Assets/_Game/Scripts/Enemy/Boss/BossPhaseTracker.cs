using System.Collections.Generic;

namespace Game4026.Enemy
{
    /// <summary>
    /// Engine-free phase selection. Phase i is entered when the health fraction is at or below
    /// thresholds[i]; phases only move forward. One big hit can cross several thresholds at once.
    /// </summary>
    public sealed class BossPhaseTracker
    {
        private readonly IReadOnlyList<float> thresholds;

        public int Current { get; private set; }

        /// <param name="thresholds">One per phase, in phase order (normally descending, first = 1).</param>
        public BossPhaseTracker(IReadOnlyList<float> thresholds)
        {
            this.thresholds = thresholds;
            Current = 0;
        }

        public int PhaseCount => thresholds.Count;

        /// <summary>Highest phase whose threshold the fraction has reached (never below the current phase).</summary>
        public int PhaseFor(float healthFraction)
        {
            int phase = Current;
            for (int i = Current + 1; i < thresholds.Count; i++)
                if (healthFraction <= thresholds[i])
                    phase = i;
            return phase;
        }

        /// <summary>Moves forward if the fraction reached a later phase. Returns true when the phase changed.</summary>
        public bool TryAdvance(float healthFraction, out int from, out int to)
        {
            from = Current;
            to = PhaseFor(healthFraction);
            if (to == from)
                return false;
            Current = to;
            return true;
        }

        public void Reset() => Current = 0;
    }
}
