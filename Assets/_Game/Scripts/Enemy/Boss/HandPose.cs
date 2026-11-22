using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Curls the fingers of a rigged hand (XR Hands sample rig: *_IndexProximal / Intermediate / Distal, *_Thumb...).
    /// 0 = open flat hand, 1 = closed fist. <see cref="Curl"/> eases toward <see cref="Target"/> each frame, with a small
    /// per-finger lag so the hand never moves as one rigid block.
    /// </summary>
    public sealed class HandPose : MonoBehaviour
    {
        private static readonly string[] Fingers = { "Index", "Middle", "Ring", "Little" };
        private static readonly string[] Joints = { "Proximal", "Intermediate", "Distal" };
        // Degrees about the joint's local X at full curl.
        private static readonly float[] JointAngles = { 78f, 95f, 62f };

        [Range(0f, 1f)] [SerializeField] private float startCurl = 0.3f;
        [Tooltip("How fast the hand reaches its target curl (per second).")]
        [SerializeField, Min(0.1f)] private float speed = 6f;

        private Transform[,] fingers;
        private Quaternion[,] fingerRest;
        private Transform[] thumb;
        private Quaternion[] thumbRest;
        private float[] fingerCurl;
        private bool built;

        public float Target { get; set; }
        public float Curl { get; private set; }

        private void Awake()
        {
            Build();
            Target = Curl = startCurl;
            Apply();
        }

        private void Build()
        {
            if (built)
                return;
            built = true;
            Transform[] all = GetComponentsInChildren<Transform>(true);
            fingers = new Transform[Fingers.Length, Joints.Length];
            fingerRest = new Quaternion[Fingers.Length, Joints.Length];
            fingerCurl = new float[Fingers.Length + 1];
            for (int f = 0; f < Fingers.Length; f++)
                for (int j = 0; j < Joints.Length; j++)
                {
                    fingers[f, j] = Find(all, Fingers[f] + Joints[j]);
                    if (fingers[f, j] != null)
                        fingerRest[f, j] = fingers[f, j].localRotation;
                }
            thumb = new[] { Find(all, "ThumbProximal"), Find(all, "ThumbDistal") };
            thumbRest = new Quaternion[thumb.Length];
            for (int i = 0; i < thumb.Length; i++)
                if (thumb[i] != null)
                    thumbRest[i] = thumb[i].localRotation;
        }

        private static Transform Find(Transform[] all, string suffix)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i].name.EndsWith(suffix))
                    return all[i];
            return null;
        }

        /// <summary>Jumps straight to a curl (no easing), e.g. when a pooled hand reappears.</summary>
        public void Snap(float curl)
        {
            Build();
            Target = Curl = Mathf.Clamp01(curl);
            for (int i = 0; i < fingerCurl.Length; i++)
                fingerCurl[i] = Curl;
            Apply();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            Curl = Mathf.MoveTowards(Curl, Mathf.Clamp01(Target), speed * dt);
            // Index leads, little finger and thumb follow a touch later.
            for (int i = 0; i < fingerCurl.Length; i++)
                fingerCurl[i] = Mathf.MoveTowards(fingerCurl[i], Curl, speed * dt * (1f - 0.12f * i));
            Apply();
        }

        private void Apply()
        {
            if (fingers == null)
                return;
            for (int f = 0; f < Fingers.Length; f++)
            {
                // The little finger curls a little more, like a real grip.
                float c = fingerCurl[f] * (1f + 0.06f * f);
                for (int j = 0; j < Joints.Length; j++)
                    if (fingers[f, j] != null)
                        fingers[f, j].localRotation = fingerRest[f, j] * Quaternion.AngleAxis(JointAngles[j] * c, Vector3.right);
            }
            float t = fingerCurl[Fingers.Length];
            if (thumb[0] != null)
                thumb[0].localRotation = thumbRest[0] * Quaternion.AngleAxis(32f * t, Vector3.right);
            if (thumb[1] != null)
                thumb[1].localRotation = thumbRest[1] * Quaternion.AngleAxis(48f * t, Vector3.right);
        }
    }
}
