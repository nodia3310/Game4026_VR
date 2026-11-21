using System;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>How one kind of hit feels.</summary>
    [Serializable]
    public struct HitReaction
    {
        [Tooltip("Seconds the enemy freezes on contact (hit stop). Damage adds more, see Damage scaling.")]
        [Min(0f)] public float hitStop;
        [Tooltip("Metres the model vibrates along the hit direction while frozen.")]
        [Min(0f)] public float shake;
        [Tooltip("Brightness of the white flash (0 = none).")]
        [Range(0f, 1f)] public float flash;
        [Tooltip("Controller rumble 0..1 on the hand that hit.")]
        [Range(0f, 1f)] public float haptic;
        [Min(0f)] public float hapticDuration;
        [Tooltip("Sparks multiplier (0 = none).")]
        [Min(0f)] public float sparks;

        public HitReaction(float hitStop, float shake, float flash, float haptic, float hapticDuration, float sparks)
        {
            this.hitStop = hitStop;
            this.shake = shake;
            this.flash = flash;
            this.haptic = haptic;
            this.hapticDuration = hapticDuration;
            this.sparks = sparks;
        }
    }

    /// <summary>
    /// Shared hit-feel settings ("卡肉"): hit stop, shake, flash, sparks, sound and haptics per <see cref="HitKind"/>.
    /// One asset can drive every enemy; give a heavy enemy its own copy to make it feel different.
    /// </summary>
    [CreateAssetMenu(menuName = "Game4026/Enemy/Hit Feedback Profile", fileName = "HitFeedback_")]
    public sealed class HitFeedbackProfile : ScriptableObject
    {
        [Header("Per kind")]
        [SerializeField] private HitReaction normal = new HitReaction(0.06f, 0.02f, 0.55f, 0.35f, 0.05f, 1f);
        [SerializeField] private HitReaction weakPoint = new HitReaction(0.09f, 0.03f, 1f, 0.6f, 0.08f, 1.6f);
        [SerializeField] private HitReaction stagger = new HitReaction(0.1f, 0.035f, 0.85f, 0.6f, 0.08f, 1.3f);
        [SerializeField] private HitReaction kill = new HitReaction(0.16f, 0.045f, 1f, 0.9f, 0.14f, 2.2f);
        [SerializeField] private HitReaction parry = new HitReaction(0.14f, 0.03f, 0.9f, 1f, 0.12f, 2f);
        [SerializeField] private HitReaction blocked = new HitReaction(0.035f, 0.012f, 0f, 0.25f, 0.04f, 0.6f);
        [Tooltip("Hit stop here freezes the bolt itself before it flies back.")]
        [SerializeField] private HitReaction reflect = new HitReaction(0.07f, 0f, 0f, 0.7f, 0.08f, 1.2f);

        [Header("Damage scaling (Normal / WeakPoint / Stagger / Kill)")]
        [Tooltip("Extra hit stop per point of damage: harder swings stick longer.")]
        [SerializeField, Min(0f)] private float hitStopPerDamage = 0.0015f;
        [SerializeField, Min(0f)] private float maxHitStop = 0.2f;
        [Tooltip("Damage at which shake is 1x; it scales 0.6x..1.6x around this.")]
        [SerializeField, Min(0.01f)] private float referenceDamage = 20f;

        [Header("Shake")]
        [SerializeField, Min(1f)] private float shakeFrequency = 50f;

        [Header("Flash")]
        [Tooltip("Unlit additive material layered over the enemy's meshes (e.g. M_HitFlash). Empty = no flash.")]
        [SerializeField] private Material flashMaterial;
        [SerializeField] private Color flashColor = new Color(1f, 0.92f, 0.8f, 1f);
        [Tooltip("Fade time; never shorter than the hit stop.")]
        [SerializeField, Min(0.01f)] private float flashDuration = 0.1f;

        [Header("Sparks")]
        [Tooltip("Particle system with emission off; it is pooled and Emit() is called on it. Empty = no sparks.")]
        [SerializeField] private ParticleSystem sparkPrefab;
        [SerializeField, Min(0)] private int sparkCount = 10;
        [SerializeField] private Color hitSparkColor = new Color(1f, 0.55f, 0.25f, 1f);
        [SerializeField] private Color parrySparkColor = new Color(1f, 0.9f, 0.55f, 1f);

        [Header("Sound (optional, one is picked at random)")]
        [SerializeField] private AudioClip[] hitClips = new AudioClip[0];
        [SerializeField] private AudioClip[] weakPointClips = new AudioClip[0];
        [SerializeField] private AudioClip[] killClips = new AudioClip[0];
        [SerializeField] private AudioClip[] parryClips = new AudioClip[0];
        [Tooltip("A blade hit outside the parry window (a missed parry). Empty = silent; parry clips are never used for it.")]
        [SerializeField] private AudioClip[] blockedClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private static HitFeedbackProfile fallback;

        /// <summary>Code defaults (no flash material, sparks or sounds) for enemies without a profile.</summary>
        public static HitFeedbackProfile Default
        {
            get
            {
                if (fallback == null)
                {
                    fallback = CreateInstance<HitFeedbackProfile>();
                    fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return fallback;
            }
        }

        public float ShakeFrequency => shakeFrequency;
        public Material FlashMaterial => flashMaterial;
        public Color FlashColor => flashColor;
        public float FlashDuration => flashDuration;
        public ParticleSystem SparkPrefab => sparkPrefab;
        public int SparkCount => sparkCount;
        public float Volume => volume;

        public HitReaction For(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.WeakPoint: return weakPoint;
                case HitKind.Stagger: return stagger;
                case HitKind.Kill: return kill;
                case HitKind.Parry: return parry;
                case HitKind.Blocked: return blocked;
                case HitKind.Reflect: return reflect;
                default: return normal;
            }
        }

        public static bool ScalesWithDamage(HitKind kind) =>
            kind == HitKind.Normal || kind == HitKind.WeakPoint || kind == HitKind.Stagger || kind == HitKind.Kill;

        /// <summary>Hit stop seconds for a hit of this kind and damage.</summary>
        public float HitStopFor(HitKind kind, float damage)
        {
            float seconds = For(kind).hitStop;
            if (ScalesWithDamage(kind) && damage > 0f)
                seconds += damage * hitStopPerDamage;
            return Mathf.Min(seconds, maxHitStop);
        }

        /// <summary>Shake metres for a hit of this kind and damage.</summary>
        public float ShakeFor(HitKind kind, float damage)
        {
            float shake = For(kind).shake;
            if (ScalesWithDamage(kind))
                shake *= Mathf.Clamp(damage / referenceDamage, 0.6f, 1.6f);
            return shake;
        }

        public Color SparkColor(HitKind kind) =>
            kind == HitKind.Parry || kind == HitKind.Blocked || kind == HitKind.Reflect ? parrySparkColor : hitSparkColor;

        public AudioClip PickClip(HitKind kind, float random01)
        {
            AudioClip[] clips;
            switch (kind)
            {
                case HitKind.WeakPoint: clips = weakPointClips.Length > 0 ? weakPointClips : hitClips; break;
                case HitKind.Kill: clips = killClips.Length > 0 ? killClips : hitClips; break;
                // Parry sounds only on a successful deflect (parry or reflect), never on a miss.
                case HitKind.Parry:
                case HitKind.Reflect: clips = parryClips; break;
                case HitKind.Blocked: clips = blockedClips; break;
                default: clips = hitClips; break;
            }
            if (clips == null || clips.Length == 0)
                return null;
            return clips[Mathf.Min(clips.Length - 1, (int)(random01 * clips.Length))];
        }

        /// <summary>Kind of a body hit: kill, then weak point, then stagger, else normal.</summary>
        public static HitKind KindOf(in EnemyDamageResult result)
        {
            if (result.Killed) return HitKind.Kill;
            if (result.IsWeakPoint) return HitKind.WeakPoint;
            if (result.CausedStagger) return HitKind.Stagger;
            return HitKind.Normal;
        }
    }
}
