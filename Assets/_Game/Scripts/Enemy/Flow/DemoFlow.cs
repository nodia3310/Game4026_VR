using System.Collections;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Playable-demo run for one hall: hall waves → fade + teleport to the boss realm → boss → victory → back to the hall.
    /// The player dying → fade out, restart in place (encounters reset, player health restored, back at the start).
    /// The HUD is OnGUI (desktop only); a world-space version is needed for the headset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoFlow : MonoBehaviour
    {
        [SerializeField] private EncounterController hallEncounter;
        [SerializeField] private EncounterController bossEncounter;
        [Tooltip("Where the player is put back on restart. Empty = where the player was at start.")]
        [SerializeField] private Transform playerStart;
        [SerializeField, Min(0f)] private float introDelay = 3f;
        [SerializeField, Min(0f)] private float bossDelay = 4f;
        [SerializeField, Min(0f)] private float restartDelay = 4f;
        [Header("Boss realm")]
        [Tooltip("Where the player arrives for the boss fight. Empty = the boss fight happens in the hall.")]
        [SerializeField] private Transform realmArrival;
        [Tooltip("Transparent unlit material (both faces) for the fade quad.")]
        [SerializeField] private Material fadeMaterial;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;
        [Tooltip("Seconds after the victory before the player is brought back to the hall.")]
        [SerializeField, Min(0f)] private float returnDelay = 5f;
        [SerializeField] private bool showHud = true;
        [Tooltip("Testing: skip the hall waves and go straight to the boss (also after a restart).")]
        [SerializeField] private bool startAtBoss;

        private DemoFlowModel model;
        private PlayerTarget player;
        private PlayerVitals vitals;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private GUIStyle style;
        private ScreenFader fader;
        private Coroutine transition;
        private bool restartPending;

        public DemoPhase Phase => model != null ? model.Phase : DemoPhase.Intro;
        public EncounterController HallEncounter => hallEncounter;
        public EncounterController BossEncounter => bossEncounter;

        /// <summary>Runtime / test setup.</summary>
        public void Configure(EncounterController hall, EncounterController boss, float intro, float bossWait, float restart)
        {
            hallEncounter = hall;
            bossEncounter = boss;
            introDelay = intro;
            bossDelay = bossWait;
            restartDelay = restart;
            model = null;
        }

        private void Start()
        {
            player = PlayerTarget.Current;
            if (player != null)
            {
                vitals = PlayerVitals.From(player.Damageable);
                startPosition = playerStart != null ? playerStart.position : player.transform.position;
                startRotation = playerStart != null ? playerStart.rotation : player.transform.rotation;
                if (!vitals.CanReadHealth)
                    Debug.LogWarning("[DemoFlow] player health can't be read; defeat/restart is disabled.", this);
                if (fadeMaterial != null)
                    fader = ScreenFader.Create(player.Head, fadeMaterial);
            }
            EnsureModel();
            model.Begin(Time.time);
            if (startAtBoss)
                SkipToBoss();
        }

        /// <summary>Test shortcut: clears the hall (no kills counted) and starts the boss intro / teleport.</summary>
        public void SkipToBoss()
        {
            EnsureModel();
            if (model.Phase != DemoPhase.Intro && model.Phase != DemoPhase.Hall)
                return;
            if (hallEncounter != null)
                hallEncounter.ForceClear();
            model.SkipToBoss(Time.time);
        }

        private void EnsureModel()
        {
            if (model != null)
                return;
            model = new DemoFlowModel(introDelay, bossDelay, restartDelay);
            model.PhaseChanged += HandlePhaseChanged;
        }

        private void Update()
        {
            EnsureModel();
            bool hallCleared = hallEncounter == null || hallEncounter.IsCleared;
            bool bossCleared = bossEncounter == null || (bossEncounter.IsCleared && !bossEncounter.WasForceCleared);
            bool dead = vitals != null && vitals.IsDead;

            switch (model.Tick(Time.time, hallCleared, bossCleared, dead))
            {
                case DemoAction.StartHall:
                    if (hallEncounter != null)
                        hallEncounter.StartEncounter();
                    break;
                case DemoAction.StartBoss:
                    if (bossEncounter != null)
                        bossEncounter.StartEncounter();
                    break;
                case DemoAction.Restart:
                    // The model keeps asking until Begin; start the routine only once.
                    if (!restartPending)
                    {
                        restartPending = true;
                        Run(RestartRoutine());
                    }
                    break;
            }
        }

        private void HandlePhaseChanged(DemoPhase from, DemoPhase to)
        {
            Debug.Log($"[DemoFlow] {from} → {to}", this);
            switch (to)
            {
                case DemoPhase.Defeat:
                    // Stop every fight so nothing keeps hitting the fallen player.
                    if (hallEncounter != null)
                        hallEncounter.ForceClear();
                    if (bossEncounter != null)
                        bossEncounter.ForceClear();
                    Run(null);
                    if (fader != null)
                        fader.FadeTo(1f, Mathf.Max(fadeSeconds, restartDelay * 0.8f));
                    break;
                case DemoPhase.BossIntro:
                    if (realmArrival != null)
                        Run(TeleportRoutine(realmArrival.position, realmArrival.rotation, 1f));
                    break;
                case DemoPhase.Victory:
                    if (realmArrival != null)
                        Run(TeleportRoutine(startPosition, startRotation, returnDelay));
                    break;
            }
        }

        private void Run(IEnumerator routine)
        {
            if (transition != null)
                StopCoroutine(transition);
            transition = routine != null ? StartCoroutine(routine) : null;
        }

        private IEnumerator TeleportRoutine(Vector3 position, Quaternion rotation, float wait)
        {
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);
            yield return Fade(1f);
            if (player != null)
                MovePlayer(player, position, rotation);
            yield return Fade(0f);
        }

        private IEnumerator RestartRoutine()
        {
            if (fader != null && fader.Alpha < 1f)
                yield return Fade(1f);
            Restart();
            yield return Fade(0f);
        }

        private IEnumerator Fade(float alpha)
        {
            if (fader == null)
                yield break;
            fader.FadeTo(alpha, fadeSeconds);
            while (fader.IsFading)
                yield return null;
        }

        /// <summary>Back to the intro: encounters reset, player healed and moved to the start.</summary>
        public void Restart()
        {
            restartPending = false;
            if (hallEncounter != null)
                hallEncounter.ResetEncounter();
            if (bossEncounter != null)
                bossEncounter.ResetEncounter();
            if (vitals != null)
                vitals.Reset();
            if (player != null)
                MovePlayer(player, startPosition, startRotation);
            EnsureModel();
            model.Begin(Time.time);
            if (startAtBoss)
                SkipToBoss();
        }

        /// <summary>
        /// Moves the player rig so the head ends up above <paramref name="position"/> looking along <paramref name="rotation"/>'s yaw
        /// (works for a mock player and for an XR rig whose head is offset from its root). Feet go to position.y.
        /// </summary>
        private static void MovePlayer(PlayerTarget target, Vector3 position, Quaternion rotation)
        {
            Transform root = target.transform;
            Transform head = target.Head;
            CharacterController controller = root.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
                controller.enabled = false;

            float yaw = rotation.eulerAngles.y - head.eulerAngles.y;
            root.RotateAround(head.position, Vector3.up, yaw);
            Vector3 offset = head.position - root.position;
            root.position = new Vector3(position.x - offset.x, position.y, position.z - offset.z);

            if (wasEnabled)
                controller.enabled = true;
            Physics.SyncTransforms();
        }

        private void OnGUI()
        {
            if (!showHud || model == null)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };

            string text = HudText();
            if (string.IsNullOrEmpty(text))
                return;
            var rect = new Rect(0f, 20f, Screen.width, 80f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        private string HudText()
        {
            switch (model.Phase)
            {
                case DemoPhase.Intro:
                    return "The exhibits are waking up...";
                case DemoPhase.Hall:
                    return hallEncounter != null
                        ? $"Defeat the guardians  {hallEncounter.KillCount} / {hallEncounter.TotalEnemies}"
                        : string.Empty;
                case DemoPhase.BossIntro:
                    return "The Pharaoh awakens";
                case DemoPhase.Boss:
                    return "Defeat the Pharaoh";
                case DemoPhase.Victory:
                    return "Victory!";
                case DemoPhase.Defeat:
                    return "You fell... restarting";
            }
            return string.Empty;
        }
    }
}
