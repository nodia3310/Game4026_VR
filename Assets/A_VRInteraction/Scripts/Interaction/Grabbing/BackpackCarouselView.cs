using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game4026.Interaction.Grabbing
{
    [DisallowMultipleComponent]
    public sealed class BackpackCarouselView : MonoBehaviour
    {
        private const float NeighborSpacing = .21f;
        private const float FocusSize = .22f;
        private const float NeighborSize = .126f;

        private sealed class Preview
        {
            public PhysicalGrabbable Item;
            public Transform Anchor;
            public Transform Model;
            public int Slot;
            public readonly List<Mesh> BakedMeshes = new();
        }

        private readonly List<Preview> previews = new(3);
        private readonly Dictionary<PhysicalGrabbable, string> displayNames = new();
        private TextMeshPro label;
        private Material labelMaterial;
        private Sprite backdrop;
        private Texture2D backdropTexture;
        public bool DesktopControls { private get; set; }

        public void Initialize(TMP_Text template)
        {
            if (label) return;

            CreateBackdrop();
            var labelObject = new GameObject("Carousel label", typeof(RectTransform)) { layer = gameObject.layer };
            labelObject.transform.SetParent(transform, false);
            label = labelObject.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(.48f, .09f);
            label.font = template && template.font ? template.font : TMP_Settings.defaultFontAsset;
            Material fontMaterial = template ? template.fontSharedMaterial : label.font ? label.font.material : null;
            if (fontMaterial)
            {
                labelMaterial = new Material(fontMaterial) { name = "Backpack label" };
                labelMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(.06f, .042f, .025f));
                labelMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .24f);
                label.fontSharedMaterial = labelMaterial;
                label.extraPadding = true;
            }

            label.fontSize = .27f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, .94f, .82f);
            label.raycastTarget = false;
        }

        private void CreateBackdrop()
        {
            // A matte field behind the silhouettes keeps dark tools readable against the exhibit tables.
            const int width = 256, height = 136;
            var pixels = new Color32[width * height];
            var top = new Color(.46f, .52f, .50f, .94f);
            var bottom = new Color(.07f, .105f, .11f, .96f);
            var trim = new Color(.70f, .59f, .38f, .92f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                const float radius = 9f;
                var corner = new Vector2(Mathf.Max(0f, Mathf.Abs(x + .5f - width * .5f) - (width * .5f - radius)),
                    Mathf.Max(0f, Mathf.Abs(y + .5f - height * .5f) - (height * .5f - radius)));
                float inset = radius - corner.magnitude;
                var color = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, (y - 28f) / (height - 48f)));
                color = Color.Lerp(trim, color, Mathf.Clamp01(inset - .6f));
                color.a *= Mathf.Clamp01(inset);
                pixels[y * width + x] = color;
            }
            backdropTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            backdropTexture.SetPixels32(pixels);
            backdropTexture.Apply(false, true);
            backdrop = Sprite.Create(backdropTexture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 320f,
                0, SpriteMeshType.FullRect);
            var plate = CreateChild(transform, "Selection backdrop");
            plate.localPosition = new Vector3(0f, -.055f, .18f);
            plate.gameObject.AddComponent<SpriteRenderer>().sprite = backdrop;
        }

        public void SetDisplayName(PhysicalGrabbable item, string displayName)
        {
            if (item && !string.IsNullOrWhiteSpace(displayName))
                displayNames[item] = displayName;
        }

        public void Show(IReadOnlyList<PhysicalGrabbable> items, int index, int direction)
        {
            Initialize(null);

            int count = items.Count;
            var focused = index >= 0 && index < count ? items[index] : null;
            PhysicalGrabbable left = null;
            PhysicalGrabbable right = null;
            if (focused && count > 1)
            {
                if (count > 2 || direction > 0 || (direction == 0 && index > 0))
                    left = items[(index + count - 1) % count];
                if (count > 2 || !left)
                    right = items[(index + 1) % count];
            }

            for (int i = previews.Count - 1; i >= 0; i--)
            {
                var preview = previews[i];
                if (preview.Item && (preview.Item == focused || preview.Item == left || preview.Item == right))
                    continue;

                Release(preview);
                previews.RemoveAt(i);
            }

            SetPreview(focused, 0, direction);
            SetPreview(left, -1, direction);
            SetPreview(right, 1, direction);

            label.transform.localPosition = new Vector3(0f, focused ? -.154f : 0f, -.015f);
            if (!focused)
            {
                label.text = DesktopControls ? "BACKPACK EMPTY\n<size=81%>Pick up a tool, then press B to store</size>" :
                    "BACKPACK EMPTY\n<size=81%>Both hands behind shoulders\nRelease Grip to store a tool</size>";
                return;
            }

            if (!displayNames.TryGetValue(focused, out string itemName))
                itemName = focused.name.Replace("(Clone)", "").Replace('_', ' ').Trim();

            string hint = DesktopControls
                ? (count > 1 ? "Scroll to browse  |  Q at center to take" : "Q at center to take")
                : (count > 1 ? "Open-hand swipe to browse  |  Grip center to take" : "Grip center to take");
            label.text = $"{itemName}\n<size=81%>{hint}</size>";
        }

        private void SetPreview(PhysicalGrabbable item, int slot, int direction)
        {
            if (!item) return;

            var preview = previews.Find(candidate => candidate.Item == item);
            if (preview == null)
            {
                preview = CreatePreview(item);
                int entrySlot = slot + (direction == 0 ? 0 : direction > 0 ? 1 : -1);
                preview.Anchor.localPosition = SlotPosition(entrySlot);
                preview.Anchor.localScale = Vector3.one * SlotSize(entrySlot);
                previews.Add(preview);
            }
            else if (direction != 0 && (slot - preview.Slot) * direction > 0)
            {
                // Wrap around the outside so the next tool always travels with the swipe.
                int from = slot + (direction > 0 ? 1 : -1);
                preview.Anchor.localPosition = SlotPosition(from);
                preview.Anchor.localScale = Vector3.one * SlotSize(from);
            }

            preview.Slot = slot;
        }

        private Preview CreatePreview(PhysicalGrabbable item)
        {
            var preview = new Preview { Item = item, Anchor = CreateChild(transform, item.name + " preview") };
            preview.Model = CreateChild(preview.Anchor, "Model");
            var geometry = CreateChild(preview.Model, "Geometry");
            geometry.localScale = item.transform.localScale;
            var copiedTransforms = new Dictionary<Transform, Transform> { [item.transform] = geometry };
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (var source in item.GetComponentsInChildren<Renderer>(true))
            {
                if (!source.enabled || source.forceRenderingOff || !IsVisibleInItem(source.transform, item.transform))
                    continue;

                Mesh mesh = null;
                if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;
                else if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh);
                    preview.BakedMeshes.Add(mesh);
                }

                if (!mesh || mesh.vertexCount == 0) continue;

                var copy = CreateChild(CopyTransform(source.transform, copiedTransforms), "Mesh");
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = copy.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // Transform the mesh corners rather than the source's world-space AABB. The item can be
                // inactive, offset from its grip, and arbitrarily rotated after the player releases it.
                Matrix4x4 toModel = preview.Model.worldToLocalMatrix * copy.localToWorldMatrix;
                Vector3 min = mesh.bounds.min;
                Vector3 max = mesh.bounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = toModel.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z));
                    if (hasBounds) bounds.Encapsulate(point);
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }

            if (hasBounds)
            {
                float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                preview.Model.localScale = Vector3.one / Mathf.Max(extent, .001f);
                geometry.localPosition = -bounds.center;
            }

            return preview;
        }

        private Transform CopyTransform(Transform source, Dictionary<Transform, Transform> copiedTransforms)
        {
            if (copiedTransforms.TryGetValue(source, out var copy)) return copy;

            copy = CreateChild(CopyTransform(source.parent, copiedTransforms), source.name);
            copy.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            copy.localScale = source.localScale;
            copiedTransforms.Add(source, copy);
            return copy;
        }

        private Transform CreateChild(Transform parent, string childName)
        {
            var child = new GameObject(childName) { layer = gameObject.layer };
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static bool IsVisibleInItem(Transform child, Transform item)
        {
            // Stored items are inactive at the root. Preserve their own child visibility flags.
            for (var current = child; current != item; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }

        private static Vector3 SlotPosition(int slot) => new(slot * NeighborSpacing,
            -.022f * Mathf.Abs(slot), .066f * Mathf.Abs(slot));

        private static float SlotSize(int slot) => slot == 0 ? FocusSize : NeighborSize;

        private void Update()
        {
            float t = Time.unscaledTime;
            float ease = 1f - Mathf.Exp(-19f * Time.unscaledDeltaTime);
            foreach (var preview in previews)
            {
                int slot = preview.Slot;
                preview.Anchor.localPosition = Vector3.Lerp(preview.Anchor.localPosition, SlotPosition(slot), ease);
                preview.Anchor.localScale = Vector3.Lerp(preview.Anchor.localScale, Vector3.one * SlotSize(slot), ease);

                float phase = t * 2f + slot;
                preview.Model.localPosition = Vector3.up * (Mathf.Sin(phase) * (slot == 0 ? .030f : .022f));
                float yaw = 34f + slot * 13f + Mathf.Sin(phase * .55f) * 19f;
                preview.Model.localRotation = Quaternion.Euler(-9f, yaw, -10f);
            }
        }

        private static void Release(Preview preview)
        {
            if (preview.Anchor)
            {
                preview.Anchor.gameObject.SetActive(false);
                DestroyGenerated(preview.Anchor.gameObject);
            }
            foreach (var mesh in preview.BakedMeshes) DestroyGenerated(mesh);
        }

        private void OnDestroy()
        {
            foreach (var preview in previews) Release(preview);
            if (label) DestroyGenerated(label.gameObject);
            if (labelMaterial) DestroyGenerated(labelMaterial);
            if (backdrop) DestroyGenerated(backdrop);
            if (backdropTexture) DestroyGenerated(backdropTexture);
        }

        private static void DestroyGenerated(Object generated)
        {
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }
    }
}
