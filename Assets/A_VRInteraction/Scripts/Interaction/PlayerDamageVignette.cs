using Game4026.Interaction.TestScene;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionDesktopMode))]
    public sealed class PlayerDamageVignette : MonoBehaviour
    {
        private const float HoldSeconds = .09f;
        private const float FadeSeconds = .48f;
        private const float HapticAmplitude = .45f;
        private const float HapticSeconds = .10f;
        private static readonly int StrengthProperty = Shader.PropertyToID("_Strength");

        [SerializeField] private PlayerHealth health;
        [SerializeField] private Material material;

        private InteractionDesktopMode cameraSource;
        private HapticImpulsePlayer leftHaptics, rightHaptics;
        private MaterialPropertyBlock properties;
        private Mesh triangle;
        private MeshRenderer overlay;
        private Camera overlayCamera;
        private double lastDamageAt = double.NegativeInfinity;

        public float Strength
        {
            get
            {
                float elapsed = (float)(Time.unscaledTimeAsDouble - lastDamageAt);
                return 1f - Mathf.SmoothStep(0f, 1f, (elapsed - HoldSeconds) / FadeSeconds);
            }
        }

        private void Awake()
        {
            cameraSource = GetComponent<InteractionDesktopMode>();
            var rig = cameraSource.XRRig;
            if (!rig) return;

            foreach (var hand in rig.GetComponentsInChildren<NearFarInteractor>(true))
            {
                var haptics = hand.GetComponentInParent<HapticImpulsePlayer>(true);
                if (!haptics || !haptics.transform.IsChildOf(rig.transform)) continue;
                if (hand.handedness == InteractorHandedness.Left && !leftHaptics)
                    leftHaptics = haptics;
                if (hand.handedness == InteractorHandedness.Right && !rightHaptics)
                    rightHaptics = haptics;
            }
        }

        private void OnEnable()
        {
            if (!health) return;
            health.Damaged += OnDamaged;
            health.HealthReset += Clear;
            RenderPipelineManager.beginCameraRendering += LimitToPlayerCamera;
        }

        private void OnDisable()
        {
            if (health)
            {
                health.Damaged -= OnDamaged;
                health.HealthReset -= Clear;
            }
            RenderPipelineManager.beginCameraRendering -= LimitToPlayerCamera;
            Clear();
        }

        private void OnDestroy()
        {
            if (overlay) Destroy(overlay.gameObject);
            if (triangle) Destroy(triangle);
        }

        private void OnDamaged(float _)
        {
            lastDamageAt = Time.unscaledTimeAsDouble;
            RefreshOverlay();
            if (cameraSource.IsDesktopActive) return;

            PulseController(leftHaptics, CommonUsages.LeftHand);
            PulseController(rightHaptics, CommonUsages.RightHand);
        }

        private static void PulseController(HapticImpulsePlayer haptics, InternedString handUsage)
        {
            if (!haptics || !haptics.isActiveAndEnabled) return;
            var output = haptics.hapticOutput;
            var amplitude = HapticAmplitude * haptics.amplitudeMultiplier;
            var channel = output.GetChannelGroup()?.GetChannel();
            if (channel != null)
            {
                // OpenXR can accept a motor command even when XRI reports failure. Send it once.
                channel.SendHapticImpulse(amplitude, HapticSeconds);
                return;
            }

            // The Starter Assets' wildcard action may lack an active control when the hand is idle.
            if (output.inputSourceMode != XRInputHapticImpulseProvider.InputSourceMode.InputActionReference ||
                output.inputActionReference?.action?.enabled != true) return;
            var controller = InputSystem.GetDevice<XRControllerWithRumble>(handUsage);
            if (controller != null && controller.enabled)
                controller.SendImpulse(amplitude, HapticSeconds);
        }

        private void Clear()
        {
            lastDamageAt = double.NegativeInfinity;
            if (overlay) overlay.enabled = false;
        }

        private void LateUpdate() => RefreshOverlay();

        private void LimitToPlayerCamera(ScriptableRenderContext _, Camera camera)
        {
            if (overlay) overlay.forceRenderingOff = camera != overlayCamera;
        }

        private void RefreshOverlay()
        {
            float strength = Strength;
            var camera = cameraSource.GameplayCamera;
            if (strength <= 0f || !material || !camera || !camera.isActiveAndEnabled)
            {
                if (overlay) overlay.enabled = false;
                return;
            }

            if (!overlay)
            {
                var go = new GameObject("Player damage vignette", typeof(MeshFilter), typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh = GetTriangle();
                properties ??= new MaterialPropertyBlock();
                overlay = go.GetComponent<MeshRenderer>();
                overlay.sharedMaterial = material;
                overlay.shadowCastingMode = ShadowCastingMode.Off;
                overlay.receiveShadows = false;
                overlay.lightProbeUsage = LightProbeUsage.Off;
                overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
                overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                overlay.allowOcclusionWhenDynamic = false;
                overlay.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                overlayCamera = null;
            }

            if (overlayCamera != camera)
            {
                overlayCamera = camera;
                overlay.transform.SetParent(camera.transform, false);
                overlay.gameObject.layer = camera.gameObject.layer;
            }

            overlay.transform.localPosition = Vector3.forward * (camera.nearClipPlane + .25f);
            properties.SetFloat(StrengthProperty, strength);
            overlay.SetPropertyBlock(properties);
            overlay.enabled = true;
        }

        private Mesh GetTriangle()
        {
            if (!triangle)
            {
                // The shader uses these positions directly in each eye's clip space.
                triangle = new Mesh
                {
                    name = "Player damage vignette triangle",
                    hideFlags = HideFlags.HideAndDontSave,
                    vertices = new[] { new Vector3(-1f, -1f), new Vector3(-1f, 3f), new Vector3(3f, -1f) },
                    triangles = new[] { 0, 1, 2 }
                };
                triangle.UploadMeshData(true);
            }

            return triangle;
        }
    }
}
