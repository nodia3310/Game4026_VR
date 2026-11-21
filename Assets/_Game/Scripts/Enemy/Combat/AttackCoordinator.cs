using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Scene settings for <see cref="AttackTokens"/> (attack slots in group fights). Optional: without one in the scene
    /// the defaults apply (2 slots, 70° view cone). Changes in the Inspector apply live in Play Mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackCoordinator : MonoBehaviour
    {
        [Tooltip("Off = every enemy attacks whenever it can.")]
        [SerializeField] private bool useAttackSlots = true;
        [Tooltip("How many attack slots exist. Normal enemies take 1, elites more (EnemyData → Attack Slot Cost).")]
        [SerializeField, Min(1)] private int maxSlots = AttackTokens.DefaultMaxTokens;
        [Tooltip("Enemies may only start an attack inside this half-angle of where the player looks (degrees). 180 = anywhere.")]
        [SerializeField, Range(10f, 180f)] private float viewHalfAngle = AttackTokens.DefaultViewHalfAngle;

        private void OnEnable() => Apply();

        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                Apply();
        }

        private void Apply()
        {
            AttackTokens.Enabled = useAttackSlots;
            AttackTokens.MaxTokens = maxSlots;
            AttackTokens.ViewHalfAngle = viewHalfAngle;
        }
    }
}
