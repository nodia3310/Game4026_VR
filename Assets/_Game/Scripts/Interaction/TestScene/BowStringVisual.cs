using UnityEngine;

namespace Game4026.Interaction.TestScene
{
    [DisallowMultipleComponent, RequireComponent(typeof(LineRenderer)), DefaultExecutionOrder(300)]
    public sealed class BowStringVisual : MonoBehaviour
    {
        [SerializeField] private Transform upper;
        [SerializeField] private Transform lower;
        [SerializeField] private Transform pullPoint;
        [SerializeField] private LineRenderer line;

        private void Awake()
        {
            if (!line) line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 3;
        }

        private void LateUpdate()
        {
            if (!upper || !lower || !pullPoint || !line) return;
            line.SetPosition(0, upper.position);
            line.SetPosition(1, pullPoint.position);
            line.SetPosition(2, lower.position);
        }
    }
}
