using TMPro;
using UnityEngine;

namespace Game4026.Interaction
{
    [DisallowMultipleComponent]
    public sealed class MuseumPatrolLogPage : MonoBehaviour
    {
        private const float VrReadingDistance = 0.42f;
        private const float VrReadingDrop = 0.10f;
        private const float VrFullSizeDistance = 0.90f;

        private static readonly Color TagColor = new Color32(37, 34, 31, 255);
        private static readonly Color TagHighlight = new Color32(100, 64, 32, 255);
        private static readonly Color TrimColor = new Color32(182, 151, 100, 255);
        private static readonly Color TextColor = new Color32(255, 246, 218, 255);
        private static readonly Vector2 CloseSize = new(0.19f, 0.068f);
        private static readonly Vector2 ReticleSize = new(0.15f, 0.15f);
        private static readonly Color ReticleTextColor = new Color32(255, 246, 218, 232);

        [SerializeField, Tooltip("The inactive scene Catalogue with the patrol spread first.")]
        private Transform catalogue;
        [SerializeField] private TMP_FontAsset font;

        private Transform openCatalogue;
        public bool IsOpen => openCatalogue && openCatalogue.gameObject.activeSelf;
        private Sprite tagSprite;
        private Sprite reticleSprite;
        private Texture2D reticleTexture;
        private TextMeshPro prompt;
        private TextMeshPro desktopCaption;
        private TextMeshPro close;
        private SpriteRenderer closeBackground;

        public void Show(Camera camera, bool isVr)
        {
            if (!camera || !catalogue || openCatalogue)
                return;

            Transform coverTransform = catalogue.Find("Cover");
            if (!coverTransform || !coverTransform.TryGetComponent(out MeshFilter sourceCover) || !sourceCover.sharedMesh)
                return;

            Transform view = camera.transform;
            SetPrompt(camera, default, false, isVr);
            float distance = Mathf.Max(isVr ? VrReadingDistance : 0.62f, camera.nearClipPlane + 0.1f);
            Vector3 center = view.position + view.forward * distance;
            if (isVr)
                center -= view.up * VrReadingDrop;

            // The Catalogue's printed face points along local +Y, with +X pointing up the page.
            Quaternion rotation = view.rotation * Quaternion.Inverse(Quaternion.LookRotation(Vector3.down, Vector3.right));
            Bounds cover = sourceCover.sharedMesh.bounds;
            Vector3 localCenter = catalogue.InverseTransformPoint(sourceCover.transform.TransformPoint(cover.center));

            // Keep the spread's apparent size while bringing both pages into hand reach.
            float scale = distance / VrFullSizeDistance;
            if (!isVr)
            {
                float viewHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float viewWidth = viewHeight * camera.aspect;
                scale = Mathf.Min(viewHeight * 0.75f / cover.size.x, viewWidth * 0.88f / cover.size.z);
            }

            // Starting from the inactive authored Catalogue gives GuideBookPageFlip a fresh first spread.
            openCatalogue = Instantiate(catalogue, transform, false);
            openCatalogue.name = "Patrol Catalogue";
            openCatalogue.SetParent(null, false);
            openCatalogue.localScale = Vector3.one * scale;
            openCatalogue.SetPositionAndRotation(center - rotation * (localCenter * scale), rotation);

            if (!isVr)
            {
                Vector3 closePosition = center
                    + view.right * ((cover.size.z * 0.5f - CloseSize.x * 0.5f) * scale)
                    + view.up * ((cover.size.x * 0.5f + CloseSize.y * 0.5f + 0.012f) * scale);
                close = CreateTag(openCatalogue, "Close patrol log", CloseSize * scale, 0.29f * scale, out closeBackground);
                close.transform.SetPositionAndRotation(closePosition, view.rotation);
                close.text = "CLOSE";
            }
            openCatalogue.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (!openCatalogue)
                return;

            openCatalogue.gameObject.SetActive(false);
            Destroy(openCatalogue.gameObject);
            openCatalogue = null;
            close = null;
            closeBackground = null;
        }

        public void SetPrompt(Camera camera, Vector3 position, bool visible, bool isVr)
        {
            bool showPrompt = visible && camera && !openCatalogue;
            if (!showPrompt)
            {
                if (prompt)
                    prompt.gameObject.SetActive(false);
                return;
            }

            if (!prompt)
            {
                prompt = CreateLabel(transform, "Read patrol log X reticle", ReticleSize, 0.55f);
                prompt.text = "X";
                prompt.color = ReticleTextColor;
                reticleSprite = CreateReticleSprite();
                CreatePlate(prompt.transform, "Translucent reticle ring", ReticleSize, 0.002f, TextColor, reticleSprite);
            }

            if (!isVr && !desktopCaption)
            {
                desktopCaption = CreateLabel(prompt.transform, "Desktop Left click caption", new Vector2(0.23f, 0.03f), 0.23f);
                desktopCaption.transform.localPosition = Vector3.down * 0.085f;
                desktopCaption.text = "Left click";
                desktopCaption.color = ReticleTextColor;
            }

            if (desktopCaption)
                desktopCaption.gameObject.SetActive(!isVr);

            Vector3 forward = position - camera.transform.position;
            if (forward.sqrMagnitude < 0.0001f)
                forward = camera.transform.forward;
            prompt.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, camera.transform.up));
            prompt.gameObject.SetActive(true);
        }

        public bool HitsClose(Ray ray)
        {
            if (!close || Vector3.Dot(ray.direction, close.transform.forward) <= 0f)
                return false;

            var plane = new Plane(close.transform.forward, close.transform.position);
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 local = close.transform.InverseTransformPoint(ray.GetPoint(distance));
            return close.rectTransform.rect.Contains(new Vector2(local.x, local.y));
        }

        public void SetCloseHovered(bool hovered)
        {
            if (closeBackground)
                closeBackground.color = hovered ? TagHighlight : TagColor;
        }

        private TextMeshPro CreateTag(Transform parent, string objectName, Vector2 size, float textSize, out SpriteRenderer background)
        {
            var label = CreateLabel(parent, objectName, size, textSize);
            if (!tagSprite)
                tagSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));

            CreatePlate(label.transform, "Tag trim", size, 0.004f, TrimColor, tagSprite);
            background = CreatePlate(label.transform, "Tag background", size - Vector2.one * 0.004f, 0.002f, TagColor, tagSprite);
            return label;
        }

        private TextMeshPro CreateLabel(Transform parent, string objectName, Vector2 size, float textSize)
        {
            var tag = new GameObject(objectName, typeof(RectTransform)) { layer = gameObject.layer };
            tag.transform.SetParent(parent, false);
            tag.transform.localScale = Vector3.one / parent.lossyScale.x;

            var label = tag.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = size;
            label.font = font ? font : TMP_Settings.defaultFontAsset;
            label.fontSize = textSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = TextColor;
            label.raycastTarget = false;
            label.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            label.renderer.receiveShadows = false;
            return label;
        }

        private Sprite CreateReticleSprite()
        {
            const int size = 96;
            const float radius = 41f;
            const float halfStroke = 1.7f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var offset = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                float edgeDistance = Mathf.Abs(offset.magnitude - radius);
                byte alpha = (byte)Mathf.RoundToInt(183f * Mathf.Clamp01(halfStroke + 0.5f - edgeDistance));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }

            reticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            reticleTexture.SetPixels32(pixels);
            reticleTexture.Apply(true, true);
            return Sprite.Create(reticleTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private SpriteRenderer CreatePlate(Transform parent, string objectName, Vector2 size, float depth, Color color, Sprite sprite)
        {
            var plate = new GameObject(objectName) { layer = gameObject.layer };
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = Vector3.forward * depth;
            var spriteRenderer = plate.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            plate.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
            return spriteRenderer;
        }

        private void OnDisable()
        {
            Hide();
            SetPrompt(null, default, false, false);
        }

        private void OnDestroy()
        {
            if (tagSprite)
                Destroy(tagSprite);
            if (reticleSprite)
                Destroy(reticleSprite);
            if (reticleTexture)
                Destroy(reticleTexture);
        }
    }
}
