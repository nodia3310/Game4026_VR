using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Museum mood for the demo: bright while the player gets ready; a few seconds before the exhibits wake up the lights
    /// flicker and go out, the hall stays dark while the enemies appear, then the lights come back up to the dim
    /// fight level (the scene's own light setup). Bright again after the victory and after a restart.
    /// The given realtime lights, the ambient colour and the skybox reflection (the shiny floor mirrors the default
    /// skybox, which otherwise keeps it bright in the dark) are dimmed smoothly. The floor and walls are lit by baked
    /// lightmaps, which can't be dimmed, so they are switched off and on in step with the flicker, stay off while it is
    /// dark and come back with a short stutter (power returning).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoLighting : MonoBehaviour
    {
        [SerializeField] private DemoFlow flow;
        [Tooltip("Realtime hall lights to drive. Their intensities in the scene are the dim fight level.")]
        [SerializeField] private Light[] lights = new Light[0];
        [Header("Bright (before the fight, after the victory)")]
        [SerializeField, Min(1f)] private float brightMultiplier = 3f;
        [Tooltip("Ambient colour while bright (the scene's ambient is the fight level).")]
        [SerializeField] private Color brightAmbient = new Color(0.3f, 0.29f, 0.27f);
        [SerializeField, Min(0.05f)] private float brightFadeSeconds = 1.5f;
        [Header("Blackout (exhibits waking up)")]
        [Tooltip("Seconds before the countdown ends that the lights start to flicker.")]
        [SerializeField, Min(0.5f)] private float blackoutLead = 3f;
        [SerializeField, Min(0f)] private float flickerSeconds = 1.2f;
        [Tooltip("Seconds fully dark (the countdown ends and the enemies appear during this).")]
        [SerializeField, Min(0f)] private float darkSeconds = 2.5f;
        [SerializeField, Min(0.05f)] private float fadeUpSeconds = 3f;
        [Tooltip("Switch the baked lightmaps (floor, walls) off with the flicker and while dark; without it the room never gets dark.")]
        [SerializeField] private bool darkenLightmaps = true;
        [Tooltip("Optional sound when the lights go out (2D).")]
        [SerializeField] private AudioClip powerDownSound;
        [SerializeField, Range(0f, 1f)] private float powerDownVolume = 0.8f;

        private float[] baseIntensity;
        private Color baseAmbient;
        private float baseReflection;
        private LightmapData[] bakedLightmaps;
        private bool lightmapsOff;

        // what is applied now, and the fade towards a steady target
        private float level = 1f;          // 0 dark, 1 fight level, brightMultiplier = bright
        private float fadeFrom, fadeTo, fadeStart, fadeSeconds;
        private float blackoutStart = -1f; // >= 0 while the blackout sequence runs
        private bool soundPlayed;

        public float Level => level;
        public bool InBlackout => blackoutStart >= 0f;

        private void Awake()
        {
            if (flow == null)
                flow = GetComponent<DemoFlow>();
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            baseAmbient = RenderSettings.ambientLight;
            baseReflection = RenderSettings.reflectionIntensity;
            level = brightMultiplier;
            fadeFrom = fadeTo = level;
            Apply();
        }

        private void OnDisable()
        {
            // Leave the scene as authored (also when Play Mode stops).
            level = 1f;
            Apply();
            SetLightmaps(true);
        }

        private void Update()
        {
            if (flow == null)
                return;
            float now = Time.time;
            DemoPhase phase = flow.Phase;

            if (blackoutStart >= 0f)
            {
                float t = now - blackoutStart;
                level = SequenceLevel(t, brightMultiplier, flickerSeconds, darkSeconds, fadeUpSeconds);
                if (!soundPlayed && t >= flickerSeconds)
                {
                    soundPlayed = true;
                    if (powerDownSound != null && Camera.main != null)
                        AudioSource.PlayClipAtPoint(powerDownSound, Camera.main.transform.position, powerDownVolume);
                }
                bool finished = t >= flickerSeconds + darkSeconds + fadeUpSeconds;
                // A restart or a jump back to the intro cancels it.
                if (finished || phase == DemoPhase.Intro && flow.IntroRemaining < 0f)
                {
                    blackoutStart = -1f;
                    SetSteady(level, 0f);
                }
            }
            else
            {
                float target = TargetFor(phase);
                if (phase == DemoPhase.Intro && flow.IntroRemaining >= 0f && flow.IntroRemaining <= blackoutLead)
                {
                    blackoutStart = now;
                    soundPlayed = false;
                }
                else if (!Mathf.Approximately(target, fadeTo))
                    SetSteady(target, Mathf.Abs(target - level) < 0.01f ? 0f : brightFadeSeconds);
                float k = fadeSeconds <= 0f ? 1f : Mathf.Clamp01((now - fadeStart) / fadeSeconds);
                level = Mathf.Lerp(fadeFrom, fadeTo, k * k * (3f - 2f * k));
            }
            Apply();
            SetLightmaps(!darkenLightmaps || blackoutStart < 0f ||
                         SequenceLightmaps(now - blackoutStart, flickerSeconds, darkSeconds));
        }

        private float TargetFor(DemoPhase phase)
        {
            switch (phase)
            {
                case DemoPhase.Intro:
                case DemoPhase.Victory:
                    return brightMultiplier;
                case DemoPhase.Defeat:
                    return level; // keep whatever it is
                default:
                    return 1f;
            }
        }

        private void SetSteady(float target, float seconds)
        {
            fadeFrom = level;
            fadeTo = target;
            fadeStart = Time.time;
            fadeSeconds = seconds;
        }

        private void Apply()
        {
            if (baseIntensity == null)
                return;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null)
                    lights[i].intensity = baseIntensity[i] * level;
            RenderSettings.reflectionIntensity = baseReflection * Mathf.Clamp01(level);
            RenderSettings.ambientLight = level <= 1f
                ? baseAmbient * level
                : Color.Lerp(baseAmbient, brightAmbient, (level - 1f) / Mathf.Max(0.001f, brightMultiplier - 1f));
        }

        private void SetLightmaps(bool on)
        {
            if (on == !lightmapsOff)
                return;
            if (!on)
            {
                bakedLightmaps = LightmapSettings.lightmaps;
                LightmapSettings.lightmaps = new LightmapData[0];
                lightmapsOff = true;
            }
            else
            {
                if (bakedLightmaps != null)
                    LightmapSettings.lightmaps = bakedLightmaps;
                lightmapsOff = false;
            }
        }

        /// <summary>Baked lightmaps on at <paramref name="t"/> seconds into the blackout: off with each flicker drop-out,
        /// off while dark, then a short stutter as the power returns.</summary>
        public static bool SequenceLightmaps(float t, float flicker, float dark)
        {
            if (t < 0f)
                return true;
            if (t < flicker)
                return Mathf.Repeat(t / flicker * 5.5f, 1f) >= 0.35f;
            t -= flicker + dark;
            if (t < 0f)
                return false;
            // power returning: on 0.08 s, off 0.15 s, on 0.06 s, off 0.2 s, then on
            return !(t >= 0.08f && t < 0.23f || t >= 0.29f && t < 0.49f);
        }

        /// <summary>
        /// Light level <paramref name="t"/> seconds into the blackout: flickers down from <paramref name="from"/>
        /// (a fixed stutter, dimmer each time), fully dark for <paramref name="dark"/> s, then eases up to 1 (fight level).
        /// </summary>
        public static float SequenceLevel(float t, float from, float flicker, float dark, float fadeUp)
        {
            if (t < 0f)
                return from;
            if (t < flicker)
            {
                float u = t / flicker;                       // 0..1
                bool off = Mathf.Repeat(u * 5.5f, 1f) < 0.35f; // five short drop-outs
                float envelope = Mathf.Lerp(from, 0.15f, u);
                return off ? envelope * 0.1f : envelope;
            }
            t -= flicker;
            if (t < dark)
                return 0f;
            t -= dark;
            float k = Mathf.Clamp01(t / fadeUp);
            return k * k * (3f - 2f * k);
        }
    }
}
