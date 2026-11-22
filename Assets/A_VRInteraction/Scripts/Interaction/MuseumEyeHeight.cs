using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game4026.Interaction
{
    // Retains the script reference in the shared Museum scenes that predate floor confirmation.
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    public sealed class MuseumEyeHeight : MonoBehaviour
    {
        private void Awake()
        {
            var origin = GetComponent<XROrigin>();
            origin.CameraYOffset = 0f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        }
    }
}
