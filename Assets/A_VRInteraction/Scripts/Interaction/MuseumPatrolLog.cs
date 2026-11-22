using Game4026.Interaction.TestScene;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game4026.Interaction
{
    [DisallowMultipleComponent]
    public sealed class MuseumPatrolLog : MonoBehaviour
    {
        [SerializeField] private Collider board;
        [SerializeField] private Transform promptAnchor;
        [SerializeField] private XROrigin origin;
        [SerializeField] private InteractionDesktopMode desktop;
        [SerializeField] private MuseumFloorConfirmation floorConfirmation;
        [SerializeField] private MuseumPatrolLogPage page;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2f;

        private const float AimDistance = 4f;
        private const float SurfaceTolerance = 0.015f;
        private readonly InputAction toggleButton = new("Toggle patrol log", InputActionType.Button,
            "<XRController>{LeftHand}/{PrimaryButton}") { wantsInitialStateCheck = true };
        private readonly InputAction mouseButton = new("Click patrol log", InputActionType.Button, "<Mouse>/leftButton");
        private Camera readingCamera;
        private bool isReading;
        private bool toggleArmed;
        private bool toggleRequested;
        private Vector2? clickPosition;

        private void OnEnable()
        {
            toggleButton.performed += OnTogglePressed;
            mouseButton.performed += OnMousePressed;
            toggleButton.Enable();
            mouseButton.Enable();
        }

        private void OnDisable()
        {
            toggleButton.performed -= OnTogglePressed;
            mouseButton.performed -= OnMousePressed;
            toggleButton.Disable();
            mouseButton.Disable();
            toggleArmed = toggleRequested = false;
            clickPosition = null;
            EndReading();
            if (page) page.SetPrompt(null, default, false, false);
        }

        private void OnDestroy()
        {
            toggleButton.Dispose();
            mouseButton.Dispose();
        }

        private void OnTogglePressed(InputAction.CallbackContext _) => toggleRequested = true;

        private void OnMousePressed(InputAction.CallbackContext context)
        {
            if (context.control.device is Mouse mouse)
                clickPosition = mouse.position.ReadValue();
        }

        private void LateUpdate()
        {
            bool requestedToggle = toggleRequested;
            Vector2? clickedAt = clickPosition;
            toggleRequested = false;
            clickPosition = null;

            if (!page || !page.isActiveAndEnabled)
            {
                toggleArmed = false;
                EndReading();
                return;
            }

            bool isDesktop = desktop && desktop.IsDesktopActive;
            Camera camera = isDesktop ? desktop.DesktopCamera : origin ? origin.Camera : null;
            if (!camera || !camera.isActiveAndEnabled || !board || !board.enabled || !board.gameObject.activeInHierarchy ||
                (floorConfirmation && !floorConfirmation.IsGameplayReady) ||
                board.bounds.SqrDistance(camera.transform.position) > interactionDistance * interactionDistance)
            {
                toggleArmed = false;
                EndReading();
                page.SetPrompt(null, default, false, !isDesktop);
                return;
            }

            if (isReading && readingCamera != camera)
                EndReading();

            ShowPrompt(camera, !isReading, !isDesktop);
            if (isDesktop)
            {
                toggleArmed = false;
                ReadDesktopInput(camera, clickedAt);
                return;
            }
            ReadControllerInput(camera, requestedToggle);
        }

        private void ReadDesktopInput(Camera camera, Vector2? clickedAt)
        {
            if (isReading)
            {
                var mouse = Mouse.current;
                bool overClose = mouse != null && TryGetMouseRay(camera, mouse.position.ReadValue(), out var hoverRay)
                    && page.HitsClose(hoverRay);
                page.SetCloseHovered(overClose);
            }

            if (!clickedAt.HasValue || !TryGetMouseRay(camera, clickedAt.Value, out var clickRay))
                return;

            if (isReading)
            {
                if (page.HitsClose(clickRay)) EndReading();
            }
            else if (HitsBoard(clickRay))
            {
                BeginReading(camera, false);
            }
        }

        private void ReadControllerInput(Camera camera, bool requestedToggle)
        {
            if (!toggleArmed)
            {
                // Wait for X to be released if it was pressed before entering range.
                toggleArmed = !toggleButton.IsPressed() && !requestedToggle;
                return;
            }

            if (!requestedToggle) return;
            if (isReading) EndReading();
            else BeginReading(camera, true);
        }

        private void BeginReading(Camera camera, bool isVr)
        {
            ShowPrompt(camera, false, isVr);
            readingCamera = camera;
            isReading = true;
            page.Show(camera, isVr);
        }

        private void EndReading()
        {
            if (isReading && page)
            {
                page.SetCloseHovered(false);
                page.Hide();
            }
            isReading = false;
            readingCamera = null;
        }

        private void ShowPrompt(Camera camera, bool visible, bool isVr)
        {
            page.SetPrompt(camera, promptAnchor ? promptAnchor.position : board.bounds.center, visible, isVr);
        }

        private bool HitsBoard(Ray ray)
        {
            if (!board.Raycast(ray, out RaycastHit hit, AimDistance))
                return false;

            // The fitted board volume is a trigger so it cannot obstruct movement or grabbing.
            return !Physics.Raycast(ray, Mathf.Max(0f, hit.distance - SurfaceTolerance),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private static bool TryGetMouseRay(Camera camera, Vector2 point, out Ray ray)
        {
            if (Cursor.lockState == CursorLockMode.Locked) point = camera.pixelRect.center;
            ray = default;
            if (!camera.pixelRect.Contains(point))
                return false;

            ray = camera.ScreenPointToRay(point);
            return true;
        }
    }
}
