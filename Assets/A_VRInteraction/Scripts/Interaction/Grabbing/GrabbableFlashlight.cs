using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Interaction.Grabbing
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PhysicalGrabbable))]
    public sealed class GrabbableFlashlight : MonoBehaviour
    {
        [SerializeField] private Light beam;
        [SerializeField] private bool startsOn = true;
        [SerializeField] private AudioSource switchAudio;
        [SerializeField] private UnityEvent<bool> onPowerChanged = new UnityEvent<bool>();
        private XRGrabInteractable grab;
        private bool isOn;
        public bool IsOn => isOn;
        public Light Beam { get => beam; set { beam = value; if (beam != null) beam.enabled = isOn; } }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (beam == null) beam = GetComponentInChildren<Light>(true);
            isOn = startsOn;
            if (beam != null) beam.enabled = isOn;
        }

        private void OnEnable()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            grab.activated.AddListener(HandleActivated);
        }

        private void OnDisable() => grab.activated.RemoveListener(HandleActivated);

        private void HandleActivated(ActivateEventArgs args)
        {
            // An unrelated hovering hand must not switch the held flashlight.
            for (var i = 0; i < grab.interactorsSelecting.Count; i++)
            {
                if (!ReferenceEquals(grab.interactorsSelecting[i], args.interactorObject)) continue;
                Toggle();
                return;
            }
        }

        public void Toggle() => SetPower(!isOn);

        public void SetPower(bool value)
        {
            if (isOn == value) return;
            isOn = value;
            if (beam != null) beam.enabled = isOn;
            if (switchAudio != null) switchAudio.Play();
            onPowerChanged.Invoke(isOn);
        }
    }
}
