using System.Collections;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Moving the player without making them sick: an instant reposition that keeps the HMD offset, optionally hidden by a
    /// quick fade to black ("blink"). Used by the demo flow (realm teleport, restart) and the pharaoh's shockwave push.
    /// </summary>
    public static class PlayerRig
    {
        /// <summary>
        /// Moves the player rig so the head ends up above <paramref name="position"/> looking along <paramref name="rotation"/>'s yaw
        /// (works for a mock player and for an XR rig whose head is offset from its root). Feet go to position.y.
        /// </summary>
        public static void MoveTo(PlayerTarget target, Vector3 position, Quaternion rotation)
        {
            if (target == null)
                return;
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

        /// <summary>Quick fade out → move → fade in (run as a coroutine). Without a <see cref="ScreenFader"/> it just moves.</summary>
        public static IEnumerator Blink(PlayerTarget target, Vector3 position, Quaternion rotation,
            float fadeOut = 0.12f, float fadeIn = 0.25f)
        {
            ScreenFader fader = ScreenFader.Current;
            if (fader != null)
            {
                fader.FadeTo(1f, fadeOut);
                while (fader.IsFading)
                    yield return null;
            }
            MoveTo(target, position, rotation);
            if (fader != null)
                fader.FadeTo(0f, fadeIn);
        }
    }
}
