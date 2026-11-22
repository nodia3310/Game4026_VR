using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>Spins a projectile's visual (e.g. a thrown weapon tumbling end over end). Put it on a child, not on the projectile root.</summary>
    public sealed class ProjectileSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 axis = Vector3.right;
        [Tooltip("Degrees per second.")]
        [SerializeField] private float speed = 900f;

        private Quaternion start;
        private EnemyProjectile projectile;

        private void Awake()
        {
            start = transform.localRotation;
            projectile = GetComponentInParent<EnemyProjectile>();
        }

        private void OnEnable() => transform.localRotation = start;

        private void Update()
        {
            // Held weapons (summoned, not thrown yet) stay still.
            if (projectile != null && !projectile.IsFlying)
            {
                transform.localRotation = start;
                return;
            }
            transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
        }
    }
}
