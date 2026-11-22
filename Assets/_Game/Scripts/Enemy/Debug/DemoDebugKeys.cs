using UnityEngine;
using UnityEngine.InputSystem;

namespace Game4026.Enemy.DebugTools
{
    /// <summary>
    /// Desktop test shortcuts for <see cref="DemoFlow"/>: B = skip the hall waves and go straight to the boss;
    /// U = the pharaoh casts his phase-change ultimate (weapon barrage, then the barrier opens) after his current move.
    /// (For a run that always starts at the boss, tick "Start At Boss" on DemoFlow instead.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoDebugKeys : MonoBehaviour
    {
        [SerializeField] private DemoFlow flow;
        [SerializeField] private Key skipToBossKey = Key.B;
        [SerializeField] private Key ultimateKey = Key.U;
        [SerializeField] private bool showHint = true;

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
