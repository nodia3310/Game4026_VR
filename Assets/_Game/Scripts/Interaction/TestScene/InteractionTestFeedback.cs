using Game4026.Contracts;
using Game4026.Interaction.Artifacts;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using UnityEngine;

namespace Game4026.Interaction.TestScene
{
    /// <summary>Local test-room instrumentation; no enemy or progression dependencies.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionTestFeedback : MonoBehaviour
    {
        [SerializeField] private MeleeWeapon melee;
        [SerializeField] private MeleeWeapon[] additionalMelee = new MeleeWeapon[0];
        [SerializeField] private RayAim ray;
        [SerializeField] private BowDrawAim bow;
        [SerializeField] private ArtifactDisplayTrigger display;
        [SerializeField] private GrabbableFlashlight flashlight;
        [SerializeField] private TextMesh statusText;
        [SerializeField] private Renderer[] targetRenderers = new Renderer[0];
        [SerializeField] private AudioSource audioSource;

        public int MeleeHitCount { get; private set; }
        public int RayShotCount { get; private set; }
        public int BowShotCount { get; private set; }
        public int RestorationCount { get; private set; }
        public float LastSpeed { get; private set; }
        public float LastEnergy { get; private set; }
        public float LastForce { get; private set; }

        private string lastEvent = "Ready - grab a tool";
        private float flashUntil;
        private float nextTextRefresh;
        private bool flashing;
        private MaterialPropertyBlock[] originalBlocks;
        private MaterialPropertyBlock flashBlock;
        private AudioClip feedbackTone;
        private int baseColorId;
        private int colorId;

        private void Awake()
        {
            flashBlock = new MaterialPropertyBlock();
            baseColorId = Shader.PropertyToID("_BaseColor");
            colorId = Shader.PropertyToID("_Color");
            originalBlocks = new MaterialPropertyBlock[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                if (targetRenderers[i]) targetRenderers[i].GetPropertyBlock(originalBlocks[i]);
            }
            const int rate = 22050;
            float[] samples = new float[2205];
            for (int i = 0; i < samples.Length; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / (samples.Length - 1));
                samples[i] = Mathf.Sin(2f * Mathf.PI * 660f * i / rate) * envelope * 0.2f;
            }
            feedbackTone = AudioClip.Create("Interaction test feedback", samples.Length, 1, rate, false);
            feedbackTone.SetData(samples, 0);
        }

        private void OnEnable()
        {
            if (melee) melee.Hit += HandleMelee;
            foreach (var weapon in additionalMelee)
                if (weapon && weapon != melee) weapon.Hit += HandleMelee;
            if (ray) ray.Fired += HandleRay;
            if (bow) bow.Shot += HandleBow;
            if (display) display.OnArtifactRestored += HandleRestored;
        }

        private void OnDisable()
        {
            if (melee) melee.Hit -= HandleMelee;
            foreach (var weapon in additionalMelee)
                if (weapon && weapon != melee) weapon.Hit -= HandleMelee;
            if (ray) ray.Fired -= HandleRay;
            if (bow) bow.Shot -= HandleBow;
            if (display) display.OnArtifactRestored -= HandleRestored;
            RestoreColors();
        }

        private void OnDestroy()
        {
            if (feedbackTone) Destroy(feedbackTone);
        }

        private void HandleMelee(MeleeHit hit)
        {
            MeleeHitCount++;
            LastSpeed = hit.Speed;
            LastEnergy = hit.KineticEnergy;
            LastForce = hit.EstimatedForce;
            lastEvent = "Melee: " + hit.Target.name;
            FlashTarget(hit.Target.transform, Color.yellow);
            PlayTone(0.8f);
        }

        private void HandleRay(RayAimResult result)
        {
            RayShotCount++;
            lastEvent = result.HasHit ? "Ray: " + result.Collider.name : "Ray: miss";
            var target = result.HasHit ? result.Collider.GetComponentInParent<MeleeHitTarget>() : null;
            if (target) FlashTarget(target.transform, Color.cyan);
            PlayTone(1.1f);
        }

        private void HandleBow(BowShot shot)
        {
            BowShotCount++;
            lastEvent = $"Bow: {shot.NormalizedDraw:P0} / {shot.Velocity.magnitude:F1} m/s";
            PlayTone(1.4f);
        }

        private void HandleRestored(ArtifactRestoredEvent restored)
        {
            RestorationCount++;
            lastEvent = "Locked: " + restored.ArtifactId;
            if (statusText) statusText.color = Color.green;
            PlayTone(1.8f);
        }

        private void FlashTarget(Transform hitTarget, Color color)
        {
            RestoreColors();
            flashBlock.SetColor(baseColorId, color);
            flashBlock.SetColor(colorId, color);
            foreach (var target in targetRenderers)
                if (target && target.transform.IsChildOf(hitTarget)) target.SetPropertyBlock(flashBlock);
            flashing = true;
            flashUntil = Time.unscaledTime + 0.18f;
        }

        private void PlayTone(float pitch)
        {
            nextTextRefresh = 0f;
            if (audioSource && feedbackTone)
            {
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(feedbackTone);
            }
        }

        private void RestoreColors()
        {
            if (!flashing || originalBlocks == null) return;
            for (int i = 0; i < targetRenderers.Length; i++)
                if (targetRenderers[i]) targetRenderers[i].SetPropertyBlock(originalBlocks[i]);
            flashing = false;
        }

        private void Update()
        {
            if (flashing && Time.unscaledTime >= flashUntil) RestoreColors();
            if (!statusText || Time.unscaledTime < nextTextRefresh) return;
            nextTextRefresh = Time.unscaledTime + 0.1f;
            statusText.text = "INTERACTION TEST / LIVE\n"
                + $"Melee {MeleeHitCount} | Ray {RayShotCount} | Bow {BowShotCount}\n"
                + $"Last hit: {LastSpeed:F2} m/s | {LastEnergy:F2} J | ~{LastForce:F1} N\n"
                + $"Draw: {(bow ? bow.NormalizedDraw : 0f):P0} | Light: {(flashlight && flashlight.IsOn ? "ON" : "OFF")}\n"
                + $"Artifact: {(display && display.IsRestored ? "LOCKED" : "waiting")} | Notifications: {RestorationCount}\n"
                + lastEvent + "\nForce is a game estimate.";
        }
    }
}
