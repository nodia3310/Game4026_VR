using System;
using System.Collections.Generic;
using Game4026.Enemy;
using Unity.AI.Navigation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>Shared Play Mode fixture: runtime-baked floor NavMesh, a pool, and code-built enemy templates.</summary>
    internal sealed class PlayModeWorld : IDisposable
    {
        private readonly List<Object> created = new List<Object>();
        private readonly NavMeshSurface surface;

        public EnemyPoolManager Pool { get; }

        public PlayModeWorld()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            created.Add(floor);

            var poolGo = new GameObject("Pool");
            Pool = poolGo.AddComponent<EnemyPoolManager>();
            created.Add(poolGo);
        }

        public T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>EnemyData with JSON overrides, e.g. {"spawnDuration":0.1}.</summary>
        public EnemyData Data(string json)
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            JsonUtility.FromJsonOverwrite(json, data);
            return Track(data);
        }

        /// <summary>Inactive enemy "prefab": Awake runs only on instances the pool activates.</summary>
        public EnemyController Template(string name, EnemyData data)
        {
            var go = Track(new GameObject(name));
            go.SetActive(false);
            var controller = go.AddComponent<EnemyController>();
            controller.GetComponent<EnemyHealth>().Initialize(data);
            var body = go.AddComponent<CapsuleCollider>();
            body.center = Vector3.up;
            return controller;
        }

        public SpawnPoint Point(Transform parent, Vector3 localPosition)
        {
            var go = new GameObject("SpawnPoint");
            if (parent != null) go.transform.SetParent(parent, false);
            else Track(go);
            go.transform.localPosition = localPosition;
            return go.AddComponent<SpawnPoint>();
        }

        public WaveDefinition Wave(EnemyController prefab, int count, float interval = 0.05f, float delay = 0f)
        {
            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            wave.Configure(new[] { new WaveDefinition.Entry(prefab, count) }, interval, delay);
            return Track(wave);
        }

        public void Dispose()
        {
            foreach (EnemyController e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include))
                Object.Destroy(e.gameObject);
            foreach (Object o in created)
                if (o != null) Object.Destroy(o);
            created.Clear();
            if (surface != null)
                surface.RemoveData();
        }
    }
}
