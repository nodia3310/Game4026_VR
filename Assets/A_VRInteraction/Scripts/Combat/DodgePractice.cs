using Game4026.Contracts;
using Game4026.Enemy;
using Game4026.Interaction;
using UnityEngine;

namespace Game4026.PlayerCombat
{
    /// <summary>Repeats one telegraphed strike when a player approaches the Museum practice area.</summary>
    [DisallowMultipleComponent]
    public sealed class DodgePractice : MonoBehaviour
    {
        [SerializeField] private EnemyAttack attack;
        [SerializeField] private EnemyHealth enemyHealth;
        [SerializeField] private AttackDefinition move;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private TextMesh sign;
        [SerializeField] private LineRenderer strikeLane;
        [SerializeField] private MuseumFloorConfirmation opening;
        [SerializeField, Min(0f)] private float startDistance = 1.9f;
        [SerializeField, Min(0f)] private float pauseBetweenAttacks = 1.5f;

        private float nextAttack;
        private int displayedHealth = -1;

        private void OnEnable()
        {
            if (!attack) return;
            attack.AttackTelegraph += ShowLane;
            attack.AttackResolved += HideLane;
        }

        private void Update()
        {
            if (!attack || !enemyHealth || !move || !playerHealth || (opening && !opening.IsGameplayReady)) return;
            int hp = Mathf.CeilToInt(playerHealth.CurrentHealth);
            if (sign && displayedHealth != hp)
            {
                displayedHealth = hp;
                sign.text = $"DODGE PRACTICE\nApproach Anubis and watch for the glint.\nPlayer HP: {hp}";
            }

            var target = PlayerTarget.Current;
            if (attack.IsAttacking)
            {
                if (!attack.Tick(Time.time, Time.deltaTime, target))
                {
                    if (strikeLane) strikeLane.enabled = false;
                    nextAttack = Time.time + pauseBetweenAttacks;
                }
                return;
            }
            if (strikeLane && strikeLane.enabled) strikeLane.enabled = false;
            if (!target || Time.time < nextAttack || hp <= 0 || enemyHealth.IsDead) return;

            Vector3 direction = Vector3.ProjectOnPlane(target.Body.position - attack.transform.position, Vector3.up);
            if (direction.sqrMagnitude <= startDistance * startDistance && direction.sqrMagnitude > .01f)
            {
                // Set the strike's bearing when the glint starts, then let the player's response change the outcome.
                attack.transform.rotation = Quaternion.LookRotation(direction);
                attack.Begin(enemyHealth.Data, Time.time, move);
            }
        }

        private void OnDisable()
        {
            if (strikeLane) strikeLane.enabled = false;
            if (!attack) return;
            attack.AttackTelegraph -= ShowLane;
            attack.AttackResolved -= HideLane;
            if (attack.Current == move)
                attack.Cancel(Time.time);
        }

        private void ShowLane(EnemyAttack enemy, AttackStepTiming timing)
        {
            if (!strikeLane || timing.Step != move) return;
            Vector3 apex = enemy.transform.position + Vector3.up * .025f;
            Vector3 reach = enemy.transform.forward * move.HitRange;
            float halfAngle = move.HitAngle * .5f;
            strikeLane.SetPosition(0, apex);
            strikeLane.SetPosition(1, apex + Quaternion.AngleAxis(-halfAngle, Vector3.up) * reach);
            strikeLane.SetPosition(2, apex + Quaternion.AngleAxis(halfAngle, Vector3.up) * reach);
            strikeLane.enabled = true;
        }

        private void HideLane(EnemyAttack enemy, AttackDefinition step, bool hit)
        {
            if (strikeLane && step == move) strikeLane.enabled = false;
        }
    }
}
