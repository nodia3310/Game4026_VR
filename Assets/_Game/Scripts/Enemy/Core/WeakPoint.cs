using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Put on a child object that holds a weak-point collider (spec 4.3). Hits on that collider resolve here
    /// first and are forwarded to the parent <see cref="EnemyHealth"/> as weak-point hits.
    /// </summary>
    public sealed class WeakPoint : MonoBehaviour, IDamageable
    {
        [Tooltip("Optional. Found in parents when left empty.")]
        [SerializeField] private EnemyHealth owner;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.85f, 0f, 0.9f);

        public EnemyHealth Owner
        {
            get
            {
                if (owner == null && transform.parent != null)
                    owner = transform.parent.GetComponentInParent<EnemyHealth>();
                return owner;
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint)
        {
            EnemyHealth target = Owner;
            if (target == null)
            {
                Debug.LogWarning($"{nameof(WeakPoint)} on '{name}' has no {nameof(EnemyHealth)} in its parents.", this);
                return;
            }

            target.TakeDamage(damage, hitPoint, true);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            if (TryGetComponent(out Collider col))
            {
                Bounds b = col.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
                Gizmos.DrawSphere(b.center, 0.03f);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 0.08f);
            }
        }
    }
}
