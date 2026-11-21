using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>What <see cref="AttackRunner"/> needs from the enemy. <see cref="EnemyAttack"/> implements it.</summary>
    public interface IAttackHost
    {
        /// <summary>Turn toward the player (only before the swing starts, so a sidestep can dodge it).</summary>
        void FaceTarget(float deltaTime);
        /// <summary>Lock the lunge direction and distance.</summary>
        void BeginLunge(AttackDefinition step);
        /// <summary>Move this fraction (0–1) of the locked lunge distance.</summary>
        void MoveLunge(float fraction);
        /// <summary>Hit check for this step; damages the player on a hit.</summary>
        bool ResolveHit(AttackDefinition step);
        /// <summary>Extra recovery wanted by the animation (e.g. let the clip finish). Negative = none.</summary>
        float RecoveryOverride { get; }
        void OnStepStarted(in AttackStepTiming timing);
        /// <summary>The step's parry window opened: the strike is coming, the player can deflect it now.</summary>
        void OnParryWindowOpened(in AttackStepTiming timing);
    }

    /// <summary>When one step's animation should play, in real seconds from the step start.</summary>
    public readonly struct AttackStepTiming
    {
        public readonly AttackDefinition Attack;
        public readonly AttackDefinition Step;
        public readonly int Index;
        public readonly bool IsLast;
        /// <summary>Animator speed for the clip (animSpeed × enemy attack speed).</summary>
        public readonly float Playback;
        public readonly float ClipStartTime;
        /// <summary>The clip freezes on its wind-up pose from here...</summary>
        public readonly float HoldStart;
        /// <summary>...for this long (the telegraph).</summary>
        public readonly float HoldDuration;
        public readonly float HitDelay;
        /// <summary>The parry window runs from here to <see cref="HitDelay"/>. Negative = this step cannot be parried.</summary>
        public readonly float ParryOpen;

        public bool IsParryable => ParryOpen >= 0f;

        public AttackStepTiming(AttackDefinition attack, AttackDefinition step, int index, bool isLast,
            float playback, float clipStart, float holdStart, float holdDuration, float hitDelay, float parryOpen = -1f)
        {
            Attack = attack; Step = step; Index = index; IsLast = isLast; Playback = playback;
            ClipStartTime = clipStart; HoldStart = holdStart; HoldDuration = holdDuration; HitDelay = hitDelay;
            ParryOpen = parryOpen;
        }
    }

    /// <summary>
    /// Runs one attack (spec 4.5 EnemyAttackRunner) as a timeline: per step, wind-up → telegraph hold → swing → hit
    /// check → gap/recovery. Combos chain their steps without returning to Chase. Plain C#; time comes from Tick.
    /// </summary>
    public sealed class AttackRunner
    {
        private AttackDefinition attack;
        private AttackDefinition step;
        private int index;
        private float speed = 1f;
        private float elapsed;

        private float faceUntil, lungeStart, lungeEnd, hitDelay, recoveryEnd, parryOpen;
        private bool isLunge, lungeBegun, hitDone, parryNotified;
        private AttackStepTiming timing;
        private float lungeDone;

        public bool IsRunning { get; private set; }
        public AttackDefinition Attack => IsRunning ? attack : null;
        public AttackDefinition Step => IsRunning ? step : null;
        public int StepIndex => index;
        /// <summary>True from the step start until its hit check.</summary>
        public bool IsBeforeHit => IsRunning && !hitDone;
        /// <summary>True between the step's parry window opening and its hit check.</summary>
        public bool IsParryWindowOpen => IsRunning && !hitDone && parryOpen >= 0f && elapsed >= parryOpen;
        /// <summary>Timing of the step being played.</summary>
        public AttackStepTiming Timing => timing;

        public int StepCount => CountSteps(attack);

        public void Start(AttackDefinition definition, float speedMultiplier, IAttackHost host)
        {
            attack = definition;
            speed = Mathf.Max(0.1f, speedMultiplier);
            IsRunning = definition != null;
            if (IsRunning)
                StartStep(0, host);
        }

        public void Stop()
        {
            IsRunning = false;
            attack = null;
            step = null;
        }

        /// <summary>Advances the attack. Returns false once it has finished (or was never started).</summary>
        public bool Tick(float deltaTime, IAttackHost host)
        {
            if (!IsRunning)
                return false;
            elapsed += deltaTime;

            if (elapsed < faceUntil)
                host.FaceTarget(deltaTime);

            if (isLunge && !hitDone)
            {
                if (!lungeBegun && elapsed >= lungeStart)
                {
                    lungeBegun = true;
                    host.BeginLunge(step);
                }
                if (lungeBegun)
                    AdvanceLunge(host, lungeEnd > lungeStart ? Mathf.Clamp01((elapsed - lungeStart) / (lungeEnd - lungeStart)) : 1f);
            }

            if (!hitDone && !parryNotified && parryOpen >= 0f && elapsed >= parryOpen)
            {
                parryNotified = true;
                host.OnParryWindowOpened(timing);
            }

            if (!hitDone && elapsed >= hitDelay)
            {
                if (isLunge)
                {
                    if (!lungeBegun) { lungeBegun = true; host.BeginLunge(step); }
                    AdvanceLunge(host, 1f);
                }
                hitDone = true;
                host.ResolveHit(step);
                bool last = index >= StepCount - 1;
                float gap = last ? FinalRecovery(host) : step.RecoveryTime / speed;
                recoveryEnd = hitDelay + gap;
            }

            if (hitDone && elapsed >= recoveryEnd)
            {
                if (index >= StepCount - 1)
                {
                    Stop();
                    return false;
                }
                StartStep(index + 1, host);
            }
            return true;
        }

        private float FinalRecovery(IAttackHost host)
        {
            float recovery = attack.RecoveryTime / speed;
            float fromAnimation = host.RecoveryOverride;
            return fromAnimation > recovery ? fromAnimation : recovery;
        }

        private void AdvanceLunge(IAttackHost host, float fraction)
        {
            if (fraction <= lungeDone)
                return;
            host.MoveLunge(fraction - lungeDone);
            lungeDone = fraction;
        }

        private void StartStep(int stepIndex, IAttackHost host)
        {
            index = stepIndex;
            step = StepAt(attack, stepIndex);
            elapsed = 0f;
            hitDone = false;
            lungeBegun = false;
            lungeDone = 0f;
            isLunge = step.Type == AttackType.Lunge;

            float playback = step.AnimSpeed * speed;
            float clipStart = Mathf.Min(step.ClipStartTime, step.HitTime);
            float pose = Mathf.Clamp(step.TelegraphPoseTime, clipStart, step.HitTime);
            float telegraph = (stepIndex == 0 ? attack.TelegraphTime : 0f) + (step != attack ? step.TelegraphTime : 0f);
            telegraph /= speed;

            float holdStart = (pose - clipStart) / playback;
            faceUntil = holdStart + telegraph;
            hitDelay = telegraph + (step.HitTime - clipStart) / playback;
            lungeStart = faceUntil;
            lungeEnd = step.LungeDuration > 0f ? Mathf.Min(hitDelay, lungeStart + step.LungeDuration / speed) : hitDelay;
            parryOpen = step.IsParryable ? Mathf.Max(0f, hitDelay - step.ParryWindow / speed) : -1f;
            parryNotified = false;

            timing = new AttackStepTiming(attack, step, stepIndex, stepIndex >= StepCount - 1,
                playback, clipStart, holdStart, telegraph, hitDelay, parryOpen);
            host.OnStepStarted(timing);
        }

        /// <summary>A Combo plays its non-null steps; anything else is a single step (itself).</summary>
        public static int CountSteps(AttackDefinition a)
        {
            if (a == null)
                return 0;
            if (a.Type != AttackType.Combo || a.ComboSteps == null)
                return 1;
            int count = 0;
            foreach (AttackDefinition s in a.ComboSteps)
                if (s != null)
                    count++;
            return count > 0 ? count : 1;
        }

        public static AttackDefinition StepAt(AttackDefinition a, int stepIndex)
        {
            if (a.Type != AttackType.Combo || a.ComboSteps == null)
                return a;
            int n = 0;
            foreach (AttackDefinition s in a.ComboSteps)
            {
                if (s == null)
                    continue;
                if (n == stepIndex)
                    return s;
                n++;
            }
            return a;
        }
    }
}
