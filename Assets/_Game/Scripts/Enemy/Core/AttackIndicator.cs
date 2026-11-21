using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Glint on the enemy's blade that tells the player when to parry:
    /// a dim glow during the telegraph hold, then a bright flash the moment the parry window opens
    /// (the strike is coming now), shrinking until the hit. A short white burst confirms a parry.
    /// A camera-facing quad with an unlit additive material; no real light. No per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackIndicator : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private EnemyAttack attack;
        [Tooltip("Optional fixed glint position. Empty = the tip of the weapon swinging this step (left-hand weapon for mirrored moves).")]
        [SerializeField] private Transform anchor;
        [Tooltip("Unlit additive material (e.g. M_AttackGlint).")]
        [SerializeField] private Material glintMaterial;

        [Header("Telegraph (wind-up hold)")]
        [SerializeField] private Color telegraphColor = new Color(1f, 0.55f, 0.15f, 0.6f);
        [SerializeField, Min(0f)] private float telegraphSize = 0.12f;
        [SerializeField, Min(0f)] private float telegraphPulseSpeed = 10f;

        [Header("Parry window (strike incoming)")]
        [SerializeField] private Color flashColor = new Color(1f, 0.95f, 0.6f, 1f);
        [SerializeField, Min(0f)] private float flashSize = 0.5f;
        [Tooltip("Seconds for the flash to pop to full size.")]
        [SerializeField, Min(0.01f)] private float flashPopTime = 0.06f;

        [Header("Parried")]
        [SerializeField] private Color parriedColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField, Min(0f)] private float parriedSize = 0.7f;
        [SerializeField, Min(0.01f)] private float parriedTime = 0.18f;

        private enum Mode { Off, Telegraph, Flash, Parried }

        private Transform rightTip, leftTip, currentTip;
        private Transform glint;
        private Renderer glintRenderer;
        private MaterialPropertyBlock block;
        private Camera viewCamera;
        private Mode mode;
        private float modeTime, flashLength;

        public bool IsShowing => mode != Mode.Off;

        private void Awake()
        {
            if (attack == null)
                attack = GetComponentInParent<EnemyAttack>();
            foreach (EnemyWeapon weapon in GetComponentsInChildren<EnemyWeapon>(true)) // once
            {
                if (weapon.IsLeftHand) { if (leftTip == null) leftTip = weapon.Tip; }
                else if (rightTip == null) rightTip = weapon.Tip;
            }
            currentTip = TipFor(false);
            CreateGlint();
        }

        private void CreateGlint()
        {
            if (glintMaterial == null)
            {
                Debug.LogWarning($"{nameof(AttackIndicator)} on '{name}' has no glint material; indicator disabled.", this);
                return;
            }
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "AttackGlint";
            Collider quadCollider = quad.GetComponent<Collider>(); // must never block weapons or sight
            if (Application.isPlaying)
                Destroy(quadCollider);
            else
                DestroyImmediate(quadCollider);
            quad.transform.SetParent(transform, false);
            glint = quad.transform;
            glintRenderer = quad.GetComponent<Renderer>();
            glintRenderer.sharedMaterial = glintMaterial;
            glintRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glintRenderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
            SetMode(Mode.Off);
        }

        private void OnEnable()
        {
            if (attack == null)
                return;
            attack.AttackTelegraph += HandleTelegraph;
            attack.ParryWindowOpened += HandleParryWindow;
            attack.AttackResolved += HandleResolved;
            attack.Parried += HandleParried;
            attack.AttackCancelled += HandleEnded;
            attack.AttackFinished += HandleEnded;
        }

        private void OnDisable()
        {
            if (attack != null)
            {
                attack.AttackTelegraph -= HandleTelegraph;
                attack.ParryWindowOpened -= HandleParryWindow;
                attack.AttackResolved -= HandleResolved;
                attack.Parried -= HandleParried;
                attack.AttackCancelled -= HandleEnded;
                attack.AttackFinished -= HandleEnded;
            }
            SetMode(Mode.Off);
        }

        /// <summary>Glint anchor for a step: the fixed anchor, else the swinging hand's weapon tip, else the other weapon, else this transform.</summary>
        private Transform TipFor(bool mirrored)
        {
            if (anchor != null)
                return anchor;
            Transform preferred = mirrored ? leftTip : rightTip;
            Transform other = mirrored ? rightTip : leftTip;
            return preferred != null ? preferred : other != null ? other : transform;
        }

        private void HandleTelegraph(EnemyAttack _, AttackStepTiming timing)
        {
            currentTip = TipFor(timing.Step.Mirror);
            SetMode(Mode.Telegraph);
        }

        private void HandleParryWindow(EnemyAttack _, AttackStepTiming timing)
        {
            currentTip = TipFor(timing.Step.Mirror);
            flashLength = Mathf.Max(0.05f, timing.HitDelay - timing.ParryOpen); // already real seconds
            SetMode(Mode.Flash);
        }

        private void HandleResolved(EnemyAttack _, AttackDefinition step, bool hit) => SetMode(Mode.Off);
        private void HandleParried(EnemyAttack _, AttackDefinition move, AttackDefinition step) => SetMode(Mode.Parried);
        private void HandleEnded(EnemyAttack _, AttackDefinition move)
        {
            if (mode != Mode.Parried)
                SetMode(Mode.Off);
        }

        private void SetMode(Mode next)
        {
            mode = next;
            modeTime = 0f;
            if (glintRenderer != null)
                glintRenderer.enabled = next != Mode.Off;
        }

        private void LateUpdate()
        {
            if (mode == Mode.Off || glint == null)
                return;
            modeTime += Time.deltaTime;

            float size;
            Color color;
            switch (mode)
            {
                case Mode.Telegraph:
                    float pulse = 0.85f + 0.15f * Mathf.Sin(modeTime * telegraphPulseSpeed);
                    size = telegraphSize * pulse;
                    color = telegraphColor;
                    break;
                case Mode.Flash:
                    size = FlashSize(modeTime, flashPopTime, flashLength, flashSize);
                    color = flashColor;
                    break;
                default: // Parried
                    if (modeTime >= parriedTime)
                    {
                        SetMode(Mode.Off);
                        return;
                    }
                    float k = 1f - modeTime / parriedTime;
                    size = parriedSize * (1.2f - 0.4f * k);
                    color = parriedColor;
                    color.a *= k;
                    break;
            }

            if (viewCamera == null || !viewCamera.isActiveAndEnabled)
                viewCamera = Camera.main;
            glint.position = currentTip.position;
            if (viewCamera != null)
                glint.rotation = Quaternion.LookRotation(glint.position - viewCamera.transform.position, viewCamera.transform.up);
            glint.localScale = new Vector3(size, size, size) / Mathf.Max(1e-4f, transform.lossyScale.x);

            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            glintRenderer.SetPropertyBlock(block);
        }

        /// <summary>Pops to full size in <paramref name="popTime"/>, then shrinks to 40% by the hit.</summary>
        public static float FlashSize(float t, float popTime, float length, float maxSize)
        {
            if (t < popTime)
                return maxSize * Mathf.Lerp(0.3f, 1f, t / popTime);
            float rest = Mathf.Max(1e-3f, length - popTime);
            return maxSize * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01((t - popTime) / rest));
        }
    }
}
