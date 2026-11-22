using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Fade to / from black with a quad just in front of the player's head (works for the desktop camera and the HMD).
    /// The material must be transparent, unlit and render both faces; its _BaseColor alpha is driven here.
    /// Uses unscaled time.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer quad;
        private MaterialPropertyBlock block;
        private float alpha, from, to, time, length;

        /// <summary>The fader on the player's head (created by the demo flow); other systems use it to blink.</summary>
        public static ScreenFader Current { get; private set; }

        public float Alpha => alpha;
        public bool IsFading => time < length;

        /// <summary>Creates the fader on <paramref name="head"/> (a child quad 0.2 m in front of it).</summary>
        public static ScreenFader Create(Transform head, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ScreenFade";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(3f, 3f, 1f);
            var fader = go.AddComponent<ScreenFader>();
            fader.quad = go.GetComponent<Renderer>();
            fader.quad.sharedMaterial = material;
            fader.quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fader.quad.receiveShadows = false;
            fader.Apply(0f);
            Current = fader;
            return fader;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void FadeTo(float target, float seconds)
        {
            from = alpha;
            to = Mathf.Clamp01(target);
            time = 0f;
            length = Mathf.Max(0f, seconds);
            if (length <= 0f)
                Apply(to);
        }

        private void Update()
        {
            if (time >= length)
                return;
            time += Time.unscaledDeltaTime;
            Apply(Mathf.Lerp(from, to, Mathf.Clamp01(time / length)));
        }

        private void Apply(float value)
        {
            alpha = value;
            if (quad == null)
                return;
            quad.enabled = alpha > 0.001f;
            if (block == null)
                block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
            quad.SetPropertyBlock(block);
        }
    }
}
