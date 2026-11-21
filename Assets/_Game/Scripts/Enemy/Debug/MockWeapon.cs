using Game4026.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game4026.Enemy.DebugTools
{
    /// <summary>
    /// Desktop stand-in for A's weapons (spec 4.8): left click raycasts from the mouse position and calls
    /// <see cref="IDamageable.TakeDamage"/> with isWeakPoint = false, exactly like A's weapons do.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MockWeapon : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0.1f)] private float range = 15f;
        [SerializeField] private LayerMask hitMask = ~0;
        [Header("Parry (desktop)")]
        [SerializeField] private Key parryKey = Key.F;
        [SerializeField, Min(0.1f)] private float parryRange = 3f;

        public float Damage { get => damage; set => damage = value; }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[parryKey].wasPressedThisFrame)
                TryParryNearest();

            Mouse mouse = Mouse.current;
            if (mouse == null || viewCamera == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Fire(viewCamera.ScreenPointToRay(mouse.position.ReadValue()));
        }

        /// <summary>
        /// Desktop parry: hits the nearest enemy weapon within <see cref="parryRange"/> the way A's weapon would
        /// (a moving dagger is hard to click). Returns true when an attack was deflected.
        /// </summary>
        public bool TryParryNearest()
        {
            // A flying bolt in range is reflected first (it is the more urgent threat).
            EnemyProjectile nearestBolt = null;
            float bestBolt = parryRange * parryRange;
            foreach (EnemyProjectile p in FindObjectsByType<EnemyProjectile>(FindObjectsInactive.Exclude)) // key press only
            {
                if (!p.IsFlying || p.IsReflected)
                    continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d <= bestBolt) { bestBolt = d; nearestBolt = p; }
            }
            if (nearestBolt != null)
            {
                bool reflected = nearestBolt.Reflect();
                Debug.Log(reflected ? "[MockWeapon] REFLECT!" : "[MockWeapon] reflect failed", nearestBolt);
                return reflected;
            }

            EnemyWeapon nearest = null;
            float best = parryRange * parryRange;
            foreach (EnemyWeapon w in FindObjectsByType<EnemyWeapon>(FindObjectsInactive.Exclude)) // key press only
            {
                float d = (w.Tip.position - transform.position).sqrMagnitude;
                if (d <= best) { best = d; nearest = w; }
            }
            if (nearest == null)
            {
                Debug.Log("[MockWeapon] parry: no enemy weapon or bolt in range");
                return false;
            }
            bool parried = nearest.TryDeflect(nearest.Tip.position, nearest.Attack != null ? nearest.Attack.Now : Time.time); // no damage: a body weapon is not hurt by a missed parry
            Debug.Log(parried ? "[MockWeapon] PARRY!" : "[MockWeapon] parry missed (no parry window open)", nearest);
            return parried;
        }

        /// <summary>Raycasts once and damages the first IDamageable hit. Returns true on a damageable hit.</summary>
        public bool Fire(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
                return false;

            // Only on click, not every frame.
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null)
                return false;

            target.TakeDamage(damage, hit.point, false);
            Debug.Log($"[MockWeapon] hit {hit.collider.name} ({target.GetType().Name}) for {damage:0.#} base damage", hit.collider);
            return true;
        }
    }
}
