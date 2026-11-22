using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Enemy sounds that are not hit feedback: a summon sound when the enemy spawns (also when reused from the pool),
    /// and, on statues, a stone sound for each <see cref="StatueLocomotion.Impacted"/> (rise, slam landing, headbutt,
    /// roll, topple). Plays through a 3D AudioSource on the enemy, so the sound follows it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemySfx : MonoBehaviour
    {
        [Tooltip("Played when the enemy spawns.")]
        [SerializeField] private AudioClip spawnClip;
        [SerializeField, Range(0f, 1f)] private float spawnVolume = 0.8f;
        [Tooltip("Statue impacts (needs StatueLocomotion). Volume is scaled by the impact's strength.")]
        [SerializeField] private AudioClip impactClip;
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.9f;
        [Tooltip("Random pitch ± this, so repeated sounds differ.")]
        [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.06f;
        [SerializeField, Min(0.1f)] private float minDistance = 2f;
        [SerializeField, Min(1f)] private float maxDistance = 30f;

        private EnemyController controller;
        private StatueLocomotion statue;
        private AudioSource source;
        private int spawnPlayedFrame = -1;

        public AudioClip SpawnClip => spawnClip;
        public AudioClip ImpactClip => impactClip;
        public AudioSource Source => source;

        private void Awake()
        {
            controller = GetComponent<EnemyController>();
            statue = GetComponent<StatueLocomotion>();
            source = CreateSource(gameObject, minDistance, maxDistance);
        }

        private void OnEnable()
        {
            controller.StateChanged += HandleStateChanged;
            if (statue != null)
                statue.Impacted += HandleImpact;
            // The controller may have entered Spawn in its own OnEnable, before this one subscribed.
            if (controller.CurrentState == EnemyStateId.Spawn)
                PlaySpawn();
        }

        private void OnDisable()
        {
            controller.StateChanged -= HandleStateChanged;
            if (statue != null)
                statue.Impacted -= HandleImpact;
        }

        private void HandleStateChanged(EnemyController _, EnemyStateId previous, EnemyStateId next)
        {
            if (next == EnemyStateId.Spawn)
                PlaySpawn();
        }

        private void HandleImpact(StatueLocomotion _, float strength) => Play(impactClip, impactVolume * Mathf.Clamp01(strength));

        private void PlaySpawn()
        {
            if (spawnPlayedFrame == Time.frameCount)
                return;
            spawnPlayedFrame = Time.frameCount;
            Play(spawnClip, spawnVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || source == null || volume <= 0f)
                return;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, volume);
        }

        /// <summary>A 3D one-shot source (no auto play) on <paramref name="go"/>, reusing one that is already there.</summary>
        public static AudioSource CreateSource(GameObject go, float minDistance, float maxDistance)
        {
            if (!go.TryGetComponent(out AudioSource s))
                s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minDistance;
            s.maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
            s.dopplerLevel = 0f;
            return s;
        }
    }
}
