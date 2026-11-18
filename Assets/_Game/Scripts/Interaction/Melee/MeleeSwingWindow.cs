using System;
using System.Collections.Generic;

namespace Game4026.Interaction.Melee
{
    /// <summary>A swing must slow down to re-arm. Sustained movement cannot retrigger on timeout.</summary>
    public sealed class MeleeSwingWindow
    {
        private readonly float startSpeed;
        private readonly float stopSpeed;
        private readonly float duration;
        private readonly HashSet<object> targets = new HashSet<object>();
        private bool armed;
        private float elapsed;
        public bool IsOpen { get; private set; }
        public uint SwingId { get; private set; }

        public MeleeSwingWindow(float startSpeed, float stopSpeed, float duration)
        {
            if (!MeleeMotion.IsFinite(startSpeed) || !MeleeMotion.IsFinite(stopSpeed) ||
                !MeleeMotion.IsFinite(duration) || startSpeed <= stopSpeed || stopSpeed < 0 || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(startSpeed));
            this.startSpeed = startSpeed;
            this.stopSpeed = stopSpeed;
            this.duration = duration;
            Reset();
        }

        public bool Advance(float speed, float deltaTime)
        {
            if (!MeleeMotion.IsFinite(speed) || !MeleeMotion.IsFinite(deltaTime) || speed < 0 || deltaTime <= 0)
            {
                Reset();
                return false;
            }
            if (speed <= stopSpeed)
            {
                IsOpen = false;
                armed = true;
                elapsed = 0f;
                return false;
            }
            if (IsOpen)
            {
                elapsed += deltaTime;
                if (elapsed >= duration) IsOpen = false;
            }
            else if (armed && speed >= startSpeed)
            {
                targets.Clear();
                armed = false;
                IsOpen = true;
                elapsed = 0f;
                ++SwingId;
            }
            return IsOpen;
        }

        public bool TryRegisterTarget(object identity) => IsOpen && identity != null && targets.Add(identity);

        /// <summary>After grab, teleport or tracking discontinuity, require slow motion before attacking.</summary>
        public void Reset()
        {
            IsOpen = false;
            armed = false;
            elapsed = 0f;
            targets.Clear();
        }
    }
}
