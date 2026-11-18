using UnityEngine;

namespace Game4026.Contracts
{
    /// <summary>
    /// Put on the player's XR Rig (A) or on a mock player (B's test scene) so enemies can find the player.
    /// Head is used for line of sight, Body for attack hits. Damage goes to the
    /// <see cref="IPlayerDamageReceiver"/> found on this object or its parents.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTarget : MonoBehaviour
    {
        [Tooltip("Tracked HMD camera. Falls back to this transform.")]
        [SerializeField] private Transform head;
        [Tooltip("Body centre used for attack hits. Falls back to Head.")]
        [SerializeField] private Transform body;

        /// <summary>The active player target, or null when none is enabled.</summary>
        public static PlayerTarget Current { get; private set; }

        public Transform Head => head != null ? head : transform;
        public Transform Body => body != null ? body : Head;
        private IPlayerDamageReceiver damageable;

        /// <summary>Resolved on first use (also works before Awake, e.g. in Edit Mode tests).</summary>
        public IPlayerDamageReceiver Damageable
        {
            get
            {
                if (damageable == null)
                    damageable = GetComponentInParent<IPlayerDamageReceiver>();
                return damageable;
            }
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        // Keeps the static clean when Domain Reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
