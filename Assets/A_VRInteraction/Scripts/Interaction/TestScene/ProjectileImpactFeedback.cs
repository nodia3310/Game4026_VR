using Game4026.Interaction.Ranged;
using UnityEngine;

namespace Game4026.Interaction.TestScene
{
    /// <summary>Marks actual physics contacts, independently of the bow's release event.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ArrowProjectile))]
    public sealed class ProjectileImpactFeedback : MonoBehaviour
    {
        [Tooltip("Optional URP Unlit material. Assign an asset so its shader is retained in builds.")]
        [SerializeField] private Material impactMaterial;
        private ArrowProjectile arrow;

        private void OnEnable()
        {
            arrow = GetComponent<ArrowProjectile>();
            arrow.Impact += HandleImpact;
        }

        private void OnDisable()
        {
            if (arrow) arrow.Impact -= HandleImpact;
        }

        private void HandleImpact(Collision collision)
        {
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Arrow impact marker";
            marker.transform.position = point;
            marker.transform.localScale = Vector3.one * 0.09f;
            var collider = marker.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = marker.GetComponent<Renderer>();
            if (impactMaterial) renderer.sharedMaterial = impactMaterial;
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader)
                {
                    var material = new Material(shader);
                    material.SetColor("_BaseColor", new Color(1f, 0.65f, 0.08f));
                    renderer.sharedMaterial = material;
                    Destroy(material, 0.45f);
                }
            }

            const int rate = 22050;
            float[] samples = new float[2205];
            for (int i = 0; i < samples.Length; i++)
            {
                float decay = 1f - (float)i / samples.Length;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 330f * i / rate) * decay * decay * 0.3f;
            }
            var clip = AudioClip.Create("Arrow impact", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            var audio = marker.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 15f;
            audio.PlayOneShot(clip);
            Destroy(clip, 0.4f);
            Destroy(marker, 0.4f);
        }
    }
}
