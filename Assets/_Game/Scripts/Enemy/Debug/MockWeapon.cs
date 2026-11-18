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

        public float Damage { get => damage; set => damage = value; }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || viewCamera == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Fire(viewCamera.ScreenPointToRay(mouse.position.ReadValue()));
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
