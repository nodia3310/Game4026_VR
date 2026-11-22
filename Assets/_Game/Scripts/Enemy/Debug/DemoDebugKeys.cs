using UnityEngine;
using UnityEngine.InputSystem;

namespace Game4026.Enemy.DebugTools
{
    /// <summary>
    /// Desktop test shortcuts for <see cref="DemoFlow"/>: B = skip the hall waves and go straight to the boss.
    /// (For a run that always starts at the boss, tick "Start At Boss" on DemoFlow instead.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoDebugKeys : MonoBehaviour
    {
        [SerializeField] private DemoFlow flow;
        [SerializeField] private Key skipToBossKey = Key.B;
        [SerializeField] private bool showHint = true;

        private void Awake()
        {
            if (flow == null)
                flow = FindAnyObjectByType<DemoFlow>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (flow != null && keyboard != null && keyboard[skipToBossKey].wasPressedThisFrame)
                flow.SkipToBoss();
        }

        private void OnGUI()
        {
            if (showHint && flow != null && (flow.Phase == DemoPhase.Intro || flow.Phase == DemoPhase.Hall))
                GUI.Label(new Rect(10f, Screen.height - 30f, 300f, 24f), $"[{skipToBossKey}] skip to boss");
        }
    }
}
