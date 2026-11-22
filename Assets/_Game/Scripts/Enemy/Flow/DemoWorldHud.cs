using Game4026.Contracts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Game4026.Enemy
{
    /// <summary>
    /// Headset HUD for the demo: the <see cref="DemoFlow"/> objective line and the player's health on a world-space text
    /// that floats in front of <see cref="PlayerTarget.Current"/>'s head, lags behind turns a little (no head-locked text),
    /// and draws over walls. Shown only while an XR device is active (OnGUI covers the desktop), unless forced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoWorldHud : MonoBehaviour
    {
        [SerializeField] private DemoFlow flow;
        [Tooltip("Show even without a headset (testing).")]
        [SerializeField] private bool showOnDesktop;
        [SerializeField, Min(0.3f)] private float distance = 1.6f;
        [Tooltip("Height relative to the eyes (negative = below the line of sight).")]
        [SerializeField] private float height = 0.35f;
        [Tooltip("How fast the HUD catches up with head turns (1/s).")]
        [SerializeField, Min(0.1f)] private float followSpeed = 4f;
        [SerializeField, Min(0.001f)] private float characterSize = 0.012f;
        [SerializeField] private Color color = new Color(1f, 0.92f, 0.7f, 1f);

        private TextMesh text;
        private Transform head;
        private string shown;
        private string lastLine;
        private int lastHp = int.MinValue, lastMax = int.MinValue;

        /// <summary>True while an XR headset is rendering (the desktop OnGUI HUD is hidden then).</summary>
        public static bool InHeadset => XRSettings.isDeviceActive;

        private void Awake()
        {
            if (flow == null)
                flow = GetComponent<DemoFlow>();
            var go = new GameObject("WorldHud");
            go.transform.SetParent(transform, false);
            text = go.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            var renderer = go.GetComponent<MeshRenderer>();
            var material = new Material(font.material);
            material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always); // readable through walls
            material.renderQueue = 4000;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            PlayerTarget player = PlayerTarget.Current;
            bool visible = flow != null && flow.ShowHud && player != null && (InHeadset || showOnDesktop);
            string message = visible ? Compose() : string.Empty;
            visible &= !string.IsNullOrEmpty(message);
            if (text.gameObject.activeSelf != visible)
                text.gameObject.SetActive(visible);
            if (!visible)
                return;
            if (message != shown)
            {
                text.text = message;
                shown = message;
            }

            Transform target = player.Head;
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.ProjectOnPlane(target.up, Vector3.up);
            forward.Normalize();
            Vector3 goal = target.position + forward * distance + Vector3.up * height;
            Transform hud = text.transform;
            if (head != target)
            {
                head = target; // first frame / player swapped: snap
                hud.position = goal;
            }
            else
                hud.position = Vector3.Lerp(hud.position, goal, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
            hud.rotation = Quaternion.LookRotation(hud.position - target.position, Vector3.up);
        }

        /// <summary>Rebuilds the string only when the line or the whole-number health changed (no per-frame garbage).</summary>
        private string Compose()
        {
            string line = flow.HudMessage;
            int hp = -1, max = -1;
            if (flow.TryGetPlayerHealth(out float current, out float maxHealth))
            {
                hp = Mathf.CeilToInt(current);
                max = Mathf.CeilToInt(maxHealth);
            }
            if (line == lastLine && hp == lastHp && max == lastMax)
                return shown;
            lastLine = line;
            lastHp = hp;
            lastMax = max;
            if (hp < 0)
                return line;
            string health = max > 0 ? $"HP {hp} / {max}" : $"HP {hp}";
            return string.IsNullOrEmpty(line) ? health : line + "\n" + health;
        }
    }
}
