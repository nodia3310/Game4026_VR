using UnityEngine;
using UnityEngine.AI;

namespace Game4026.Enemy
{
    /// <summary>
    /// Scene marker for enemy spawns (spec 4.6). A position is valid when it is on the NavMesh and at
    /// least minPlayerDistance from the player, so enemies never appear in the player's face.
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 1f;
        [SerializeField, Min(0f)] private float minPlayerDistance = 3f;

        public float MinPlayerDistance => minPlayerDistance;

        /// <param name="hasPlayer">False when no player target exists (distance check is skipped).</param>
        public bool TryGetSpawnPosition(bool hasPlayer, Vector3 playerPosition, out Vector3 position)
        {
            position = transform.position;
            if (hasPlayer && EnemyGeometry.HorizontalDistance(position, playerPosition) < minPlayerDistance)
                return false;

            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                return false;

            position = hit.position;
            return true;
        }

        private void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(p, 0.35f);
            Gizmos.DrawLine(p, p + Vector3.up * 2f);
            Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.up * 0.1f + transform.forward * 0.8f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, minPlayerDistance);
        }
    }
}
