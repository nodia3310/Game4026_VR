using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game4026.Interaction.Melee
{
    /// <summary>Optional: put one on a target root to group all child hit colliders into one hit per swing.</summary>
    public sealed class MeleeHitTarget : MonoBehaviour { }

    [Serializable]
    public struct MeleeHit
    {
        public Collider Collider;
        public GameObject Target;
        public Vector3 Point;
        public Vector3 Direction;
        public float Speed;
        public float KineticEnergy;
        public float EstimatedForce;
        public uint SwingId;
    }

    [Serializable]
    public sealed class MeleeHitEvent : UnityEvent<MeleeHit> { }
}
