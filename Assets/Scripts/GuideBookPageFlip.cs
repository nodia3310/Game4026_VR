using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Turns FlipPage across the gap between the two page blocks.
// Assign the flipping sheet and both blocks. The spine is the gap between them.
// A near pinch on a page drags that sheet around the spine. Letting go past halfway finishes the turn.
[DisallowMultipleComponent]
public class GuideBookPageFlip : MonoBehaviour
{
    [SerializeField] float flipDuration = 0.8f;
    [SerializeField] float openAngle = 180f;
    [SerializeField] float handReach = 0.3f;
    [SerializeField] Transform flipPage;
    [SerializeField] Transform pageLeft;
    [SerializeField] Transform pageRight;
    [SerializeField] Material[] pages;

    Mesh deformMesh;
    Vector3[] originalVertices;
    Vector3[] posedVertices;
    Vector3 spineLocal;
    Vector3 hingeAxis;
    Vector3 upAxis;
    Vector3 closedDirection;
    Vector3 liftDirection;
    Vector3 spineInBook;
    Vector3 hingeInBook;
    Vector3 closedInBook;
    Vector3 liftInBook;

    Material pageMaterial;
    Material ownedFlipMaterial;
    Vector2 baseTextureScale = Vector2.one;
    Vector2 baseTextureOffset;

    bool coveredLeftPage;
    bool revealedPreviousLeft;

    int spread;
    int turnDirection;
    bool showingNextLeft;
    bool showingPreviousRight;

    Transform hinge;
    Quaternion hingeClosedRotation;
    bool useHinge;

    float angle;
    float angleFrom;
    float angleTo = 0f;
    float animTime = 1f;
    bool turned;
    bool restoreOnFinish;
    bool dragging;
    Vector3 grabDirection;
    float grabSheetAngle;

    BoxCollider clickCollider;
    BoxCollider rightGrab;
    BoxCollider leftGrab;
    XRSimpleInteractable handTurn;
    IXRSelectInteractor holdingHand;

    void Awake()
    {
        ResolveReferences();
        if (flipPage == null)
        {
            Debug.LogError("GuideBookPageFlip: assign Flip Page, or name that object FlipPage.", this);
            enabled = false;
            return;
        }

        if (pageLeft == null || pageRight == null)
            Debug.LogWarning("GuideBookPageFlip: assign Page Left and Page Right. The spine is the gap between those blocks.", this);

        var pageMesh = MeshOf(flipPage);
        if (pageMesh == null)
        {
            Debug.LogError("GuideBookPageFlip: FlipPage has no mesh.", flipPage);
            enabled = false;
            return;
        }

        if (!TryComputeSpine(flipPage, pageMesh, out spineLocal, out hingeAxis, out upAxis))
        {
            Debug.LogError("GuideBookPageFlip: could not find the spine on FlipPage.", flipPage);
            enabled = false;
            return;
        }

        Vector3 closed = Vector3.ProjectOnPlane(pageMesh.bounds.center - spineLocal, upAxis);
        closedDirection = closed.sqrMagnitude > 1e-10f ? closed.normalized : Vector3.Cross(hingeAxis, upAxis).normalized;
        liftDirection = Vector3.Cross(hingeAxis, closedDirection).normalized;

        if (pageMesh.isReadable)
            SetupDeform(pageMesh);
        else
            SetupHinge();

        CaptureBookFrame();
        FitClickCollider();
        if (HasPages)
            ShowSpread();
        else
            MakeDoubleSided();
        SetupHandTurn();
    }

    bool HasPages => pages != null && pages.Length >= 2;

    int SpreadCount => pages.Length / 2;

    void Update()
    {
        if (dragging)
            return;

        if (animTime < 1f)
        {
            animTime += Time.deltaTime / Mathf.Max(0.05f, flipDuration);
            if (animTime >= 1f && HasPages)
            {
                animTime = 1f;
                CompleteSettle();
                return;
            }

            animTime = Mathf.Clamp01(animTime);
            float t = Mathf.SmoothStep(0f, 1f, animTime);
            angle = Mathf.Lerp(angleFrom, angleTo, t);
            ApplyAngle();
            if (HasPages && !(restoreOnFinish && turnDirection < 0))
            {
                if (turnDirection < 0)
                {
                    UpdateTurnBackFace();
                    RevealPreviousLeft();
                }
                else if (turnDirection > 0)
                {
                    UpdateTurnForwardFace();
                    if (!restoreOnFinish)
                        CoverLeftPage();
                }
            }
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null)
            return;

        bool turnForward = mouse.leftButton.wasPressedThisFrame;
        bool turnBack = mouse.rightButton.wasPressedThisFrame;
        if (!turnForward && !turnBack)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!HitsBook(ray))
            return;

        if (HasPages)
        {
            if (turnBack && spread > 0)
                BeginTurnBack();
            else if (turnForward && spread < SpreadCount - 1)
                BeginTurnForward();
            return;
        }

        if (!turnForward)
            return;

        turned = !turned;
        restoreOnFinish = false;
        angleFrom = angle;
        angleTo = turned ? openAngle : 0f;
        animTime = 0f;
    }

    void LateUpdate()
    {
        if (dragging)
            FollowHand();
    }

    void SetupHandTurn()
    {
        rightGrab = CreateGrabVolume("PageTurnRight", pageRight);
        leftGrab = CreateGrabVolume("PageTurnLeft", pageLeft);
        if (rightGrab == null && leftGrab == null)
        {
            Debug.LogWarning("GuideBookPageFlip: no page blocks to pinch, so the sheet stays on the mouse.", this);
            return;
        }

        bool created = !TryGetComponent(out handTurn);
        if (created)
            handTurn = gameObject.AddComponent<XRSimpleInteractable>();

        handTurn.selectMode = InteractableSelectMode.Single;
        if (!created)
        {
            handTurn.colliders.Clear();
            if (rightGrab != null)
                handTurn.colliders.Add(rightGrab);
            if (leftGrab != null)
                handTurn.colliders.Add(leftGrab);
        }

        handTurn.selectEntered.RemoveListener(OnHandGrab);
        handTurn.selectExited.RemoveListener(OnHandRelease);
        handTurn.selectEntered.AddListener(OnHandGrab);
        handTurn.selectExited.AddListener(OnHandRelease);
        handTurn.selectFilters.Add(new XRSelectFilterDelegate((interactor, interactable) => CanGrab(interactor, interactable)));
        handTurn.hoverFilters.Add(new XRHoverFilterDelegate((interactor, interactable) => CanGrab(interactor, interactable)));
    }

    BoxCollider CreateGrabVolume(string name, Transform block)
    {
        if (!TryBoundsInBookSpace(block, out Bounds bounds))
            return null;

        Vector3 lift = liftInBook.sqrMagnitude > 0.5f ? liftInBook : Vector3.up;
        Vector3 proud = new Vector3(Mathf.Abs(lift.x), Mathf.Abs(lift.y), Mathf.Abs(lift.z)) * 0.02f;
        var volume = new GameObject(name);
        volume.layer = gameObject.layer;
        volume.transform.SetParent(transform, false);
        var box = volume.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size + proud;
        return box;
    }

    bool TryBoundsInBookSpace(Transform block, out Bounds bounds)
    {
        bounds = default;
        if (block == null)
            return false;

        bool any = false;
        foreach (var filter in block.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 ext = meshBounds.extents;
            Vector3 center = meshBounds.center;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + new Vector3(
                    (i & 1) == 0 ? -ext.x : ext.x,
                    (i & 2) == 0 ? -ext.y : ext.y,
                    (i & 4) == 0 ? -ext.z : ext.z);
                Vector3 point = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return any;
    }

    void CaptureBookFrame()
    {
        spineInBook = transform.InverseTransformPoint(flipPage.TransformPoint(spineLocal));
        hingeInBook = transform.InverseTransformDirection(flipPage.TransformDirection(hingeAxis)).normalized;
        closedInBook = transform.InverseTransformDirection(flipPage.TransformDirection(closedDirection)).normalized;
        liftInBook = transform.InverseTransformDirection(flipPage.TransformDirection(liftDirection)).normalized;
    }

    bool CanGrab(IXRInteractor interactor, IXRInteractable interactable)
    {
        if (dragging && ReferenceEquals(interactor, holdingHand))
            return true;
        if (dragging || animTime < 1f || interactor == null)
            return false;

        Transform attach = interactor.GetAttachTransform(interactable);
        if (attach == null || DistanceToGrabVolume(attach.position) > Mathf.Max(0.05f, handReach))
            return false;

        bool onRight = IsRightPage(attach.position);
        if (!HasPages)
            return onRight ? !turned : turned;
        return onRight ? spread < SpreadCount - 1 : spread > 0;
    }

    void OnHandGrab(SelectEnterEventArgs args)
    {
        holdingHand = args.interactorObject;
        Transform attach = holdingHand != null ? holdingHand.GetAttachTransform(handTurn) : null;
        if (attach == null || !TryPlaneDirection(attach.position, out grabDirection))
            return;

        bool onRight = IsRightPage(attach.position);
        if (HasPages)
        {
            if (onRight)
            {
                if (spread >= SpreadCount - 1)
                    return;
                turnDirection = 1;
                showingNextLeft = false;
                coveredLeftPage = false;
                grabSheetAngle = angle;
            }
            else
            {
                if (spread <= 0)
                    return;
                PrepareTurnBack();
                // The resting sheet is on the right. Put it under the hand on the left page.
                grabSheetAngle = SignedPageAngle(grabDirection);
            }
        }
        else
        {
            turnDirection = onRight ? 1 : -1;
            grabSheetAngle = onRight ? angle : SignedPageAngle(grabDirection);
        }

        restoreOnFinish = false;
        dragging = true;
        ApplyGrabAngle(grabDirection);
        UpdateDragFaces();
    }

    void OnHandRelease(SelectExitEventArgs args)
    {
        if (!ReferenceEquals(args.interactorObject, holdingHand))
            return;
        EndHandDrag();
    }

    void FollowHand()
    {
        if (holdingHand == null || handTurn == null || !handTurn.isSelected)
        {
            EndHandDrag();
            return;
        }

        Transform attach = holdingHand.GetAttachTransform(handTurn);
        if (attach == null || !TryPlaneDirection(attach.position, out Vector3 direction))
            return;

        ApplyGrabAngle(direction);
        UpdateDragFaces();
    }

    void EndHandDrag()
    {
        if (!dragging)
        {
            holdingHand = null;
            return;
        }

        dragging = false;
        holdingHand = null;
        float limit = Mathf.Abs(openAngle);
        float mid = limit * 0.5f;
        float absAngle = Mathf.Abs(angle);

        if (!HasPages)
        {
            turned = absAngle > mid;
            restoreOnFinish = false;
            SettleTo(turned ? openAngle : 0f);
            return;
        }

        bool commit = turnDirection < 0 ? absAngle <= mid : absAngle >= mid;
        if (commit)
        {
            restoreOnFinish = false;
            SettleTo(turnDirection < 0 ? 0f : openAngle);
            return;
        }

        restoreOnFinish = true;
        if (turnDirection < 0)
        {
            int currentLeft = spread * 2;
            if (currentLeft >= 0 && currentLeft < pages.Length)
            {
                SetPageMaterial(pageLeft, pages[currentLeft]);
                SetFlipMaterial(pages[currentLeft]);
            }
            revealedPreviousLeft = false;
            showingPreviousRight = false;
            SettleTo(openAngle);
            return;
        }

        if (coveredLeftPage)
        {
            coveredLeftPage = false;
            int left = spread * 2;
            if (left >= 0 && left < pages.Length)
                SetPageMaterial(pageLeft, pages[left]);
        }
        SettleTo(0f);
    }

    void SettleTo(float target)
    {
        angleFrom = angle;
        angleTo = target;
        if (Mathf.Abs(angleFrom - angleTo) < 0.5f)
        {
            angle = angleTo;
            ApplyAngle();
            animTime = 1f;
            if (HasPages)
                CompleteSettle();
            return;
        }

        animTime = 0f;
    }

    void CompleteSettle()
    {
        if (restoreOnFinish)
        {
            restoreOnFinish = false;
            turnDirection = 0;
            showingNextLeft = false;
            coveredLeftPage = false;
            showingPreviousRight = false;
            revealedPreviousLeft = false;
            angle = 0f;
            angleFrom = 0f;
            angleTo = 0f;
            ApplyAngle();
            ShowSpread();
            return;
        }

        if (turnDirection < 0)
            FinishTurnBack();
        else
            AdvanceSpread();
    }

    void UpdateDragFaces()
    {
        if (!HasPages)
            return;

        if (turnDirection < 0)
        {
            UpdateTurnBackFace();
            RevealPreviousLeft();
        }
        else if (turnDirection > 0)
        {
            UpdateTurnForwardFace();
            CoverLeftPage();
        }
    }

    bool TryPlaneDirection(Vector3 worldPoint, out Vector3 direction)
    {
        Vector3 fromSpine = transform.InverseTransformPoint(worldPoint) - spineInBook;
        direction = fromSpine - hingeInBook * Vector3.Dot(fromSpine, hingeInBook);
        if (direction.sqrMagnitude < 1e-8f)
            return false;

        direction.Normalize();
        return true;
    }

    void ApplyGrabAngle(Vector3 direction)
    {
        float delta = Mathf.DeltaAngle(SignedPageAngle(grabDirection), SignedPageAngle(direction));
        float next = grabSheetAngle + delta;
        float limit = Mathf.Abs(openAngle);
        angle = openAngle < 0f ? Mathf.Clamp(next, openAngle, 0f) : Mathf.Clamp(next, 0f, limit);
        ApplyAngle();
    }

    float SignedPageAngle(Vector3 direction)
    {
        float across = Vector3.Dot(direction, closedInBook);
        float lifted = Vector3.Dot(direction, liftInBook);
        float signed = Mathf.Atan2(lifted, across) * Mathf.Rad2Deg;
        float limit = Mathf.Abs(openAngle);
        if (signed < 0f)
            signed = across >= 0f ? 0f : limit;
        signed = Mathf.Clamp(signed, 0f, limit);
        return openAngle < 0f ? -signed : signed;
    }

    bool IsRightPage(Vector3 worldPoint)
    {
        float rightDistance = DistanceToVolume(rightGrab, worldPoint);
        float leftDistance = DistanceToVolume(leftGrab, worldPoint);
        if (float.IsPositiveInfinity(rightDistance) && float.IsPositiveInfinity(leftDistance))
        {
            Vector3 fromSpine = transform.InverseTransformPoint(worldPoint) - spineInBook;
            Vector3 inPlane = fromSpine - hingeInBook * Vector3.Dot(fromSpine, hingeInBook);
            return Vector3.Dot(inPlane, closedInBook) >= 0f;
        }

        return rightDistance <= leftDistance;
    }

    float DistanceToGrabVolume(Vector3 worldPoint)
    {
        return Mathf.Min(DistanceToVolume(rightGrab, worldPoint), DistanceToVolume(leftGrab, worldPoint));
    }

    static float DistanceToVolume(BoxCollider volume, Vector3 worldPoint)
    {
        if (volume == null)
            return float.PositiveInfinity;
        return Vector3.Distance(worldPoint, volume.ClosestPoint(worldPoint));
    }

    void BeginTurnForward()
    {
        turnDirection = 1;
        showingNextLeft = false;
        coveredLeftPage = false;
        restoreOnFinish = false;
        angleFrom = angle;
        angleTo = openAngle;
        animTime = 0f;
    }

    void BeginTurnBack()
    {
        PrepareTurnBack();
        restoreOnFinish = false;
        angle = openAngle;
        angleFrom = openAngle;
        angleTo = 0f;
        animTime = 0f;
        ApplyAngle();
    }

    void PrepareTurnBack()
    {
        int currentLeft = spread * 2;
        // Leave the current left page in place. It is replaced by the previous
        // page only after the sheet has lifted off it.
        SetPageMaterial(pageRight, pages[currentLeft + 1]);
        SetFlipMaterial(pages[currentLeft]);

        turnDirection = -1;
        showingPreviousRight = false;
        revealedPreviousLeft = false;
    }

    void UpdateTurnForwardFace()
    {
        bool showNextLeft = Mathf.Abs(angle) > Mathf.Abs(openAngle) * 0.5f;
        if (showNextLeft == showingNextLeft)
            return;

        showingNextLeft = showNextLeft;
        int right = spread * 2 + 1;
        int nextLeft = right + 1;
        SetFlipMaterial(showNextLeft ? pages[nextLeft] : pages[right]);
    }

    void CoverLeftPage()
    {
        // The sheet is over the left block now, so that block becomes the incoming
        // page before the sheet settles. Page 1 is not left showing underneath.
        if (coveredLeftPage || Mathf.Abs(angle) < Mathf.Abs(openAngle) * 0.7f)
            return;

        int nextLeft = spread * 2 + 2;
        if (nextLeft >= pages.Length)
            return;

        coveredLeftPage = true;
        SetPageMaterial(pageLeft, pages[nextLeft]);
    }

    void UpdateTurnBackFace()
    {
        bool showPreviousRight = angle <= Mathf.Abs(openAngle) * 0.5f;
        if (showPreviousRight == showingPreviousRight)
            return;

        showingPreviousRight = showPreviousRight;
        int currentLeft = spread * 2;
        int previousRight = currentLeft - 1;
        SetFlipMaterial(showPreviousRight ? pages[previousRight] : pages[currentLeft]);
    }

    void RevealPreviousLeft()
    {
        if (revealedPreviousLeft || angle > Mathf.Abs(openAngle) * 0.75f)
            return;

        int previousLeft = spread * 2 - 2;
        if (previousLeft < 0)
            return;

        revealedPreviousLeft = true;
        SetPageMaterial(pageLeft, pages[previousLeft]);
    }

    void ShowSpread()
    {
        int left = spread * 2;
        int right = left + 1;
        SetPageMaterial(pageLeft, pages[left]);
        SetFlipMaterial(pages[right]);

        int underneath = right + 2;
        if (underneath < pages.Length)
            SetPageMaterial(pageRight, pages[underneath]);
        else
            SetPageMaterial(pageRight, pages[right]);
    }

    void AdvanceSpread()
    {
        spread++;
        turnDirection = 0;
        showingNextLeft = false;
        coveredLeftPage = false;
        angle = 0f;
        angleFrom = 0f;
        angleTo = 0f;
        ApplyAngle();
        ShowSpread();
    }

    void FinishTurnBack()
    {
        spread = Mathf.Max(0, spread - 1);
        turnDirection = 0;
        showingPreviousRight = false;
        revealedPreviousLeft = false;
        angle = 0f;
        angleFrom = 0f;
        angleTo = 0f;
        ApplyAngle();
        ShowSpread();
    }

    static void SetPageMaterial(Transform target, Material source)
    {
        if (target == null || source == null)
            return;

        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = source;
    }

    void SetFlipMaterial(Material source)
    {
        if (flipPage == null || source == null)
            return;

        if (ownedFlipMaterial != null)
            Destroy(ownedFlipMaterial);

        ownedFlipMaterial = new Material(source);
        pageMaterial = ownedFlipMaterial;
        if (pageMaterial.HasProperty("_Cull"))
            pageMaterial.SetFloat("_Cull", 2f);

        if (pageMaterial.HasProperty("_BaseMap"))
        {
            baseTextureScale = pageMaterial.GetTextureScale("_BaseMap");
            baseTextureOffset = pageMaterial.GetTextureOffset("_BaseMap");
        }
        else if (pageMaterial.HasProperty("_MainTex"))
        {
            baseTextureScale = pageMaterial.GetTextureScale("_MainTex");
            baseTextureOffset = pageMaterial.GetTextureOffset("_MainTex");
        }

        var renderer = flipPage.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = pageMaterial;

        ApplyTextureMirror();
    }

    void SetupDeform(Mesh source)
    {
        originalVertices = source.vertices;
        posedVertices = new Vector3[originalVertices.Length];
        deformMesh = Instantiate(source);
        deformMesh.name = source.name + " Flip";
        deformMesh.MarkDynamic();
        flipPage.GetComponent<MeshFilter>().sharedMesh = deformMesh;
        useHinge = false;
    }

    void SetupHinge()
    {
        Debug.LogWarning(
            "GuideBookPageFlip: FlipPage mesh is not readable, so it rotates as a stiff card. " +
            "Enable Read/Write on GuideBook.fbx for the curved turn.",
            this);

        var pivot = new GameObject("FlipHinge").transform;
        pivot.SetParent(flipPage.parent, false);
        pivot.position = flipPage.TransformPoint(spineLocal);
        pivot.rotation = flipPage.rotation;
        hingeClosedRotation = pivot.localRotation;
        flipPage.SetParent(pivot, true);
        hinge = pivot;
        useHinge = true;
    }

    void ApplyAngle()
    {
        if (useHinge)
        {
            hinge.localRotation = hingeClosedRotation * Quaternion.AngleAxis(angle, hingeAxis);
            return;
        }

        Quaternion swing = Quaternion.AngleAxis(angle, hingeAxis);
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 rel = originalVertices[i] - spineLocal;
            float height = Vector3.Dot(rel, upAxis);
            float across = Vector3.Dot(rel, hingeAxis);
            Vector3 flat = rel - upAxis * height - hingeAxis * across;
            posedVertices[i] = spineLocal + hingeAxis * across + swing * flat + upAxis * (height + 0.0015f);
        }

        deformMesh.vertices = posedVertices;
        deformMesh.RecalculateNormals();
        deformMesh.RecalculateBounds();
        ApplyTextureMirror();
    }

    void ApplyTextureMirror()
    {
        if (pageMaterial == null)
            return;

        // The sheet has a face for each side. Draw only the one toward the camera,
        // using the texture as authored. Flipping the texture scale folds the two
        // faces into a mirrored copy while the page is turning.
        if (pageMaterial.HasProperty("_Cull"))
            pageMaterial.SetFloat("_Cull", 2f);

        pageMaterial.SetTextureScale("_BaseMap", baseTextureScale);
        pageMaterial.SetTextureOffset("_BaseMap", baseTextureOffset);
        pageMaterial.SetTextureScale("_MainTex", baseTextureScale);
        pageMaterial.SetTextureOffset("_MainTex", baseTextureOffset);
    }


    bool TryComputeSpine(Transform page, Mesh pageMesh, out Vector3 spine, out Vector3 axis, out Vector3 up)
    {
        spine = default;
        axis = Vector3.right;
        up = Vector3.up;

        Bounds bounds = pageMesh.bounds;
        if (bounds.size.sqrMagnitude < 1e-8f)
            return false;

        Vector3 blockCenter = GapCenter(page, bounds.center);
        Vector3 towardBlock = blockCenter - bounds.center;
        if (towardBlock.sqrMagnitude < 1e-10f)
            towardBlock = Vector3.forward;

        Vector3 faceNormal = towardBlock.normalized;
        Vector3[] verts = pageMesh.isReadable ? pageMesh.vertices : null;
        float face = float.MinValue;
        if (verts != null)
        {
            for (int i = 0; i < verts.Length; i++)
                face = Mathf.Max(face, Vector3.Dot(verts[i], faceNormal));
        }

        // Spine is the edge of the sheet nearest the middle of the book.
        if (verts != null)
        {
            float eps = bounds.size.magnitude * 0.03f;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                if (Vector3.Dot(verts[i], faceNormal) < face - eps)
                    continue;
                sum += verts[i];
                count++;
            }

            spine = count > 0 ? sum / count : bounds.center;
        }
        else
        {
            spine = SpineFromBounds(bounds, faceNormal);
        }

        up = SmallestAxis(bounds.size);
        if (Vector3.Dot(bounds.center - spine, up) < 0f)
            up = -up;

        Vector3 length = Vector3.ProjectOnPlane(bounds.center - spine, up);
        if (length.sqrMagnitude < 1e-10f)
            return false;

        axis = Vector3.Cross(up, length.normalized).normalized;
        Vector3 lifted = Quaternion.AngleAxis(90f, axis) * length.normalized;
        if (Vector3.Dot(lifted, up) < 0f)
            axis = -axis;

        return true;
    }

    void ResolveReferences()
    {
        if (flipPage == null)
            flipPage = FindNamed("FlipPage");
        if (pageLeft == null)
            pageLeft = FindNamed("PageLeft", "Left");
        if (pageRight == null)
            pageRight = FindNamed("PageRight", "Right");
    }

    Vector3 GapCenter(Transform page, Vector3 pageCenter)
    {
        bool hasLeft = TryCenterInPageSpace(page, pageLeft, out Vector3 leftCenter);
        bool hasRight = TryCenterInPageSpace(page, pageRight, out Vector3 rightCenter);
        if (hasLeft && hasRight)
            return (leftCenter + rightCenter) * 0.5f;
        if (hasLeft)
            return leftCenter;
        if (hasRight)
            return rightCenter;
        return pageCenter;
    }

    static bool TryCenterInPageSpace(Transform page, Transform block, out Vector3 center)
    {
        center = default;
        if (page == null || block == null)
            return false;

        var meshes = block.GetComponentsInChildren<MeshFilter>();
        bool any = false;
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i].sharedMesh == null)
                continue;
            sum += page.InverseTransformPoint(meshes[i].transform.TransformPoint(meshes[i].sharedMesh.bounds.center));
            count++;
            any = true;
        }

        if (!any)
        {
            center = page.InverseTransformPoint(block.position);
            return true;
        }

        center = sum / count;
        return true;
    }

    static Vector3 SpineFromBounds(Bounds bounds, Vector3 faceNormal)
    {
        Vector3 abs = new Vector3(Mathf.Abs(faceNormal.x), Mathf.Abs(faceNormal.y), Mathf.Abs(faceNormal.z));
        Vector3 spine = bounds.center;
        if (abs.x >= abs.y && abs.x >= abs.z)
            spine.x = faceNormal.x > 0f ? bounds.max.x : bounds.min.x;
        else if (abs.z >= abs.x && abs.z >= abs.y)
            spine.z = faceNormal.z > 0f ? bounds.max.z : bounds.min.z;
        else
            spine.y = faceNormal.y > 0f ? bounds.max.y : bounds.min.y;

        Vector3 up = SmallestAxis(bounds.size);
        float gutter = Vector3.Dot(bounds.center, up) - Vector3.Dot(bounds.extents, Abs(up));
        float shift = gutter - Vector3.Dot(spine, up);
        return spine + up * shift;
    }

    static Vector3 SmallestAxis(Vector3 size)
    {
        if (size.x <= size.y && size.x <= size.z)
            return Vector3.right;
        if (size.y <= size.x && size.y <= size.z)
            return Vector3.up;
        return Vector3.forward;
    }

    static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    void MakeDoubleSided()
    {
        var renderer = flipPage.GetComponent<MeshRenderer>();
        if (renderer == null || renderer.sharedMaterial == null)
            return;

        pageMaterial = renderer.material;
        if (pageMaterial.HasProperty("_Cull"))
            pageMaterial.SetFloat("_Cull", 0f);

        if (pageMaterial.HasProperty("_BaseMap"))
        {
            baseTextureScale = pageMaterial.GetTextureScale("_BaseMap");
            baseTextureOffset = pageMaterial.GetTextureOffset("_BaseMap");
        }
        else if (pageMaterial.HasProperty("_MainTex"))
        {
            baseTextureScale = pageMaterial.GetTextureScale("_MainTex");
            baseTextureOffset = pageMaterial.GetTextureOffset("_MainTex");
        }
    }

    void FitClickCollider()
    {
        clickCollider = GetComponent<BoxCollider>();
        if (clickCollider == null)
            clickCollider = gameObject.AddComponent<BoxCollider>();

        bool any = false;
        Bounds local = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 ext = meshBounds.extents;
            Vector3 center = meshBounds.center;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + new Vector3(
                    (i & 1) == 0 ? -ext.x : ext.x,
                    (i & 2) == 0 ? -ext.y : ext.y,
                    (i & 4) == 0 ? -ext.z : ext.z);
                Vector3 point = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                {
                    local = new Bounds(point, Vector3.zero);
                    any = true;
                }
                else
                {
                    local.Encapsulate(point);
                }
            }
        }

        if (!any)
            return;

        clickCollider.center = local.center;
        clickCollider.size = local.size;
        clickCollider.isTrigger = true;
    }

    bool HitsBook(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        float nearest = float.PositiveInfinity;
        bool bookIsNearest = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].distance >= nearest)
                continue;
            nearest = hits[i].distance;
            Transform hit = hits[i].transform;
            bookIsNearest = hit == transform || hit.IsChildOf(transform);
        }

        return bookIsNearest;
    }

    static Mesh MeshOf(Transform target)
    {
        if (target == null || !target.TryGetComponent(out MeshFilter filter))
            return null;
        return filter.sharedMesh;
    }

    Transform FindNamed(params string[] names)
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (child.name == names[i])
                    return child;
            }
        }

        return null;
    }

    void OnDestroy()
    {
        if (handTurn != null)
        {
            handTurn.selectEntered.RemoveListener(OnHandGrab);
            handTurn.selectExited.RemoveListener(OnHandRelease);
        }
        if (deformMesh != null)
            Destroy(deformMesh);
        if (ownedFlipMaterial != null)
            Destroy(ownedFlipMaterial);
    }

    void OnDrawGizmosSelected()
    {
        if (flipPage == null || pageLeft == null || pageRight == null)
            ResolveReferences();

        Transform page = flipPage;
        Mesh mesh = MeshOf(page);
        if (page == null || mesh == null)
            return;

        Vector3 spine;
        Vector3 axis;
        if (Application.isPlaying && flipPage != null)
        {
            spine = spineLocal;
            axis = hingeAxis;
        }
        else if (!TryComputeSpine(page, mesh, out spine, out axis, out _))
        {
            return;
        }

        Vector3 worldSpine = page.TransformPoint(spine);
        Vector3 worldAxis = page.TransformVector(axis).normalized;
        float length = page.TransformVector(mesh.bounds.size).magnitude * 0.35f;
        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        Gizmos.DrawLine(worldSpine - worldAxis * length, worldSpine + worldAxis * length);
        Gizmos.DrawSphere(worldSpine, length * 0.08f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureAttached()
    {
        if (FindAnyObjectByType<GuideBookPageFlip>() != null)
            return;

        var page = GameObject.Find("FlipPage");
        if (page == null)
            return;

        page.transform.root.gameObject.AddComponent<GuideBookPageFlip>();
    }
}
