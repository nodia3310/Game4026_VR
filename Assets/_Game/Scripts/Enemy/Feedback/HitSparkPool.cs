using System.Collections.Generic;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Shared spark emitters per prefab: one instance per prefab is enough, since Emit() adds particles to a
    /// system that is already playing. Root survives scene loads; statics reset at SubsystemRegistration.
    /// </summary>
    internal static class HitSparkPool
    {
        private static readonly Dictionary<ParticleSystem, ParticleSystem> Instances = new Dictionary<ParticleSystem, ParticleSystem>();
        private static Transform root;

        public static void Emit(ParticleSystem prefab, Vector3 point, Vector3 normal, Color color, int count)
        {
            if (prefab == null || count <= 0 || !Application.isPlaying)
                return;
            if (!Instances.TryGetValue(prefab, out ParticleSystem system) || system == null)
            {
                if (root == null)
                {
                    root = new GameObject("[HitSparks]").transform;
                    Object.DontDestroyOnLoad(root.gameObject);
                }
                system = Object.Instantiate(prefab, root);
                ParticleSystem.MainModule main = system.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                Instances[prefab] = system;
            }

            var emit = new ParticleSystem.EmitParams
            {
                startColor = color,
                applyShapeToPosition = true,
            };
            Transform t = system.transform;
            t.SetPositionAndRotation(point, Quaternion.LookRotation(normal.sqrMagnitude > 1e-6f ? normal : Vector3.up));
            system.Emit(emit, count);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instances.Clear();
            root = null;
        }
    }
}
