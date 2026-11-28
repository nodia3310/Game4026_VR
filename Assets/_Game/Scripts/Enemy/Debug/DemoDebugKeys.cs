using UnityEngine;
using UnityEngine.InputSystem;

namespace Game4026.Enemy.DebugTools
{
    /// <summary>
    /// Desktop test shortcuts for <see cref="DemoFlow"/>: B = skip the hall waves and go straight to the boss;
    /// U = the pharaoh casts his phase-change ultimate (weapon barrage, then the barrier opens) after his current move.
    /// (For a run that always starts at the boss, tick "Start At Boss" on DemoFlow instead.)
    /// Also speeds up desktop walking: A's desktop mode walks at a fixed 1.5 m/s; with no headset active this adds
    /// the difference up to <c>desktopMoveSpeed</c> along the same WASD direction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoDebugKeys : MonoBehaviour
    {
        [SerializeField] private DemoFlow flow;
        [SerializeField] private Key skipToBossKey = Key.B;
        [SerializeField] private Key ultimateKey = Key.U;
        [SerializeField] private bool showHint = true;
        [Tooltip("Desktop walking speed (m/s) without a headset. A's desktop mode walks at 1.5; 1.5 or less = unchanged.")]
        [SerializeField, Min(0f)] private float desktopMoveSpeed = 4.5f;

        private const float BaseDesktopSpeed = 1.5f; // InteractionDesktopMode's fixed walk speed

        private void Awake()
        {
            if (flow == null)
                flow = FindAnyObjectByType<DemoFlow>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (flow != null && keyboard[skipToBossKey].wasPressedThisFrame)
                flow.SkipToBoss();
            if (keyboard[ultimateKey].wasPressedThisFrame)
            {
                PharaohStand stand = FindAnyObjectByType<PharaohStand>();
                if (stand != null)
                    stand.QueueUltimate();
            }
            FasterDesktopWalk(keyboard);
        }

        private void FasterDesktopWalk(Keyboard keyboard)
        {
            float extra = desktopMoveSpeed - BaseDesktopSpeed;
            if (extra <= 0f || UnityEngine.XR.XRSettings.isDeviceActive || !ResolveDesktopRig())
                return;
            var input = new Vector3((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f), 0f,
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            if (input.sqrMagnitude < 0.01f)
                return;
            // Same direction as A's desktop mode: relative to the desktop rig's yaw.
            Vector3 move = desktopRig.transform.TransformDirection(input.normalized);
            desktopCharacter.Move(move * (extra * Mathf.Min(Time.deltaTime, 0.05f)));
        }

        // A's InteractionDesktopMode keeps its rig private; read it by reflection so this debug assembly
        // does not reference A's (avoids a dependency on A's code for a test convenience).
        private MonoBehaviour desktopMode;
        private GameObject desktopRig;
        private CharacterController desktopCharacter;
        private float nextLookup;

        private bool ResolveDesktopRig()
        {
            if (desktopRig != null && desktopCharacter != null && desktopCharacter.enabled)
                return true;
            if (Time.unscaledTime < nextLookup)
                return false;
            nextLookup = Time.unscaledTime + 1f;
            if (desktopMode == null)
                foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (behaviour.GetType().Name == "InteractionDesktopMode")
                    {
                        desktopMode = behaviour;
                        break;
                    }
            if (desktopMode == null)
                return false;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Type type = desktopMode.GetType();
            desktopRig = type.GetField("desktopRig", flags)?.GetValue(desktopMode) as GameObject;
            desktopCharacter = type.GetField("character", flags)?.GetValue(desktopMode) as CharacterController;
            return desktopRig != null && desktopCharacter != null && desktopCharacter.enabled;
        }

        private void OnGUI()
        {
            if (!showHint || flow == null)
                return;
            if (flow.Phase == DemoPhase.Intro || flow.Phase == DemoPhase.Hall)
                GUI.Label(new Rect(10f, Screen.height - 30f, 300f, 24f), $"[{skipToBossKey}] skip to boss");
            else if (flow.Phase == DemoPhase.Boss)
                GUI.Label(new Rect(10f, Screen.height - 30f, 300f, 24f), $"[{ultimateKey}] pharaoh ultimate");
        }
    }
}
