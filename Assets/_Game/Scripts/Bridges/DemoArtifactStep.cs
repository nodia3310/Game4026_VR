using Game4026.Contracts;
using Game4026.Enemy;
using Game4026.Interaction.Artifacts;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>
    /// Demo step between the hall and the boss: when the last hall guardian falls, its artifact (A's
    /// <see cref="RestorableArtifact"/>) appears floating where it died; putting it on its <see cref="ArtifactDisplayTrigger"/>
    /// lets <see cref="DemoFlow"/> go on to the boss. On a restart the artifact is taken back and hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoArtifactStep : MonoBehaviour
    {
        [SerializeField] private DemoFlow flow;
        [SerializeField] private RestorableArtifact artifact;
        [SerializeField] private ArtifactDisplayTrigger display;
        [Tooltip("Where the artifact appears if no kill was seen (e.g. the hall was skipped).")]
        [SerializeField] private Transform fallbackSpawn;
        [Tooltip("Shown while the flow waits for the artifact (desktop + headset HUD).")]
        [SerializeField] private string hint = "Return the golden mask to the Sphinx";
        [Tooltip("Height over the floor where the artifact floats until picked up.")]
        [SerializeField, Min(0.2f)] private float floatHeight = 1.1f;
        [SerializeField, Min(0f)] private float spinSpeed = 60f;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.06f;
        [Tooltip("Optional glow on the display, on while the artifact is out.")]
        [SerializeField] private GameObject displayGlow;

        private Vector3 lastKill;
        private bool sawKill, shown, everHeld;
        private Vector3 floatBase;
        private float shownTime;

        public bool IsShown => shown;

        private void Awake()
        {
            if (flow == null)
                flow = GetComponent<DemoFlow>();
            if (flow != null)
            {
                flow.RequireArtifact = true;
                flow.ArtifactHint = hint;
            }
            Hide();
        }

        private void OnEnable()
        {
            HitImpacts.Played += HandleImpact;
            if (display != null)
                display.OnArtifactRestored += HandleRestored;
        }

        private void OnDisable()
        {
            HitImpacts.Played -= HandleImpact;
            if (display != null)
                display.OnArtifactRestored -= HandleRestored;
        }

        private void HandleImpact(HitImpact impact)
        {
            if (impact.Kind == HitKind.Kill && flow != null && flow.Phase == DemoPhase.Hall)
            {
                lastKill = impact.Point;
                sawKill = true;
            }
        }

        private void HandleRestored(ArtifactRestoredEvent _)
        {
            if (flow != null)
                flow.MarkArtifactRestored();
            if (displayGlow != null)
                displayGlow.SetActive(false);
        }

        private void Update()
        {
            if (flow == null || artifact == null)
                return;
            DemoPhase phase = flow.Phase;
            if (phase == DemoPhase.Intro && (shown || artifact.IsRestored))
            {
                Hide(); // restarted
                return;
            }
            if (!shown && flow.AwaitingArtifact)
                Show();
            if (shown)
                Float();
        }

        private void Show()
        {
            Vector3 at = sawKill ? lastKill : fallbackSpawn != null ? fallbackSpawn.position : transform.position;
            // Down to the floor under the kill point, then up to hand height.
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                at = hit.point;
            floatBase = at + Vector3.up * floatHeight;
            artifact.transform.SetPositionAndRotation(floatBase, Quaternion.identity);
            if (artifact.TryGetComponent(out Rigidbody body))
            {
                body.position = floatBase;
                body.isKinematic = true; // floats; stays where it is let go
                body.useGravity = false;
            }
            artifact.gameObject.SetActive(true);
            if (displayGlow != null)
                displayGlow.SetActive(true);
            shown = true;
            everHeld = false;
            shownTime = Time.time;
        }

        private void Float()
        {
            if (artifact.IsRestored)
                return;
            if (artifact.IsHeld)
                everHeld = true;
            if (everHeld)
                return;
            float t = Time.time - shownTime;
            artifact.transform.SetPositionAndRotation(floatBase + Vector3.up * (Mathf.Sin(t * 2f) * bobAmplitude),
                Quaternion.Euler(0f, t * spinSpeed, 0f));
        }

        private void Hide()
        {
            if (display != null && display.IsRestored)
                display.ResetRestoration(); // also unlocks the artifact
            if (artifact != null)
            {
                if (artifact.IsRestored)
                    artifact.ResetRestoration();
                artifact.gameObject.SetActive(false);
            }
            if (displayGlow != null)
                displayGlow.SetActive(false);
            shown = false;
            sawKill = false;
        }
    }
}
