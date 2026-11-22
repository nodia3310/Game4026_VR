using System;
using Game4026.Contracts;
using UnityEngine;
using UnityEngine.Pool;

namespace Game4026.Enemy
{
    /// <summary>
    /// A short bolt with travel time (spec 4.5 EnemyProjectile). Flies straight; hitting the environment ends it,
    /// touching the player calls <see cref="IPlayerDamageReceiver.ApplyPlayerDamage"/>.
    /// It is also an <see cref="IDamageable"/> with a non-trigger collider, so A's melee weapon can hit it:
    /// that reflects it back toward the shooter (its weak point if it has one) as a player-side bolt that damages enemies.
    /// Arrows never reflect it (<see cref="IParryTarget"/>). Use an unlit/emissive material; no real light.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyProjectile : MonoBehaviour, IDamageable, IParryTarget
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [Tooltip("Sweep radius against walls and bodies.")]
        [SerializeField, Min(0.005f)] private float radius = 0.06f;
        [Tooltip("Hits the player when its path passes this close to the line from the player's body to head.")]
        [SerializeField, Min(0f)] private float playerHitRadius = 0.3f;
        [SerializeField, Min(0.1f)] private float lifetime = 4f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Reflected")]
        [SerializeField, Min(1f)] private float reflectSpeedMultiplier = 1.5f;
        [Tooltip("Base damage to an enemy (weak points multiply it).")]
        [SerializeField, Min(0f)] private float reflectedDamage = 20f;
        [Tooltip("Degrees per second the reflected bolt steers toward its target.")]
        [SerializeField, Min(0f)] private float reflectedHoming = 120f;
        [SerializeField] private Renderer[] tintRenderers = new Renderer[0];
        [SerializeField] private Color flyingColor = new Color(1f, 0.08f, 0.04f, 1f);
        [SerializeField] private Color reflectedColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Hit feel of the reflect (Reflect entry: the bolt freezes for its hit stop, then flies back). Empty = defaults.")]
        [SerializeField] private HitFeedbackProfile hitFeedback;

        private MaterialPropertyBlock block;
        private EnemyController owner;
        private Transform reflectTarget;
        private Vector3 direction;
        private float speed, damage, age, hitStopRemaining, hitStopScale;

        /// <summary>Reflected by the player's weapon (clang, sparks, haptics).</summary>
        public event Action<EnemyProjectile> Reflected;
        /// <summary>Ended. bool = it hit the player (or, when reflected, an enemy).</summary>
        public event Action<EnemyProjectile, bool> Expired;

        public bool IsFlying { get; private set; }
        public bool IsReflected { get; private set; }
        public EnemyController Owner => owner;
        public Vector3 Direction => direction;
        public float Speed => speed;

        internal ObjectPool<EnemyProjectile> Pool { get; set; }
        private Collider[] colliders;

        public void Launch(EnemyController shooter, Vector3 position, Vector3 flyDirection, float flySpeed, float playerDamage)
        {
            owner = shooter;
            direction = flyDirection.sqrMagnitude > 1e-6f ? flyDirection.normalized : Vector3.forward;
            speed = flySpeed;
            damage = playerDamage;
            age = 0f;
            hitStopRemaining = 0f;
            IsReflected = false;
            IsFlying = true;
            reflectTarget = null;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            gameObject.SetActive(true);
            Tint(flyingColor);
            IgnorePlayerBody();
        }

        /// <summary>Shows a pooled projectile before it is thrown (held by the pharaoh's hands), in its normal colour.</summary>
        public void ShowHeld()
        {
            IsFlying = false;
            IsReflected = false;
            gameObject.SetActive(true);
            Tint(flyingColor);
        }

        /// <summary>
        /// The projectile's collider exists only so the player's weapon can hit it. It must never shove the player's
        /// CharacterController (big thrown weapons pushed the player around); damage is checked separately in <see cref="Step"/>.
        /// </summary>
        private void IgnorePlayerBody()
        {
            PlayerTarget player = PlayerTarget.Current;
            if (player == null)
                return;
            CharacterController body = player.GetComponentInParent<CharacterController>();
            if (body == null)
                body = player.GetComponentInChildren<CharacterController>();
            if (body == null)
                return;
            if (colliders == null)
                colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                Physics.IgnoreCollision(colliders[i], body, true);
        }

        private void Update() => Step(Time.deltaTime);

        /// <summary>One frame of flight (public for tests).</summary>
        public void Step(float deltaTime)
        {
            if (!IsFlying)
                return;
            if (hitStopRemaining > 0f)
            {
                // Slow motion on reflect: the bolt hangs almost still, then flies back at full speed.
                float slowed = Mathf.Min(hitStopRemaining, deltaTime);
                hitStopRemaining -= slowed;
                deltaTime -= slowed * (1f - hitStopScale);
                if (deltaTime <= 0f)
                    return;
            }
            age += deltaTime;
            if (age >= lifetime)
            {
                End(false);
                return;
            }

            if (IsReflected)
                SteerTowardTarget(deltaTime);

            Vector3 from = transform.position;
            float distance = speed * deltaTime;
            Vector3 to = from + direction * distance;
            PlayerTarget player = PlayerTarget.Current;

            // VR heads have no reliable collider: test the player's body-to-head line directly.
            if (!IsReflected && player != null &&
                SegmentDistance(from, to, player.Body.position, player.Head.position) <= playerHitRadius)
            {
                player.Damageable?.ApplyPlayerDamage(damage);
                End(true);
                return;
            }

            if (TryFindHit(from, distance, player, out RaycastHit hit))
            {
                bool struck = false;
                if (IsReflected)
                    struck = DamageEnemy(hit.collider, hit.point);
                else if (player != null && hit.collider.transform.IsChildOf(player.transform))
                {
                    player.Damageable?.ApplyPlayerDamage(damage);
                    struck = true;
                }
                End(struck);
                return;
            }

            transform.position = to;
        }

        private bool TryFindHit(Vector3 from, float distance, PlayerTarget player, out RaycastHit best)
        {
            best = default;
            if (distance <= 0f)
                return false;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, Hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count && i < Hits.Length; i++)
            {
                Transform t = Hits[i].collider.transform;
                if (t.IsChildOf(transform))
                    continue;
                if (Hits[i].collider.GetComponentInParent<EnemyProjectile>() != null)
                    continue; // bolts pass through each other
                if (!IsReflected && owner != null && t.IsChildOf(owner.transform))
                    continue; // leaving the shooter
                if (IsReflected && player != null && t.IsChildOf(player.transform))
                    continue; // player-side now
                // Initial overlaps report distance 0 and a zero point; still count them.
                if (Hits[i].distance < bestDistance)
                {
                    bestDistance = Hits[i].distance;
                    best = Hits[i];
                    if (best.point == Vector3.zero && best.distance == 0f)
                        best.point = from;
                }
            }
            return bestDistance < float.PositiveInfinity;
        }

        private bool DamageEnemy(Collider collider, Vector3 point)
        {
            EnemyHealth health = collider.GetComponentInParent<EnemyHealth>();
            if (health == null)
                return false;
            // Straight to health (never a parry); weak point → weak-point hit. No hit cooldown: a volley of reflected
            // weapons lands together and every one of them should count.
            bool weak = collider.GetComponent<WeakPoint>() != null;
            health.TakeProjectileDamage(reflectedDamage, point, weak);
            return true;
        }

        /// <summary>The player's melee weapon hit the bolt: reflect it back at the shooter.</summary>
        public void TakeDamage(float hitDamage, Vector3 hitPoint, bool isWeakPoint) => Reflect();

        /// <summary>Arrows cannot reflect bolts.</summary>
        public bool TakeUnparryableHit(float hitDamage, Vector3 hitPoint) => false;

        /// <summary>Turns the bolt into a player-side bolt flying at its shooter. Returns false if already reflected or not flying.</summary>
        public bool Reflect()
        {
            if (!IsFlying || IsReflected)
                return false;
            IsReflected = true;
            speed *= reflectSpeedMultiplier;
            age = 0f;
            reflectTarget = FindReflectTarget(owner);
            direction = reflectTarget != null
                ? (reflectTarget.position - transform.position).normalized
                : -direction;
            if (direction.sqrMagnitude < 1e-6f)
                direction = Vector3.up;
            transform.rotation = Quaternion.LookRotation(direction);
            Tint(reflectedColor);
            PlayReflectFeedback();
            Reflected?.Invoke(this);
            return true;
        }

        private void PlayReflectFeedback()
        {
            HitFeedbackProfile p = hitFeedback != null ? hitFeedback : HitFeedbackProfile.Default;
            HitReaction r = p.For(HitKind.Reflect);
            float stop = p.HitStopFor(HitKind.Reflect, 0f);
            hitStopRemaining = stop;
            hitStopScale = p.HitStopTimeScale;
            Vector3 point = transform.position;
            if (r.sparks > 0f)
                HitSparkPool.Emit(p.SparkPrefab, point, direction, p.SparkColor(HitKind.Reflect), Mathf.RoundToInt(p.SparkCount * r.sparks));
            AudioClip clip = p.PickClip(HitKind.Reflect, UnityEngine.Random.value);
            if (clip != null && Application.isPlaying)
                AudioSource.PlayClipAtPoint(clip, point, p.Volume);
            HitImpacts.Raise(new HitImpact(HitKind.Reflect, point, stop, r.haptic, r.hapticDuration));
        }

        private void SteerTowardTarget(float deltaTime)
        {
            if (reflectTarget == null || !reflectTarget.gameObject.activeInHierarchy || reflectedHoming <= 0f)
                return;
            Vector3 wanted = reflectTarget.position - transform.position;
            if (wanted.sqrMagnitude < 1e-6f)
                return;
            direction = Vector3.RotateTowards(direction, wanted.normalized, reflectedHoming * Mathf.Deg2Rad * deltaTime, 0f);
            transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// The shooter's active weak point, else the centre of its biggest enabled solid collider (the body: the eye sits at
        /// the top of it, so a bolt aimed there can graze over), else its eye.
        /// </summary>
        private static Transform FindReflectTarget(EnemyController shooter)
        {
            if (shooter == null || !shooter.isActiveAndEnabled)
                return null;
            WeakPoint weakPoint = shooter.GetComponentInChildren<WeakPoint>(); // once per reflect
            if (weakPoint != null)
                return weakPoint.transform;

            Collider body = null;
            float biggest = 0f;
            foreach (Collider c in shooter.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger || c.GetComponent<ArenaBarrier>() != null)
                    continue; // aim at the body, not at a barrier / corridor wall around it
                Vector3 s = c.bounds.size;
                float volume = s.x * s.y * s.z;
                if (volume > biggest)
                {
                    biggest = volume;
                    body = c;
                }
            }
            return body != null ? body.transform : shooter.Eye;
        }

        private void End(bool struck)
        {
            IsFlying = false;
            Expired?.Invoke(this, struck);
            EnemyProjectilePool.Release(this);
        }

        private void Tint(Color color)
        {
            if (tintRenderers == null || tintRenderers.Length == 0)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (Renderer r in tintRenderers)
                if (r != null)
                    r.SetPropertyBlock(block);
        }

        /// <summary>Shortest distance between segments [a0,a1] and [b0,b1].</summary>
        public static float SegmentDistance(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            Vector3 d1 = a1 - a0, d2 = b1 - b0, r = a0 - b0;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-8f && e <= 1e-8f)
                return r.magnitude;
            if (a <= 1e-8f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-8f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return ((a0 + d1 * s) - (b0 + d2 * t)).magnitude;
        }
    }
}
