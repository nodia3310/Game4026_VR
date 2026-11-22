using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Background music following <see cref="DemoFlow"/>: the hall fight plays the battle track, the boss realm the boss
    /// track; silent before the fight, after victory and on defeat. Tracks crossfade (two 2D looping sources).
    /// Polls <see cref="DemoFlow.Phase"/> once per frame (an enum compare).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoMusic : MonoBehaviour
    {
        public enum Track { None, Hall, Boss }

        [SerializeField] private DemoFlow flow;
        [SerializeField] private AudioClip hallMusic;
        [SerializeField] private AudioClip bossMusic;
        [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
        [Tooltip("Seconds to fade a track in or out.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;

        private AudioSource[] sources = new AudioSource[0];
        private int current;
        private Track playing = Track.None;

        public Track Playing => playing;

        /// <summary>Which track a phase plays.</summary>
        public static Track TrackFor(DemoPhase phase)
        {
            switch (phase)
            {
                case DemoPhase.Hall: return Track.Hall;
                case DemoPhase.BossIntro:
                case DemoPhase.Boss: return Track.Boss;
                default: return Track.None;
            }
        }

        private void Awake()
        {
            if (flow == null)
                flow = GetComponent<DemoFlow>();
            sources = new[] { CreateSource("Music A"), CreateSource("Music B") };
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D: same in both ears wherever the player stands
            source.volume = 0f;
            source.priority = 0;
            return source;
        }

        private void Update()
        {
            Track wanted = flow != null && flow.isActiveAndEnabled ? TrackFor(flow.Phase) : Track.None;
            if (wanted != playing)
                Switch(wanted);
            Fade(Time.unscaledDeltaTime);
        }

        private void Switch(Track track)
        {
            playing = track;
            AudioClip clip = track == Track.Hall ? hallMusic : track == Track.Boss ? bossMusic : null;
            current = 1 - current; // the old source fades out, the new one fades in
            AudioSource next = sources[current];
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            if (clip != null)
                next.Play();
        }

        private void Fade(float dt)
        {
            float step = fadeSeconds > 0f ? volume * dt / fadeSeconds : volume;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = sources[i];
                float target = i == current && s.clip != null ? volume : 0f;
                s.volume = Mathf.MoveTowards(s.volume, target, step);
                if (i != current && s.isPlaying && s.volume <= 0f)
                    s.Stop();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null)
                {
                    sources[i].Stop();
                    sources[i].volume = 0f;
                }
            playing = Track.None;
        }
    }
}
