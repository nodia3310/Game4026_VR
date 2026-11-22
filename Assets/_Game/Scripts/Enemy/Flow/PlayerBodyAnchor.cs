using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Body centre for an XR rig, whose only tracked body part is the head: stays under the head (follows the player walking
    /// in the room) at chest height, never below <see cref="minHeight"/> over the rig floor. Assign it as
    /// <c>PlayerTarget.Body</c> so enemy hits and projectiles aim at the chest, not the eyes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerBodyAnchor : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [Tooltip("Rig floor (XR Origin). Empty = parent.")]
        [SerializeField] private Transform floor;
        [Tooltip("How far below the eyes the chest is.")]
        [SerializeField, Min(0f)] private float belowHead = 0.45f;
        [SerializeField, Min(0f)] private float minHeight = 0.4f;

        public void Configure(Transform headTransform, Transform floorTransform)
        {
            head = headTransform;
            floor = floorTransform;
            Follow();
        }

        private void LateUpdate() => Follow();

        private void Follow()
        {
            if (head == null)
                return;
            Transform ground = floor != null ? floor : transform.parent;
            float floorY = ground != null ? ground.position.y : 0f;
            Vector3 p = head.position;
            p.y = Mathf.Max(floorY + minHeight, p.y - belowHead);
            transform.position = p;
        }
    }
}
