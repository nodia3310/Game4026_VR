using System.Collections.Generic;
using Game4026.Enemy;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Bridges
{
    /// <summary>
    /// Starts the demo's first wave when the player first picks up a weapon: listens to every grabbable carrying A's
    /// <see cref="MeleeWeapon"/> or <see cref="BowDrawAim"/> (XR hands and A's desktop hands both raise selectEntered)
    /// and calls <see cref="DemoFlow.MarkPlayerReady"/>. Weapons created later need <see cref="Refresh"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoStartOnGrab : MonoBehaviour
    {
        [Tooltip("Empty = the scene's DemoFlow.")]
        [SerializeField] private DemoFlow flow;

        private readonly List<XRBaseInteractable> watched = new List<XRBaseInteractable>();

        public int WatchedCount => watched.Count;

        private void Start()
        {
            if (flow == null)
                flow = FindAnyObjectByType<DemoFlow>();
            Refresh();
        }

        private void OnDestroy()
        {
            foreach (XRBaseInteractable interactable in watched)
                if (interactable != null)
                    interactable.selectEntered.RemoveListener(HandleGrabbed);
            watched.Clear();
        }

        /// <summary>Watches weapons not watched yet (also inactive ones, e.g. stowed in the backpack).</summary>
        public void Refresh()
        {
            foreach (XRBaseInteractable interactable in FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Include))
            {
                if (watched.Contains(interactable) || !IsWeapon(interactable))
                    continue;
                interactable.selectEntered.AddListener(HandleGrabbed);
                watched.Add(interactable);
            }
        }

        private static bool IsWeapon(XRBaseInteractable interactable) =>
            interactable.GetComponentInParent<MeleeWeapon>(true) != null || interactable.GetComponentInParent<BowDrawAim>(true) != null;

        private void HandleGrabbed(SelectEnterEventArgs _)
        {
            if (flow != null)
                flow.MarkPlayerReady();
        }
    }
}
