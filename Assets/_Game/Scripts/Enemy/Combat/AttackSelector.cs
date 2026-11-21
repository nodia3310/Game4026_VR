namespace Game4026.Enemy
{
    /// <summary>
    /// Picks the next attack (spec 4.5): in range, own cooldown over, global cooldown over, chosen by weight.
    /// Plain C# with time passed in, so it is tested without Play Mode. No allocation after the first call.
    /// </summary>
    public sealed class AttackSelector
    {
        private float[] nextReady = new float[0];
        private float globalReady;

        /// <summary>Index into <paramref name="attacks"/>, or -1. <paramref name="random01"/> is in [0, 1).</summary>
        public int Select(AttackDefinition[] attacks, float distance, float now, float random01)
        {
            if (attacks == null || now < globalReady)
                return -1;
            EnsureSize(attacks.Length);

            float total = 0f;
            for (int i = 0; i < attacks.Length; i++)
                if (IsEligible(attacks[i], i, distance, now))
                    total += attacks[i].Weight;
            if (total <= 0f)
                return -1;

            float pick = random01 * total;
            int last = -1;
            for (int i = 0; i < attacks.Length; i++)
            {
                if (!IsEligible(attacks[i], i, distance, now))
                    continue;
                last = i;
                pick -= attacks[i].Weight;
                if (pick < 0f)
                    return i;
            }
            return last; // random01 == 1 or float rounding
        }

        private bool IsEligible(AttackDefinition a, int index, float distance, float now) =>
            a != null && a.IsSupported && a.Weight > 0f &&
            distance >= a.MinRange && distance <= a.MaxRange && now >= nextReady[index];

        /// <summary>The attack started: its own cooldown runs from now.</summary>
        public void MarkStarted(int index, AttackDefinition attack, float now, float speedMultiplier)
        {
            if (index < 0 || attack == null)
                return;
            EnsureSize(index + 1);
            nextReady[index] = now + attack.Cooldown / speedMultiplier;
        }

        /// <summary>The attack ended (finished or interrupted): no attack at all until the global cooldown is over.</summary>
        public void MarkEnded(float now, float globalCooldown, float speedMultiplier) =>
            globalReady = now + globalCooldown / speedMultiplier;

        public bool IsReady(int index, float now) => now >= globalReady && (index >= nextReady.Length || now >= nextReady[index]);

        public void Reset()
        {
            for (int i = 0; i < nextReady.Length; i++)
                nextReady[i] = 0f;
            globalReady = 0f;
        }

        /// <summary>
        /// While nothing is ready, Chase stops and faces the player inside this distance instead of pushing into them:
        /// the longest reach of the attacks usable from point-blank range (minRange 0).
        /// </summary>
        public static float HoldRange(AttackDefinition[] attacks)
        {
            float range = 0f;
            if (attacks == null)
                return range;
            foreach (AttackDefinition a in attacks)
                if (a != null && a.IsSupported && a.MinRange <= 0f && a.MaxRange > range)
                    range = a.MaxRange;
            return range;
        }

        private void EnsureSize(int size)
        {
            if (nextReady.Length >= size)
                return;
            var grown = new float[size];
            nextReady.CopyTo(grown, 0);
            nextReady = grown;
        }
    }
}
