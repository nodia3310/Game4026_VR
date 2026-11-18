using Game4026.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game4026.Enemy.DebugTools
{
    /// <summary>
    /// Test tool (spec 4.8): keys 1 / 2 / 3 simulate restoring RosettaStone / CanopicJar / TutankhamunMask,
    /// so hall clearing and the boss trigger can be tested without A's artifact system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugArtifactButton : MonoBehaviour
    {
        [SerializeField] private EncounterDirector director;
        [SerializeField] private bool showHud = true;

        private string hudText = string.Empty;

        private void Awake()
        {
            if (director == null)
                director = FindAnyObjectByType<EncounterDirector>();
            UpdateHud();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || director == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame) Restore(ArtifactType.RosettaStone);
            if (keyboard.digit2Key.wasPressedThisFrame) Restore(ArtifactType.CanopicJar);
            if (keyboard.digit3Key.wasPressedThisFrame) Restore(ArtifactType.TutankhamunMask);
        }

        /// <summary>Same as pressing the key. Returns false if this artifact was already restored.</summary>
        public bool Restore(ArtifactType type)
        {
            if (director == null)
                return false;
            bool accepted = director.NotifyArtifactRestored(new ArtifactRestoredEvent("debug-" + type, type));
            Debug.Log($"[DebugArtifactButton] {type} restored (accepted={accepted}, boss started={director.BossStarted})", this);
            UpdateHud();
            return accepted;
        }

        private void UpdateHud()
        {
            if (director == null)
            {
                hudText = "DebugArtifactButton: no EncounterDirector";
                return;
            }
            ArtifactProgress p = director.Progress;
            hudText = "1 Rosetta " + Mark(p.IsRestored(ArtifactType.RosettaStone)) +
                      "   2 Canopic " + Mark(p.IsRestored(ArtifactType.CanopicJar)) +
                      "   3 Mask " + Mark(p.IsRestored(ArtifactType.TutankhamunMask)) +
                      (director.BossStarted ? "   BOSS" : string.Empty);
        }

        private static string Mark(bool restored) => restored ? "[x]" : "[ ]";

        private void OnGUI()
        {
            if (showHud)
                GUI.Label(new Rect(10f, 60f, 520f, 24f), hudText);
        }
    }
}
