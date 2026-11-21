using System.Collections.Generic;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Universal hit feel ("卡肉") for any enemy, rigged or not. On every accepted hit, parry, blade block and death:
    /// freezes the enemy for a few frames (<see cref="EnemyController.HitStop"/>: AI, NavMesh, Animator and statue motion
    /// all pause), vibrates the model along the hit direction while frozen, flashes it white, emits sparks, plays a sound
    /// and raises <see cref="HitImpacts"/> so the weapon bridge can rumble the controller that hit.
    /// Only the enemy freezes, never Time.timeScale: in VR the player's hands and head must keep moving.
    /// Settings live in a shared <see cref="HitFeedbackProfile"/>. No per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after StatueLocomotion / Animator writes, so the shake adds on top
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyHitFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private HitFeedbackProfile profile;
        [Tooltip("Transform that shakes. Empty = the statue model, else the Animator's object, else the first child.")]
        [SerializeField] private Transform visual;
        [Tooltip("Renderers that flash. Empty = every mesh under this enemy (the attack glint excluded).")]
        [SerializeField] private Renderer[] flashRenderers = new Renderer[0];

        private EnemyController controller;
        private EnemyHealth health;
        private EnemyWeapon[] weapons = new EnemyWeapon[0];

        // Shake
        private Vector3 shakeDirection, shakeSide, appliedOffset;
        private float shakeAmount, shakeTime, shakeLength;

        // Flash
        private Material flashInstance;
        private Material[][] originalMaterials, flashedMaterials;
        private bool flashing;
        private float flashTime, flashLength, flashPeak;

        public HitFeedbackProfile Profile => profile != null ? profile : HitFeedbackProfile.Default;
        public Transform Visual => visual;
        public bool IsFlashing => flashing;
        public bool IsShaking => shakeTime < shakeLength;

        /// <summary>Raised for every reaction this enemy plays.</summary>
        public event System.Action<EnemyHitFeedback, HitImpact> Played;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            health = GetComponent<EnemyHealth>();
            weapons = GetComponentsInChildren<EnemyWeapon>(true); // once
            if (visual == null)
                visual = FindVisual();
        }

        private Transform FindVisual()
        {
            if (TryGetComponent(out StatueLocomotion statue) && statue.Model != null)
                return statue.Model;
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
                return animator.transform;
            return transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        private void Start() => BuildFlash();

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].Deflected += HandleDeflected;
                weapons[i].Blocked += HandleBlocked;
            }
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].Deflected -= HandleDeflected;
                weapons[i].Blocked -= HandleBlocked;
            }
            UndoShake();
            shakeLength = 0f;
            EndFlash();
        }

        private void OnDestroy()
        {
            if (flashInstance != null)
                Destroy(flashInstance);
        }

        // ---- Sources ----

        private void HandleDamaged(EnemyHealth _, EnemyDamageResult result) =>
            Play(HitFeedbackProfile.KindOf(result), result.HitPoint, result.AppliedDamage);

        private void HandleDeflected(EnemyWeapon _, Vector3 point) => Play(HitKind.Parry, point, 0f);

        private void HandleBlocked(EnemyWeapon _, Vector3 point) => Play(HitKind.Blocked, point, 0f);

        /// <summary>Plays one reaction. Public so other systems (and tests) can trigger it.</summary>
        public HitImpact Play(HitKind kind, Vector3 point, float damage)
        {
            HitFeedbackProfile p = Profile;
            HitReaction r = p.For(kind);
            float stop = p.HitStopFor(kind, damage);

            if (stop > 0f)
                controller.HitStop(stop);
            StartShake(point, p.ShakeFor(kind, damage), Mathf.Max(stop, 0.05f));
            if (r.flash > 0f)
                StartFlash(r.flash, Mathf.Max(stop, p.FlashDuration));

            Vector3 normal = AwayFromPlayer(point);
            if (r.sparks > 0f)
                HitSparkPool.Emit(p.SparkPrefab, point, -normal, p.SparkColor(kind), Mathf.RoundToInt(p.SparkCount * r.sparks));
            AudioClip clip = p.PickClip(kind, Random.value);
            if (clip != null && Application.isPlaying)
                AudioSource.PlayClipAtPoint(clip, point, p.Volume);

            var impact = new HitImpact(kind, point, stop, r.haptic, r.hapticDuration);
            Played?.Invoke(this, impact);
            HitImpacts.Raise(impact);
            return impact;
        }

        /// <summary>Horizontal direction from the player (or the hit point) through the enemy: where a hit pushes it.</summary>
        private Vector3 AwayFromPlayer(Vector3 point)
        {
            PlayerTarget player = PlayerTarget.Current;
            Vector3 from = player != null ? player.Head.position : point;
            Vector3 d = transform.position - from;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f)
                d = -transform.forward;
            return d.normalized;
        }

        // ---- Shake (only while frozen) ----

        private void StartShake(Vector3 point, float amount, float length)
        {
            if (visual == null || amount <= 0f)
                return;
            shakeDirection = AwayFromPlayer(point);
            shakeSide = Vector3.Cross(Vector3.up, shakeDirection);
            shakeAmount = amount;
            shakeTime = 0f;
            shakeLength = length;
        }

        private void Update() => UndoShake(); // before the motion scripts write the model pose again

        private void UndoShake()
        {
            if (appliedOffset == Vector3.zero || visual == null)
                return;
            visual.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime; // real time: the reaction itself must not freeze
            if (visual != null && shakeTime < shakeLength)
            {
                Vector3 world = ShakeOffset(shakeTime, shakeLength, shakeAmount, Profile.ShakeFrequency, shakeDirection, shakeSide);
                Transform parent = visual.parent;
                appliedOffset = parent != null ? parent.InverseTransformVector(world) : world;
                visual.localPosition += appliedOffset;
                shakeTime += dt;
            }
            if (flashing)
                TickFlash(dt);
        }

        /// <summary>
        /// Decaying back-and-forth vibration: starts pushed away from the hitter (the "dent"), then rattles out.
        /// </summary>
        internal static Vector3 ShakeOffset(float time, float length, float amount, float frequency, Vector3 direction, Vector3 side)
        {
            if (length <= 0f || time >= length)
                return Vector3.zero;
            float decay = 1f - time / length;
            float phase = time * frequency * 2f * Mathf.PI;
            return (direction * Mathf.Cos(phase) + side * (0.35f * Mathf.Sin(phase * 1.7f))) * (amount * decay);
        }

        // ---- Flash: an extra additive material layered on each mesh ----

        private void BuildFlash()
        {
            Material template = Profile.FlashMaterial;
            if (template == null || flashedMaterials != null)
                return;
            flashInstance = new Material(template) { name = template.name + " (hit flash)" };
            SetFlashColor(0f);

            Renderer[] renderers = flashRenderers != null && flashRenderers.Length > 0 ? flashRenderers : CollectRenderers();
            originalMaterials = new Material[renderers.Length][];
            flashedMaterials = new Material[renderers.Length][];
            flashRenderers = renderers;
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] original = renderers[i].sharedMaterials;
                var flashed = new Material[original.Length + 1];
                original.CopyTo(flashed, 0);
                flashed[original.Length] = flashInstance;
                originalMaterials[i] = original;
                flashedMaterials[i] = flashed;
            }
        }

        private Renderer[] CollectRenderers()
        {
            var list = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) // once
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    continue;
                if (r.gameObject.name == "AttackGlint" || r.GetComponentInParent<EnemyProjectile>() != null)
                    continue;
                list.Add(r);
            }
            return list.ToArray();
        }

        private void StartFlash(float peak, float length)
        {
            if (flashedMaterials == null)
                BuildFlash();
            if (flashedMaterials == null)
                return;
            flashPeak = peak;
            flashTime = 0f;
            flashLength = length;
            SetFlashColor(peak);
            if (flashing)
                return;
            flashing = true;
            for (int i = 0; i < flashRenderers.Length; i++)
                if (flashRenderers[i] != null)
                    flashRenderers[i].sharedMaterials = flashedMaterials[i];
        }

        private void TickFlash(float dt)
        {
            flashTime += dt;
            if (flashTime >= flashLength)
            {
                EndFlash();
                return;
            }
            float k = 1f - flashTime / flashLength;
            SetFlashColor(flashPeak * k * k);
        }

        private void EndFlash()
        {
            if (!flashing)
                return;
            flashing = false;
            for (int i = 0; i < flashRenderers.Length; i++)
                if (flashRenderers[i] != null)
                    flashRenderers[i].sharedMaterials = originalMaterials[i];
        }

        private void SetFlashColor(float strength)
        {
            if (flashInstance == null)
                return;
            Color c = Profile.FlashColor * strength; // additive: black adds nothing, so brightness is linear
            c.a = strength > 0f ? 1f : 0f;
            if (flashInstance.HasProperty(BaseColorId)) flashInstance.SetColor(BaseColorId, c);
            if (flashInstance.HasProperty(ColorId)) flashInstance.SetColor(ColorId, c);
        }
    }
}
