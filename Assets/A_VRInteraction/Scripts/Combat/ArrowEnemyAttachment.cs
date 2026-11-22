using Game4026.Enemy;
using Game4026.Interaction.Ranged;
using UnityEngine;

namespace Game4026.PlayerCombat
{
    [DisallowMultipleComponent, RequireComponent(typeof(ArrowProjectile), typeof(Rigidbody))]
    public sealed class ArrowEnemyAttachment : MonoBehaviour
    {
        private ArrowProjectile arrow;
        private Rigidbody body;
        private EnemyHealth enemy;
        private EnemyController controller;

        private void Awake()
        {
            arrow = GetComponent<ArrowProjectile>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable() => arrow.Impact += AttachOnImpact;

        private void OnDisable()
        {
            arrow.Impact -= AttachOnImpact;
            // The target's pool can re-enable the same object before the arrow's lifetime expires.
            if (enemy && !gameObject.activeInHierarchy) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (enemy) enemy.Died -= RemoveOnDeath;
            if (controller) controller.StateChanged -= RemoveOnDeadState;
        }

        private void AttachOnImpact(Collision collision)
        {
            var hitbox = collision.collider;
            if (!body.isKinematic || !hitbox) return;

            var weakPoint = hitbox.GetComponentInParent<WeakPoint>(true);
            enemy = weakPoint ? weakPoint.Owner : hitbox.GetComponentInParent<EnemyHealth>(true);
            if (!enemy) return;
            controller = enemy.GetComponent<EnemyController>();
            if (enemy.IsDead || !hitbox.gameObject.activeInHierarchy || (controller && controller.CurrentState == EnemyStateId.Dead))
            {
                Destroy(gameObject);
                return;
            }

            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            transform.SetParent(hitbox.transform, true);
            enemy.Died += RemoveOnDeath;
            if (controller) controller.StateChanged += RemoveOnDeadState;
        }

        private void RemoveOnDeath(EnemyHealth _) => Destroy(gameObject);

        private void RemoveOnDeadState(EnemyController _, EnemyStateId previous, EnemyStateId next)
        {
            if (next == EnemyStateId.Dead) Destroy(gameObject);
        }
    }
}
