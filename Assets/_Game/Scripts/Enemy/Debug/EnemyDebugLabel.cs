using UnityEngine;

namespace Game4026.Enemy.DebugTools
{
    /// <summary>Editor-only text above an enemy in the Game view: current state and health (spec 4.8).</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDebugLabel : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private EnemyController controller;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.3f, 0f);

        private Camera viewCamera;
        private GUIStyle style;
        private string text = string.Empty;
        private EnemyStateId shownState = EnemyStateId.None;
        private float shownHealth = -1f;
        private int shownPhase = -1;
        private AttackDefinition shownStep;
        private PharaohBoss boss;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<EnemyController>();
            boss = GetComponent<PharaohBoss>();
        }

        private void OnGUI()
        {
            if (controller == null || controller.Health == null)
                return;

            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null)
                return;

            Vector3 screen = viewCamera.WorldToScreenPoint(transform.position + offset);
            if (screen.z <= 0f)
                return;

            EnemyStateId state = controller.CurrentState;
            float health = controller.Health.CurrentHealth;
            int phase = boss != null ? boss.CurrentPhase : -1;
            AttackDefinition step = controller.Attack != null ? controller.Attack.CurrentStep : null;
            if (state != shownState || phase != shownPhase || step != shownStep || !Mathf.Approximately(health, shownHealth))
            {
                shownState = state;
                shownHealth = health;
                shownPhase = phase;
                shownStep = step;
                string stateText = step != null ? $"{state}:{controller.Attack.Current.name}" : state.ToString();
                if (step != null && step != controller.Attack.Current)
                    stateText += $" [{controller.Attack.CurrentStepIndex + 1}]";
                text = boss != null
                    ? $"{stateText}  {health:0}/{controller.Health.MaxHealth:0}  P{phase + 1} {boss.CurrentPhaseName}"
                    : $"{stateText}  {health:0}/{controller.Health.MaxHealth:0}";
            }

            if (style == null)
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            GUI.Label(new Rect(screen.x - 120f, Screen.height - screen.y - 12f, 240f, 24f), text, style);
        }
#endif
    }
}
