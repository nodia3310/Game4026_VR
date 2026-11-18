using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace Game4026.Interaction.Grabbing
{
    [DisallowMultipleComponent]
    public sealed class NearGrabOverlapCaster : SphereInteractionCaster
    {
        private Collider[] overlaps = new Collider[16];
        private readonly HashSet<IXRInteractable> found = new HashSet<IXRInteractable>();

        public override bool TryGetColliderTargets(XRInteractionManager interactionManager, List<Collider> targets)
        {
            targets.Clear();
            if (!isInitialized) return false;
            UpdateInternalData();

            // Only the current grip volume counts, including colliders already overlapping the hand.
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
            Vector3 center = effectiveCastOrigin.position;
            float radius = castRadius * transform.lossyScale.x;
            int count;
            while ((count = physicsScene.OverlapSphere(center, radius, overlaps, physicsLayerMask,
                       physicsTriggerInteraction)) == overlaps.Length)
                Array.Resize(ref overlaps, overlaps.Length * 2);

            found.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                if (interactionManager.TryGetInteractableForCollider(collider, out var item) && found.Add(item))
                    targets.Add(collider);
            }

            return targets.Count > 0;
        }
    }
}
