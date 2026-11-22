using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Where a thrown weapon is held: the handle centre in the projectile's local space (its length lies along local Z).
    /// The pharaoh's hands grip here and hold the weapon blade-up.
    /// </summary>
    public sealed class WeaponGrip : MonoBehaviour
    {
        [Tooltip("Handle centre, local to this projectile (length along Z). Negative Z = handle at the -Z end.")]
        [SerializeField] private Vector3 grip = new Vector3(0f, 0f, -0.6f);

        public Vector3 LocalGrip => grip;

        /// <summary>+1 when the handle is at the +Z end, -1 at the -Z end.</summary>
        public float HandleSign => grip.z >= 0f ? 1f : -1f;

        public void Configure(Vector3 localGrip) => grip = localGrip;
    }
}
