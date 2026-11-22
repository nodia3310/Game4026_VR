using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game4026.Contracts;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Interaction.TestScene
{
    /// <summary>Editor-only keyboard adapter. Selection and physics still run through XRI.</summary>
    public sealed class InteractionDesktopMode : MonoBehaviour
    {
        [SerializeField] private GameObject xrRig;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private BowDrawAim bow;
        [SerializeField] private MeleeWeapon melee;
        [SerializeField] private bool enableInEditorWithoutHeadset = true;
        [SerializeField] private float grabRadius = 0.22f;

        private GameObject desktopRig;
        private Camera desktopCamera;
        private CharacterController character;
        private bool originalRigActive;
        private Vector3 originalRigPosition, originalHeadPosition;
        private Quaternion originalRigRotation, originalHeadRotation;
        private FieldInfo trackedField;
        private bool originalRequireTracked;
        private float yaw, pitch;
        private Material leftMaterial, rightMaterial;
        public bool IsDesktopActive => desktopRig != null;
        public DesktopTestHand LeftHand { get; private set; }
        public DesktopTestHand RightHand { get; private set; }
        public DesktopTestHand ActiveHand { get; private set; }
        public Camera DesktopCamera => desktopCamera;
        public const string Controls = "DESKTOP: WASD move | RMB look | Tab hand | Q grab/release/catch | E use/throw axe\nArrows move hand X/Y | R/F hand forward/back | Shift fast hand (melee)";

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            // Give XR startup a chance to supply the real HMD before choosing the editor adapter.
            yield return null;
            yield return null;
            if (!enableInEditorWithoutHeadset || XRSettings.isDeviceActive ||
                InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid) yield break;
            EnableDesktop();
#else
            yield break;
#endif
        }

        private void EnableDesktop()
        {
            if (xrRig == null || xrCamera == null || IsDesktopActive) return;
            originalRigActive = xrRig.activeSelf;
            originalRigPosition = xrRig.transform.position;
            originalRigRotation = xrRig.transform.rotation;
            originalHeadPosition = xrCamera.transform.localPosition;
            originalHeadRotation = xrCamera.transform.localRotation;
            xrRig.SetActive(false);
            desktopRig = new GameObject("Desktop test rig (Editor only)");
            desktopRig.layer = 2; // Ignore Raycast: the simulated player must not obstruct its own tools.
            desktopRig.transform.SetPositionAndRotation(originalRigPosition, originalRigRotation);
            yaw = desktopRig.transform.eulerAngles.y;
            character = desktopRig.AddComponent<CharacterController>();
            character.height = 1.7f;
            character.radius = 0.2f;
            character.center = Vector3.up * 0.87f;
            character.minMoveDistance = 0f;
            var cameraObject = new GameObject("Desktop test camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(desktopRig.transform, false);
            cameraObject.transform.localPosition = Vector3.up * 1.65f;
            desktopCamera = cameraObject.GetComponent<Camera>();
            desktopCamera.CopyFrom(xrCamera);
            desktopCamera.enabled = true;
            desktopCamera.nearClipPlane = 0.03f;
            cameraObject.tag = "MainCamera";
            if (xrRig.TryGetComponent<PlayerTarget>(out _))
            {
                if (xrRig.TryGetComponent<PlayerHealth>(out var health))
                    cameraObject.AddComponent<DesktopPlayerDamageReceiver>().Health = health;
                cameraObject.AddComponent<PlayerTarget>();
            }

            var manager = FindAnyObjectByType<XRInteractionManager>();
            if (manager == null) manager = desktopRig.AddComponent<XRInteractionManager>();
            LeftHand = MakeHand("Desktop left hand", -0.25f, InteractorHandedness.Left, manager, out leftMaterial);
            RightHand = MakeHand("Desktop right hand", 0.25f, InteractorHandedness.Right, manager, out rightMaterial);
            SelectHand(false);
            if (bow != null)
            {
                // Kept local to this editor adapter so the authored XR configuration remains strict.
                trackedField = typeof(BowDrawAim).GetField("requireTrackedControllers", BindingFlags.Instance | BindingFlags.NonPublic);
                if (trackedField != null)
                {
                    originalRequireTracked = (bool)trackedField.GetValue(bow);
                    trackedField.SetValue(bow, false);
                }
                bow.SetTrackingValid(true);
            }
            SyncReferences();
            Debug.Log("InteractionTest desktop adapter active. " + Controls);
        }

        private DesktopTestHand MakeHand(string name, float x, InteractorHandedness side,
            XRInteractionManager manager, out Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(desktopCamera.transform, false);
            go.transform.localPosition = new Vector3(x, -0.35f, 0.6f);
            var hand = go.AddComponent<DesktopTestHand>();
            hand.handedness = side;
            hand.interactionManager = manager;
            hand.attachTransform = hand.transform;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Hand marker (no collider)";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * 0.055f;
            Destroy(visual.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            return hand;
        }

        public void SelectHand(bool left)
        {
            ActiveHand = left ? LeftHand : RightHand;
            if (leftMaterial != null) leftMaterial.color = left ? Color.green : Color.cyan;
            if (rightMaterial != null) rightMaterial.color = left ? new Color(1f, 0.5f, 0.1f) : Color.green;
        }

        private void Update()
        {
            if (!IsDesktopActive) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.12f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -75f, 75f);
                desktopRig.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                desktopCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            var move = new Vector3((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                0f, (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            character.Move((desktopRig.transform.TransformDirection(move.normalized) * 1.5f + Vector3.down) * dt);
            if (keyboard.tabKey.wasPressedThisFrame) SelectHand(ActiveHand != LeftHand);
            var offset = new Vector3(
                (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f),
                (keyboard.rKey.isPressed ? 1f : 0f) - (keyboard.fKey.isPressed ? 1f : 0f));
            ActiveHand.transform.localPosition += offset * ((keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? 2.5f : 0.65f) * dt;
            if (keyboard.qKey.wasPressedThisFrame)
            {
                if (ActiveHand.hasSelection) ActiveHand.Release();
                else CatchOrGrab();
            }
            if (keyboard.eKey.wasPressedThisFrame)
            {
                const float throwSpeed = 9f;
                if (ActiveHand.firstInteractableSelected is XRBaseInteractable item &&
                    item.TryGetComponent<ReboundingWeapon>(out var weapon) &&
                    weapon.QueueDesktopThrow(ActiveHand, ActiveHand.transform.forward * throwSpeed))
                    ActiveHand.Release();
                else
                    ActiveHand.ActivateSelected();
            }
            SyncReferences();
        }

        private void SyncReferences()
        {
            xrRig.transform.SetPositionAndRotation(desktopRig.transform.position, desktopRig.transform.rotation);
            xrCamera.transform.SetPositionAndRotation(desktopCamera.transform.position, desktopCamera.transform.rotation);
        }

        private void CatchOrGrab()
        {
            foreach (var weapon in FindObjectsByType<ReboundingWeapon>())
                if (weapon.TryCatch(ActiveHand)) return;

            XRBaseInteractable nearest = null;
            float closest = grabRadius * grabRadius;
            foreach (var item in FindObjectsByType<XRBaseInteractable>())
            {
                if (!item.isActiveAndEnabled || item.isSelected ||
                    (!(item is XRGrabInteractable) && !(item is XRSimpleInteractable)) ||
                    !ActiveHand.interactionManager.IsSelectPossible(ActiveHand, item)) continue;
                foreach (var collider in item.colliders)
                {
                    if (collider == null || !collider.enabled) continue;
                    float distance = (collider.ClosestPoint(ActiveHand.transform.position) - ActiveHand.transform.position).sqrMagnitude;
                    if (distance >= closest) continue;
                    closest = distance;
                    nearest = item;
                }
            }
            if (nearest != null) ActiveHand.TryGrab(nearest);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!IsDesktopActive) return;
            if (bow != null) bow.SetTrackingValid(focused);
            if (!focused && melee != null) melee.ResetSampling();
        }

        private void OnDisable()
        {
            if (!IsDesktopActive) return;
            if (LeftHand != null) LeftHand.Release();
            if (RightHand != null) RightHand.Release();
            if (bow != null)
            {
                bow.CancelDraw();
                if (trackedField != null) trackedField.SetValue(bow, originalRequireTracked);
                bow.SetTrackingValid(true);
            }
            if (xrRig != null)
            {
                xrRig.transform.SetPositionAndRotation(originalRigPosition, originalRigRotation);
                if (xrCamera != null)
                {
                    xrCamera.transform.localPosition = originalHeadPosition;
                    xrCamera.transform.localRotation = originalHeadRotation;
                }
                xrRig.SetActive(originalRigActive);
            }
            Destroy(desktopRig);
            if (leftMaterial != null) Destroy(leftMaterial);
            if (rightMaterial != null) Destroy(rightMaterial);
            desktopRig = null;
        }
    }

    internal sealed class DesktopPlayerDamageReceiver : MonoBehaviour, IPlayerDamageReceiver
    {
        public PlayerHealth Health { private get; set; }

        public void ApplyPlayerDamage(float damage)
        {
            if (Health) Health.ApplyPlayerDamage(damage);
        }
    }

    /// <summary>A deterministic XRI hand used by the desktop adapter and scene smoke tests.</summary>
    public sealed class DesktopTestHand : XRBaseInteractor, IXRActivateInteractor
    {
        private XRBaseInteractable target;
        private bool selecting;
        public override bool isSelectActive => selecting;
        public bool shouldActivate => false;
        public bool shouldDeactivate => false;
        public override void GetValidTargets(List<IXRInteractable> targets)
        {
            targets.Clear();
            if (target != null) targets.Add(target);
        }
        public bool TryGrab(XRBaseInteractable item)
        {
            if (item == null || hasSelection || item.isSelected || interactionManager == null ||
                !interactionManager.IsSelectPossible(this, item)) return false;
            interactionManager.SelectEnter((IXRSelectInteractor)this, item);
            return hasSelection;
        }
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            target = args.interactableObject as XRBaseInteractable;
            selecting = true;
            base.OnSelectEntered(args);
        }
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            if (!hasSelection)
            {
                selecting = false;
                target = null;
            }
            base.OnSelectExited(args);
        }
        public void Release()
        {
            selecting = false;
            while (hasSelection && interactionManager != null)
                interactionManager.SelectExit((IXRSelectInteractor)this, interactablesSelected[0]);
            target = null;
        }
        public void GetActivateTargets(List<IXRActivateInteractable> targets)
        {
            targets.Clear();
            foreach (var item in interactablesSelected)
                if (item is IXRActivateInteractable activatable) targets.Add(activatable);
        }
        public void ActivateSelected()
        {
            // Same IXRActivateInteractable dispatch used by XRBaseInputInteractor.
            var selected = new List<IXRActivateInteractable>();
            GetActivateTargets(selected);
            foreach (var item in selected)
            {
                item.OnActivated(new ActivateEventArgs { interactorObject = this, interactableObject = item });
                item.OnDeactivated(new DeactivateEventArgs { interactorObject = this, interactableObject = item });
            }
        }
    }
}
